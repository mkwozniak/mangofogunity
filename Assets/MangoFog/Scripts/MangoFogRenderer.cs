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

        //variables needed to construct proper matrix for gpu draw mode
        protected Vector3 meshPosition;
        protected Vector3 meshScale;
        protected Quaternion meshRotation;
        protected Matrix4x4 meshMatrix;
        protected bool didConstructMatrix = false;

        public MeshRenderer GetMeshRenderer() { return render; }
        public MeshFilter GetMeshFilter() { return filter; }
        public void SetChunk(MangoFogChunk chunk) { this.chunk = chunk; }
        public void SetMesh(Mesh mesh) { this.mesh = mesh; }

        public void DestroySelf()
        {
            if (drawMode == 0)
            {
                if (render)
                    Destroy(render);
                if (filter)
                    Destroy(filter);
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
                if (drawMode == 0)
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
                else
                {
                    //copy the fog material and shader directly into the material
                    mat = new Material(MangoFogInstance.Instance.fogShader);
                    mat.CopyPropertiesFromMaterial(MangoFogInstance.Instance.fogMat);
                }
            }

            if (mat == null)
            {
                enabled = false;
                return;
            }
        }

        /// <summary>
        /// Called when draw mode is MeshRenderer.
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

        /// <summary>
        /// Executed when draw mode is GPU.
        /// </summary>
		protected void Update()
		{
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
            }
        }

	}
}

