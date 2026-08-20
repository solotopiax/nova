/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridConfigs.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   HybridCLR 运行时配置数据结构
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HybridCLR 运行时配置；只承载 Player 启动阶段实际消费的数据。
    /// link.xml、DLL 源路径和目标路径属于 Editor 构建数据，不进入本类型。
    /// </summary>
    [Serializable]
    public sealed class HybridConfigs
    {
        /// <summary>
        /// 业务入口 Procedure 相对类型名（不含 Namespace）。
        /// </summary>
        public string GameEntranceProcedureName;

        /// <summary>
        /// AOT 元数据 DLL 的运行时 Asset 地址列表。
        /// </summary>
        public List<DllAssetEntry> AotMetadataDlls = new();

        /// <summary>
        /// 启动阶段自动加载的业务 DLL 运行时 Asset 地址列表。
        /// </summary>
        [FormerlySerializedAs("GameDlls")]
        public List<DllAssetEntry> StartupGameDlls = new();
    }
}
