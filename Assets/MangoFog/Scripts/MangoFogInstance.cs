using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using UnityEngine;

namespace MangoFog
{
	public enum MangoFogState
	{
		Blending,
		NeedUpdate,
		UpdateTexture,
		Idle,
	}

	public enum MangoFogOrientation
	{
		Perspective3D,
		Orthographic2D,
	}

	public enum MangoDrawMode
	{
		MeshRenderer,
		GPU,
		Sprite,
	}

	public enum MangoFogChunkSquared
	{
		One,
		TwoByTwo,
		FourByFour,
	}

	public enum RevealerType
	{
		Radius,
		LOS,
	}

	public enum BufferType
	{
		Fog,
		Blend,
		Blur,
	}

	[System.Serializable]
	public struct MangoBufferData
	{
		public byte[] fogBuffer;
		public byte[] blendBuffer;
		public byte[] blurBuffer;

		public MangoBufferData(byte[] buffer0, byte[] buffer1, byte[] buffer2)
		{
			this.fogBuffer = buffer0;
			this.blendBuffer = buffer1;
			this.blurBuffer = buffer2; 
		}
	}

	public class MangoFogInstance : MonoBehaviour
    {
		/// <summary>
		/// The MongoFog singleton instance
		/// </summary>
		public static MangoFogInstance Instance;

		/// <summary>
		/// The chunks dictionary
		/// </summary>
		protected Dictionary<Vector3, MangoFogChunk> chunksByPosition = new Dictionary<Vector3, MangoFogChunk>();
		protected Dictionary<int, MangoFogChunk> chunksByID = new Dictionary<int, MangoFogChunk>();

		//the dictionary to register revealers
		public static Dictionary<int, MangoFogRevealer> revealerRegister = new Dictionary<int, MangoFogRevealer>();

		//the revealers list
		public static BetterList<IMangoFogRevealer> revealers = new BetterList<IMangoFogRevealer>();

		// the revealers that have been added since last update
		public static BetterList<IMangoFogRevealer> revealersAdded = new BetterList<IMangoFogRevealer>();

		// the revealers that have been removed since last update
		public static BetterList<IMangoFogRevealer> revealersRemoved = new BetterList<IMangoFogRevealer>();

		/// <summary>
		/// Enable the Debug Logs and MongoFogDebug Instance if it exists.
		/// </summary>
		[HideInInspector] public bool debugModeEnabled = false;

		/// <summary>
		/// The draw mode of the fog. MeshRenderer mode will create a MeshFilter and MeshRenderer component and render it through a gameobject.
		/// GPU mode will render the mesh and material directly to the GPU without adding components.
		/// </summary>
		[HideInInspector] public MangoDrawMode drawMode;

		/// <summary>
		/// The renderer rotation for 3D perspective, change this if your custom mesh requires it.
		/// </summary>
		/// <returns></returns>
		[HideInInspector] public Vector3 Perspective3DRenderRotation = new Vector3(90, 0, 0); 
		// other quads/planes may use (-90, 180, 0), the default 3d quad is 90, 0, 0

		/// <summary>
		/// The renderer rotation for 2D perspective, change this if your custom mesh requires it.
		/// </summary>
		/// <returns></returns>
		[HideInInspector] public Vector3 Orthographic2DRenderRotation = new Vector3(0, 0, 0);
		// other quads/planes may use (-90, 0, 0), the default 2d quad is 0, 0, 0

		/// <summary>
		/// The mesh that the fog will be renderered on.
		/// </summary>
		[HideInInspector] public Mesh fogMesh;

		/// <summary>
		/// The PPU of the fog sprite when using Sprite render mode.
		/// </summary>
		[HideInInspector] public float fogSpritePPU;

		/// <summary>
		/// The slicing width and height of the fog sprite when using Sprite render mode.
		/// </summary>
		[HideInInspector] public Vector2 fogSpriteSlicingSize;

