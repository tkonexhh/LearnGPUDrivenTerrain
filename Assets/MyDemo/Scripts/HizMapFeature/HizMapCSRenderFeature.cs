using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HizMapCSRenderFeature : ScriptableRendererFeature
{

    [SerializeField] private ComputeShader m_ComputeShader;
    private HizMapPass m_Pass;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if (cameraData.isSceneViewCamera || cameraData.isPreviewCamera)
        {
            return;
        }
        if (cameraData.camera.name == "Preview Camera")
        {
            return;
        }
        if (m_Pass != null)
        {
            renderer.EnqueuePass(m_Pass);
        }
    }

    public override void Create()
    {
        if (m_Pass == null)
        {
            if (!m_ComputeShader)
            {
                Debug.LogError("missing Hiz compute shader");
                return;
            }
            m_Pass = new HizMapPass(this.m_ComputeShader);
        }
    }
}


public class HizMapPass : ScriptableRenderPass
{
    private HizMap m_Hizmap;
    public HizMapPass(ComputeShader computeShader)
    {
        this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        m_Hizmap = new HizMap(computeShader);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        m_Hizmap.Update(context, renderingData.cameraData.camera);
    }
}


