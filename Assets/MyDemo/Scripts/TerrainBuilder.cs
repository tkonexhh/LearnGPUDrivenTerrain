using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class TerrainBuilder : System.IDisposable
{
    private TerrainAsset m_TerrainAsset;
    private CommandBuffer m_CommandBuffer = new CommandBuffer();
    private ComputeShader m_ComputeShader;

    private ComputeBuffer m_MaxLODNodeList;//MAXLOD下的Node list


    private ComputeBuffer _nodeListA;
    private ComputeBuffer _nodeListB;
    private ComputeBuffer m_IndirectArgsBuffer;

    private ComputeBuffer m_CulledPatchBuffer;//裁剪之后剩余的Patch
    private ComputeBuffer m_PatchIndirectArgs;//最终用于DrawIndirect

    private ComputeBuffer m_FinalNodeListBuffer;//CS 中的AppendFinalNodeList FinalNodeList
    private ComputeBuffer m_NodeDescriptors;//每一层Lod节点描述


    //==========PatchBounds 可视化
    private ComputeBuffer m_PatchBoundsIndirectArgs;//Patch Bounds 
    private ComputeBuffer m_PatchBoundsBuffer;
    public ComputeBuffer patchBoundsIndirectArgs => m_PatchBoundsIndirectArgs;
    public ComputeBuffer patchBoundsBuffer => m_PatchBoundsBuffer;
    //----------

    //=========NodeBounds 可视化
    private ComputeBuffer m_NodeBoundsIndirectArgs;//Patch Bounds 
    private ComputeBuffer m_NodeBoundsBuffer;
    public ComputeBuffer nodeBoundsIndirectArgs => m_NodeBoundsIndirectArgs;
    public ComputeBuffer nodeBoundsBuffer => m_NodeBoundsBuffer;
    //---------

    //=========摄像机裁剪
    //摄像机平面
    private Plane[] m_CameraFrustumPlanes = new Plane[6];
    //摄像机平面传入Shader的值 xyz法线方向 w
    private Vector4[] m_CameraFrustumPlanesV4 = new Vector4[6];
    //--------

    //====== LOD Map
    private RenderTexture m_LodMapRT;
    //------


    public ComputeBuffer patchIndirectArgs => m_PatchIndirectArgs;
    public ComputeBuffer culledPatchBuffer => m_CulledPatchBuffer;



    private int m_KernelOfTraverseQuadTree;
    private int m_KernelOfBuildLodMap;
    private int m_KernelOfBuildPatches;

    /// <summary>
    /// Buffer的大小需要根据预估的最大分割情况进行分配.
    /// </summary>
    private int m_MaxNodeBufferSize = 200;
    private int _tempNodeBufferSize = 50;


    private bool _isPatchBoundsBufferOn;
    public bool isPatchBoundsBufferOn
    {
        set
        {
            if (value)
            {
                m_ComputeShader.EnableKeyword("PATCH_BOUNDS_DEBUG");
            }
            else
            {
                m_ComputeShader.DisableKeyword("PATCH_BOUNDS_DEBUG");
            }
            _isPatchBoundsBufferOn = value;
        }
        get
        {
            return _isPatchBoundsBufferOn;
        }
    }

    private bool _isNodeBoundsBufferOn;
    public bool isNodeBoundsBufferOn
    {
        set
        {
            if (value)
            {
                m_ComputeShader.EnableKeyword("NODE_BOUNDS_DEBUG");
            }
            else
            {
                m_ComputeShader.DisableKeyword("NODE_BOUNDS_DEBUG");
            }
            _isNodeBoundsBufferOn = value;
        }
        get
        {
            return _isNodeBoundsBufferOn;
        }
    }


    private bool m_IsCullOn;
    public bool isCullOn
    {
        set
        {
            if (value)
            {
                m_ComputeShader.EnableKeyword("CULL_ON");
            }
            else
            {
                m_ComputeShader.DisableKeyword("CULL_ON");
            }
            m_IsCullOn = value;
        }

        get => m_IsCullOn;
    }

    public int boundsHeightRedundance
    {
        set
        {
            m_ComputeShader.SetInt("_BoundsHeightRedundance", value);
        }
    }

    public TerrainBuilder(TerrainAsset asset)
    {
        m_TerrainAsset = asset;
        m_ComputeShader = m_TerrainAsset.computeShader;
        m_CommandBuffer.name = "GPUDTerrain";

        //最大LOD情况下的NodeBuffer 用于首次四叉树细分
        m_MaxLODNodeList = new ComputeBuffer(TerrainAsset.MAX_LOD_NODE_COUNT * TerrainAsset.MAX_LOD_NODE_COUNT, 8, ComputeBufferType.Append);
        this.InitMaxLODNodeListDatas();
        //用于细分的两个零食ComputerBuffer
        _nodeListA = new ComputeBuffer(_tempNodeBufferSize, 8, ComputeBufferType.Append);
        _nodeListB = new ComputeBuffer(_tempNodeBufferSize, 8, ComputeBufferType.Append);

        m_FinalNodeListBuffer = new ComputeBuffer(m_MaxNodeBufferSize, 12, ComputeBufferType.Append);
        m_NodeDescriptors = new ComputeBuffer(TerrainDefine.MAX_NODE_ID, 4);

        m_IndirectArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);
        m_IndirectArgsBuffer.SetData(new uint[] { 1, 1, 1 });


        {//Patch Bounds Debug 之后相关的都可以去掉
            m_PatchBoundsBuffer = new ComputeBuffer(m_MaxNodeBufferSize * 64, 4 * 10, ComputeBufferType.Append);
            m_PatchBoundsIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
            m_PatchBoundsIndirectArgs.SetData(new uint[] { TerrainAsset.unitCubeMesh.GetIndexCount(0), 0, 0, 0, 0 });
        }

        {//Node Bounds Debug

            m_NodeBoundsBuffer = new ComputeBuffer(m_MaxNodeBufferSize * 64, 4 * 10, ComputeBufferType.Append);
            m_NodeBoundsIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
            m_NodeBoundsIndirectArgs.SetData(new uint[] { TerrainAsset.unitCubeMesh.GetIndexCount(0), 0, 0, 0, 0 });
        }

        //lodMap
        m_LodMapRT = TextureUtility.CreateLODMap(160);//5*Mathf.Pow(2,TerrainAsset.MAX_LOD)=160;


        m_CulledPatchBuffer = new ComputeBuffer(m_MaxNodeBufferSize * 64, 9 * 4, ComputeBufferType.Append);

        m_PatchIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        m_PatchIndirectArgs.SetData(new uint[] { TerrainAsset.patchMesh.GetIndexCount(0), 0, 0, 0, 0 });


        if (SystemInfo.usesReversedZBuffer)
        {
            m_ComputeShader.EnableKeyword("_REVERSE_Z");
        }
        else
        {
            m_ComputeShader.DisableKeyword("_REVERSE_Z");
        }

        this.InitKernels();
        this.InitWorldParams();
    }

    private void InitMaxLODNodeListDatas()
    {
        var maxLODNodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
        uint2[] datas = new uint2[maxLODNodeCount * maxLODNodeCount];
        var index = 0;
        for (uint i = 0; i < maxLODNodeCount; i++)
        {
            for (uint j = 0; j < maxLODNodeCount; j++)
            {
                datas[index] = new uint2(i, j);
                index++;
            }
        }
        //初始赋值 5*5的Node
        m_MaxLODNodeList.SetData(datas);
    }

    private void InitKernels()
    {
        //设置kernel
        //绑定相关变量 buffer
        m_KernelOfTraverseQuadTree = m_ComputeShader.FindKernel("TraverseQuadTree");
        m_KernelOfBuildPatches = m_ComputeShader.FindKernel("BuildPatches");
        m_KernelOfBuildLodMap = m_ComputeShader.FindKernel("BuildLodMap");

        this.BindComputeShader(m_KernelOfTraverseQuadTree);
        this.BindComputeShader(m_KernelOfBuildPatches);
        this.BindComputeShader(m_KernelOfBuildLodMap);
    }

    private void BindComputeShader(int kernelIndex)
    {
        if (kernelIndex == m_KernelOfTraverseQuadTree)
        {
            m_ComputeShader.SetBuffer(kernelIndex, ShaderConstants.AppendFinalNodeList, m_FinalNodeListBuffer);
            m_ComputeShader.SetTexture(kernelIndex, "MinMaxHeightTexture", m_TerrainAsset.minMaxHeightMap);
            m_ComputeShader.SetBuffer(kernelIndex, "NodeBoundsList", m_NodeBoundsBuffer);
            m_ComputeShader.SetBuffer(kernelIndex, ShaderConstants.NodeDescriptors, m_NodeDescriptors);

        }
        else if (kernelIndex == m_KernelOfBuildLodMap)
        {
            m_ComputeShader.SetTexture(kernelIndex, ShaderConstants.LodMap, m_LodMapRT);
            m_ComputeShader.SetBuffer(kernelIndex, ShaderConstants.NodeDescriptors, m_NodeDescriptors);
        }
        else if (kernelIndex == m_KernelOfBuildPatches)
        {
            m_ComputeShader.SetTexture(kernelIndex, ShaderConstants.LodMap, m_LodMapRT);
            m_ComputeShader.SetTexture(kernelIndex, "MinMaxHeightTexture", m_TerrainAsset.minMaxHeightMap);
            //TraverseQuadTree 中的AppendFinalNodeList 填入到 FinalNodeList
            m_ComputeShader.SetBuffer(kernelIndex, ShaderConstants.FinalNodeList, m_FinalNodeListBuffer);
            m_ComputeShader.SetBuffer(kernelIndex, "CulledPatchList", m_CulledPatchBuffer);
            m_ComputeShader.SetBuffer(kernelIndex, "PatchBoundsList", m_PatchBoundsBuffer);
        }

    }


    private void InitWorldParams()
    {
        float wSize = m_TerrainAsset.worldSize.x;
        int nodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
        Vector4[] worldLODParams = new Vector4[TerrainAsset.MAX_LOD + 1];
        /*
        记录了每个Lod级别的(nodeSize,halfPatchSize,nodeCount,sectorCountPerNode) 目前0-5 6层
        其中:
        - nodeSize为Node的边长(米)
        - halfPatchSize等于nodeSize/8/2 patch宽的一半   
        - nodeCount等于WorldSize/nodeSize
        - sectorCountPerNode等于2^lod LODMap不同层级下Node 在160*160 这张图上占的像素大小
        */
        for (var lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
        {
            var nodeSize = wSize / nodeCount;//2048 1024 512 256 128 64
            var halfPatchSize = nodeSize / TerrainDefine.Node_PACTH_COM / 2;//128 64 32 16 8 4  
            var sectorCountPerNode = (int)Mathf.Pow(2, lod);//32 16 8 4 2 1 
            // Debug.LogError(sectorCountPerNode);
            worldLODParams[lod] = new Vector4(nodeSize, halfPatchSize, nodeCount, sectorCountPerNode);
            nodeCount *= 2;//5 10 20 40 80 160
        }
        m_ComputeShader.SetVectorArray(ShaderConstants.WorldLodParams, worldLODParams);


        //5:0
        //4:5*5=25
        //3:25+10*10=125 
        //2:125+20*20=525
        //1:525+40*40=2125 
        //0:2125+80*80=8525
        int[] nodeIDOffsetLOD = new int[(TerrainAsset.MAX_LOD + 1) * 4];//24 为啥*4 ?????
        int nodeIdOffset = 0;
        for (int lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
        {
            nodeIDOffsetLOD[lod * 4] = nodeIdOffset;
            nodeIdOffset += (int)(worldLODParams[lod].z * worldLODParams[lod].z);
        }
        m_ComputeShader.SetInts(ShaderConstants.NodeIDOffsetOfLOD, nodeIDOffsetLOD);
    }

    private void ClearBufferCounter()
    {
        m_CommandBuffer.SetBufferCounterValue(m_MaxLODNodeList, (uint)m_MaxLODNodeList.count);
        m_CommandBuffer.SetBufferCounterValue(_nodeListA, 0);
        m_CommandBuffer.SetBufferCounterValue(_nodeListB, 0);
        m_CommandBuffer.SetBufferCounterValue(m_FinalNodeListBuffer, 0);
        m_CommandBuffer.SetBufferCounterValue(m_CulledPatchBuffer, 0);

        m_CommandBuffer.SetBufferCounterValue(m_PatchBoundsBuffer, 0);
        m_CommandBuffer.SetBufferCounterValue(m_NodeBoundsBuffer, 0);
    }

    /// <summary>
    /// 更新摄像机椎体
    /// </summary>
    /// <param name="camera"></param>
    private void UpdateCameraFrustunPlanes(Camera camera)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, m_CameraFrustumPlanes);
        for (var i = 0; i < m_CameraFrustumPlanes.Length; i++)
        {
            Vector4 v4 = (Vector4)m_CameraFrustumPlanes[i].normal;
            v4.w = m_CameraFrustumPlanes[i].distance;
            m_CameraFrustumPlanesV4[i] = v4;
        }
        m_ComputeShader.SetVectorArray(ShaderConstants.CameraFrustumPlanes, m_CameraFrustumPlanesV4);
    }

    public void Dispatch()
    {
        var camera = Camera.main;
        //clear
        m_CommandBuffer.Clear();
        this.ClearBufferCounter();
        {//使用摄像机裁剪
            this.UpdateCameraFrustunPlanes(camera);
        }

        m_CommandBuffer.SetComputeVectorParam(m_ComputeShader, ShaderConstants.WorldSize, m_TerrainAsset.worldSize);
        m_CommandBuffer.SetComputeVectorParam(m_ComputeShader, ShaderConstants.CameraPositionWS, camera.transform.position);


        //四叉树分割计算得到初步的Patch列表
        m_CommandBuffer.CopyCounterValue(m_MaxLODNodeList, m_IndirectArgsBuffer, 0);
        ComputeBuffer consumeNodeList = _nodeListA;
        ComputeBuffer appendNodeList = _nodeListB;
        for (var lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)//6层LOD
        {
            //CS 设置LOD
            m_CommandBuffer.SetComputeIntParam(m_ComputeShader, ShaderConstants.PassLOD, lod);
            //最大LOD的Node
            //一开始的Node 5*5 入栈
            m_CommandBuffer.SetComputeBufferParam(m_ComputeShader, m_KernelOfTraverseQuadTree, ShaderConstants.ConsumeNodeList, lod == TerrainAsset.MAX_LOD ? m_MaxLODNodeList : consumeNodeList);
            m_CommandBuffer.SetComputeBufferParam(m_ComputeShader, m_KernelOfTraverseQuadTree, ShaderConstants.AppendNodeList, appendNodeList);
            //执行CS m_IndirectArgsBuffer是线程组的数量
            m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfTraverseQuadTree, m_IndirectArgsBuffer, 0);
            m_CommandBuffer.CopyCounterValue(appendNodeList, m_IndirectArgsBuffer, 0);
            var temp = consumeNodeList;
            consumeNodeList = appendNodeList;
            appendNodeList = temp;

        }

        //生成LODMap
        m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfBuildLodMap, 20, 20, 1);

        //生成Patch
        //FinalNodeList的Counter拷贝给IndirectArgs，代表我们要起的线程组数量
        m_CommandBuffer.CopyCounterValue(m_FinalNodeListBuffer, m_IndirectArgsBuffer, 0);
        m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfBuildPatches, m_IndirectArgsBuffer, 0);
        m_CommandBuffer.CopyCounterValue(m_CulledPatchBuffer, m_PatchIndirectArgs, 4);

        {//Patchs Bound Debug
            if (isPatchBoundsBufferOn)
            {
                m_CommandBuffer.CopyCounterValue(m_PatchBoundsBuffer, m_PatchBoundsIndirectArgs, 4);
            }
        }

        {//Node Bounds Debug
            if (isNodeBoundsBufferOn)
            {
                m_CommandBuffer.CopyCounterValue(m_NodeBoundsBuffer, m_NodeBoundsIndirectArgs, 4);
            }
        }

        Graphics.ExecuteCommandBuffer(m_CommandBuffer);
        // LogMipmapY();
    }


    // public void LogMipmapY()
    // {
    //     Debug.LogError("------------");
    //     uint2[] datas = new uint2[20];
    //     m_MinMaxHeightBuffer.GetData(datas);
    //     for (int i = 0; i < datas.Length; i++)
    //     {
    //         Debug.LogError(datas[i].x + "===" + datas[i].y);
    //     }
    // }


    public void Dispose()
    {
        m_CulledPatchBuffer.Dispose();
        m_FinalNodeListBuffer.Dispose();
        m_MaxLODNodeList.Dispose();
        _nodeListA.Dispose();
        _nodeListB.Dispose();
        m_NodeDescriptors.Dispose();
        {
            m_PatchBoundsBuffer.Dispose();
            m_PatchBoundsIndirectArgs.Dispose();
        }
        {
            m_NodeBoundsBuffer.Dispose();
            m_NodeBoundsIndirectArgs.Dispose();
        }
        m_PatchIndirectArgs.Dispose();
        m_IndirectArgsBuffer.Dispose();
    }

    private class ShaderConstants
    {
        public static readonly int WorldSize = Shader.PropertyToID("_WorldSize");
        public static readonly int CameraPositionWS = Shader.PropertyToID("_CameraPositionWS");
        public static readonly int CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");
        public static readonly int PassLOD = Shader.PropertyToID("PassLOD");
        public static readonly int AppendFinalNodeList = Shader.PropertyToID("AppendFinalNodeList");
        public static readonly int FinalNodeList = Shader.PropertyToID("FinalNodeList");
        public static readonly int AppendNodeList = Shader.PropertyToID("AppendNodeList");
        public static readonly int ConsumeNodeList = Shader.PropertyToID("ConsumeNodeList");
        public static readonly int WorldLodParams = Shader.PropertyToID("WorldLodParams");
        public static readonly int NodeIDOffsetOfLOD = Shader.PropertyToID("NodeIDOffsetOfLOD");
        public static readonly int NodeDescriptors = Shader.PropertyToID("NodeDescriptors");
        public static readonly int LodMap = Shader.PropertyToID("_LodMap");
    }
}


