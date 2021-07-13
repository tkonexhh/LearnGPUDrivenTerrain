using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

public class TerrainBuilder
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

    private ComputeBuffer m_FinalNodeListBuffer;//compute 中的AppendFinalNodeList

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
        m_MaxLODNodeList.SetData(datas);
    }

    private void InitKernels()
    {
        m_KernelOfTraverseQuadTree = m_ComputeShader.FindKernel("TraverseQuadTree");
        m_ComputeShader.SetBuffer(m_KernelOfTraverseQuadTree, ShaderConstants.AppendFinalNodeList, m_FinalNodeListBuffer);


        m_KernelOfBuildPatches = m_ComputeShader.FindKernel("BuildPatches");
        m_ComputeShader.SetBuffer(m_KernelOfBuildPatches, ShaderConstants.FinalNodeList, m_FinalNodeListBuffer);
        m_ComputeShader.SetBuffer(m_KernelOfBuildPatches, "CulledPatchList", m_CulledPatchBuffer);
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
            var nodeSize = wSize / nodeCount;
            var patchExtent = nodeSize / TerrainDefine.PatchSize;
            var sectorCountPerNode = (int)Mathf.Pow(2, lod);
            worldLODParams[lod] = new Vector4(nodeSize, patchExtent, nodeCount, sectorCountPerNode);
            nodeCount *= 2;
        }
        m_ComputeShader.SetVectorArray(ShaderConstants.WorldLodParams, worldLODParams);



        int[] nodeIDOffsetLOD = new int[(TerrainAsset.MAX_LOD + 1) * 4];//24
        int nodeIdOffset = 0;
        for (int lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)
        {
            nodeIDOffsetLOD[lod * 4] = nodeIdOffset;
            nodeIdOffset += (int)(worldLODParams[lod].z * worldLODParams[lod].z);
        }
        //5:160*160 
        //4:160*160+80*80 
        //3:160*160+80*80+40*40 
        //2:160*160+80*80+40*40+20*20 
        //1:160*160+80*80+40*40+20*20+10*10 
        //0:160*160+80*80+40*40+20*20+10*10+5*5 = 34125
        m_ComputeShader.SetInts(ShaderConstants.NodeIDOffsetOfLOD, nodeIDOffsetLOD);
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
        //clear
        m_CommandBuffer.Clear();
        this.ClearBufferCounter();

        //四叉树分割计算得到初步的Patch列表
        m_CommandBuffer.CopyCounterValue(m_MaxLODNodeList, m_IndirectArgsBuffer, 0);
        ComputeBuffer consumeNodeList = _nodeListA;
        ComputeBuffer appendNodeList = _nodeListB;
        for (var lod = TerrainAsset.MAX_LOD; lod >= 0; lod--)//6层LOD
        {
            m_CommandBuffer.SetComputeIntParam(m_ComputeShader, ShaderConstants.PassLOD, lod);
            if (lod == TerrainAsset.MAX_LOD)
            {
                m_CommandBuffer.SetComputeBufferParam(m_ComputeShader, m_KernelOfTraverseQuadTree, ShaderConstants.ConsumeNodeList, m_MaxLODNodeList);
            }
            else
            {
                m_CommandBuffer.SetComputeBufferParam(m_ComputeShader, m_KernelOfTraverseQuadTree, ShaderConstants.ConsumeNodeList, consumeNodeList);
            }
            m_CommandBuffer.SetComputeBufferParam(m_ComputeShader, m_KernelOfTraverseQuadTree, ShaderConstants.AppendNodeList, appendNodeList);
            m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfTraverseQuadTree, m_IndirectArgsBuffer, 0);
            m_CommandBuffer.CopyCounterValue(appendNodeList, m_IndirectArgsBuffer, 0);
            var temp = consumeNodeList;
            consumeNodeList = appendNodeList;
            appendNodeList = temp;
        }

        // m_CommandBuffer.CopyCounterValue(m_CulledPatchBuffer, m_PatchIndirectArgs, 4);

        //生成Patch
        m_CommandBuffer.CopyCounterValue(m_FinalNodeListBuffer, m_IndirectArgsBuffer, 0);
        m_CommandBuffer.DispatchCompute(m_ComputeShader, m_KernelOfBuildPatches, m_IndirectArgsBuffer, 0);

        Graphics.ExecuteCommandBuffer(m_CommandBuffer);
    }

    private class ShaderConstants
    {
        public static readonly int PassLOD = Shader.PropertyToID("PassLOD");
        public static readonly int AppendFinalNodeList = Shader.PropertyToID("AppendFinalNodeList");
        public static readonly int FinalNodeList = Shader.PropertyToID("FinalNodeList");
        public static readonly int AppendNodeList = Shader.PropertyToID("AppendNodeList");
        public static readonly int ConsumeNodeList = Shader.PropertyToID("ConsumeNodeList");
        public static readonly int WorldLodParams = Shader.PropertyToID("WorldLodParams");
        public static readonly int NodeIDOffsetOfLOD = Shader.PropertyToID("NodeIDOffsetOfLOD");
    }
}


