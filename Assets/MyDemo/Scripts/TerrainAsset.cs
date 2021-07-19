using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CreateAssetMenu(menuName = "TerrainLearn/TerrainAsset")]
public class TerrainAsset : ScriptableObject
{

    public const int MAX_LOD = 5;

    /// <summary>
    /// MAX LOD下，世界由5x5个区块组成
    /// </summary>
    public const int MAX_LOD_NODE_COUNT = 5;

    [SerializeField] private Vector3 m_WorldSize = new Vector3(10240, 2048, 10240);
    [SerializeField] private Texture2D m_HeightMap;
    [SerializeField] private Texture2D m_AlbedoMap;
    [SerializeField] private Texture2D m_NormalMap;

    [SerializeField] private Texture2D[] minMaxHeightMaps;

    [SerializeField] private ComputeShader _terrainCompute;



    private static Mesh _patchMesh;

    public static Mesh patchMesh
    {
        get
        {
            if (!_patchMesh)
            {
                _patchMesh = MeshUtility.CreatePlaneMesh(TerrainDefine.PATCH_MESH_GRID_COUNT);
            }
            return _patchMesh;
        }
    }


    private static Mesh _unitCubeMesh;

    public static Mesh unitCubeMesh
    {
        get
        {
            if (!_unitCubeMesh)
            {
                _unitCubeMesh = MeshUtility.CreateCube(1);
            }
            return _unitCubeMesh;
        }
    }

    private RenderTexture _minMaxHeightMap;
    public RenderTexture minMaxHeightMap
    {
        get
        {
            if (!_minMaxHeightMap)
            {
                _minMaxHeightMap = TextureUtility.CreateRenderTextureWithMipTextures(minMaxHeightMaps, RenderTextureFormat.RG32);
                TextureUtility.SaveRenderTexture(_minMaxHeightMap, Application.streamingAssetsPath, "MinMaxHeightMap");
            }
            return _minMaxHeightMap;
        }
    }

    private Material m_PatchBoundsDebugMaterial;
    public Material patchBoundsDebugMaterial
    {
        get
        {
            if (!m_PatchBoundsDebugMaterial)
            {
                m_PatchBoundsDebugMaterial = new Material(Shader.Find("XHH/BoundsDebug"));
            }
            return m_PatchBoundsDebugMaterial;
        }
    }

    private Material m_NodeBoundsDebugMaterial;
    public Material nodeBoundsDebugMaterial
    {
        get
        {
            if (!m_NodeBoundsDebugMaterial)
                m_NodeBoundsDebugMaterial = new Material(Shader.Find("XHH/BoundsDebug"));
            return m_NodeBoundsDebugMaterial;
        }
    }


    public Vector3 worldSize => m_WorldSize;
    public Texture2D heightMap => m_HeightMap;
    public Texture2D albedoMap => m_AlbedoMap;
    public Texture2D normalMap => m_NormalMap;
    public ComputeShader computeShader => _terrainCompute;
}
