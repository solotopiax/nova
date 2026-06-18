/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Helpers.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 导出 Step 辅助方法：ConfigMasterSO 定位 + 活动场景 Nova 根节点 Component 定位
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出 Step 共用的辅助方法，供各导出 Step 文件调用，避免重复定位逻辑。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// 导出 Step 辅助方法集合；仅在 EditorUtil.Pipify 内部使用。
        /// </summary>
        internal static class Helpers
        {
            /// <summary>
            /// 定位工程内唯一的 ConfigMasterSO 资产；未找到时抛出 InvalidOperationException。
            /// </summary>
            /// <returns>找到的 ConfigMasterSO 实例。</returns>
            internal static ConfigMasterSO ResolveConfigMaster()
            {
                ConfigMasterSO master = EditorUtil.Asset.Operator.Find<ConfigMasterSO>();
                if (master == null)
                {
                    throw new InvalidOperationException("[Pipify] 未找到 ConfigMasterSO，请在工程内创建并配置后重试。");
                }
                return master;
            }

            /// <summary>
            /// 从当前活动场景的 Nova 根节点（含全部子节点）上获取指定类型的 Component；
            /// 场景内未找到 Nova 根节点或 Nova 层级下未挂载该 Component 时均抛出 InvalidOperationException。
            /// </summary>
            /// <typeparam name="T">要获取的 Component 类型。</typeparam>
            /// <returns>Nova 层级下挂载的 T 实例。</returns>
            internal static T ResolveComponentOnNova<T>() where T : Component
            {
                Nova nova = UnityEngine.Object.FindAnyObjectByType<Nova>(FindObjectsInactive.Include);
                if (nova == null)
                {
                    throw new InvalidOperationException("[Pipify] 未在当前场景找到 Nova 根节点，请打开挂载 Nova 组件的场景后重跑 Pipify。");
                }
                T component = nova.GetComponentInChildren<T>(true);
                if (component == null)
                {
                    throw new InvalidOperationException(string.Format("[Pipify] Nova 根节点及其子节点上未找到 {0}，请先在 Nova 层级下添加该组件。", typeof(T).Name));
                }
                return component;
            }
        }
    }
}
