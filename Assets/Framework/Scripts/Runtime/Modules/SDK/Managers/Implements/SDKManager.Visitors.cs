/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 管理器 —— 字段与属性
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    internal sealed partial class SDKManager
    {
        /// <summary>
        /// 插件字典，以插件具体 Type 为键，用于 O(1) Get/TryGet 查询。
        /// </summary>
        private readonly Dictionary<Type, ISDKPlugin> m_Plugins = new Dictionary<Type, ISDKPlugin>();

        /// <summary>
        /// 按 Priority 升序排列的插件列表，用于有序初始化、有序广播与 GetAll 遍历。
        /// </summary>
        private readonly List<ISDKPlugin> m_SortedPlugins = new List<ISDKPlugin>();

        /// <summary>
        /// 初始化完成信号源，InitializeAsync 完成后调用 TrySetResult。
        /// </summary>
        private UniTaskCompletionSource m_InitializedTcs = new UniTaskCompletionSource();

        /// <summary>
        /// 管理器是否已完成 InitializeAsync（含失败隔离后的最终状态）。
        /// </summary>
        private bool m_IsInitialized;
        public override bool IsInitialized => m_IsInitialized;

        /// <summary>
        /// 事件管理器引用，在 Initialize 中通过 FrameworkManagersGroup 注入。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 配置管理器引用，在 Initialize 中通过 FrameworkManagersGroup 注入，供 InitializePluginAsync 按 RequiredConfigType 拉取 PluginConfig。
        /// </summary>
        private IConfigManager m_ConfigManager;

    }
}