		/// <summary>
		/// The render layer of the fog sprite when using Sprite render mode.
		/// </summary>
		[HideInInspector] public string fogSpriteRenderLayer;

		/// <summary>
		/// The render order of the fog sprite when using Sprite render mode.
		/// </summary>
		[HideInInspector] public int fogSpriteRenderOrder;

		/// <summary>
		/// Enables the editor inspector options for the experimental chunk feature
		/// </summary>
		[HideInInspector] public bool doExperimentalChunkFeature = false;

		/// <summary>
		/// If you are using a custom mesh for your fog (not a Quad), you may need to adjust this value to scale your mesh.
		/// </summary>
		[HideInInspector] public float meshScaleDivisor = 1.0f;

		/// <summary>
		/// If you are using a custom mesh for your fog (not a Quad), you may need to adjust this value to scale your mesh.
		/// </summary>
		[HideInInspector] public float meshScalePostMultiplier = 2.56f;

		/// <summary>
		/// The unexplored color of the fog.
		/// </summary>
		[HideInInspector] public Color unexploredColor = new Color(0f, 0f, 0f, 255f);

		/// <summary>
		/// The explored color of the fog.
		/// </summary>
		[HideInInspector] public Color exploredColor = new Color(0f, 0f, 0f, 200f / 255f);

		/// <summary>
		/// The fog material to use.
		/// </summary>
		[HideInInspector] public Material fogMat;

		/// <summary>
		/// The fog shader to use.
		/// </summary>
		[HideInInspector] public Shader fogShader;

		/// <summary>
		/// The fog texture filter mode
		/// </summary>
		[HideInInspector] public FilterMode fogFilterMode;

		/// <summary>
		/// The height obstacle mask for LOS revealers.
		/// </summary>
		[HideInInspector] public LayerMask heightObstacleMask;

		/// <summary>
		/// What is the lowest and highest height of the world. Revealers below the X will not reveal anything,
		/// while revealers above Y will reveal everything around them. This only applies to LOS Revealers.
		/// </summary>
		[HideInInspector] public Vector2 heightRange = new Vector2(0f, 10f);

		/// <summary>
		/// Allows for some height variance when performing line-of-sight checks.
		/// </summary>
		[HideInInspector] public float margin = 0.4f;

		/// <summary>
		/// The radius sphere of each FogChunks LOS revealer raycasts. If 0, line-based raycasting will be used instead.
		/// 2D LOS will use line based raycasting regardless of this option.
		/// </summary>
		[HideInInspector] public float chunkLOSRaycastRadius = 1.0f;

		/// <summary>
		/// The render height of the fog. This is the y axis in 3D and the z axis in 2D.
		/// </summary>
		[HideInInspector] public int fogRenderHeightPosition = 0;

		/// <summary>
		/// The render orientation of the fog. 3D uses the XZ axis and 2D uses the XY axis.
		/// </summary>
		[HideInInspector] public MangoFogOrientation orientation;

		/// <summary>
		/// How long it takes for textures to blend from one to another.
		/// </summary>
		[HideInInspector] public float textureBlendTime = 0.5f;

		/// <summary>
		/// How many blur iterations will be performed. More iterations result in smoother edges.
		/// Blurring happens on a separate thread and does not affect performance.
		/// </summary>
		[HideInInspector] public int blurIterations = 2;

		/// <summary>
		/// How frequently the visibility checks get performed.
		/// </summary>
		[HideInInspector] public float updateFrequency = 0.3f;

		/// <summary>
		/// Clamps the blend factor between 0 and 1 to itself + Time.deltaTime / textureBlendTime.
		/// I found it's better to leave false in higher quality blurred fog.
		/// </summary>
		[HideInInspector] public bool clampBlendFactorToTextureTime = false;

		/// <summary>
		/// Controls when to spawn multiple chunks. Will be false if chunkSquared is One.
		/// This is experimental and does not work flawlessly.
		/// </summary>
		bool doChunks = true;
		public bool DoChunks() { return doChunks; }
		int chunksCreated = 0;

