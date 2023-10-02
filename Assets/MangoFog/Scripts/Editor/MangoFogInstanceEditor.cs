//C# Example (LookAtPointEditor.cs)
using UnityEngine;
using UnityEditor;
using MangoFog;

[CustomEditor(typeof(MangoFogInstance))]
[CanEditMultipleObjects]
public class LookAtPointEditor : Editor
{
    MangoFogInstance targ;

    string drawMode_tooltip = "The draw mode of the fog. MeshRenderer mode will create a MeshFilter and MeshRenderer component and render it through a gameobject. \n" +
    "GPU mode will render the mesh and material directly to the GPU without adding components.";
    string textureBlendTime_tooltip = "How long it takes for textures to blend from one to another.";
    string fogRenderHeightPosition_tooltip = "The render height of the fog. This is the y axis in 3D and the z axis in 2D. \n" +
        "3D Recommended Value: 1 \n" +
        "2D Recommended Value: -1";
    string orientation_tooltip = "The render orientation of the fog. 3D uses the XZ axis and 2D uses the XY axis.";
    string blurIterations_tooltip = "How many blur iterations will be performed. More iterations results in smoother edges.";
    string updateFrequency_tooltip = "How frequently the visibility checks get performed.";
    string clampBlendFactorToTextureTime_tooltip = "Clamps the blend factor between 0 and 1 to itself + Time.deltaTime / textureBlendTime. \n" +
        "I found leaving this false in high quality blurred fog removes weird blending flashes. If you know a fix for this, let me know.";
    string rootChunkPosition_tooltip = "Offsets all chunks from this position.";
    string chunkBoundsMultiplier_tooltip = "Each chunk is only updated when a revealer is within its bounds. " +
        "If a chunk is not updating when moving near it, you may increase the bounds multiplier to make the bounds box larger. \n" +
        "Default Value: (1,1,1)";
    string chunkSeamBufferDelay_tooltip = "Adjust this number to avoid leftover visibility when revealers exit a chunk. " +
        "After this delay, a chunks buffer will be updated once after a revealer has left its bounds. \n" +
        "Default Value: 0.5";
    string boundsDepth_tooltip = "The bounds depth of each chunk. This value is applied to the axis respective to the MangoFogOrientation used. " +
    "Ex. This will apply the Y Axis size of the chunk bound box in Perspective3D, and apply the Z axis in Orthographic2D. " +
        "If you are using Perspective3D, this value should be greater than the highest point of access of your level. \n" +
        "Default Value: 100";
    string chunkSquaredAmount_tooltip = "If this is not One, the value below will come into play. This will spawn multiple fog chunks each with their own thread." +
        "Multiple fog chunks is an experimental feature and has some visual glitches when entering / exiting chunks. \n" +
        "Default Value: One";
    string textureQualityPerChunk_tooltip = "The resolution texture quality of the fog per chunk.";
    string chunkSize_tooltip = "The size of each chunk in world units. ";
    string meshScaleDivisor_tooltip = "If you are using a custom mesh for your fog (not a Quad), you may need to adjust this value to scale your mesh. \n" +
        "Default value: 1";
    string meshScalePostMultiplier_tooltip = "If you are using a custom mesh for your fog (not a Quad), you may need to adjust this value to scale your mesh. \n" +
        "Default Value: 1";
    string unexploredColor_tooltip = "The unexplored color of the fog.";
    string exploredColor_tooltip = "The explored color of the fog.";
    string fogMat_tooltip = "The fog material to use.";
    string fogShader_tooltip = "The fog shader to use.";
    string fogSpritePPU_tooltip = "The PPU of the fog sprite when using Sprite render mode.";
    string fogSpriteSlicingSize_tooltip = "The slicing width and height of the fog sprite when using Sprite render mode.";
    string fogSpriteRenderLayer_tooltip = "The render layer of the fog sprite when using Sprite render mode.";
    string fogSpriteRenderOrder_tooltip = "The render order of the fog sprite when using Sprite render mode.";
    string fogFilterMode_tooltip = "The fog texture filter mode";
    string heightObstacleMask_tooltip = "The height obstacle mask for LOS revealers.";
    string heightRange_tooltip = "The height range for the level for LOS revealers. If LOS is not working properly, this range may be the cause of it. Try different ranges. \n" +
        "For 2D, this range will most likely need to range in to the negative values. (As the Z axis is used to perform the raycasts).";
    string margin_tooltip = "Allows for some height variance when performing line-of-sight checks.";
    string chunkLOSRaycastRadius_tooltip = "The radius sphere of each FogChunks LOS revealer raycasts. If 0, line-based raycasting will be used instead. \n" +
        "2D LOS will use line based raycasting regardless of this option.";
    string Perspective3DRenderRotation_tooltip = "The renderer rotation for 3D perspective, change this if your custom mesh requires it. \n" +
        "Default Value for Unity Quad: (90, 0, 0)";
    string Orthographic2DRenderRotation_tooltip = "The renderer rotation for 2D perspective, change this if your custom mesh requires it. \n" +
        "Default Value for Unity Quad: (0, 0, 0)";

