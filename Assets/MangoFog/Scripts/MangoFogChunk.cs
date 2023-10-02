using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using UnityEngine;

namespace MangoFog
{
	public class MangoFogChunk : MonoBehaviour
    {
		#region Public Members

		/// <summary>
		/// The fog chunk generated texture
		/// </summary>
		public Texture2D texture { get { return fogTexture; } }

		/// <summary>
		/// Factor used to blend between the texture.
		/// </summary>
		public float BlendFactor { get { return blendFactor; } }

		/// <summary>
		/// Returns the elapsed time since running the chunk.
		/// </summary>
		public float Elapsed{ get { return elapsed; } }

		/// <summary>
		/// Returns the chunks texture size.
		/// </summary>
		public int TextureSize { get { return textureSize; } }

		/// <summary>
		/// Returns the chunks object size.
		/// </summary>
		public float ChunkSize { get { return chunkSize; } }

		/// <summary>
		/// Experimental used for multiple chunks
		/// </summary>
		public static int[] changeStates;

		#endregion

		#region Private Members

		/// <summary>
		/// Size of your world in units. For example, if you have a 256x256 terrain, then just leave this at '256'.
		/// </summary>
		/// 
		protected float chunkSize = 512f;

		/// <summary>
		/// Size of the fog of war texture. Higher resolution will result in more precise fog of war, at the cost of performance.
		/// </summary>
		protected int textureSize = 512;

		/// <summary>
		/// How frequently the visibility checks get performed.
		/// </summary>
		protected float updateFrequency = 0.3f;

		/// <summary>
		/// How long it takes for textures to blend from one to another.
		/// </summary>
		protected float textureBlendTime = 0.5f;

		/// <summary>
		/// How many blur iterations will be performed. More iterations result in smoother edges.
		/// Blurring happens on a separate thread and does not affect performance.
		/// </summary>
		protected int blurIterations = 2;

		// the fog instance
		protected MangoFogInstance rootInstance;

		// positions and transform
		protected int chunkID;
		protected Transform chunkTransform;
		protected Vector3 chunkOrigin = Vector3.zero;
		protected Vector3 chunkScale = Vector3.one;
		protected Vector3 chunk3DSize = Vector3.one;
		protected Vector3 chunkPosition;
		protected int textureSizeSq;

		// Color buffers -- prepared on the worker thread.
		protected Color32[] fogBuffer;
		protected Color32[] blendBuffer;
		protected Color32[] blurBuffer;
		protected Color32 white = new Color32(255, 255, 255, 255);

		// texture
		protected Texture2D fogTexture;

		// bounding box
		protected Bounds bounds;

		// mango renderer reference
		protected MangoFogRenderer fogRenderer;

		// the orientation
		protected MangoFogOrientation orientation;

		// timers and states
		protected float blendFactor = 0f;
		protected float nextUpdate = 0f;
		protected float elapsed = 0f;
		protected int[,] mHeights;
		bool chunkActive = false;
		bool clampBlendFactor = false;

		// the fog processing thread for this chunk
		protected Thread fogThread;
		protected volatile bool doThread;
		protected ConcurrentQueue<MangoFogState> _nextThreadStates = new ConcurrentQueue<MangoFogState>();

		#endregion

		#region Public Methods

		public bool ChunkActive() { return chunkActive; }
		public void SetChunkID(int id) { chunkID = id; }
		public int GetChunkID() { return chunkID; }
		public int GetChangeState() { return changeStates[chunkID]; }
		public MangoFogRenderer GetRenderer() { return fogRenderer; }
		public void SetRenderer(MangoFogRenderer renderer) { fogRenderer = renderer; }

		public void EnqueueState(MangoFogState state)
		{
			_nextThreadStates.Enqueue(state);
		}

		public void ClearStates()
		{
			_nextThreadStates = new ConcurrentQueue<MangoFogState>();
		}


