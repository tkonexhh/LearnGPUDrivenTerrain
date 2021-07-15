using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUTerrain : MonoBehaviour
{
    public TerrainAsset terrainAsset;

    public bool patchDebug = false;
    public bool nodeDebug = false;
    public bool mipDebug = false;
    public bool patchBoundsDebug = false;

    // [Range(0.1f, 1.9f)] public float distanceEvaluation = 1.2f;

    private TerrainBuilder m_TerrainBuilder;
    private Material m_TerrainMaterial;


    private bool m_IsTerrainMaterialDirty = false;

    private void Awake()
    {
        m_TerrainBuilder = new TerrainBuilder(terrainAsset);
        InitMaterial();

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
            // Debug.LogError(terrainAsset.worldSize);
            material.SetVector("_WorldSize", terrainAsset.worldSize);
            m_TerrainMaterial = material;
            this.UpdateTerrainMaterialProeprties();
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
            m_TerrainBuilder.isBoundsBufferOn = this.patchBoundsDebug;
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
    }
}
