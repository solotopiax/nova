/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductList.cs
 * author:    yingzheng
 * created:   2026/6/3
 * descrip:   对 List<IAPProductEntry> 的 [Serializable] 包装类，使 PropertyDrawer 系统可为其注册自定义 Drawer
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 所有 store 共用的 IAP 商品条目列表包装类。
    /// 将裸 List&lt;IAPProductEntry&gt; 封装为具名 [Serializable] 类型，
    /// 使 Unity PropertyDrawer 系统可为其注册自定义 Drawer。
    /// 由 IAPPluginConfig 以 [SerializeField] 持有实例，Inspector 侧通过 IAPProductListDrawer 绘制。
    /// </summary>
    [Serializable]
    public sealed class IAPProductList
    {
        /// <summary>
        /// 所有 store 共用的 IAP 商品条目列表，为空时 IAP 功能整体不可用。
        /// 由 Inspector Drawer 管理增删，运行期只读。
        /// </summary>
        [SerializeField, Tooltip("所有 store 共用的 IAP 商品条目列表，为空时 IAP 功能整体不可用。")]
        private List<IAPProductEntry> m_Items = new List<IAPProductEntry>();

        /// <summary>
        /// 商品条目的只读视图，供 IAPPluginConfig 及外部消费侧遍历与查询。
        /// </summary>
        public IReadOnlyList<IAPProductEntry> Items => m_Items;
    }
}