		/// <summary>
		/// Use this if doChunks is false and you want to set the root position of the single fog.
		/// You can also use this to offset all chunks.
		/// </summary>
		[HideInInspector] public Vector3 rootChunkPosition;
		Vector3 trueRootChunkPosition;

		/// <summary>
		/// Each chunk is only updated when a revealer is within its bounds.
		/// If a chunk is not updating when moving near it, you may increase the bounds multiplier to make the bounds box larger.
		/// The standard value is (1,1,1).
		/// </summary>
		[HideInInspector] public Vector3 chunkBoundsMultiplier = new Vector3(1,1,1);

		/// <summary>
		/// Adjust this number to avoid visible seams between fog chunks.
		/// After this delay, a chunks buffer will be updated once after a revealer has left its bounds.
		/// </summary>
		[HideInInspector] public float chunkSeamBufferDelay = 0.5f;

		/// <summary>
		/// The bounds depth of each chunk. This value is applied to the axis respective to the MangoFogOrientation used.
		/// Ex. This will apply the Y Axis size of the chunk bound box in Perspective3D, and apply the Z axis in Orthographic2D.
		/// If you are using Perspective3D, this value should be greater than the highest point of access of your level.
		/// </summary>
		[HideInInspector] public float boundsDepth = 100;

		/// <summary>
		/// The amount of chunks squared. Ex. if Four, there will be 4 total chunks in a 2x2 grid.
		/// It is not recommended to increase the amount past FourByFour(Sixteen). It can easily overload cpus.
		/// </summary>
		[HideInInspector] public MangoFogChunkSquared chunkSquaredAmount;
		int chunkSquared = 0;

		/// <summary>
		/// The resolution texture quality of the fog per chunk
		/// </summary>
		[HideInInspector] public int textureQualityPerChunk = 512;

		/// <summary>
		/// The size of each chunk in world units. 
		/// </summary>
		/// 
		[HideInInspector] public int chunkSize = 512;

		/// <summary>
		/// The root chunk prefab to Instantiate from.
		/// </summary>
		[HideInInspector] public GameObject chunkPrefab;

		#region Public Methods

		public int GetTotalRevealers() { return revealers.size; }

		public void AddRevealer(MangoFogRevealer rev)
		{
			if (!revealerRegister.ContainsKey(rev.GetUniqueID()))
			{
				revealerRegister[rev.GetUniqueID()] = rev;
				DoAddRevealer(rev);
			}
		}

		public void RemoveRevealer(MangoFogRevealer rev)
		{
			if (revealerRegister.ContainsKey(rev.GetUniqueID()))
			{
				revealerRegister.Remove(rev.GetUniqueID());
				DoRemoveRevealer(rev);
			}
		}

		/// <summary>
		/// Create a new fog revealer.
		/// </summary>
		static void DoAddRevealer(IMangoFogRevealer rev)
		{
			if (rev != null)
			{
				lock (revealersAdded) revealersAdded.Add(rev);
			}
		}

		/// <summary>
		/// Delete the specified revealer.
		/// </summary>
		static void DoRemoveRevealer(IMangoFogRevealer rev)
		{
			if (rev != null)
			{
				lock (revealersRemoved) revealersRemoved.Add(rev);
			}
		}

