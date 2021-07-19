using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Android;

public class GPUTerrain : MonoBehaviour
{
    public TerrainAsset terrainAsset;

    public bool Cull = true;
    public bool patchDebug = false;
    public bool nodeDebug = false;
    public bool mipDebug = false;
    public bool nodeBoundsDebug = false;//Node包围盒
    public bool patchBoundsDebug = false;//Patch包围盒

    // [Range(0.1f, 1.9f)] public float distanceEvaluation = 1.2f;

    private TerrainBuilder m_TerrainBuilder;
    private Material m_TerrainMaterial;


    private bool m_IsTerrainMaterialDirty = false;

    private void Awake()
    {
        m_TerrainBuilder = new TerrainBuilder(terrainAsset);


        InitMaterial();
        this.ApplySettings();
    }

    private void InitMaterial()
    {
        if (!m_TerrainMaterial)
        {

            var material = new Material(Shader.Find("XHH/Terrain"));

            material.SetTexture("_MainTex", terrainAsset.albedoMap);
            material.SetTexture("_HeightMap", terrainAsset.heightMap);
            material.SetTexture("_NormalMap", terrainAsset.normalMap);
            material.SetBuffer("PatchList", m_TerrainBuilder.culledPatchBuffer);
            material.SetVector("_WorldSize", terrainAsset.worldSize);
            m_TerrainMaterial = material;
            this.UpdateTerrainMaterialProeprties();

            Debug.LogError("RenderTextureFormat.RG32:" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RG32));
            Debug.LogError("copyTextureSupport:" + SystemInfo.copyTextureSupport);
            Debug.LogError("supportsComputeShaders:" + SystemInfo.supportsComputeShaders);
            Debug.LogError("graphicsShaderLevel:" + SystemInfo.graphicsShaderLevel);
            Debug.LogError("OpenGL:" + GetOpenGL());
        }

        if (patchBoundsDebug)
        {
            terrainAsset.patchBoundsDebugMaterial.SetBuffer("BoundsList", m_TerrainBuilder.patchBoundsBuffer);
        }

        if (nodeBoundsDebug)
        {
            terrainAsset.nodeBoundsDebugMaterial.SetBuffer("BoundsList", m_TerrainBuilder.nodeBoundsBuffer);
        }
    }

    private void UpdateTerrainMaterialProeprties()
    {
        m_IsTerrainMaterialDirty = false;
        if (m_TerrainMaterial)
        {
            if (this.mipDebug)
                m_TerrainMaterial.EnableKeyword("ENABLE_MIP_DEBUG");
            else
                m_TerrainMaterial.DisableKeyword("ENABLE_MIP_DEBUG");

            if (this.patchDebug)
                m_TerrainMaterial.EnableKeyword("ENABLE_PATCH_DEBUG");
            else
                m_TerrainMaterial.DisableKeyword("ENABLE_PATCH_DEBUG");

            if (this.nodeDebug)
                m_TerrainMaterial.EnableKeyword("ENABLE_NODE_DEBUG");
            else
                m_TerrainMaterial.DisableKeyword("ENABLE_NODE_DEBUG");



            m_TerrainMaterial.SetMatrix("_WorldToNormalMapMatrix", Matrix4x4.Scale(this.terrainAsset.worldSize).inverse);

        }

        if (patchBoundsDebug)
        {
            if (this.mipDebug)
                terrainAsset.patchBoundsDebugMaterial.EnableKeyword("ENABLE_MIP_DEBUG");
            else
                terrainAsset.patchBoundsDebugMaterial.DisableKeyword("ENABLE_MIP_DEBUG");

            if (this.patchDebug)
                terrainAsset.patchBoundsDebugMaterial.EnableKeyword("ENABLE_PATCH_DEBUG");
            else
                terrainAsset.patchBoundsDebugMaterial.DisableKeyword("ENABLE_PATCH_DEBUG");
        }

        if (nodeBoundsDebug)
        {
            if (this.mipDebug)
                terrainAsset.nodeBoundsDebugMaterial.EnableKeyword("ENABLE_MIP_DEBUG");
            else
                terrainAsset.nodeBoundsDebugMaterial.DisableKeyword("ENABLE_MIP_DEBUG");

            if (this.patchDebug)
                terrainAsset.nodeBoundsDebugMaterial.EnableKeyword("ENABLE_PATCH_DEBUG");
            else
                terrainAsset.nodeBoundsDebugMaterial.DisableKeyword("ENABLE_PATCH_DEBUG");
        }


    }

    private void OnValidate()
    {
        this.ApplySettings();
    }

    private void ApplySettings()
    {
        if (m_TerrainBuilder != null)
        {
            // m_TerrainBuilder.nodeEvalDistance = this.distanceEvaluation;
            m_TerrainBuilder.isPatchBoundsBufferOn = this.patchBoundsDebug;
            m_TerrainBuilder.isNodeBoundsBufferOn = this.nodeBoundsDebug;
            m_TerrainBuilder.isCullOn = this.Cull;
            m_IsTerrainMaterialDirty = true;
        }
    }


    private void Update()
    {
        m_TerrainBuilder.Dispatch();

        if (m_IsTerrainMaterialDirty)
        {
            this.UpdateTerrainMaterialProeprties();
        }


        Graphics.DrawMeshInstancedIndirect(TerrainAsset.patchMesh, 0, m_TerrainMaterial, new Bounds(Vector3.zero, terrainAsset.worldSize), m_TerrainBuilder.patchIndirectArgs);
        if (patchBoundsDebug)
        {
            Graphics.DrawMeshInstancedIndirect(TerrainAsset.unitCubeMesh, 0, terrainAsset.patchBoundsDebugMaterial, new Bounds(Vector3.zero, Vector3.one * 10240), m_TerrainBuilder.patchBoundsIndirectArgs);
        }

        if (nodeBoundsDebug)
        {
            Graphics.DrawMeshInstancedIndirect(TerrainAsset.unitCubeMesh, 0, terrainAsset.nodeBoundsDebugMaterial, new Bounds(Vector3.zero, Vector3.one * 10240), m_TerrainBuilder.nodeBoundsIndirectArgs);
        }
    }


    public static string GetOpenGL()
    {
        string version = "0";
#if (UNITY_ANDROID && !UNITY_EDITOR)
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (AndroidJavaObject curApplication = currentActivity.Call<AndroidJavaObject>("getApplication"))
                    {
                        using (AndroidJavaObject curSystemService = curApplication.Call<AndroidJavaObject>("getSystemService", "activity"))
                        {
                            using (AndroidJavaObject curConfigurationInfo = curSystemService.Call<AndroidJavaObject>("getDeviceConfigurationInfo"))
                            {
                                int reqGlEsVersion = curConfigurationInfo.Get<int>("reqGlEsVersion");
                                using (AndroidJavaClass curInteger = new AndroidJavaClass("java.lang.Integer"))
                                {
                                    version = curInteger.CallStatic<string>("toString", reqGlEsVersion, 16);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            //ILog.Warn(TAG + ", GetOpenGL, Exception: " + e.ToString());
        }
#elif (UNITY_IOS && !UNITY_EDITOR) || IOS_CODE_VIEW
        version = "-1";
#endif
        return version;
    }
}
