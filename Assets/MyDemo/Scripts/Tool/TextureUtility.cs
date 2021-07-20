using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class TextureUtility
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="size">LOD0 下的最大Node数量</param>
    /// <returns></returns>
    public static RenderTexture CreateLODMap(int size)
    {
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.R8, 0, 1);//RenderTextureFormat.R8手机上有问题 之后在测试
        descriptor.autoGenerateMips = false;
        descriptor.enableRandomWrite = true;
        RenderTexture rt = new RenderTexture(descriptor);
        rt.filterMode = FilterMode.Point;
        rt.Create();
        return rt;
    }

    public static RenderTexture CreateRenderTextureWithMipTextures(Texture2D[] mipmaps, RenderTextureFormat format)
    {
        // format = RenderTextureFormat.RGB565;
        var mip0 = mipmaps[0];
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(mip0.width, mip0.height, format, 0, mipmaps.Length);
        descriptor.autoGenerateMips = false;
        descriptor.useMipMap = true;
        // descriptor.mipCount = mipmaps.Length;
        RenderTexture rt = new RenderTexture(descriptor);
        rt.filterMode = mip0.filterMode;
        rt.useMipMap = true;
        // rt.mip = 9;
        rt.Create();
        for (var i = 0; i < mipmaps.Length; i++)
        {
            //Graphics.CopyTexture called for entire mipmaps with different memory size (source (RGB8 sRGB) is 75 bytes and destination (RG16 UNorm) is 100 bytes)
            Graphics.CopyTexture(mipmaps[i], 0, 0, rt, 0, i);
        }
        return rt;
    }


    public static void SaveRenderTexture(RenderTexture rt, string path, string pngName)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D png = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        byte[] bytes = png.EncodeToPNG();
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        FileStream file = File.Open(path + "/" + pngName + ".png", FileMode.Create);
        BinaryWriter writer = new BinaryWriter(file);
        writer.Write(bytes);
        file.Close();
        Texture2D.DestroyImmediate(png);
        png = null;
        RenderTexture.active = prev;
    }
}
