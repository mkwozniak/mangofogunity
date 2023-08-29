using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MangoFog
{
    public class MangoFogRenderer : MonoBehaviour
    {
        /// <summary>
        /// The chunk associated with this renderer
        /// </summary>
        protected MangoFogChunk chunk;

        /// <summary>
        /// The mesh renderer when using MeshRenderer draw mode.
        /// </summary>
        protected MeshRenderer render;

        /// <summary>
        /// The mesh filter when using MeshRenderer draw mode.
        /// </summary>
        protected MeshFilter filter;

        /// <summary>
        /// The mesh that will be rendered.
        /// </summary>
        protected Mesh mesh;

        /// <summary>
        /// The sprite renderer when using Sprite draw mode.
        /// </summary>
        protected SpriteRenderer spriteRenderer;

        /// <summary>
        /// The material that will be copied to and rendered
        /// </summary>
        [SerializeField] protected Material mat;

        /// <summary>
        /// The unexplored color storage for this renderer
        /// </summary>
        protected Color unexploredColor;

        /// <summary>
        /// The explored color storage for this renderer
        /// </summary>
        protected Color exploredColor;

        /// <summary>
        /// The draw mode is stored as an integer for each renderer instead of checking the instance value constantly
        /// </summary>
        protected int drawMode = 0;

        // members to construct proper matrix for gpu draw mode
        protected Vector3 meshPosition;
        protected Vector3 meshScale;
        protected Quaternion meshRotation;
        protected Matrix4x4 meshMatrix;
        protected bool didConstructMatrix = false;

        // members to create sprite fog
        protected float _spriteUpdateTime = 0f;
        protected float _spriteUpdateTimer = 0f;
        protected Rect spriteRect;
        protected float _spritePPU;
        protected Vector2 _spriteSize;

        public MeshRenderer GetMeshRenderer() { return render; }
        public MeshFilter GetMeshFilter() { return filter; }
        public void SetChunk(MangoFogChunk chunk) { this.chunk = chunk; }
        public void SetMesh(Mesh mesh) { this.mesh = mesh; }

        public void SetSpriteUpdateTime(float time) { _spriteUpdateTime = time; }

        public void DestroySelf()
        {
            if (drawMode == 0)
            {
                if (render)
                    Destroy(render);
                if (filter)
                    Destroy(filter);
                return;
            }

            if (drawMode == 2)
            {
                if (spriteRenderer)
                    Destroy(spriteRenderer);
                return;
            }
        }

        public void ConstructMeshMatrix(Vector3 position, Vector3 scale, Quaternion rotation) 
        { 
            meshMatrix = Matrix4x4.TRS(position, rotation, scale);  
            didConstructMatrix = true; 
        }

        public void SetDrawMode(MangoDrawMode mode)
		{
			switch (mode)
			{
                case MangoDrawMode.MeshRenderer:
                    drawMode = 0;
                    break;
                case MangoDrawMode.GPU:
                    drawMode = 1;
                    break;
                case MangoDrawMode.Sprite:
                    drawMode = 2;
                    break;
            }
		}

        public void SetColors(Color unexploredColor, Color exploredColor)
        {
            this.unexploredColor = unexploredColor;
            this.exploredColor = exploredColor;
        }

        public void Init()
        {
            if (mat == null)
            {
                if (drawMode == 0) // mesh mode
                {
                    filter = gameObject.AddComponent<MeshFilter>();
                    render = gameObject.AddComponent<MeshRenderer>();
                    render.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    render.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    render.receiveShadows = false;
                    render.allowOcclusionWhenDynamic = true;

                    filter.sharedMesh = mesh;
                    if (render != null)
                    {
                        //create a copy of the fog material and shader
                        render.sharedMaterial = new Material(MangoFogInstance.Instance.fogShader);
                        render.sharedMaterial.CopyPropertiesFromMaterial(MangoFogInstance.Instance.fogMat);

                        //apply the material to the renderer
                        mat = render.sharedMaterial;
                    }
                }
                else if (drawMode == 1) // gpu mode
                {
                    //copy the fog material and shader directly into the material
                    mat = new Material(MangoFogInstance.Instance.fogShader);
                    mat.CopyPropertiesFromMaterial(MangoFogInstance.Instance.fogMat);
                }
                else if (drawMode == 2) // sprite mode
                {
                    mat = new Material(MangoFogInstance.Instance.fogShader);
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                    spriteRenderer.material = mat;
                    spriteRenderer.sortingLayerName = MangoFogInstance.Instance.fogSpriteRenderLayer;
                    spriteRenderer.sortingOrder = MangoFogInstance.Instance.fogSpriteRenderOrder;
                    spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                    spriteRect = new Rect(0, 0, chunk.TextureSize, chunk.TextureSize);
                    _spriteSize = MangoFogInstance.Instance.fogSpriteSlicingSize;
                    _spritePPU = MangoFogInstance.Instance.fogSpritePPU;
                }
            }

            if (mat == null)
            {
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// Called when draw mode is MeshRenderer or Sprite.
        /// </summary>
        protected void OnWillRenderObject()
        {
			if (chunk)
			{
                if(mat == null)
				{
                    Debug.LogError("The fog material is null.");
                    return;
                }
                if (mat != null && chunk.texture != null && chunk.ChunkActive())
                {
                    mat.SetTexture("_MainTex", chunk.texture);
                    mat.SetFloat("_BlendFactor", chunk.BlendFactor);
                    mat.SetColor("_Unexplored", unexploredColor);
                    mat.SetColor("_Explored", exploredColor);
                }
            }
        }

		protected void Update()
		{
            // gpu draw mode
            if (drawMode == 1 && didConstructMatrix)
			{
                if (chunk)
                {
                    if (mat == null)
                    {
                        Debug.LogError("The fog material is null.");
                        return;
                    }
                    if (mat != null && chunk.texture != null && chunk.ChunkActive())
                    {
                        mat.SetTexture("_MainTex", chunk.texture);
                        mat.SetFloat("_BlendFactor", chunk.BlendFactor);
                        mat.SetColor("_Unexplored", unexploredColor);
                        mat.SetColor("_Explored", exploredColor);
                    }
                }

                Graphics.DrawMesh(mesh, meshMatrix, mat, 0);
                return;
            }

            // sprite draw mode
            if (drawMode == 2)
            {
                _spriteUpdateTimer += Time.deltaTime;
                if (_spriteUpdateTimer > _spriteUpdateTime)
				{
                    spriteRenderer.sprite = Sprite.Create(chunk.texture, spriteRect, new Vector2(0.5f, 0.5f), _spritePPU, 0, SpriteMeshType.FullRect);
                    spriteRenderer.size = _spriteSize;
                    _spriteUpdateTime = 0f;
                }

                return;
            }
        }

	}
}

