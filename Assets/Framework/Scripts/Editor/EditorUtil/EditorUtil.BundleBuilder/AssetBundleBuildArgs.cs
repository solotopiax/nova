/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetBundleBuildArgs.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   YooAsset 资源包构建参数（对齐 BundleBuilderWindow 的 ScriptableBuildPipeline 视图）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// YooAsset 资源包构建参数（ScriptableBuildPipeline）。
    /// 字段一一对齐 BundleBuilderWindow → ScriptableBuildPipelineViewer 的 11 项 UI 控件；
    /// 字段上的 PipifyDropdown / PipifyDynamicDropdown / PipifyDynamicDefault / PipifyVisibleWhen 由 PipifyWindow 解析，与 Bundle Builder 窗口同源行为对齐。
    /// </summary>
    [Serializable]
    public sealed class AssetBundleBuildArgs
    {
        /// <summary>
        /// 目标构建平台；NoTarget 表示使用 EditorUserBuildSettings.activeBuildTarget。
        /// </summary>
        public BuildTarget Target = BuildTarget.NoTarget;

        /// <summary>
        /// 包裹名称（必填）；默认 Default，PipifyWindow 从 AssetComponent 配置中收集候选项以下拉形式渲染。
        /// </summary>
        [PipifyDynamicDropdown(typeof(AssetBundleBuildArgs), nameof(GetPackageNameOptions))]
        public string PackageName = "Default";

        /// <summary>
        /// 构建版本号；空字符串时由实现侧按 yyyy-MM-dd-totalMinutes 自动生成（与 Bundle Builder 窗口一致）。
        /// </summary>
        [PipifyDynamicDefault(typeof(EditorUtil.BundleBuilder), nameof(EditorUtil.BundleBuilder.GetDefaultPackageVersion))]
        public string BuildVersion = string.Empty;

        /// <summary>
        /// 是否清理构建缓存。
        /// </summary>
        public bool ClearBuildCache = true;

        /// <summary>
        /// 是否使用资源依赖缓存数据库。
        /// </summary>
        public bool UseAssetDependencyDB = false;

        /// <summary>
        /// 资源包加密器全类型名；默认 YooAsset.Editor.EncryptionNone。
        /// </summary>
        [PipifyDropdown(typeof(IBundleEncryptor))]
        public string BundleEncryptorClassName = typeof(EncryptionNone).FullName;

        /// <summary>
        /// 资源清单加密器全类型名；默认 YooAsset.Editor.ManifestEncryptorNone。
        /// </summary>
        [PipifyDropdown(typeof(IManifestEncryptor))]
        public string ManifestEncryptorClassName = typeof(ManifestEncryptorNone).FullName;

        /// <summary>
        /// 资源清单解密器全类型名；默认 YooAsset.Editor.ManifestDecryptorNone。
        /// </summary>
        [PipifyDropdown(typeof(IManifestDecryptor))]
        public string ManifestDecryptorClassName = typeof(ManifestDecryptorNone).FullName;

        /// <summary>
        /// 压缩方式。
        /// </summary>
        public ECompressOption Compression = ECompressOption.LZ4;

        /// <summary>
        /// 远端资源文件命名风格。
        /// </summary>
        public EFileNameStyle FileNameStyle = EFileNameStyle.BundleName_HashName;

        /// <summary>
        /// 首包资源拷贝选项。
        /// </summary>
        public EBundledCopyOption BundledCopyOption = EBundledCopyOption.ClearAndCopyAll;

        /// <summary>
        /// 首包资源拷贝标签参数（仅按标签拷贝时生效，多标签以分号分隔）。
        /// </summary>
        [PipifyVisibleWhen(nameof(BundledCopyOption), (int)EBundledCopyOption.ClearAndCopyByTags, (int)EBundledCopyOption.OnlyCopyByTags)]
        public string BundledCopyParams = string.Empty;

        /// <summary>
        /// 收集当前活动场景内 AssetComponent 配置的包名列表（PipifyDynamicDropdown 数据源）；
        /// 找不到 Nova / AssetComponent / m_Packages 字段时回退 ["Default"]，不阻塞 Pipify 窗口绘制。
        /// </summary>
        /// <returns>非空 string[]；至少包含一项 "Default"。</returns>
        public static string[] GetPackageNameOptions()
        {
            try
            {
                Nova nova = UnityEngine.Object.FindAnyObjectByType<Nova>(FindObjectsInactive.Include);
                if (nova == null) return new[] { "Default" };
                AssetComponent asset = nova.GetComponentInChildren<AssetComponent>(true);
                if (asset == null) return new[] { "Default" };
                FieldInfo packagesField = typeof(AssetComponent).GetField("m_Packages", BindingFlags.Instance | BindingFlags.NonPublic);
                if (packagesField == null) return new[] { "Default" };
                List<string> packages = packagesField.GetValue(asset) as List<string>;
                if (packages == null || packages.Count == 0) return new[] { "Default" };
                List<string> filtered = new List<string>(packages.Count);
                foreach (string p in packages)
                {
                    if (!string.IsNullOrEmpty(p) && !filtered.Contains(p)) filtered.Add(p);
                }
                return filtered.Count == 0 ? new[] { "Default" } : filtered.ToArray();
            }
            catch
            {
                return new[] { "Default" };
            }
        }
    }
}
