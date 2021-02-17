using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MangoFog
{
	public class MangoFogChunk : MonoBehaviour
    {		
		//positions and transform
		protected int chunkID;
		protected Transform chunkTransform;
		protected Vector3 chunkOrigin = Vector3.zero;
		protected Vector3 chunkScale = Vector3.one;
		protected Vector3 chunk3DSize = Vector3.one;
		protected Vector3 chunkPosition;
		protected int textureSizeSquared;

		// Color buffers -- prepared on the worker thread.
		protected Color32[] buffer0;
		protected Color32[] buffer1;
		protected Color32[] buffer2;

		// texture
		protected Texture2D fogTexture;

		//bounding box
		protected Bounds bounds;

		//renderer
		protected MangoFogRenderer fogRenderer;

		//the orientation
		protected MangoFogOrientation orientation;		

		// timers and states
		protected float blendFactor = 0f;
		protected float nextUpdate = 0f;
		protected float elapsed = 0f;
		protected MangoFogState fogState = MangoFogState.Blending;
		protected int[,] mHeights;
		bool chunkActive = false;
		bool clampBlendFactor = false;

		//the fog processing thread for this chunk
		protected Thread fogThread;
		protected volatile bool doThread;

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
		/// Experimental used for multiple chunks
		/// </summary>
		public static int[] changeStates;

		public bool ChunkActive() { return chunkActive; }
		public void SetChunkID(int id) { chunkID = id; }
		public int GetChunkID() { return chunkID; }
		public MangoFogState GetFogState() { return fogState; }
		public int GetChangeState() { return changeStates[chunkID]; }
		public MangoFogRenderer GetRenderer() { return fogRenderer; }
		public void SetRenderer(MangoFogRenderer renderer) { fogRenderer = renderer; }


		/// <summary>
		/// Initializes the chunk
		/// </summary>
		public void Init()
		{
			orientation = MangoFogInstance.Instance.orientation;
			chunkSize = MangoFogInstance.Instance.chunkSize;
			textureSize = MangoFogInstance.Instance.textureQualityPerChunk;
			updateFrequency = MangoFogInstance.Instance.updateFrequency;
			textureBlendTime = MangoFogInstance.Instance.textureBlendTime;
			blurIterations = MangoFogInstance.Instance.blurIterations;

			Vector3 boundsMultiplier = MangoFogInstance.Instance.chunkBoundsMultiplier;
			int fogRenderHeightPosition = MangoFogInstance.Instance.fogRenderHeightPosition;
			float boundsDepth = MangoFogInstance.Instance.boundsDepth;
			clampBlendFactor = MangoFogInstance.Instance.clampBlendFactorToTextureTime;

			chunkTransform = transform;
			chunkOrigin = transform.position;
			chunkPosition = transform.position;
			chunkOrigin.x -= chunkSize * 0.5f;

			var originOrientation = 
				orientation == MangoFogOrientation.Perspective3D ? chunkOrigin.z -= chunkSize * 0.5f : chunkOrigin.y -= chunkSize * 0.5f;

			Vector3 rendererRotation = 
				orientation == MangoFogOrientation.Perspective3D ? MangoFogInstance.Instance.Perspective3DRenderRotation : MangoFogInstance.Instance.Orthographic2DRenderRotation;
			float rendererScale = chunkSize / MangoFogInstance.Instance.meshScaleDivisor * MangoFogInstance.Instance.meshScalePostMultiplier;
			Vector3 rendererScaleVector = new Vector3(rendererScale, rendererScale, 1f);

			//handle the renderer
			fogRenderer.SetChunk(this);
			fogRenderer.SetDrawMode(MangoFogInstance.Instance.drawMode);
			fogRenderer.SetColors(MangoFogInstance.Instance.unexploredColor, MangoFogInstance.Instance.exploredColor);
			fogRenderer.SetMesh(MangoFogInstance.Instance.fogMesh);
			fogRenderer.transform.position = transform.position;

			if (MangoFogInstance.Instance.drawMode == MangoDrawMode.GPU)
				fogRenderer.ConstructMeshMatrix(transform.position, rendererScaleVector, Quaternion.Euler(rendererRotation.x, rendererRotation.y, rendererRotation.z));
			else
			{
				fogRenderer.transform.position = transform.position;
				fogRenderer.transform.eulerAngles = rendererRotation;
				fogRenderer.transform.localScale = rendererScaleVector;
			}

			//init the renderer
			fogRenderer.Init();

			textureSizeSquared = textureSize * textureSize;
			buffer0 = new Color32[textureSizeSquared];
			buffer1 = new Color32[textureSizeSquared];
			buffer2 = new Color32[textureSizeSquared];

			bounds = orientation == MangoFogOrientation.Perspective3D ?
				new Bounds(transform.position, new Vector3(chunkSize * boundsMultiplier.x, (fogRenderHeightPosition + boundsDepth), chunkSize * boundsMultiplier.z)) :
				new Bounds(transform.position, new Vector3(chunkSize * boundsMultiplier.x, chunkSize * boundsMultiplier.y, fogRenderHeightPosition + boundsDepth));

			mHeights = new int[textureSize, textureSize];
			
			chunk3DSize = orientation == MangoFogOrientation.Perspective3D ?
				new Vector3(chunkSize, MangoFogInstance.Instance.heightRange.y - MangoFogInstance.Instance.heightRange.x, chunkSize) :
				new Vector3(chunkSize, chunkSize, MangoFogInstance.Instance.heightRange.y - MangoFogInstance.Instance.heightRange.x);

			CreateGrid();
		}

		public void StartChunk()
		{
			//buffer update at the start
			changeStates[chunkID] = 1;
			fogState = MangoFogState.Blending;

			//enables the system to start updating
			chunkActive = true;

			// creates a new thread for this chunk
			fogThread = new Thread(() => UpdateThread(chunkID));
			doThread = true;
			fogThread.Start();

		}

		/// <summary>
		/// Ensure that the thread gets terminated.
		/// </summary>
		public void Dispose()
		{
			if (fogThread != null)
			{
				doThread = false;
				fogThread.Join();
				fogThread = null;
			}

			buffer0 = null;
			buffer1 = null;
			buffer2 = null;

			if (fogTexture != null)
			{
				Destroy(fogTexture);
				fogTexture = null;
			}

			UnityEngine.Object.Destroy(gameObject);
		}

		// disposes the thread and destroys the chunk
		public void DestroySelf()
		{
			Dispose();
		}

		/// <summary>
		/// Checks to see if the specified position is currently visible.
		/// </summary>
		public bool IsVisible(Vector3 pos)
		{
			if (buffer0 == null || buffer1 == null)
			{
				return false;
			}

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

			int index = cx + cy * textureSize;

			return buffer0[index].r > 64 || buffer1[index].r > 0;
		}

		/// <summary>
		/// Determine if the specified point is visible or not using line-of-sight checks.
		/// </summary>
		bool IsVisible(int sx, int sy, int fx, int fy, float outer, int sightHeight, int variance, Quaternion rot, float fovCosine, bool reverseDir)
		{
			int dx = Mathf.Abs(fx - sx);
			int dy = Mathf.Abs(fy - sy);
			int ax = sx < fx ? 1 : -1;
			int ay = sy < fy ? 1 : -1;
			int dir = dx - dy;

			float sh = sightHeight;
			float fh = mHeights[fx, fy];

			float invDist = 1f / outer;
			float lerpFactor = 0f;

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

			for (; ; )
			{
				if (sx == fx && sy == fy) return true;

				int xd = fx - sx;
				int yd = fy - sy;

				// If the sampled height is higher than expected, then the point must be obscured
				lerpFactor = invDist * Mathf.Sqrt(xd * xd + yd * yd);
				if (mHeights[sx, sy] > Mathf.Lerp(fh, sh, lerpFactor) + variance) return false;

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
			if (buffer0 == null)
			{
				return false;
			}

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

			return buffer0[cx + cy * textureSize].g > 0;
		}

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

			//will it blend?
			if (fogState == MangoFogState.Blending)
			{
				float time = Time.time;
				if (nextUpdate < time)
				{
					nextUpdate = time + updateFrequency;
					fogState = MangoFogState.NeedUpdate;
				}
			}
			//the buffer was updated, update the texture
			else if (fogState != MangoFogState.NeedUpdate)
			{
				UpdateTexture();
			}
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
		public virtual void CreateGrid()
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

					if (MangoFogInstance.Instance.heightObstacleMask != 0)
					{
						RaycastHit hit;
						if(orientation == MangoFogOrientation.Perspective3D)
						{
							bool useSphereCast = MangoFogInstance.Instance.chunkLOSRaycastRadius > 0f;
							if (useSphereCast)
							{
								if (Physics.SphereCast(new Ray(pos, Vector3.down), MangoFogInstance.Instance.chunkLOSRaycastRadius, out hit, chunk3DSize.y, MangoFogInstance.Instance.heightObstacleMask))
								{
									mHeights[x, z] = WorldToGridHeight(pos.y - hit.distance - MangoFogInstance.Instance.chunkLOSRaycastRadius);
									continue;
								}
							}
							else if (Physics.Raycast(new Ray(pos, Vector3.down), out hit, Mathf.Infinity, MangoFogInstance.Instance.heightObstacleMask))
							{
								mHeights[x, z] = WorldToGridHeight(pos.y - hit.distance);
								continue;
							}
						}
						else 
						{
							RaycastHit2D hit2d;
							hit2d = Physics2D.Raycast(pos, Vector3.forward, Mathf.Infinity, MangoFogInstance.Instance.heightObstacleMask);
							if (hit2d.collider)
							{
								mHeights[x, z] = WorldToGridHeight(pos.z - hit2d.distance - MangoFogInstance.Instance.chunkLOSRaycastRadius);
								continue;
							}
						}
					}
					mHeights[x, z] = 0;
				}
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

			// Clear the buffer's red channel (channel used for current visibility -- it's updated right after)
			for (int i = 0, imax = buffer0.Length; i < imax; ++i)
			{
				buffer0[i] = Color32.Lerp(buffer0[i], buffer1[i], factor);
				buffer1[i].r = 0;
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

			//ready to update the texture , multi chunk experimental only
			changeStates[chunkID] = 2;
		}

		/// <summary>
		/// Reveal the map by updating the green channel to be the maximum of the red channel.
		/// </summary>
		protected void RevealMap()
		{
			for (int index = 0; index < textureSizeSquared; ++index)
				if (buffer1[index].g < buffer1[index].r)
					buffer1[index].g = buffer1[index].r;
		}

		/// <summary>
		/// Merges the buffers into one
		/// </summary>
		protected void MergeBuffer()
		{
			for (int index = 0; index < textureSizeSquared; ++index)
			{
				buffer0[index].b = buffer1[index].r;
				buffer0[index].a = buffer1[index].g;
			}
		}

		/// <summary>
		/// The fastest form of visibility updates -- radius-based, no line of sights checks.
		/// </summary>
		protected void RevealUsingRadius(IMangoFogRevealer r, float worldToTex)
		{
			// Position relative to the fog of war
			Vector3 pos = (r.GetPosition() - chunkOrigin) * worldToTex;

			//Vector3 pos = (r.GetPosition() - chunkOrigin) * worldToTex;
			float radius = r.GetRadius() * worldToTex;

			// Coordinates we'll be dealing with
			int xmin = Mathf.RoundToInt(pos.x - radius);
			int xmax = Mathf.RoundToInt(pos.x + radius);

			int ymin, ymax, cy;

			//use z for 3d, and y for 2d
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
				if (y > -1 && y < textureSize)
				{
					int yw = y * textureSize;
					for (int x = xmin; x < xmax; ++x)
					{
						int xd = x - cx;
						int yd = y - cy;
						int dist = xd * xd + yd * yd;

						if (x > -1 && x < textureSize)
							// Reveal this pixel
							if (dist < radiusSqr)
								buffer1[x + yw].r = 255;
					}
				}
			}
		}

		/// <summary>
		/// Reveal the map around the revealer performing line-of-sight checks.
		/// </summary>
		void RevealUsingLOS(IMangoFogRevealer r, float worldToTex)
		{
			// Position relative to the fog of war
			Vector3 pos = r.GetPosition() - chunkOrigin;

			int ymin, ymax, xmin, xmax, cx, cy, gh;

			// Coordinates we'll be dealing with
			//use z for 3d, and y for 2d
			if (orientation == MangoFogOrientation.Perspective3D)
			{
				ymin = Mathf.RoundToInt((pos.z - r.GetLOSOuterRadius()) * worldToTex);
				ymax = Mathf.RoundToInt((pos.z + r.GetLOSOuterRadius()) * worldToTex);
				cy = Mathf.RoundToInt(pos.z * worldToTex);
				gh = WorldToGridHeight(r.GetPosition().y);
			}
			else
			{
				ymin = Mathf.RoundToInt((pos.y - r.GetLOSOuterRadius()) * worldToTex);
				ymax = Mathf.RoundToInt((pos.y + r.GetLOSOuterRadius()) * worldToTex);
				cy = Mathf.RoundToInt(pos.y * worldToTex);
				gh = WorldToGridHeight(r.GetPosition().z);
			}

			cx = Mathf.RoundToInt(pos.x * worldToTex);

			xmin = Mathf.RoundToInt((pos.x - r.GetLOSOuterRadius()) * worldToTex);
			xmax = Mathf.RoundToInt((pos.x + r.GetLOSOuterRadius()) * worldToTex);

			xmin = Mathf.Clamp(xmin, 0, textureSize - 1);
			xmax = Mathf.Clamp(xmax, 0, textureSize - 1);
			ymin = Mathf.Clamp(ymin, 0, textureSize - 1);
			ymax = Mathf.Clamp(ymax, 0, textureSize - 1);

			cx = Mathf.Clamp(cx, 0, textureSize - 1);
			cy = Mathf.Clamp(cy, 0, textureSize - 1);

			int minRange = Mathf.RoundToInt(r.GetLOSInnerRadius() * r.GetLOSInnerRadius() * worldToTex * worldToTex);
			int maxRange = Mathf.RoundToInt(r.GetLOSOuterRadius() * r.GetLOSOuterRadius() * worldToTex * worldToTex);
			int variance = Mathf.RoundToInt(Mathf.Clamp01(MangoFogInstance.Instance.margin / (MangoFogInstance.Instance.heightRange.y - MangoFogInstance.Instance.heightRange.x)) * 255);
			Color32 white = new Color32(255, 255, 255, 255);

			// Leave the edges unrevealed
			int limit = textureSize - 1;

			for (int y = ymin; y < ymax; ++y)
			{
				if (y > -1 && y < limit)
				{
					for (int x = xmin; x < xmax; ++x)
					{
						if (x > -1 && x < limit)
						{
							int xd = x - cx;
							int yd = y - cy;
							int dist = xd * xd + yd * yd;
							int index = x + y * textureSize;

							if (dist < minRange || (cx == x && cy == y))
							{
								buffer1[index] = white;
							}
							else if (dist < maxRange)
							{
								Vector2 v = new Vector2(xd, yd);
								v.Normalize();
								v *= r.GetRadius() / 2;

								int sx = cx + Mathf.RoundToInt(v.x);
								int sy = cy + Mathf.RoundToInt(v.y);

								if (sx > -1 && sx < textureSize &&
									sy > -1 && sy < textureSize &&
									IsVisible(sx, sy, x, y, Mathf.Sqrt(dist), gh, variance, r.GetRot(), r.GetFOVCosine(), r.DoReverseLOSDirection()))
								{
									buffer1[index] = white;
								}
							}
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
					int val = buffer1[index].r;

					val += buffer1[x0 + yw].r;
					val += buffer1[x1 + yw].r;
					val += buffer1[x + yw0].r;
					val += buffer1[x + yw1].r;

					val += buffer1[x0 + yw0].r;
					val += buffer1[x1 + yw0].r;
					val += buffer1[x0 + yw1].r;
					val += buffer1[x1 + yw1].r;

					c = buffer2[index];
					c.r = (byte)(val / 9);
					buffer2[index] = c;
				}
			}

			// Swap the buffer so that the blurred one is used
			Color32[] temp = buffer1;
			buffer1 = buffer2;
			buffer2 = temp;
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
				fogTexture.SetPixels32(buffer0);
				fogTexture.filterMode = MangoFogInstance.Instance.fogFilterMode;
				fogTexture.Apply();
				fogState = MangoFogState.Blending;
			}
			else if (fogState == MangoFogState.UpdateTexture)
			{
				fogTexture.SetPixels32(buffer0);
				fogTexture.Apply();
				blendFactor = 0f;
				fogState = MangoFogState.Blending;
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
				//make sure the buffer needs updating and revealer changes were made
				if (fogState == MangoFogState.NeedUpdate)
				{
					sw.Reset();
					sw.Start();
					UpdateBuffer();
					sw.Stop();
					elapsed = 0.001f * (float)sw.ElapsedMilliseconds;
					//buffer was updated, update the texture
					fogState = MangoFogState.UpdateTexture;
				}
				Thread.Sleep(1);
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
			Gizmos.color = Color.yellow;
			bool drawGizmo3D = orientation == MangoFogOrientation.Perspective3D ? true: false;
			if (drawGizmo3D)
				Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(chunkSize, 0f, chunkSize));
			else
				Gizmos.DrawWireCube(new Vector3(0f, 0f, 0f), new Vector3(chunkSize, chunkSize, 0f));
		}

		/// <summary>
		/// Retrieve the revealed buffer
		/// </summary>
		public byte[] GetRevealedBuffer()
		{
			if (buffer1 == null) return null;
			int size = textureSize * textureSize;
			byte[] buff = new byte[size];
			for (int i = 0; i < size; ++i) buff[i] = buffer1[i].g;
			return buff;
		}

		/// <summary>
		/// Reveal the area, given the specified array of bytes.
		/// </summary>
		public void SetRevealedBuffer(byte[] arr)
		{
			if (arr == null) return;
			int mySize = textureSize * textureSize;

			if (arr.Length != mySize)
			{
				Debug.LogError("Buffer size mismatch. Fog is " + mySize + ", but passed array is " + arr.Length);
			}
			else
			{
				if (buffer0 == null)
				{
					buffer0 = new Color32[mySize];
					buffer1 = new Color32[mySize];
				}

				for (int i = 0; i < mySize; ++i)
				{
					buffer0[i].g = arr[i];
					buffer1[i].g = arr[i];
				}
			}
		}
	}

}
