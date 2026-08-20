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
using UnityEngine.Serialization;

namespace NovaFramework.Editor
{
    /// <summary>
    /// HybridCLR 编辑期配置；路径与 DLL 构建来源不进入 Runtime 配置。
    /// </summary>
    [Serializable]
    public class HybridEditorConfigs
    {
        public List<DllMasterAssetEntry> AotMetadataDlls = new();

        /// <summary>
        /// 启动阶段自动加载的业务 DLL；该列表会导出到 ConfigRuntimeSO。
        /// </summary>
        [FormerlySerializedAs("GameDlls")]
        public List<DllMasterAssetEntry> StartupGameDlls = new();

        /// <summary>
        /// 游戏运行过程中按需加载的业务 DLL；仅供 Editor 编译产物映射、复制与校验。
        /// </summary>
        public List<DllMasterAssetEntry> RunningGameDlls = new();

        public string LinkXmlTargetPath;
        public string GameEntranceProcedureName;
    }
}
