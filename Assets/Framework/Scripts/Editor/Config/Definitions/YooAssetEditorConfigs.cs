/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetEditorConfigs.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   YooAsset 编辑期配置
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// YooAsset 编辑期配置；仅用于定位编辑器工程资产，不进入 Runtime 配置。
    /// </summary>
    [Serializable]
    public class YooAssetEditorConfigs
    {
        public string YooAssetSettingsPath;
        public string BundleCollectorSettingPath;
        public string YooFolderName = "yoo";
        public string PackageFilePrefix = string.Empty;
    }
}
