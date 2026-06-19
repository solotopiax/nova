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
            private const string c_DefaultExternalUrl = "http://172.16.22.175:4873";

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
                /// 内部云 registry 根地址（空白时回退默认 4874）。
                /// </summary>
                public string internalUrl;

                /// <summary>
                /// 内部云 registry 名称。
                /// </summary>
                public string internalName;
            }

            /// <summary>
            /// 读取 registry 配置；文件缺失或字段空时回退默认（公网 172.16.22.175:4873、内部云内网地址）。
            /// </summary>
            public static RegistriesConfig LoadRegistries()
            {
                string path = GetRegistriesPath();
                RegistriesConfig raw = null;
                if (File.Exists(path))
                {
                    try
                    {
                        raw = JsonUtility.FromJson<RegistriesConfig>(File.ReadAllText(path));
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogTag.Editor, "PlugPals.LoadRegistries 解析失败，回退默认: {0}", e.Message);
                    }
                }

                return NormalizeRegistries(raw);
            }

            /// <summary>
            /// 原子写入 registry 配置到 ProjectSettings/Nova/PlugPalsRegistries.json。
            /// </summary>
            public static void SaveRegistries(RegistriesConfig config)
            {
                RegistriesConfig normalized = NormalizeRegistries(config);
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
            /// 归一化配置：空白 externalUrl 回退默认公网地址、空白 internalUrl 回退默认内网地址，空 name 回退默认名。
            /// </summary>
            private static RegistriesConfig NormalizeRegistries(RegistriesConfig config)
            {
                string externalUrl = config?.externalUrl?.Trim();
                string externalName = config?.externalName?.Trim();
                string internalUrl = config?.internalUrl?.Trim();
                string internalName = config?.internalName?.Trim();

                return new RegistriesConfig
                {
                    externalUrl = string.IsNullOrEmpty(externalUrl) ? c_DefaultExternalUrl : externalUrl,
                    externalName = string.IsNullOrEmpty(externalName) ? c_DefaultExternalName : externalName,
                    internalUrl = string.IsNullOrEmpty(internalUrl) ? c_DefaultInternalUrl : internalUrl,
                    internalName = string.IsNullOrEmpty(internalName) ? c_DefaultInternalName : internalName,
                };
            }

            private static string GetRegistriesPath()
            {
                string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                return System.IO.Path.Combine(projectRoot, c_RegistriesRelPath);
            }

            internal static RegistriesConfig NormalizeRegistriesForTest(RegistriesConfig config)
            {
                return NormalizeRegistries(config);
            }
        }
    }
}
