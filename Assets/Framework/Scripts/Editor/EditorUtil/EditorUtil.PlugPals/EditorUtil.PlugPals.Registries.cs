/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.Registries.cs
 * author:    taoye
 * created:   2026/6/16
 * descrip:   PlugPals 工具 —— registry 地址配置（ProjectSettings/Nova/PlugPalsRegistries.json）
 ***************************************************************/

using System;
using System.IO;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            /// <summary>
            /// registry 配置相对工程根路径。该文件被 .gitignore 忽略，不入库。
            /// </summary>
            private const string c_RegistriesRelPath = "ProjectSettings/Nova/PlugPalsRegistries.json";

            /// <summary>
            /// 公网 registry 默认地址（公网域名）。
            /// </summary>
            private const string c_DefaultExternalUrl = "https://upm.solotopiax.com";

            /// <summary>
            /// 公网 registry 默认名称。
            /// </summary>
            private const string c_DefaultExternalName = "Solotopia";

            /// <summary>
            /// 内部云 registry 默认地址。内网 IP 硬编码（内网地址，外网不可达，无需脱敏；将来可替换为内网域名）。
            /// </summary>
            private const string c_DefaultInternalUrl = "http://172.16.22.175:4874";

            /// <summary>
            /// 内部云 registry 默认名称。
            /// </summary>
            private const string c_DefaultInternalName = "Solotopia Internal";

            /// <summary>
            /// Verdaccio 包列表 API 路径（协议固定，非敏感信息）。
            /// </summary>
            public const string c_RegistryApiPath = "/-/verdaccio/data/packages";

            /// <summary>
            /// registry 地址配置（字段名即 JSON key，camelCase 便于人工读写）。
            /// </summary>
            [Serializable]
            public sealed class RegistriesConfig
            {
                /// <summary>
                /// 公网 registry 根地址。
                /// </summary>
                public string externalUrl;

                /// <summary>
                /// 公网 registry 名称（写入 manifest scopedRegistries）。
                /// </summary>
                public string externalName;

                /// <summary>
                /// 内部云 registry 根地址；存档中为空表示不请求内部云。
                /// </summary>
                public string internalUrl;

                /// <summary>
                /// 内部云 registry 名称。
                /// </summary>
                public string internalName;
            }

            /// <summary>
            /// 读取 registry 配置；文件缺失或解析失败时使用默认地址，已有存档时保留其中的空 URL。
            /// </summary>
            public static RegistriesConfig LoadRegistries()
            {
                string path = GetRegistriesPath();
                if (!File.Exists(path))
                {
                    return NormalizeRegistries(null, false);
                }

                try
                {
                    RegistriesConfig raw = JsonUtility.FromJson<RegistriesConfig>(File.ReadAllText(path));
                    return raw == null
                        ? NormalizeRegistries(null, false)
                        : NormalizeRegistries(raw, true);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "PlugPals.LoadRegistries 解析失败，回退默认: {0}", e.Message);
                    return NormalizeRegistries(null, false);
                }
            }

            /// <summary>
            /// 原子写入 registry 配置到 ProjectSettings/Nova/PlugPalsRegistries.json。
            /// </summary>
            public static void SaveRegistries(RegistriesConfig config)
            {
                RegistriesConfig normalized = NormalizeRegistries(config, true);
                string json = JsonUtility.ToJson(normalized, true);
                string path = GetRegistriesPath();
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tmp = path + ".tmp";
                try
                {
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path))
                    {
                        File.Replace(tmp, path, null);
                    }
                    else
                    {
                        File.Move(tmp, path);
                    }
                }
                catch (Exception e)
                {
                    if (File.Exists(tmp))
                    {
                        File.Delete(tmp);
                    }

                    Log.Warning(LogTag.Editor, "PlugPals.SaveRegistries 写入失败: {0}", e.Message);
                }
            }

            /// <summary>
            /// 归一化 registry 配置：无存档时补默认 URL，有存档时保留空 URL；名称始终补默认值。
            /// </summary>
            /// <param name="config">待归一化的配置；可为空。</param>
            /// <param name="hasPersistedConfig">是否已有存档；为 true 时空 URL 表示显式禁用对应仓库。</param>
            /// <returns>完成裁剪和默认值处理后的配置。</returns>
            private static RegistriesConfig NormalizeRegistries(RegistriesConfig config, bool hasPersistedConfig)
            {
                string externalUrl = config?.externalUrl?.Trim();
                string externalName = config?.externalName?.Trim();
                string internalUrl = config?.internalUrl?.Trim();
                string internalName = config?.internalName?.Trim();

                return new RegistriesConfig
                {
                    externalUrl = hasPersistedConfig ? externalUrl ?? string.Empty : c_DefaultExternalUrl,
                    externalName = string.IsNullOrEmpty(externalName) ? c_DefaultExternalName : externalName,
                    internalUrl = hasPersistedConfig ? internalUrl ?? string.Empty : c_DefaultInternalUrl,
                    internalName = string.IsNullOrEmpty(internalName) ? c_DefaultInternalName : internalName,
                };
            }

            private static string GetRegistriesPath()
            {
                string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                return System.IO.Path.Combine(projectRoot, c_RegistriesRelPath);
            }

            /// <summary>
            /// 为 Editor 测试暴露 registry 归一化规则，不参与生产调用。
            /// </summary>
            /// <param name="config">待归一化的配置。</param>
            /// <param name="hasPersistedConfig">是否已有存档。</param>
            /// <returns>归一化后的配置。</returns>
            internal static RegistriesConfig NormalizeRegistriesForTest(RegistriesConfig config, bool hasPersistedConfig)
            {
                return NormalizeRegistries(config, hasPersistedConfig);
            }
        }
    }
}
