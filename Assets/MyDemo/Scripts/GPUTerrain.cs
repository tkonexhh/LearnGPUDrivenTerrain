using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUTerrain : MonoBehaviour
{
    public TerrainAsset terrainAsset;
    private TerrainBuilder m_TerrainBuilder;
    private Material m_TerrainMaterial;

    private void Awake()
    {
        m_TerrainBuilder = new TerrainBuilder(terrainAsset);
        InitMaterial();

        Mesh mesh = TerrainAsset.patchMesh;

        // float grid = (0.5f * 16);
        // for (int y = 0; y < 5; y++)
        // {
        //     for (int x = 0; x < 5; x++)
        //     {
        //         GameObject obj = new GameObject();
        //         obj.name = x + "-" + y;
        //         obj.transform.position = new Vector3(x * grid, 0, y * grid);
        //         obj.AddComponent<MeshFilter>().mesh = mesh;
        //         obj.AddComponent<MeshRenderer>().material = m_TerrainMaterial;
        //     }
        // }
    }

    private void InitMaterial()
    {
        if (!m_TerrainMaterial)
        {
            var material = new Material(Shader.Find("XHH/Terrain"));
            material.SetTexture("_MainTex", terrainAsset.albedoMap);
            material.SetTexture("_HeightMap", terrainAsset.heightMap);
            material.SetTexture("_NormalMap", terrainAsset.normalMap);
            // material.SetBuffer("PatchList", m_TerrainBuilder.culledPatchBuffer);
            Debug.LogError(terrainAsset.worldSize);
            material.SetVector("_WorldSize", terrainAsset.worldSize);
            m_TerrainMaterial = material;
        }
    }


    private void Update()
    {
        m_TerrainBuilder.Dispatch();
        Graphics.DrawMeshInstancedIndirect(TerrainAsset.patchMesh, 0, m_TerrainMaterial, new Bounds(Vector3.zero, Vector3.one * 10240), m_TerrainBuilder.patchIndirectArgs);
    }
}