		/// <summary>
		/// Initializes the chunk
		/// </summary>
		public void Init(MangoFogInstance instance)
		{
			rootInstance = instance;
			orientation = instance.orientation;
			chunkSize = instance.chunkSize;
			textureSize = instance.textureQualityPerChunk;
			updateFrequency = instance.updateFrequency;
			textureBlendTime = instance.textureBlendTime;
			blurIterations = instance.blurIterations;

			Vector3 boundsMultiplier = instance.chunkBoundsMultiplier;
			int fogRenderHeightPosition = instance.fogRenderHeightPosition;
			float boundsDepth = instance.boundsDepth;
			clampBlendFactor = instance.clampBlendFactorToTextureTime;

			chunkTransform = transform;
			chunkOrigin = transform.position;
			chunkPosition = transform.position;
			chunkOrigin.x -= chunkSize * 0.5f;

			var originOrientation = 
				orientation == MangoFogOrientation.Perspective3D ? chunkOrigin.z -= chunkSize * 0.5f : chunkOrigin.y -= chunkSize * 0.5f;

			Vector3 rendererRotation = 
				orientation == MangoFogOrientation.Perspective3D ? instance.Perspective3DRenderRotation : instance.Orthographic2DRenderRotation;
			float rendererScale = chunkSize / instance.meshScaleDivisor * instance.meshScalePostMultiplier;
			Vector3 rendererScaleVector = new Vector3(rendererScale, rendererScale, 1f);

			//handle the renderer
			fogRenderer.SetChunk(this);
			fogRenderer.SetDrawMode(instance.drawMode);
			fogRenderer.SetColors(instance.unexploredColor, instance.exploredColor);
			fogRenderer.SetMesh(instance.fogMesh);
			fogRenderer.transform.position = transform.position;

			if (instance.drawMode == MangoDrawMode.GPU)
				fogRenderer.ConstructMeshMatrix(transform.position, rendererScaleVector, Quaternion.Euler(rendererRotation.x, rendererRotation.y, rendererRotation.z));
			else
			{
				fogRenderer.transform.position = transform.position;
				fogRenderer.transform.eulerAngles = rendererRotation;
				fogRenderer.transform.localScale = rendererScaleVector;
			}

			//init the renderer
			fogRenderer.Init();

			textureSizeSq = textureSize * textureSize;
			fogBuffer = new Color32[textureSizeSq];
			blendBuffer = new Color32[textureSizeSq];
			blurBuffer = new Color32[textureSizeSq];
			blendFactor = 0f;

			bounds = orientation == MangoFogOrientation.Perspective3D ?
				new Bounds(transform.position, new Vector3(chunkSize * boundsMultiplier.x, (fogRenderHeightPosition + boundsDepth), chunkSize * boundsMultiplier.z)) :
				new Bounds(transform.position, new Vector3(chunkSize * boundsMultiplier.x, chunkSize * boundsMultiplier.y, fogRenderHeightPosition + boundsDepth));

			mHeights = new int[textureSize, textureSize];
			
			chunk3DSize = orientation == MangoFogOrientation.Perspective3D ?
				new Vector3(chunkSize, instance.heightRange.y - instance.heightRange.x, chunkSize) :
				new Vector3(chunkSize, chunkSize, instance.heightRange.y - instance.heightRange.x);

			CreateGrid();
		}

		public void StartChunk()
		{
			//buffer update at the start
			changeStates[chunkID] = 1;
			_nextThreadStates.Enqueue(MangoFogState.Blending);

			// enables the system to start updating
			chunkActive = true;

			// creates a new thread for this chunk
			fogThread = new Thread(() => UpdateThread(chunkID));
			doThread = true;
			fogThread.Start();
		}

		/// <summary>
		/// Joins the thread and clears the buffers.
		/// </summary>
		public void StopChunk()
		{
			if (fogThread != null)
			{
				doThread = false;
				fogThread.Join();
				fogThread = null;
			}

			fogBuffer = null;
			blendBuffer = null;
			blurBuffer = null;
			chunkActive = false;
		}