    SerializedProperty chunkPrefab;
    SerializedProperty drawMode;
    SerializedProperty textureBlendTime;
    SerializedProperty meshScaleDivisor;
    SerializedProperty meshScalePostMultiplier;
    SerializedProperty blurIterations;
    SerializedProperty updateFrequency;
    SerializedProperty rootChunkPosition;
    SerializedProperty chunkBoundsMultiplier;
    SerializedProperty chunkSeamBufferDelay;
    SerializedProperty boundsDepth;
    SerializedProperty chunkSquaredAmount;
    SerializedProperty textureQualityPerChunk;
    SerializedProperty clampBlendFactorToTextureTime;
    SerializedProperty chunkSize;
    SerializedProperty fogRenderHeightPosition;
    SerializedProperty orientation;
    SerializedProperty unexploredColor;
    SerializedProperty exploredColor;
    SerializedProperty fogMat;
    SerializedProperty fogShader;
    SerializedProperty fogSpritePPU;
    SerializedProperty fogSpriteSlicingSize;
    SerializedProperty fogSpriteRenderLayer;
    SerializedProperty fogSpriteRenderOrder;
    SerializedProperty fogFilterMode;
    SerializedProperty heightObstacleMask;
    SerializedProperty heightRange;
    SerializedProperty margin;
    SerializedProperty chunkLOSRaycastRadius;
    SerializedProperty Perspective3DRenderRotation;
    SerializedProperty Orthographic2DRenderRotation;
    SerializedProperty fogMesh;

    SerializedProperty doExperimentalChunkFeature;
    SerializedProperty debugModeEnabled;

