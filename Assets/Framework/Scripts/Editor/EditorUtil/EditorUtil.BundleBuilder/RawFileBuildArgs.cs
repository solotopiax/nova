/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RawFileBuildArgs.cs
 * author:    taoye
 * created:   2026/8/18
 * descrip:   YooAsset RawFile 资源构建参数
 ***************************************************************/

using System;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// YooAsset RawFile 构建参数，仅暴露 RawFileBuildPipeline 实际支持的配置。
    /// </summary>
    [Serializable]
    public sealed class RawFileBuildArgs
    {
        /// <summary>
        /// 目标构建平台；NoTarget 表示使用当前活动构建平台。
        /// </summary>
        public BuildTarget Target = BuildTarget.NoTarget;

        /// <summary>
        /// 包裹名称（必填）。
        /// </summary>
        [PipifyDynamicDropdown(typeof(AssetBundleBuildArgs), nameof(AssetBundleBuildArgs.GetPackageNameOptions))]
        public string PackageName = "Default";

        /// <summary>
        /// 构建版本号；空字符串时自动生成默认版本号。
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
        /// RawBundle 加密器全类型名。
        /// </summary>
        [PipifyDropdown(typeof(IBundleEncryptor))]
        public string BundleEncryptorClassName = typeof(EncryptionNone).FullName;

        /// <summary>
        /// 资源清单加密器全类型名。
        /// </summary>
        [PipifyDropdown(typeof(IManifestEncryptor))]
        public string ManifestEncryptorClassName = typeof(ManifestEncryptorNone).FullName;

        /// <summary>
        /// 资源清单解密器全类型名。
        /// </summary>
        [PipifyDropdown(typeof(IManifestDecryptor))]
        public string ManifestDecryptorClassName = typeof(ManifestDecryptorNone).FullName;

        /// <summary>
        /// 远端资源文件命名风格。
        /// </summary>
        public EFileNameStyle FileNameStyle = EFileNameStyle.BundleName_HashName;

        /// <summary>
        /// 首包资源拷贝选项。
        /// </summary>
        public EBundledCopyOption BundledCopyOption = EBundledCopyOption.ClearAndCopyAll;

        /// <summary>
        /// 首包资源拷贝标签参数，仅按标签拷贝时生效。
        /// </summary>
        [PipifyVisibleWhen(nameof(BundledCopyOption), (int)EBundledCopyOption.ClearAndCopyByTags, (int)EBundledCopyOption.OnlyCopyByTags)]
        public string BundledCopyParams = string.Empty;

        /// <summary>
        /// 计算 RawFile 哈希时是否包含文件路径。
        /// </summary>
        public bool IncludePathInHash = false;
    }
}
