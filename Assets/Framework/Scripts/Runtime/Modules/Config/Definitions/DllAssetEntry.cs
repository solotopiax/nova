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
        /// 原始运行期 Asset 地址，由 ProcedureLoadDll 传给 Util.HybridCLR 加载工具。
        /// 该值不是命名空间、asmdef 程序集名或经 DLL 内容校验的程序集身份。
        /// </summary>
        [SerializeField]
        private string m_AssetLocation;

        /// <summary>
        /// 获取构造时原样保存的运行期 Asset 地址，不执行 Trim、地址校验或 DLL 加载。
        /// Asset 地址、DLL 文件和实际加载的 Assembly 需由消费者分别核对。
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
