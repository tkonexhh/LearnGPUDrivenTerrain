using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HizMap
{
    private CommandBuffer m_CommandBuffer;
    private ComputeShader m_ComputerShader;
    private RenderTexture m_HizMap;

    private const int KERNEL_BLIT = 0;
    private const int KERNEL_REDUCE = 1;

    public HizMap(ComputeShader computeShader)
    {
        m_CommandBuffer = new CommandBuffer();
        m_CommandBuffer.name = "HizMap";
        m_ComputerShader = computeShader;

        if (SystemInfo.usesReversedZBuffer)
        {
            computeShader.EnableKeyword("_REVERSE_Z");
        }
    }


    public static int GetHizMapSize(Camera camera)
    {
        // Debug.LogError(camera.pixelWidth + "----" + camera.pixelHeight);
        var screenSize = Mathf.Max(camera.pixelWidth, camera.pixelHeight);
        var textureSize = Mathf.NextPowerOfTwo(screenSize);
        //屏幕大小 2的幂
        return textureSize;
    }

    private RenderTexture GetTempHizMapTexture(int size, int mipCount)
    {
        var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.RFloat, 0, mipCount);
        var rt = RenderTexture.GetTemporary(desc);
        rt.autoGenerateMips = false;
        rt.useMipMap = mipCount > 1;
        rt.filterMode = FilterMode.Point;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private RenderTexture EnsureHizMap(Camera camera)
    {
        var preferMapSize = GetHizMapSize(camera);
        if (m_HizMap && m_HizMap.width == preferMapSize && m_HizMap.height == preferMapSize)
        {
            return m_HizMap;
        }
        if (m_HizMap)
        {
            RenderTexture.ReleaseTemporary(m_HizMap);
        }
        var mipCount = (int)Mathf.Log(preferMapSize, 2) + 1;
        m_HizMap = GetTempHizMapTexture(preferMapSize, mipCount);
        return m_HizMap;
    }

    /// <summary>
    /// 生成HizMap
    /// </summary>
    /// <param name="context"></param>
    /// <param name="camera"></param>
    public void Update(ScriptableRenderContext context, Camera camera)
    {
        var hizMap = this.EnsureHizMap(camera);
        m_CommandBuffer.Clear();

        var dstWidth = hizMap.width;
        var dstHeight = hizMap.height;
        uint threadX, threadY, threadZ;

        m_ComputerShader.GetKernelThreadGroupSizes(KERNEL_BLIT, out threadX, out threadY, out threadZ);
        //blit begin
        m_CommandBuffer.SetComputeTextureParam(m_ComputerShader, KERNEL_BLIT, ShaderConstants.InTex, ShaderConstants.CameraDepthTexture);
        m_CommandBuffer.SetComputeTextureParam(m_ComputerShader, KERNEL_BLIT, ShaderConstants.MipTex, hizMap, 0);

        m_CommandBuffer.SetComputeVectorParam(m_ComputerShader, ShaderConstants.SrcTexSize, new Vector4(camera.pixelWidth, camera.pixelHeight, 0, 0));
        m_CommandBuffer.SetComputeVectorParam(m_ComputerShader, ShaderConstants.DstTexSize, new Vector4(dstWidth, dstHeight, 0, 0));

        int groupX = Mathf.CeilToInt(dstWidth * 1.0f / threadX);
        int groupY = Mathf.CeilToInt(dstHeight * 1.0f / threadY);
        m_CommandBuffer.DispatchCompute(m_ComputerShader, KERNEL_BLIT, groupX, groupY, 1);
        //blit end

        //mip begin
        m_ComputerShader.GetKernelThreadGroupSizes(KERNEL_REDUCE, out threadX, out threadY, out threadZ);
        m_CommandBuffer.SetComputeTextureParam(m_ComputerShader, KERNEL_REDUCE, ShaderConstants.InTex, hizMap);
        for (int i = 1; i < hizMap.mipmapCount; i++)
        {
            dstWidth = Mathf.CeilToInt(dstWidth * 0.5f);
            dstHeight = Mathf.CeilToInt(dstHeight * 0.5f);

            m_CommandBuffer.SetComputeVectorParam(m_ComputerShader, ShaderConstants.DstTexSize, new Vector4(dstWidth, dstHeight, 0, 0));
            m_CommandBuffer.SetComputeIntParam(m_ComputerShader, ShaderConstants.Mip, i);
            m_CommandBuffer.SetComputeTextureParam(m_ComputerShader, KERNEL_REDUCE, ShaderConstants.MipTex, hizMap, i);

            groupX = Mathf.CeilToInt(dstWidth * 1.0f / threadX);
            groupY = Mathf.CeilToInt(dstHeight * 1.0f / threadY);
            m_CommandBuffer.DispatchCompute(m_ComputerShader, KERNEL_REDUCE, groupX, groupY, 1);
        }
        //mip end
        m_CommandBuffer.SetGlobalTexture(ShaderConstants.HizMap, hizMap);
        var matrixVP = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
        m_CommandBuffer.SetGlobalMatrix(ShaderConstants.HizCameraMatrixVP, matrixVP);
        m_CommandBuffer.SetGlobalVector(ShaderConstants.HizMapSize, new Vector4(hizMap.width, hizMap.height, hizMap.mipmapCount));
        m_CommandBuffer.SetGlobalVector(ShaderConstants.HizCameraPosition, camera.transform.position);
        context.ExecuteCommandBuffer(m_CommandBuffer);
    }

    private class ShaderConstants
    {
        public static readonly RenderTargetIdentifier CameraDepthTexture = "_CameraDepthTexture";
        public static readonly int InTex = Shader.PropertyToID("InTex");
        public static readonly int MipTex = Shader.PropertyToID("MipTex");
        public static readonly int SrcTexSize = Shader.PropertyToID("_SrcTexSize");
        public static readonly int DstTexSize = Shader.PropertyToID("_DstTexSize");
        public static readonly int Mip = Shader.PropertyToID("_Mip");
        public static readonly int HizMap = Shader.PropertyToID("_HizMap");

        public static readonly int HizMapSize = Shader.PropertyToID("_HizMapSize");
        public static readonly int HizCameraMatrixVP = Shader.PropertyToID("_HizCameraMatrixVP");
        public static readonly int HizCameraPosition = Shader.PropertyToID("_HizCameraPositionWS");

    }
}