		/// <summary>
		/// Ensures that the thread gets terminated.
		/// </summary>
		public void Dispose(bool withObject = true)
		{
			if (fogThread != null)
			{
				doThread = false;
				fogThread.Join();
				fogThread = null;
			}

			fogBuffer = null;
			blendBuffer = null;
			blurBuffer = null;

			if (fogTexture != null)
			{
				Destroy(fogTexture);
				fogTexture = null;
			}

			if(withObject)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
		}

		/// <summary>
		/// Joins the thread and destroys the chunk.
		/// </summary>
		public void DestroySelf()
		{
			Dispose();
		}

		/// <summary>
		/// Get a reveal buffer by type.
		/// </summary>
		/// <param name="bufferType"> The type of buffer to retrieve. </param>
		/// <returns> The types respective reveal buffer. </returns>
		public byte[] GetBuffer(BufferType bufferType)
		{
			switch (bufferType)
			{
				case BufferType.Fog:
					return BuildByteBuffer(fogBuffer);
				case BufferType.Blend:
					return BuildByteBuffer(blendBuffer);
				case BufferType.Blur:
					return BuildByteBuffer(blurBuffer);
			}

			return new byte[0];
		}

		/// <summary>
		/// Set all reveal buffers to specific bytes.
		/// </summary>
		public void SetAllBuffers(byte[] fogBuffer, byte[] blendBuffer, byte[] blurBuffer)
		{
			if (fogBuffer == null || blendBuffer == null || blurBuffer == null)
			{
				Debug.LogError($"Null buffer found. {fogBuffer}, {blendBuffer}, {blurBuffer}");
				return;
			} 

			if (fogBuffer.Length != textureSizeSq 
				|| blendBuffer.Length != textureSizeSq 
				|| blurBuffer.Length != textureSizeSq)
			{
				Debug.LogError($"Buffer size mismatch. Expected: {textureSizeSq}. \n Received: " +
					$"{fogBuffer.Length}, {blendBuffer.Length}, and {this.blurBuffer.Length}.");
				return;
			}

			if (this.fogBuffer == null)
			{
				this.fogBuffer = new Color32[textureSizeSq];
				this.blendBuffer = new Color32[textureSizeSq];
				this.blurBuffer = new Color32[textureSizeSq];
			}

			for (int i = 0; i < textureSizeSq; ++i)
			{
				this.fogBuffer[i].g = fogBuffer[i];
				this.blendBuffer[i].g = blendBuffer[i];
				this.blurBuffer[i].g = blurBuffer[i];
			}
		}

		/// <summary>
		/// Converts a Vector3 world position to the matching index in the buffer.
		/// </summary>
		/// <param name="pos"></param>
		/// <returns></returns>
		public int PositionToBufferIndex(Vector3 pos)
		{
			pos -= chunkOrigin;
			float worldToTex = (float)textureSize / chunkSize;

			int cx = Mathf.RoundToInt(pos.x * worldToTex);
			int cy;
			if (orientation == MangoFogOrientation.Perspective3D)
				cy = Mathf.RoundToInt(pos.z * worldToTex);
			else
				cy = Mathf.RoundToInt(pos.y * worldToTex);

			cx = Mathf.Clamp(cx, 0, textureSize - 1);
			cy = Mathf.Clamp(cy, 0, textureSize - 1);

			return cx + cy * textureSize;
		}

		/// <summary>
		/// Checks to see if the specified position is currently visible.
		/// </summary>
		public bool IsVisible(Vector3 pos)
		{
			if (fogBuffer == null || blendBuffer == null)
			{
				return false;
			}

			int index = PositionToBufferIndex(pos);

			return fogBuffer[index].r > 64 || blendBuffer[index].r > 0;
		}

