using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureUtility
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="size">LOD0 下的最大Node数量</param>
    /// <returns></returns>
    public static RenderTexture CreateLODMap(int size)
    {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.R8, 0, 1);
        descriptor.autoGenerateMips = false;
        descriptor.enableRandomWrite = true;
        RenderTexture rt = new RenderTexture(descriptor);
        rt.filterMode = FilterMode.Point;
        rt.Create();
        return rt;
    }
}
