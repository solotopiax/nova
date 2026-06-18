/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DllAssetEntry.cs
 * author:    taoye
 * created:   2026/5/8
 * descrip:   DLL 资源条目，描述一个 DLL 的 Asset 地址
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DLL 资源条目（运行期视图）。
    /// 仅持有运行期 Asset 地址，供 ConfigRuntimeSO 持有。
    /// 不含源/目标路径语义，拷贝相关字段由编辑期 DllMasterAssetEntry 负责。
    /// </summary>
    [Serializable]
    public struct DllAssetEntry
    {
        /// <summary>
        /// 运行期 Asset 地址（传入 AssetComponent.LoadAssetAsync 的 location 参数；与 HybridCLR assemblyName 等价）。
        /// </summary>
        [SerializeField]
        private string m_AssetLocation;

        /// <summary>
        /// 运行期 Asset 地址（与 HybridCLR assemblyName 等价）。
        /// </summary>
        public string AssetLocation => m_AssetLocation;

        /// <summary>
        /// 由 EditorUtil.Config.Exporter 从 DllMasterAssetEntry 提取 AssetLocation 构造运行期条目。
        /// </summary>
        /// <param name="assetLocation">运行期 Asset 地址。</param>
        public DllAssetEntry(string assetLocation)
        {
            m_AssetLocation = assetLocation;
        }
    }
}
