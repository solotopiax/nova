/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.Methods.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   AssetManager 私有 helper
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager : AssetManagerBase
    {
        /// <summary>
        /// 解析包名：null/empty 走默认包。
        /// </summary>
        /// <param name="package">调用方传入的包名，允许 null 或空串。</param>
        /// <returns>实际使用的包名。</returns>
        private string ResolvePackageName(string package)
        {
            return string.IsNullOrEmpty(package) ? m_DefaultPackageName : package;
        }

        /// <summary>
        /// 取已注册的 YooAsset 包，未注册则抛异常。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <returns>对应的 ResourcePackage 实例。</returns>
        private ResourcePackage GetPackage(string name)
        {
            if (m_Packages.TryGetValue(name, out var pkg) == false)
            {
                throw new InvalidOperationException($"Package '{name}' is not registered.");
            }
            return pkg;
        }

        /// <summary>
        /// 获取指定包「本地缓存版本记录文件」的绝对路径。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <returns>persistentDataPath 下的记录文件绝对路径。</returns>
        private static string GetLocalCachedVersionFilePath(string name)
        {
            return Path.Persistent.GetFileFullPath($"Persist/Asset/CachedVersion/{name}.version");
        }

        /// <summary>
        /// 记录指定包当前激活的缓存版本号到本地文件，供下次启动远端不可达时离线回退。
        /// 写失败不抛异常，仅告警——记录失败不得中断启动流程。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">当前激活的包裹版本号。</param>
        private static void SaveLocalCachedVersion(string name, string version)
        {
            if (string.IsNullOrEmpty(version))
            {
                return;
            }
            try
            {
                string filePath = GetLocalCachedVersionFilePath(name);
                string dir = System.IO.Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(dir) == false && Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(filePath, version);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("写入本地缓存版本记录失败（不影响启动）。Package={0}, Version={1}, Error={2}", name, version, ex.Message));
            }
        }

        /// <summary>
        /// 读取指定包的本地缓存版本记录。文件缺失、为空或读取异常均返回 false。
        /// </summary>
        /// <param name="name">包名。</param>
        /// <param name="version">输出读取到的版本号。</param>
        /// <returns>true 表示读到有效版本号。</returns>
        private static bool TryLoadLocalCachedVersion(string name, out string version)
        {
            version = null;
            try
            {
                string filePath = GetLocalCachedVersionFilePath(name);
                if (File.Exists(filePath) == false)
                {
                    return false;
                }
                string content = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return false;
                }
                version = content.Trim();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Asset, Txt.Format("读取本地缓存版本记录失败。Package={0}, Error={1}", name, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 按 Inspector 配置的 EditorPlayMode / RuntimePlayMode 构造 YooAsset 初始化参数。
        /// 编辑器下使用 EditorPlayMode，非编辑器下使用 RuntimePlayMode，运行时不再二次覆盖。
        /// </summary>
        /// <param name="package">包名，用于 Host/Web 模式构建远端寻址服务。</param>
        /// <returns>对应运行模式的 InitializePackageOptions 实例。</returns>
        private InitializePackageOptions BuildPlayModeOptions(string package)
        {
            AssetPlayMode effectiveMode = Application.isEditor
                ? m_Config.EditorPlayMode
                : m_Config.RuntimePlayMode;

            switch (effectiveMode)
            {
                case AssetPlayMode.EditorSimulateMode:
                    return BuildEditorSimulateOptions(package);
                case AssetPlayMode.OfflinePlayMode:
                    return BuildOfflineOptions();
                case AssetPlayMode.HostPlayMode:
                    return BuildHostOptions(package);
                case AssetPlayMode.WebPlayMode:
                    return BuildWebOptions(package);
                default:
                    throw new InvalidOperationException($"Unsupported play mode: {effectiveMode}");
            }
        }

        /// <summary>
        /// 构造编辑器模拟模式初始化参数。
        /// 调用 EditorSimulateBuildInvoker.Build 执行模拟构建，将返回的
        /// PackageRootDirectory 注入 CreateDefaultEditorFileSystemParameters，
        /// 避免 EditorFileSystem package root is null or empty 异常。
        /// </summary>
        /// <param name="package">包名，传入 null/空串时走默认包名。</param>
        /// <returns>EditorSimulateModeOptions 实例。</returns>
        private InitializePackageOptions BuildEditorSimulateOptions(string package)
        {
#if UNITY_EDITOR
            string pkgName = ResolvePackageName(package);
            var buildResult = EditorSimulateBuildInvoker.Build(pkgName, (int)EBundleType.VirtualBundle);
            return new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory),
            };
#else
            throw new PlatformNotSupportedException("EditorSimulateMode 仅支持 Unity Editor 平台。");
#endif
        }

        /// <summary>
        /// 构造离线运行模式初始化参数。
        /// </summary>
        /// <returns>OfflinePlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildOfflineOptions()
        {
            return new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
            };
        }

        /// <summary>
        /// 构造联机运行模式初始化参数，含解密器注入。
        /// </summary>
        /// <param name="package">包名，用于构建远端 URL 模板。</param>
        /// <returns>HostPlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildHostOptions(string package)
        {
            var remote = new AssetRemoteService(m_Config.HostServerUrl, m_Config.HostServerUrlFallback, package);
            var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remote);
            ApplyDecryptor(cacheParams);
            return new HostPlayModeOptions
            {
                BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                CacheFileSystemParameters = cacheParams,
            };
        }

        /// <summary>
        /// 构造 WebGL 运行模式初始化参数。
        /// </summary>
        /// <param name="package">包名，用于构建远端 URL 模板。</param>
        /// <returns>WebPlayModeOptions 实例。</returns>
        private InitializePackageOptions BuildWebOptions(string package)
        {
            var remote = new AssetRemoteService(m_Config.HostServerUrl, m_Config.HostServerUrlFallback, package);
            return new WebPlayModeOptions
            {
                WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters(),
                WebRemoteFileSystemParameters = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remote),
            };
        }

        /// <summary>
        /// 按解密器类型构造解密器实例。
        /// </summary>
        /// <param name="type">配置指定的解密器类型枚举。</param>
        /// <returns>解密器实例；None 时返回 null。</returns>
        private object CreateDecryptor(AssetDecryptorType type)
        {
            switch (type)
            {
                case AssetDecryptorType.None:
                    return null;
                case AssetDecryptorType.OffsetBundleDecryptor:
                    return new OffsetBundleDecryptor();
                default:
                    throw new InvalidOperationException($"Unsupported decryptor type: {type}");
            }
        }

        /// <summary>
        /// 把解密器注入沙盒文件系统参数。
        /// IBundleMemoryDecryptor 走备用解密通道；
        /// IBundleDecryptor（如 OffsetBundleDecryptor）走标准解密通道。
        /// </summary>
        /// <param name="parameters">沙盒文件系统参数，将被就地修改。</param>
        private void ApplyDecryptor(FileSystemParameters parameters)
        {
            if (m_Decryptor == null)
            {
                return;
            }
            if (m_Decryptor is IBundleMemoryDecryptor)
            {
                parameters.AddParameter(EFileSystemParameter.AssetbundleFallbackDecryptor, m_Decryptor);
            }
            else if (m_Decryptor is IBundleDecryptor)
            {
                parameters.AddParameter(EFileSystemParameter.AssetbundleDecryptor, m_Decryptor);
            }
        }

        /// <summary>
        /// Editor 下加载真实 AssetBundle 时，将 bundle 内 shader 引用重绑到当前 Editor 可用的同名 shader。
        /// 这只服务非 EditorSimulateMode 的编辑器预览，Player 运行时保持 AssetBundle 原始 shader 引用。
        /// </summary>
        /// <param name="asset">刚加载完成的主资源。</param>
        private void RepairLoadedAssetShadersForEditor(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (m_Config == null || m_Config.EditorPlayMode == AssetPlayMode.EditorSimulateMode || asset == null)
            {
                return;
            }

            RepairObjectShadersForEditor(asset);
#endif
        }

        /// <summary>
        /// Editor 下批量修正刚加载完成的资源集合 shader 引用。
        /// </summary>
        /// <param name="assets">刚加载完成的资源集合。</param>
        private void RepairLoadedAssetShadersForEditor(IReadOnlyList<UnityEngine.Object> assets)
        {
#if UNITY_EDITOR
            if (m_Config == null || m_Config.EditorPlayMode == AssetPlayMode.EditorSimulateMode || assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Count; i++)
            {
                RepairObjectShadersForEditor(assets[i]);
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 对常见资源类型做局部 shader 重绑，避免跨平台 bundle shader 在 Editor HostPlayMode 下渲染为洋红色。
        /// </summary>
        /// <param name="asset">待修正资源。</param>
        private void RepairObjectShadersForEditor(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case null:
                    return;
                case Material material:
                    RepairMaterialShaderForEditor(material);
                    return;
                case TMPro.TMP_FontAsset fontAsset:
                    RepairMaterialShaderForEditor(fontAsset.material);
                    return;
                case GameObject gameObject:
                    RepairGameObjectShadersForEditor(gameObject);
                    return;
            }
        }

        /// <summary>
        /// 修正 GameObject 资源内部 Renderer 与 TMP FontAsset 的 shader 引用。
        /// </summary>
        /// <param name="gameObject">待修正的 GameObject 资源。</param>
        private void RepairGameObjectShadersForEditor(GameObject gameObject)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    RepairMaterialShaderForEditor(materials[j]);
                }
            }

            TMPro.TMP_Text[] tmpTexts = gameObject.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                RepairMaterialShaderForEditor(tmpTexts[i].fontSharedMaterial);

                if (tmpTexts[i].font != null)
                {
                    RepairMaterialShaderForEditor(tmpTexts[i].font.material);
                }
            }
        }

        /// <summary>
        /// 将 AssetBundle 反序列化出的 shader 对象替换为当前 Editor 进程中的同名 shader。
        /// </summary>
        /// <param name="material">待修正材质。</param>
        private void RepairMaterialShaderForEditor(Material material)
        {
            if (material == null || material.shader == null)
            {
                return;
            }

            Shader editorShader = Shader.Find(material.shader.name);
            if (editorShader != null && !ReferenceEquals(editorShader, material.shader))
            {
                material.shader = editorShader;
            }
        }
#endif
    }
}
