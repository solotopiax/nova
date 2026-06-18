/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IConfigManager.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   配置管理器对外契约
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Config Manager 对外契约；负责按 AB 路径异步加载 ConfigRuntimeSO，
    /// 运行期以 ConfigRuntimeSO 为数据源直读配置，不维护内部字段缓存。
    /// 同时提供 ConfigRuntimeSO 的已启用 SDK 配置的按类型查询能力，供 SDKManager 拉取 PluginConfig。
    /// </summary>
    public interface IConfigManager
    {
        /// <summary>
        /// 是否已完成加载；ConfigComponent.Start 流程中应 await 直至此属性为 true 再进后续启动步骤。
        /// </summary>
        bool IsLoadOver { get; }

        /// <summary>
        /// 当前运行时的开发模式（Debug / Release）；LoadAsync 完成后可读。
        /// </summary>
        DevelopMode DevelopMode { get; }

        /// <summary>
        /// 全局公共配置整块；LoadAsync 完成后可读，未加载时返回 null。
        /// </summary>
        CommonConfig Common { get; }

        /// <summary>
        /// 当前解析后的 Namespace；LoadAsync 完成后可读。
        /// </summary>
        string Namespace { get; }

        /// <summary>
        /// 业务入口 Procedure 相对类型名（直读 ConfigRuntimeSO.GameEntranceProcedureName）；LoadAsync 完成后可读。
        /// </summary>
        string GameEntranceProcedureName { get; }

        /// <summary>
        /// AOT 元数据 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        IReadOnlyList<DllAssetEntry> AotMetadataDlls { get; }

        /// <summary>
        /// 业务 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        IReadOnlyList<DllAssetEntry> GameDlls { get; }

        /// <summary>
        /// 本次加载 ConfigRuntimeSO 时记录的目标平台；由导出侧在 Export 时写入。
        /// </summary>
        PlatformType Platform { get; }

        /// <summary>
        /// 本次加载 ConfigRuntimeSO 时记录的目标渠道；由导出侧在 Export 时写入。
        /// </summary>
        ChannelType Channel { get; }

        /// <summary>
        /// 初始化配置管理器；由 ConfigComponent 在 Start 阶段构造 ConfigManagerConfig 后调用。
        /// 校验不通过时抛 ArgumentNullException / ArgumentException。
        /// </summary>
        /// <param name="config">
        /// 初始化配置，含 AB 路径 + ConfigRuntimeSO 资产名。
        /// </param>
        void Initialize(ConfigManagerConfig config);

        /// <summary>
        /// 异步加载并解析 ConfigRuntimeSO；加载失败直接 Log.Error + throw，
        /// 由 Procedure 层捕获并决定是否中断启动流程。
        /// </summary>
        /// <returns>
        /// 加载完成的异步任务。
        /// </returns>
        UniTask LoadAsync();

        /// <summary>
        /// 按泛型类型取 SDK Plugin 配置实例；未启用或类型不匹配返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// SDK Plugin 所需配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;

        /// <summary>
        /// 按类型对象取 SDK Plugin 配置实例（非泛型版，供反射调用方使用）；未启用或类型不匹配返回 null。
        /// </summary>
        /// <param name="type">
        /// SDK Plugin 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        ISDKPluginConfig GetSDKPluginConfig(Type type);

        /// <summary>
        /// 按泛型类型取 Kit 配置实例；未启用或类型不匹配返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// Kit 所需配置类型，须实现 IKitConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        T GetKitConfig<T>() where T : class, IKitConfig;

        /// <summary>
        /// 按类型对象取 Kit 配置实例（非泛型版，供反射调用方使用）；未启用或类型不匹配返回 null。
        /// </summary>
        /// <param name="type">
        /// Kit 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        IKitConfig GetKitConfig(Type type);

        /// <summary>
        /// 当前已加载的所有启用 SDK Plugin 配置集合；LoadAsync 完成后可读，
        /// 主要供 Inspector 面板运行时只读展示。
        /// </summary>
        /// <returns>
        /// 当前启用的 Plugin 配置只读集合；未加载时返回空集合。
        /// </returns>
        IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs();

    }
}
