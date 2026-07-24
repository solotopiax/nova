/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridEditorConfigs.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   HybridCLR 编辑期配置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Editor
{
    /// <summary>
    /// HybridCLR 编辑期配置；路径与 DLL 构建来源不进入 Runtime 配置。
    /// </summary>
    [Serializable]
    public class HybridEditorConfigs
    {
        public List<DllMasterAssetEntry> AotMetadataDlls = new();
        public List<DllMasterAssetEntry> GameDlls = new();
        public string LinkXmlTargetPath;
        public string GameEntranceProcedureName;
    }
}