		/// <summary>
		/// Saves the root chunk buffer to the persistent data storage slot.
		/// Saving multiple chunk buffers is not yet included
		/// </summary>
		/// <param name="id"></param>
		public void SaveChunkBufferToSlot(int id)
		{
			if (DoChunks()) 
			{
				Debug.LogWarning("Saving / Loading multiple chunk buffers is not yet supported."); 
				return; 
			}

			MangoFogChunk chunk = chunksByPosition[trueRootChunkPosition];

			if (File.Exists(Application.persistentDataPath + "/fogData" + id.ToString() + ".dat"))
			{
				BinaryFormatter newFormatter = new BinaryFormatter();
				FileStream fl = File.Open(Application.persistentDataPath + "/fogData" + id.ToString() + ".dat", FileMode.Open);
				MangoBufferData data = 
					new MangoBufferData(chunk.GetBuffer(BufferType.Fog), chunk.GetBuffer(BufferType.Blend), chunk.GetBuffer(BufferType.Blur));
				newFormatter.Serialize(fl, data);
				Debug.Log("Saved current buffer to slot " + id.ToString());
				fl.Close();
			}
			else
			{
				BinaryFormatter newFormatter = new BinaryFormatter();
				FileStream fl = File.Create(Application.persistentDataPath + "/fogData" + id.ToString() + ".dat");
				MangoBufferData data = 
					new MangoBufferData(chunk.GetBuffer(BufferType.Fog), chunk.GetBuffer(BufferType.Blend), chunk.GetBuffer(BufferType.Blur));
				newFormatter.Serialize(fl, data);
				Debug.Log("Created and saved current buffer to slot " + id.ToString());
				fl.Close();
			}
		}

		/// <summary>
		/// Loads the persistent data storage slot to the root chunk buffer.
		/// Loading to multiple chunk buffers is not yet included
		/// </summary>
		/// <param name="id"></param>
		public void LoadSlotBufferToChunk(int id)
		{
			if (DoChunks())
			{
				Debug.LogWarning("Saving / Loading multiple chunk buffers doesn't work yet :( ");
				return;
			}

			if (File.Exists(Application.persistentDataPath + "/fogData" + id.ToString() + ".dat"))
			{
				// stop thread and clear states
				chunksByPosition[trueRootChunkPosition].StopChunk();
				chunksByPosition[trueRootChunkPosition].ClearStates();

				// open file
				BinaryFormatter newFormatter = new BinaryFormatter();
				FileStream fl = File.Open(Application.persistentDataPath + "/fogData" + id.ToString() + ".dat", FileMode.Open);
				MangoBufferData data = (MangoBufferData)newFormatter.Deserialize(fl);

				Debug.Log("Loaded buffer from slot " + id.ToString() + " to current ");

				// start thread again
				chunksByPosition[trueRootChunkPosition].StartChunk();

				// set buffer and update texture
				chunksByPosition[trueRootChunkPosition].SetAllBuffers(data.fogBuffer, data.blendBuffer, data.blurBuffer);
				chunksByPosition[trueRootChunkPosition].EnqueueState(MangoFogState.UpdateTexture);
				fl.Close();
			}
			else
			{
				Debug.LogError("The requested storage slot does not yet exist.");
			}
		}

		/// <summary>
		/// The mango fog instance will init on Awake.
		/// </summary>
		public void Awake()
		{
			//singleton
			Instance = this;

			//init
			Init();
		}

		public void Start()
		{
			if (debugModeEnabled && MangoFogDebug.Instance)
			{
				Debug.Log("Mango Fog Instance Initialized");
				Debug.Log("Mango Fog Debug Enabled.");
				MangoFogDebug.Instance.CreateDebugBoxes(chunksByPosition);
			}
		}

		public void OnApplicationExit()
		{

		}

		/// <summary>
		/// Initializes the fog of war from the beginning and generates chunks.
		/// </summary>
		public void Init()
		{
			switch (chunkSquaredAmount)
			{
				case MangoFogChunkSquared.One:
					doChunks = false;
					break;
				case MangoFogChunkSquared.TwoByTwo:
					chunkSquared = 1;
					break;
				case MangoFogChunkSquared.FourByFour:
					chunkSquared = 2;
					break;
			}
			
			revealers.Clear();
			revealersAdded.Clear();
			revealersRemoved.Clear();
			GenerateChunks(orientation);
		}

		public void OnApplicationQuit()
		{
			Dispose();
		}

