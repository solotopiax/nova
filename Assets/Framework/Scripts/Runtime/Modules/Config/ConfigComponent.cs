/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigComponent.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   配置组件
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 配置组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ConfigComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_ConfigManager = Util.TypeCreator.Create<IConfigManager>(m_CurManagerTypeName);
            if (m_ConfigManager == null)
            {
                throw new InvalidOperationException("ConfigManager 无效。");
            }
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            m_ConfigManager.Initialize(new ConfigManagerConfig
            {
                AssetLocation = m_AssetLocation,
            });
        }

        /// <summary>
        /// 异步加载 ConfigRuntimeSO；失败时由 Manager 内部 throw，调用方（Procedure）自行捕获。
        /// </summary>
        /// <returns>加载完成的异步任务。</returns>
        public UniTask LoadAsync()
        {
            return m_ConfigManager.LoadAsync();
        }

        /// <summary>
        /// 按泛型类型取 SDK Plugin 配置实例；未加载或未启用时返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// SDK Plugin 所需配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；Manager 未就绪或未启用时返回 null。
        /// </returns>
        public T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig
            => m_ConfigManager != null ? m_ConfigManager.GetSDKPluginConfig<T>() : null;

        /// <summary>
        /// 按类型对象取 SDK Plugin 配置实例（非泛型版）；未加载、type 为 null 或未启用时返回 null。
        /// </summary>
        /// <param name="type">
        /// SDK Plugin 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；Manager 未就绪或未启用时返回 null。
        /// </returns>
        public ISDKPluginConfig GetSDKPluginConfig(System.Type type)
            => m_ConfigManager != null ? m_ConfigManager.GetSDKPluginConfig(type) : null;

        /// <summary>
        /// 按泛型类型取 Kit 配置实例；未加载或未启用时返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// Kit 所需配置类型，须实现 IKitConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；Manager 未就绪或未启用时返回 null。
        /// </returns>
        public T GetKitConfig<T>() where T : class, IKitConfig
            => m_ConfigManager != null ? m_ConfigManager.GetKitConfig<T>() : null;

        /// <summary>
        /// 按类型对象取 Kit 配置实例（非泛型版）；未加载、type 为 null 或未启用时返回 null。
        /// </summary>
        /// <param name="type">
        /// Kit 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；Manager 未就绪或未启用时返回 null。
        /// </returns>
        public IKitConfig GetKitConfig(System.Type type)
            => m_ConfigManager != null ? m_ConfigManager.GetKitConfig(type) : null;

        /// <summary>
        /// 销毁。
        /// </summary>
        private void OnDestroy()
        {
            m_ConfigManager = null;
        }
    }
}