    void OnEnable()
    {
        targ = (MangoFogInstance)target;
        chunkPrefab = serializedObject.FindProperty("chunkPrefab");
        drawMode = serializedObject.FindProperty("drawMode");
        orientation = serializedObject.FindProperty("orientation");
        fogRenderHeightPosition = serializedObject.FindProperty("fogRenderHeightPosition");
        textureBlendTime = serializedObject.FindProperty("textureBlendTime");
        blurIterations = serializedObject.FindProperty("blurIterations");
        updateFrequency = serializedObject.FindProperty("updateFrequency");
        clampBlendFactorToTextureTime = serializedObject.FindProperty("clampBlendFactorToTextureTime");
        rootChunkPosition = serializedObject.FindProperty("rootChunkPosition");
        chunkBoundsMultiplier = serializedObject.FindProperty("chunkBoundsMultiplier");
        chunkSeamBufferDelay = serializedObject.FindProperty("chunkSeamBufferDelay");
        boundsDepth = serializedObject.FindProperty("boundsDepth");
        chunkSquaredAmount = serializedObject.FindProperty("chunkSquaredAmount");
        textureQualityPerChunk = serializedObject.FindProperty("textureQualityPerChunk");
        chunkSize = serializedObject.FindProperty("chunkSize");
        meshScaleDivisor = serializedObject.FindProperty("meshScaleDivisor");
        meshScalePostMultiplier = serializedObject.FindProperty("meshScalePostMultiplier");
        unexploredColor = serializedObject.FindProperty("unexploredColor");
        exploredColor = serializedObject.FindProperty("exploredColor");
        fogMat = serializedObject.FindProperty("fogMat");
        fogShader = serializedObject.FindProperty("fogShader");
        fogSpritePPU = serializedObject.FindProperty("fogSpritePPU");
        fogSpriteSlicingSize = serializedObject.FindProperty("fogSpriteSlicingSize");
        fogSpriteRenderLayer = serializedObject.FindProperty("fogSpriteRenderLayer");
        fogSpriteRenderOrder = serializedObject.FindProperty("fogSpriteRenderOrder");
        fogFilterMode = serializedObject.FindProperty("fogFilterMode");
        heightObstacleMask = serializedObject.FindProperty("heightObstacleMask");
        heightRange = serializedObject.FindProperty("heightRange");
        margin = serializedObject.FindProperty("margin");
        chunkLOSRaycastRadius = serializedObject.FindProperty("chunkLOSRaycastRadius");
        Perspective3DRenderRotation = serializedObject.FindProperty("Perspective3DRenderRotation");
        Orthographic2DRenderRotation = serializedObject.FindProperty("Orthographic2DRenderRotation");
        fogMesh = serializedObject.FindProperty("fogMesh");

        doExperimentalChunkFeature = serializedObject.FindProperty("doExperimentalChunkFeature");
        debugModeEnabled = serializedObject.FindProperty("debugModeEnabled");

    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();

        EditorGUILayout.PropertyField(chunkPrefab, new GUIContent("Chunk Prefab", "The Chunk Prefab"));

        GUILayout.Label("Material & Shader Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(unexploredColor, new GUIContent("Unexplored Color", unexploredColor_tooltip));
        EditorGUILayout.PropertyField(exploredColor, new GUIContent("Explored Color", exploredColor_tooltip));
        EditorGUILayout.PropertyField(fogMat, new GUIContent("Fog Material", fogMat_tooltip));
        EditorGUILayout.PropertyField(fogShader, new GUIContent("Fog Shader", fogShader_tooltip));
        GUILayout.Space(5f);

        GUILayout.Label("Render Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(drawMode, new GUIContent("Draw Mode", drawMode_tooltip));
        EditorGUILayout.PropertyField(orientation, new GUIContent("Orientation", orientation_tooltip));
        EditorGUILayout.PropertyField(fogFilterMode, new GUIContent("Fog Filter Mode", fogFilterMode_tooltip));
        EditorGUILayout.PropertyField(textureQualityPerChunk, new GUIContent("Texture Quality", textureQualityPerChunk_tooltip));
        EditorGUILayout.PropertyField(textureBlendTime, new GUIContent("Texture Blend Time", textureBlendTime_tooltip));
        EditorGUILayout.PropertyField(blurIterations, new GUIContent("Blur Iterations", blurIterations_tooltip));
        EditorGUILayout.PropertyField(updateFrequency, new GUIContent("Update Frequency", updateFrequency_tooltip));
        EditorGUILayout.PropertyField(fogRenderHeightPosition, new GUIContent("Fog Render Height Position", fogRenderHeightPosition_tooltip));
        EditorGUILayout.PropertyField(clampBlendFactorToTextureTime, new GUIContent("Clamp BlendFactor to Texture Time", clampBlendFactorToTextureTime_tooltip));
        GUILayout.Space(5f);

        GUILayout.Label("Mesh Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fogMesh, new GUIContent("Fog Mesh", "The mesh that the fog will be renderered on."));
        EditorGUILayout.PropertyField(meshScaleDivisor, new GUIContent("Mesh Scale Divisor", meshScaleDivisor_tooltip));
        EditorGUILayout.PropertyField(meshScalePostMultiplier, new GUIContent("Mesh Scale Post Multiplier", meshScalePostMultiplier_tooltip));
        if (targ.orientation == MangoFogOrientation.Perspective3D)
            EditorGUILayout.PropertyField(Perspective3DRenderRotation, new GUIContent("Perspective 3D Mesh Rotation", Perspective3DRenderRotation_tooltip));
        else
            EditorGUILayout.PropertyField(Orthographic2DRenderRotation, new GUIContent("Orthographic 2D Mesh Rotation", Orthographic2DRenderRotation_tooltip));

        GUILayout.Space(5f);

        GUILayout.Label("Sprite Mode Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fogSpritePPU, new GUIContent("Fog Sprite PPU", fogSpritePPU_tooltip));
        EditorGUILayout.PropertyField(fogSpriteSlicingSize, new GUIContent("Fog Sprite Slicing Size", fogSpriteSlicingSize_tooltip));
        EditorGUILayout.PropertyField(fogSpriteRenderLayer, new GUIContent("Fog Sprite Render Layer", fogSpriteRenderLayer_tooltip));
        EditorGUILayout.PropertyField(fogSpriteRenderOrder, new GUIContent("Fog Sprite Render Order", fogSpriteRenderOrder_tooltip));

        GUILayout.Space(5f);

        GUILayout.Label("World Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(rootChunkPosition, new GUIContent("Root Chunk Position", rootChunkPosition_tooltip));
        EditorGUILayout.PropertyField(chunkSize, new GUIContent("Fog Size", chunkSize_tooltip));
        GUILayout.Space(5f);

        GUILayout.Label("Line Of Sight Options", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(heightObstacleMask, new GUIContent("LOS Obstacle LayerMask", heightObstacleMask_tooltip));
        EditorGUILayout.PropertyField(heightRange, new GUIContent("LOS Level Height Range", heightRange_tooltip));
		EditorGUILayout.PropertyField(margin, new GUIContent("LOS Height Margin", margin_tooltip));
        EditorGUILayout.PropertyField(chunkLOSRaycastRadius, new GUIContent("LOS Raycast Radius", chunkLOSRaycastRadius_tooltip));
        GUILayout.Space(5f);

        GUILayout.Label("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(debugModeEnabled, new GUIContent("Debug Mode", "Enable the Debug Logs and MongoFogDebug Instance if it exists."));

		if (targ.debugModeEnabled)
		{
            GUILayout.Label("Experimental", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(doExperimentalChunkFeature, new GUIContent("Enable Experimental Chunk Feature", "Enable the experimental options"));
            if (targ.doExperimentalChunkFeature)
            {
                EditorGUILayout.PropertyField(chunkSquaredAmount, new GUIContent("Chunk Squared Amount", chunkSquaredAmount_tooltip));
                EditorGUILayout.PropertyField(chunkBoundsMultiplier, new GUIContent("Chunk Bounds Multiplier", chunkBoundsMultiplier_tooltip));
                EditorGUILayout.PropertyField(boundsDepth, new GUIContent("Chunk Bounds Depth", boundsDepth_tooltip));
                EditorGUILayout.PropertyField(chunkSeamBufferDelay, new GUIContent("Chunk Seam Buffer Delay", chunkSeamBufferDelay_tooltip));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