		/// <summary>
		/// Make sure to call this before switching scenes!
		/// </summary>
		public void Dispose()
		{
			revealers.Clear();
			revealersAdded.Clear();
			revealersRemoved.Clear();

			for(int i = 0; i < chunksCreated; i++)
			{
				chunksByID[i].DestroySelf();
			}
			chunksByPosition.Clear();
			chunksByID.Clear();
		}

		public void Update()
		{
			int deltaMS = (int)(Time.deltaTime * 1000f);
			UpdateRevealers(deltaMS);
		}


		public void GenerateChunks(MangoFogOrientation orientation)
		{
			if (!doChunks)
			{
				if (orientation == MangoFogOrientation.Perspective3D)
				{
					trueRootChunkPosition = new Vector3(rootChunkPosition.x, fogRenderHeightPosition, rootChunkPosition.z);
					InstantiateChunk(trueRootChunkPosition);
				}
				else
				{
					trueRootChunkPosition = new Vector3(rootChunkPosition.x, rootChunkPosition.y, fogRenderHeightPosition);
					InstantiateChunk(trueRootChunkPosition);
				}
			}
			else
				if (orientation == MangoFogOrientation.Perspective3D)
					for (int y = -(chunkSquared); y < (chunkSquared); ++y)
						for (int x = -(chunkSquared); x < (chunkSquared); ++x)
							InstantiateChunk(new Vector3((x * chunkSize), fogRenderHeightPosition, (y * chunkSize)) + rootChunkPosition);
				else
					for (int y = -(chunkSquared); y < (chunkSquared); ++y)
						for (int x = -(chunkSquared); x < (chunkSquared); ++x)
							InstantiateChunk(new Vector3((x * chunkSize), (y * chunkSize), fogRenderHeightPosition) + rootChunkPosition);

			MangoFogChunk.changeStates = new int[chunksCreated];

			// loop through chunks and start them.
			foreach (KeyValuePair<Vector3, MangoFogChunk> chunk in chunksByPosition)
			{
				chunk.Value.StartChunk();
			}


			if (debugModeEnabled)
				Debug.Log("Mango Fog Instance Generated Chunks.");
		}

		#endregion

		#region Private Methods

		protected void InstantiateChunk(Vector3 pos)
		{
			GameObject chunkObj = Instantiate(chunkPrefab, pos, Quaternion.identity, transform);
			MangoFogChunk newFogChunk = chunkObj.AddComponent<MangoFogChunk>();
			MangoFogRenderer newFogRenderer = chunkObj.AddComponent<MangoFogRenderer>();
			// gives the new chunk a reference to its renderer
			newFogChunk.SetRenderer(newFogRenderer);
			// inits the chunk
			newFogChunk.Init(this);
			newFogChunk.gameObject.SetActive(true);
			// set the chunk id
			newFogChunk.SetChunkID(chunksCreated);
			//add the chunk to the dictionary
			chunksByPosition.Add(pos, newFogChunk);
			chunksByID.Add(chunksCreated, newFogChunk);
			chunkObj.gameObject.name = "Mango Fog Chunk " + chunksCreated;
			chunksCreated += 1;
		}

		protected void UpdateRevealers(int deltaMS)
		{
			// Add all items scheduled to be added
			if (revealersAdded.size > 0)
			{
				lock (revealersAdded)
				{
					while (revealersAdded.size > 0)
					{
						int index = revealersAdded.size - 1;
						revealers.Add(revealersAdded.buffer[index]);
						revealersAdded.RemoveAt(index);
					}
				}
			}

			// Remove all items scheduled for removal
			if (revealersRemoved.size > 0)
			{
				lock (revealersRemoved)
				{
					while (revealersRemoved.size > 0)
					{
						int index = revealersRemoved.size - 1;
						revealers.Remove(revealersRemoved.buffer[index]);
						revealersRemoved.RemoveAt(index);
					}
				}
			}

			for (int i = revealers.size - 1; i >= 0; i--)
			{
				revealers[i].Update(deltaMS);
				if (!revealers[i].IsValid())
					revealers[i].Release();
			}
		}
	}

	#endregion
}

