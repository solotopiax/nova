/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DllMasterAssetEntry.cs
 * author:    taoye
 * created:   2026/5/18
 * descrip:   DLL 主配置条目，供 ConfigMasterSO 持有；含源/目标路径与运行期加载地址三字段
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DLL 主配置条目（编辑期视图）。
    /// 供 ConfigMasterSO 持有，持有源位置、目标位置与运行期 Asset 地址三个字段。
    /// 源/目标位置均为项目根相对路径，所见即所得，不追加任何扩展名。
    /// 运行期仅使用 AssetLocation；EditorUtil.HybridCLR 拷贝时使用源/目标路径。
    /// </summary>
    [Serializable]
    public struct DllMasterAssetEntry
    {
        /// <summary>
        /// 源位置：项目根相对路径，EditorUtil.HybridCLR 从该路径读取文件。
        /// </summary>
        [SerializeField]
        private string m_SourceLocation;
        /// <summary>
        /// 源位置（项目根相对路径）。
        /// </summary>
        public string SourceLocation => m_SourceLocation;

        /// <summary>
        /// 目标位置：项目根相对路径，文件拷贝到此处，所见即所得，不追加 .bytes。
        /// </summary>
        [SerializeField]
        private string m_TargetLocation;
        /// <summary>
        /// 目标位置（项目根相对路径，不追加 .bytes）。
        /// </summary>
        public string TargetLocation => m_TargetLocation;

        /// <summary>
        /// 运行期 Asset 地址（传入 AssetComponent.LoadAssetAsync 的 location 参数；与 HybridCLR assemblyName 等价）。
        /// </summary>
        [SerializeField]
        private string m_AssetLocation;
        /// <summary>
        /// 运行期 Asset 地址（与 HybridCLR assemblyName 等价）。
        /// </summary>
        public string AssetLocation => m_AssetLocation;
    }
}
