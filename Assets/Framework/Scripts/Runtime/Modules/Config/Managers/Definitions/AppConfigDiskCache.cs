/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppConfigDiskCache.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   应用配置磁盘缓存原子读写
 ***************************************************************/

using System;
using System.IO;
using System.Text;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 磁盘缓存载荷；Name 隔离不同 GM 配置项，Json 保存已校验的完整远端 object。
    /// </summary>
    [Serializable]
    internal sealed class AppConfigCachePayload
    {
        /// <summary>
        /// GM 后台配置项名称。
        /// </summary>
        public string Name;

        /// <summary>
        /// 已验证的远端完整 JSON object 字符串。
        /// </summary>
        public string Json;
    }

    /// <summary>
    /// 应用配置磁盘缓存工具；通过同目录临时文件与原子替换避免半写 JSON。
    /// </summary>
    internal static class AppConfigDiskCache
    {
        private static readonly Encoding s_Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>
        /// 从磁盘读取缓存文本；文件不存在视为正常未命中。
        /// </summary>
        /// <param name="path">缓存完整路径。</param>
        /// <param name="json">读取成功时的完整文本。</param>
        /// <param name="error">IO 失败原因；未命中或成功时为空。</param>
        /// <returns>文件存在且读取成功返回 true。</returns>
        public static bool TryRead(string path, out string json, out string error)
        {
            json = null;
            error = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                json = File.ReadAllText(path, s_Utf8WithoutBom);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// 将 JSON 写入同目录临时文件，再原子创建或替换目标文件。
        /// </summary>
        /// <param name="path">缓存完整路径。</param>
        /// <param name="json">已完成序列化的完整 JSON。</param>
        /// <param name="error">写入失败原因；成功时为空。</param>
        /// <returns>完整写入并替换成功返回 true。</returns>
        public static bool TryWriteAtomic(string path, string json, out string error)
        {
            error = null;
            string temporaryPath = path + ".tmp";
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(temporaryPath, json ?? string.Empty, s_Utf8WithoutBom);
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // 临时文件清理失败不覆盖原始写入错误；下次写入会直接覆盖同名临时文件。
                }
                return false;
            }
        }
    }
}
