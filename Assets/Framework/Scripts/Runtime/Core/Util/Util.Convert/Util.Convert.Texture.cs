/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Convert.Texture.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   类型转换工具 —— 纹理转码
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static partial class Convert
        {
            /// <summary>
            /// 获取纹理的字节流。
            /// </summary>
            /// <param name="texture">纹理对象。</param>
            /// <param name="isJpeg">是否为 jpeg。</param>
            /// <returns>转换后的字节流。</returns>
            public static byte[] TextureToBytes(Texture2D texture, bool isJpeg)
            {
                try
                {
                    return isJpeg ? texture.EncodeToJPG(100) : texture.EncodeToPNG();
                }
                catch (Exception e) when (e is UnityException || e is ArgumentException)
                {
                    return TextureToBytesFromCopy(texture, isJpeg);
                }
            }

            /// <summary>
            /// 从副本中获取纹理字节流。
            /// </summary>
            /// <param name="texture">纹理对象。</param>
            /// <param name="isJpeg">是否为 jpeg。</param>
            /// <returns>转换后的字节流。</returns>
            private static byte[] TextureToBytesFromCopy(Texture2D texture, bool isJpeg)
            {
                Log.Warning(LogTag.Convert, "保存 non-readable 纹理比保存 readable 纹理要慢。");

                Texture2D sourceTexReadable = null;
                RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
                RenderTexture activeRT = RenderTexture.active;

                try
                {
                    Graphics.Blit(texture, rt);
                    RenderTexture.active = rt;

                    sourceTexReadable = new Texture2D(texture.width, texture.height, isJpeg ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
                    sourceTexReadable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
                    sourceTexReadable.Apply(false, false);
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Convert, "发生异常，错误信息：{0} 。", e);

                    UnityEngine.Object.DestroyImmediate(sourceTexReadable);
                    return null;
                }
                finally
                {
                    RenderTexture.active = activeRT;
                    RenderTexture.ReleaseTemporary(rt);
                }

                try
                {
                    return isJpeg ? sourceTexReadable.EncodeToJPG(100) : sourceTexReadable.EncodeToPNG();
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.Convert, "发生异常，错误信息：{0} 。", e);
                    return null;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sourceTexReadable);
                }
            }
        }
    }
}