		/// <summary>
		/// Determine if the specified point is visible or not using line-of-sight checks.
		/// </summary>
		public bool IsVisible(int sx, int sy, int fx, int fy, float outer, int sightHeight, int variance, Quaternion rot, float fovCosine, bool reverseDir)
		{
			//if (sx < 0 || sx >= textureSize) return true;
			//if (sy < 0 || sy >= textureSize) return true;
			//if (fx < 0 || fx >= textureSize) return true;
			//if (fy < 0 || fy >= textureSize) return true;

			int dx = Mathf.Abs(fx - sx);
			int dy = Mathf.Abs(fy - sy);
			int ax = sx < fx ? 1 : -1;
			int ay = sy < fy ? 1 : -1;
			int dir = dx - dy;

			float sh = sightHeight;
			float fh = mHeights[fx, fy];

			float invDist = 1f / outer;
			Vector3 facing;
			Vector3 direction;
			if(orientation == MangoFogOrientation.Perspective3D)
			{
				if(reverseDir)
					facing = rot * -Vector3.forward;
				else
					facing = rot * Vector3.forward;
				direction = new Vector3(fx - sx, 0f , fy - sy).normalized;
			}
			else
			{
				if(reverseDir)
					facing = rot * -Vector3.up;
				else
					facing = rot * Vector3.up;
				direction = new Vector3(fx - sx, fy - sy, 0f).normalized;
			}

			float cosineAngle = Vector3.Dot(facing, direction);

			if (cosineAngle < fovCosine)
			{
				return false;
			}

			float lerpFactor;
			for (; ; )
			{
				if (sx == fx && sy == fy) return true;

				int xd = fx - sx;
				int yd = fy - sy;

				// If the sampled height is higher than expected, then the point must be obscured
				lerpFactor = invDist * Mathf.Sqrt(xd * xd + yd * yd);

				if (mHeights[sx, sy] > Mathf.Lerp(fh, sh, lerpFactor) + variance) 
					return false;

				int dir2 = dir << 1;

				if (dir2 > -dy)
				{
					dir -= dy;
					sx += ax;
				}

				if (dir2 < dx)
				{
					dir += dx;
					sy += ay;
				}
			}
		}

		/// <summary>
		/// Checks to see if the specified position has been explored.
		/// </summary>
		public bool IsExplored(Vector3 pos)
		{
			if (fogBuffer == null)
			{
				return false;
			}

			int index = PositionToBufferIndex(pos);

			return fogBuffer[index].g > 0;
		}

		public void SetVisible(Vector3 pos)
		{
			int index = PositionToBufferIndex(pos);
			blendBuffer[index].r = 255;
		}

		public void SetHidden(Vector3 pos)
		{
			int index = PositionToBufferIndex(pos);
			blendBuffer[index].r = 0;
		}

		public void SetHeights(int[,] heights)
		{
			mHeights = heights;
		}

		public void SetHeight(Vector3 pos, int height)
		{
			pos -= chunkOrigin;

			float worldToTex = (float)textureSize / chunkSize;

			int cx = Mathf.RoundToInt(pos.x * worldToTex);
			int cy = Mathf.RoundToInt(pos.y * worldToTex);

			cx = Mathf.Clamp(cx, 0, textureSize - 1);
			cy = Mathf.Clamp(cy, 0, textureSize - 1);

			mHeights[cx, cy] = height;
		}

		/// <summary>
		/// Convert the specified height into the internal integer representation. Integer checks are much faster than floats.
		/// </summary>
		public int WorldToGridHeight(float height)
		{
			int val;
			if (orientation == MangoFogOrientation.Perspective3D)
			{
				val = Mathf.RoundToInt(height / chunk3DSize.y * 255f);
			}
			else
				val = Mathf.RoundToInt(height / chunk3DSize.z * 255f);

			return Mathf.Clamp(val, 0, 255);
		}

