/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponent.Visitors.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 组件 —— 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class SDKComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 SDK 管理器类型名，Inspector 可选择具体实现类，TypeCreator 据此反射创建。
        /// </summary>
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.SDKManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// Inspector 序列化的插件条目列表，Manager.Initialize 据此反射实例化插件。
        /// </summary>
        [SerializeField]
        private List<SDKPluginEntry> m_PluginEntries = new List<SDKPluginEntry>();
        public IReadOnlyList<SDKPluginEntry> PluginEntries => m_PluginEntries;

        /// <summary>
        /// SDK 管理器实例，以接口持有，TypeCreator 在 Awake 中注入。
        /// </summary>
        private ISDKManager m_SDKManager;
        public ISDKManager SDKManager => m_SDKManager;

        /// <summary>
        /// InitializeTask 的缓存，首次访问时由 GetOrCreateInitializeTask 创建。
        /// 惰性 InitializeAsync 任务；首次访问时调用 Manager.InitializeAsync，后续调用返回同一任务。
        /// 业务层通过 await Nova.SDK.InitializeTask 完成 SDK 统一初始化。
        /// </summary>
        private UniTask? m_InitializeTaskCache;
        public UniTask InitializeTask => GetOrCreateInitializeTask();

        /// <summary>
        /// SDK 管理器是否已完成 InitializeAsync。
        /// </summary>
        public bool IsInitialized => m_SDKManager != null && m_SDKManager.IsInitialized;
    }
}
