/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigManager.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   配置管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 配置管理器实现；AB 异步加载 ConfigRuntimeSO，
    /// 运行期以 ConfigRuntimeSO 为数据源直读，供 SDKManager 按类型拉取 SDK Plugin 配置。
    /// </summary>
    internal sealed partial class ConfigManager : ConfigManagerBase
    {
        /// <summary>
        /// 构造器；无参，供反射创建。
        /// </summary>
        public ConfigManager() { }

        /// <summary>
        /// 初始化；接收 Component 构造的 ConfigManagerConfig 并获取 AssetManager。
        /// </summary>
        /// <param name="config">
        /// 配置信息，含 ConfigRuntimeSO Asset 地址（AssetLocation）。
        /// </param>
        public override void Initialize(ConfigManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (string.IsNullOrEmpty(config.AssetLocation))
            {
                throw new ArgumentException("ConfigManagerConfig.AssetLocation 不能为空。", nameof(config));
            }

            m_AssetLocation = config.AssetLocation;
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
        }

        /// <summary>
        /// 管理器轮询；本模块加载后为稳态无需轮询。
        /// </summary>
        public override void Update() { }

        /// <summary>
        /// 关闭并清理管理器；Release ConfigRuntimeSO 句柄使引用计数归零，清空内部引用。
        /// </summary>
        public override void Shutdown()
        {
            m_ConfigHandle?.Release();
            m_ConfigHandle = null;
            m_Runtime = null;
            m_IsLoadOver = false;
            m_AssetLocation = null;
            m_AssetManager = null;
        }

        /// <summary>
        /// 异步加载 ConfigRuntimeSO，加载成功后直接持有引用作为数据源；
        /// 已加载时幂等返回（m_IsLoadOver 短路），避免 AB 引用计数紊乱；
        /// 加载失败时 Log.Error 并重新抛出异常，由 Procedure 层决定启动流程是否中断。
        /// </summary>
        /// <returns>
        /// 加载完成的异步任务。
        /// </returns>
        public override async UniTask LoadAsync()
        {
            if (m_IsLoadOver)
            {
                return;
            }

            IAssetHandle<ConfigRuntimeSO> handle;
            try
            {
                handle = await m_AssetManager.LoadAsync<ConfigRuntimeSO>(m_AssetLocation);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Config, "ConfigManager 加载 ConfigRuntimeSO 异常：location={0}, Error={1}", m_AssetLocation, e);
                throw;
            }

            if (handle.Asset == null)
            {
                handle.Release();
                Log.Error(LogTag.Config, "ConfigManager 未能加载 ConfigRuntimeSO：location={0}", m_AssetLocation);
                throw new InvalidOperationException("ConfigRuntimeSO 加载结果为 null。");
            }

            m_ConfigHandle = handle;
            m_Runtime = handle.Asset;
            int enabledCount = m_Runtime.EnabledSDKConfigs != null ? m_Runtime.EnabledSDKConfigs.Count : 0;
            m_IsLoadOver = true;
            Log.Debug(LogTag.Config, "Config 成功加载，共计 1 份儿通用配置，{0} 个 SDKPluginConfig 数据。", enabledCount);
        }

        /// <summary>
        /// 按泛型类型取 SDK Plugin 配置实例；透传 ConfigRuntimeSO.GetSDKPluginConfig，未启用返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// SDK Plugin 所需配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        public override T GetSDKPluginConfig<T>() => m_Runtime?.GetSDKPluginConfig<T>();

        /// <summary>
        /// 按类型对象取 SDK Plugin 配置实例（非泛型版）；透传 ConfigRuntimeSO.GetSDKPluginConfig，type 为 null 或未启用返回 null。
        /// </summary>
        /// <param name="type">
        /// SDK Plugin 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        public override ISDKPluginConfig GetSDKPluginConfig(Type type) => m_Runtime?.GetSDKPluginConfig(type);

        /// <summary>
        /// 按泛型类型取 Kit 配置实例；透传 ConfigRuntimeSO.GetKitConfig，未启用返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// 目标 Kit 配置类型。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；Runtime 未就绪或未启用返回 null。
        /// </returns>
        public override T GetKitConfig<T>() => m_Runtime?.GetKitConfig<T>();

        /// <summary>
        /// 按类型对象取 Kit 配置实例（非泛型版）；透传 ConfigRuntimeSO.GetKitConfig，type 为 null 或未启用返回 null。
        /// </summary>
        /// <param name="type">
        /// 目标 Kit 配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；Runtime 未就绪、type 为 null 或未启用返回 null。
        /// </returns>
        public override IKitConfig GetKitConfig(Type type) => m_Runtime?.GetKitConfig(type);

        /// <summary>
        /// 当前已加载的所有启用 SDK Plugin 配置集合；未加载时返回空集合。
        /// </summary>
        /// <returns>
        /// SDK Plugin 配置只读集合。
        /// </returns>
        public override IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs()
        {
            return m_Runtime?.EnabledSDKConfigs ?? (IReadOnlyCollection<ISDKPluginConfig>)System.Array.Empty<ISDKPluginConfig>();
        }
    }
}