		/// <summary>
		/// Create the heightmap grid using the default technique (raycasting).
		/// </summary>
		public void CreateGrid()
		{
			Vector3 pos = chunkOrigin;
			if (orientation == MangoFogOrientation.Perspective3D)
				pos.y += chunk3DSize.y;
			else
				pos.z += chunk3DSize.z;

			float texToWorld = (float)chunkSize / textureSize;

			for (int z = 0; z < textureSize; ++z)
			{
				if (orientation == MangoFogOrientation.Perspective3D)
					pos.z = chunkOrigin.z + z * texToWorld;
				else
					pos.y = chunkOrigin.y + z * texToWorld;

				for (int x = 0; x < textureSize; ++x)
				{
					pos.x = chunkOrigin.x + x * texToWorld;

					if (rootInstance.heightObstacleMask == 0)
						continue;

					RaycastHit hit;
					mHeights[x, z] = 0;

					// 3d
					if (orientation == MangoFogOrientation.Perspective3D)
					{
						bool useSphereCast = rootInstance.chunkLOSRaycastRadius > 0f;
						if (useSphereCast)
						{
							if (Physics.SphereCast(new Ray(pos, Vector3.down), rootInstance.chunkLOSRaycastRadius, 
								out hit, chunk3DSize.y, rootInstance.heightObstacleMask))
							{
								mHeights[x, z] = WorldToGridHeight(pos.y - hit.distance - rootInstance.chunkLOSRaycastRadius);
							}

							continue;
						}

						if (Physics.Raycast(new Ray(pos, Vector3.down), out hit, Mathf.Infinity, rootInstance.heightObstacleMask))
						{
							mHeights[x, z] = WorldToGridHeight(pos.y - hit.distance);
						}

						continue;
					}

					// 2d
					RaycastHit2D hit2d;
					hit2d = Physics2D.Raycast(pos, Vector3.forward, Mathf.Infinity, rootInstance.heightObstacleMask);
					if (hit2d.collider)
					{
						mHeights[x, z] = WorldToGridHeight(pos.z - hit2d.distance - rootInstance.chunkLOSRaycastRadius);
						continue;
					}

				}
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Updates the fog chunk states. The chunk will update its buffer and texture once at the start.
		/// It will then be in the idle state and check for revealers within its bounds moving.
		/// If a revealer within its bounds changes position, it will then update the buffer and texture.
		/// This avoids needless buffer and texture updating when nothing in the fog of war chunk has changed.
		/// </summary>
		protected void Update()
		{
			if (!chunkActive)
				return;

			if (clampBlendFactor)
			{
				if (textureBlendTime > 0f)
					blendFactor = Mathf.Clamp01(blendFactor + Time.deltaTime / textureBlendTime);
				else blendFactor = 1f;
			}

			MangoFogState nextState;
			bool hasNextState = _nextThreadStates.TryPeek(out nextState);
			if(!hasNextState) // no next state, things went horribly wrong 
			{
				Debug.LogWarning("MangoFogChunk has no next state!");
				return;
			}

			// will it blend?
			if (nextState == MangoFogState.Blending)
			{
				float time = Time.time;
				if (nextUpdate < time)
				{
					nextUpdate = time + updateFrequency;
					_nextThreadStates.TryDequeue(out nextState);
					_nextThreadStates.Enqueue(MangoFogState.NeedUpdate);
				}
			}
			// the buffer was updated, update the texture
			else if (nextState == MangoFogState.UpdateTexture)
			{
				_nextThreadStates.TryDequeue(out nextState);
				UpdateTexture();
			}
		}

		/// <summary>
		/// Update the fog of war's visibility.
		/// </summary>
		protected void UpdateBuffer()
		{
			// Use the texture blend time, thus estimating the time this update will finish
			// Doing so helps avoid visible changes in blending caused by the blended result being X milliseconds behind.
			float factor = (textureBlendTime > 0f) ? Mathf.Clamp01(blendFactor + elapsed / textureBlendTime) : 1f;

			// Clear the blend buffer's red channel (channel used for current visibility -- it's updated right after)
			for (int i = 0, imax = fogBuffer.Length; i < imax; ++i)
			{
				fogBuffer[i] = Color32.Lerp(fogBuffer[i], blendBuffer[i], factor);
				blendBuffer[i].r = 0;
			}

			// For conversion from world coordinates to texture coordinates
			float worldToTex = (float)textureSize / chunkSize;

			// Update the visibility buffer, one revealer at a time
			for (int i = 0; i < MangoFogInstance.revealers.size; ++i)
			{
				IMangoFogRevealer rev = MangoFogInstance.revealers[i];
				if (rev.IsValid())
					if (rev.GetRevealerType() == RevealerType.Radius)
						RevealUsingRadius(rev, worldToTex);
					else
						RevealUsingLOS(rev, worldToTex);
			}

			// Blur the final visibility data
			for (int i = 0; i < blurIterations; ++i) BlurVisibility();

			// Reveal the map based on what's currently visible
			RevealMap();

			// Merge two buffer to one
			MergeBuffer();

			// ready to update the texture, multi chunk experimental only
			changeStates[chunkID] = 2;
		}

		protected byte[] BuildByteBuffer(Color32[] colorBuffer)
		{
			if (colorBuffer == null)
			{
				return null;
			}

			byte[] buffer = new byte[textureSizeSq];
			for (int i = 0; i < textureSizeSq; ++i)
			{
				buffer[i] = colorBuffer[i].g;
			}

			return buffer;
		}

		/// <summary>
		/// Reveal the map by updating the green channel to be the maximum of the red channel.
		/// </summary>
		protected void RevealMap()
		{
			for (int index = 0; index < textureSizeSq; ++index)
				if (blendBuffer[index].g < blendBuffer[index].r)
					blendBuffer[index].g = blendBuffer[index].r;
		}

		/// <summary>
		/// Merges the fog buffer and blend buffer into one
		/// </summary>
		protected void MergeBuffer()
		{
			for (int index = 0; index < textureSizeSq; ++index)
			{
				fogBuffer[index].b = blendBuffer[index].r;
				fogBuffer[index].a = blendBuffer[index].g;
			}
		}

		/// <summary>
		/// The fastest form of visibility updates -- radius-based, no line of sights checks.
		/// </summary>
		protected void RevealUsingRadius(IMangoFogRevealer r, float worldToTex)
		{
			// Position relative to the fog of war
			Vector3 pos = (r.GetPosition() - chunkOrigin) * worldToTex;

			float radius = r.GetRadius() * worldToTex;

			// Coordinates we'll be dealing with
			int xmin = Mathf.RoundToInt(pos.x - radius);
			int xmax = Mathf.RoundToInt(pos.x + radius);

			int ymin, ymax, cy;

			// use z for 3d, and y for 2d
			if (orientation == MangoFogOrientation.Perspective3D)
			{
				ymin = Mathf.RoundToInt(pos.z - radius);
				ymax = Mathf.RoundToInt(pos.z + radius);
				cy = Mathf.RoundToInt(pos.z);
			}
			else
			{
				ymin = Mathf.RoundToInt(pos.y - radius);
				ymax = Mathf.RoundToInt(pos.y + radius);
				cy = Mathf.RoundToInt(pos.y);
			}

			int cx = Mathf.RoundToInt(pos.x);

			int radiusSqr = Mathf.RoundToInt(radius * radius);
			for (int y = ymin; y < ymax; ++y)
			{
				// stay within limits
				if (!(y > -1 && y < textureSize))
				{
					continue;
				}

				int yw = y * textureSize;
				for (int x = xmin; x < xmax; ++x)
				{
					// stay within limits
					if (!(x > -1 && x < textureSize))
					{
						continue;
					}

					// get dist from texture point to revealer pos
					int xd = x - cx;
					int yd = y - cy;
					int dist = xd * xd + yd * yd;

					// check outside distance of revealing
					if (dist > radiusSqr)
					{
						continue;
					}

					// Reveal this pixel
					blendBuffer[x + yw].r = 255;
				}
			}
		}

		/// <summary>
		/// Reveal the map around the revealer performing line-of-sight checks.
		/// </summary>
		protected void RevealUsingLOS(IMangoFogRevealer r, float worldToTex)
		{
			Vector3 revealerPos = r.GetPosition();
			// Position relative to the fog of war
			Vector3 pos = revealerPos - chunkOrigin;

			int ymin, ymax, xmin, xmax, cx, cy, gh, px, py;

			float outerRadi = r.GetLOSOuterRadius();
			float innerRadi = r.GetLOSInnerRadius();

			// Coordinates we'll be dealing with
			//use z for 3d, and y for 2d
			if (orientation == MangoFogOrientation.Perspective3D)
			{
				ymin = Mathf.RoundToInt((pos.z - outerRadi) * worldToTex);
				ymax = Mathf.RoundToInt((pos.z + outerRadi) * worldToTex);
				cy = Mathf.RoundToInt(pos.z * worldToTex);
				gh = WorldToGridHeight(revealerPos.y);
			}
			else
			{
				ymin = Mathf.RoundToInt((pos.y - outerRadi) * worldToTex);
				ymax = Mathf.RoundToInt((pos.y + outerRadi) * worldToTex);
				cy = Mathf.RoundToInt(pos.y * worldToTex);
				gh = WorldToGridHeight(revealerPos.z);
			}

			cx = Mathf.RoundToInt(pos.x * worldToTex);
			px = cx;
			py = cy;

			xmin = Mathf.RoundToInt((pos.x - outerRadi) * worldToTex);
			xmax = Mathf.RoundToInt((pos.x + outerRadi) * worldToTex);

			cx = Mathf.Clamp(cx, 0, textureSize - 1);
			cy = Mathf.Clamp(cy, 0, textureSize - 1);

			int minRange = Mathf.RoundToInt(innerRadi * innerRadi * worldToTex * worldToTex);
			int maxRange = Mathf.RoundToInt(outerRadi * outerRadi * worldToTex * worldToTex);
			int variance = Mathf.RoundToInt(Mathf.Clamp01(rootInstance.margin / (rootInstance.heightRange.y - rootInstance.heightRange.x)) * 255);

			// Leave the edges unrevealed
			int limit = textureSize;
			bool overLimitX = false;
			bool overLimitY = false;

			if (px >= limit || px < 0)
			{
				overLimitX = true;
			}
			if (py >= limit || py < 0)
			{
				overLimitX = true;
			}

			for (int y = ymin; y < ymax; ++y)
			{
				if (y >= limit || y < 0)
				{
					continue;
				}

				for (int x = xmin; x < xmax; ++x)
				{
					int xd = x - cx;
					int yd = y - cy;
					int dist = xd * xd + yd * yd;
					int index = x + y * textureSize;

					if (x >= limit || x < 0)
					{
						continue;
					}

					// instant reveal min range
					if ((dist < minRange || (cx == x && cy == y)) && !overLimitX && !overLimitY)
					{
						blendBuffer[index] = white;
						continue;
					}

					if (dist < maxRange)
					{
						int sx = cx;
						int sy = cy;
						bool outOfBounds = (sx < 0 || sx > textureSize) || (sy < 0 || sy > textureSize);
						bool visible = IsVisible(sx, sy, x, y, Mathf.Sqrt(dist), gh, variance, r.GetRot(), r.GetFOVCosine(), r.DoReverseLOSDirection());

						if (!outOfBounds && visible)
						{
							blendBuffer[index] = white;
						}
					}
				}
			}
		}

		/// <summary>
		/// Blur the visibility data.
		/// </summary>
		protected void BlurVisibility()
		{
			Color32 c;

			for (int y = 0; y < textureSize; ++y)
			{
				int yw = y * textureSize;
				int yw0 = (y - 1);
				if (yw0 < 0) yw0 = 0;
				int yw1 = (y + 1);
				if (yw1 == textureSize) yw1 = y;

				yw0 *= textureSize;
				yw1 *= textureSize;

				for (int x = 0; x < textureSize; ++x)
				{
					int x0 = (x - 1);
					if (x0 < 0) x0 = 0;
					int x1 = (x + 1);
					if (x1 == textureSize) x1 = x;

					int index = x + yw;
					int val = blendBuffer[index].r;

					val += blendBuffer[x0 + yw].r;
					val += blendBuffer[x1 + yw].r;
					val += blendBuffer[x + yw0].r;
					val += blendBuffer[x + yw1].r;

					val += blendBuffer[x0 + yw0].r;
					val += blendBuffer[x1 + yw0].r;
					val += blendBuffer[x0 + yw1].r;
					val += blendBuffer[x1 + yw1].r;

					c = blurBuffer[index];
					c.r = (byte)(val / 9);
					blurBuffer[index] = c;
				}
			}

			// Swap the buffer so that the blurred one is used
			Color32[] temp = blendBuffer;
			blendBuffer = blurBuffer;
			blurBuffer = temp;
		}

		/// <summary>
		/// Update the specified texture with the new color buffer.
		/// </summary>
		protected void UpdateTexture()
		{
			if (!chunkActive)
				return;

			if (fogTexture == null)
			{
				// Native ARGB format is the fastest as it involves no data conversion
				fogTexture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false);
				fogTexture.wrapMode = TextureWrapMode.Clamp;
				fogTexture.SetPixels32(fogBuffer);
				fogTexture.filterMode = MangoFogInstance.Instance.fogFilterMode;
				fogTexture.Apply();
				_nextThreadStates.Enqueue(MangoFogState.Blending);
			}
			else
			{
				fogTexture.SetPixels32(fogBuffer);
				fogTexture.Apply();
				blendFactor = 0f;
				_nextThreadStates.Enqueue(MangoFogState.Blending);
			}

			//let it blend until the chunk needs to be updated again
			//changeStates[chunkID] = 0;
		}

		/// <summary>
		/// If it's time to update, do so now.
		/// </summary>
		protected void UpdateThread(int chunkID)
		{
			System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
			int cid = chunkID;

			while (doThread)
			{
				MangoFogState nextState;
				bool hasNextState = _nextThreadStates.TryPeek(out nextState);
				if (!hasNextState) // no next state, things went horribly wrong 
				{
					Thread.Sleep(1);
				}

				// make sure the buffer needs updating and revealer changes were made
				if (nextState == MangoFogState.NeedUpdate)
				{
					sw.Reset();
					sw.Start();
					UpdateBuffer();
					sw.Stop();
					elapsed = 0.001f * (float)sw.ElapsedMilliseconds;
					_nextThreadStates.TryDequeue(out nextState);

					// buffer was updated, update the texture
					_nextThreadStates.Enqueue(MangoFogState.UpdateTexture);
				}
			}
			#if UNITY_EDITOR
				Debug.Log("The thread has exited.");
			#endif
		}

		/// <summary>
		/// Show the area covered by the fog of war.
		/// </summary>
		protected void OnDrawGizmosSelected()
		{
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.color = UnityEngine.Color.yellow;
			bool drawGizmo3D = orientation == MangoFogOrientation.Perspective3D ? true: false;
			if (drawGizmo3D)
				Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(chunkSize, 0f, chunkSize));
			else
				Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(chunkSize, chunkSize, 0f));
		}

		#endregion
	}

}
