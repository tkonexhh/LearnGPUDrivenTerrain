using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class TerrainBuilder : System.IDisposable
{
    private TerrainAsset m_TerrainAsset;

    private ComputeShader m_ComputeShader;
    private ComputeBuffer m_MaxLODNodeList;//MAXLOD下的Node list
    private CommandBuffer m_CommandBuffer = new CommandBuffer();

    private ComputeBuffer _nodeListA;
    private ComputeBuffer _nodeListB;

    private ComputeBuffer m_IndirectArgsBuffer;
    private ComputeBuffer m_PatchIndirectArgs;
    private ComputeBuffer m_CulledPatchBuffer;

    private ComputeBuffer m_FinalNodeListBuffer;//CS 中的AppendFinalNodeList FinalNodeList

    public ComputeBuffer patchIndirectArgs => m_PatchIndirectArgs;
    public ComputeBuffer culledPatchBuffer => m_CulledPatchBuffer;

    private int m_KernelOfTraverseQuadTree;
    private int m_KernelOfBuildPatches;

    /// <summary>
    /// Buffer的大小需要根据预估的最大分割情况进行分配.
    /// </summary>
    private int m_MaxNodeBufferSize = 200;
    private int _tempNodeBufferSize = 50;

    public TerrainBuilder(TerrainAsset asset)
    {
        m_TerrainAsset = asset;
        m_ComputeShader = m_TerrainAsset.computeShader;
        m_CommandBuffer.name = "GPUTerrain";

        //最大LOD情况下的NodeBuffer
        m_MaxLODNodeList = new ComputeBuffer(TerrainAsset.MAX_LOD_NODE_COUNT * TerrainAsset.MAX_LOD_NODE_COUNT, 8, ComputeBufferType.Append | ComputeBufferType.Counter | ComputeBufferType.Structured);
        this.InitMaxLODNodeListDatas();

        _nodeListA = new ComputeBuffer(_tempNodeBufferSize, 8, ComputeBufferType.Append | ComputeBufferType.Counter | ComputeBufferType.Structured);
        _nodeListB = new ComputeBuffer(_tempNodeBufferSize, 8, ComputeBufferType.Append | ComputeBufferType.Counter | ComputeBufferType.Structured);

        m_FinalNodeListBuffer = new ComputeBuffer(m_MaxNodeBufferSize, 12, ComputeBufferType.Append);

        m_IndirectArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);
        m_IndirectArgsBuffer.SetData(new uint[] { 1, 1, 1 });

        m_CulledPatchBuffer = new ComputeBuffer(m_MaxNodeBufferSize * 64, 9 * 4, ComputeBufferType.Append | ComputeBufferType.Counter);

        m_PatchIndirectArgs = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        m_PatchIndirectArgs.SetData(new uint[] { TerrainAsset.patchMesh.GetIndexCount(0), 0, 0, 0, 0 });


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

        this.BindComputeShader(m_KernelOfTraverseQuadTree);
        this.BindComputeShader(m_KernelOfBuildPatches);
    }

    private void BindComputeShader(int kernelIndex)
    {
        if (kernelIndex == m_KernelOfTraverseQuadTree)
        {
            m_ComputeShader.SetBuffer(m_KernelOfTraverseQuadTree, ShaderConstants.AppendFinalNodeList, m_FinalNodeListBuffer);
        }
        else if (kernelIndex == m_KernelOfBuildPatches)
        {
            //TraverseQuadTree 中的AppendFinalNodeList 填入到 FinalNodeList
            m_ComputeShader.SetBuffer(m_KernelOfBuildPatches, ShaderConstants.FinalNodeList, m_FinalNodeListBuffer);
            m_ComputeShader.SetBuffer(m_KernelOfBuildPatches, "CulledPatchList", m_CulledPatchBuffer);
        }
    }


    private void InitWorldParams()
    {
        float wSize = m_TerrainAsset.worldSize.x;
        int nodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
        Vector4[] worldLODParams = new Vector4[TerrainAsset.MAX_LOD + 1];
        /*
        记录了每个Lod级别的(nodeSize,patchExtent,nodeCount,sectorCountPerNode) 目前0-5 6层
        其中:
        - nodeSize为Node的边长(米)
        - patchExtent等于nodeSize/16  
        - nodeCount等于WorldSize/nodeSize
        - sectorCountPerNode等于2^lod
        */
        for (var lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
        {
            var nodeSize = wSize / nodeCount;//2048 1024 512 256 128 64
            var patchExtent = nodeSize / TerrainDefine.PatchSize;//128 64 32 16 8 4  
            var sectorCountPerNode = (int)Mathf.Pow(2, lod);//32 16 8 4 2 1
            worldLODParams[lod] = new Vector4(nodeSize, patchExtent, nodeCount, sectorCountPerNode);
            nodeCount *= 2;//5 10 20 40 80 160
        }
        m_ComputeShader.SetVectorArray(ShaderConstants.WorldLodParams, worldLODParams);



        // int[] nodeIDOffsetLOD = new int[(TerrainAsset.MAX_LOD + 1) * 4];//24
        // int nodeIdOffset = 0;
        // for (int lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
        // {
        //     nodeIDOffsetLOD[lod * 4] = nodeIdOffset;
        //     nodeIdOffset += (int)(worldLODParams[lod].z * worldLODParams[lod].z);
        // }
        // //5:160*160 
        // //4:160*160+80*80 
        // //3:160*160+80*80+40*40 
        // //2:160*160+80*80+40*40+20*20 
        // //1:160*160+80*80+40*40+20*20+10*10 
        // //0:160*160+80*80+40*40+20*20+10*10+5*5 = 34125
        // m_ComputeShader.SetInts(ShaderConstants.NodeIDOffsetOfLOD, nodeIDOffsetLOD);
    }

    private void ClearBufferCounter()
    {
        m_CommandBuffer.SetBufferCounterValue(m_MaxLODNodeList, (uint)m_MaxLODNodeList.count);
        m_CommandBuffer.SetBufferCounterValue(_nodeListA, 0);
        m_CommandBuffer.SetBufferCounterValue(_nodeListB, 0);
        m_CommandBuffer.SetBufferCounterValue(m_FinalNodeListBuffer, 0);
        m_CommandBuffer.SetBufferCounterValue(m_CulledPatchBuffer, 0);
        // m_CommandBuffer.SetBufferCounterValue(m_PatchBoundsBuffer, 0);
    }

    public void Dispatch()
    {
        var camera = Camera.main;
        //clear
        m_CommandBuffer.Clear();
        this.ClearBufferCounter();

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



        //生成Patch
        //FinalNodeList的Counter拷贝给IndirectArgs，代表我们要起的线程组数量
        m_CommandBuffer.CopyCounterValue(m_FinalNodeListBuffer, m_IndirectArgsBuffer, 0);
        m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfBuildPatches, m_IndirectArgsBuffer, 0);
        m_CommandBuffer.CopyCounterValue(m_CulledPatchBuffer, m_PatchIndirectArgs, 4);

        Graphics.ExecuteCommandBuffer(m_CommandBuffer);

    }

    private void LogMaxArg()
    {
        var maxLODNodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
        uint2[] datas = new uint2[maxLODNodeCount * maxLODNodeCount];
        m_MaxLODNodeList.GetData(datas);
        for (int i = 0; i < datas.Length; i++)
        {
            Debug.LogError(datas[i].x + "===" + datas[i].y);
        }
    }

    private void LogFinalNode()
    {
        Debug.LogError("------------");
        // var maxLODNodeCount = TerrainAsset.MAX_LOD_NODE_COUNT;
        uint3[] datas = new uint3[200];
        m_FinalNodeListBuffer.GetData(datas);
        for (int i = 0; i < datas.Length; i++)
        {
            Debug.LogError(datas[i].x + "===" + datas[i].y + "===" + datas[i].z);
        }
    }


    public void Dispose()
    {
        m_FinalNodeListBuffer.Dispose();
        m_MaxLODNodeList.Dispose();
        _nodeListA.Dispose();
        _nodeListB.Dispose();
    }

    private class ShaderConstants
    {
        public static readonly int CameraPositionWS = Shader.PropertyToID("_CameraPositionWS");
        public static readonly int PassLOD = Shader.PropertyToID("PassLOD");
        public static readonly int AppendFinalNodeList = Shader.PropertyToID("AppendFinalNodeList");
        public static readonly int FinalNodeList = Shader.PropertyToID("FinalNodeList");
        public static readonly int AppendNodeList = Shader.PropertyToID("AppendNodeList");
        public static readonly int ConsumeNodeList = Shader.PropertyToID("ConsumeNodeList");
        public static readonly int WorldLodParams = Shader.PropertyToID("WorldLodParams");
        public static readonly int NodeIDOffsetOfLOD = Shader.PropertyToID("NodeIDOffsetOfLOD");
    }
}


