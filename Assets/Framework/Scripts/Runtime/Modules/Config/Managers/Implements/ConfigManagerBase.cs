/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigManagerBase.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   配置管理器基类
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 配置管理器基类；继承 FrameworkManager 并声明 IConfigManager 的所有 abstract 成员。
    /// 派生类 ConfigManager 提供 AB 异步加载并以 ConfigRuntimeSO 为数据源直读的具体实现。
    /// </summary>
    internal abstract class ConfigManagerBase : FrameworkManager, IConfigManager
    {
        /// <summary>
        /// 管理器优先级；Config 需早于使用方，取 10（低于默认 0 以外的其他业务模块）。
        /// 值越小越先 Update、越后 Shutdown。
        /// </summary>
        public override int Priority => 10;

        /// <summary>
        /// 初始化入口；接收 Component 从 Inspector 构造的 ConfigManagerConfig。
        /// </summary>
        /// <param name="config">
        /// 初始化配置，含 AB 路径与 ConfigRuntimeSO 资产名。
        /// </param>
        public abstract void Initialize(ConfigManagerConfig config);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 是否已完成加载；Component.Start 应 await 直至此值为 true。
        /// </summary>
        public abstract bool IsLoadOver { get; }

        /// <summary>
        /// 当前运行时的开发模式（Debug / Release）；LoadAsync 完成后可读。
        /// </summary>
        public abstract DevelopMode DevelopMode { get; }

        /// <summary>
        /// 全局公共配置整块；LoadAsync 完成后可读，未加载时返回 null。
        /// </summary>
        public abstract CommonConfig Common { get; }

        /// <summary>
        /// 当前解析后的 Namespace。
        /// </summary>
        public abstract string Namespace { get; }

        /// <summary>
        /// 业务入口 Procedure 相对类型名；LoadAsync 完成后可读。
        /// </summary>
        public abstract string GameEntranceProcedureName { get; }

        /// <summary>
        /// AOT 元数据 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        public abstract IReadOnlyList<DllAssetEntry> AotMetadataDlls { get; }

        /// <summary>
        /// 业务 DLL 列表；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        public abstract IReadOnlyList<DllAssetEntry> GameDlls { get; }

        /// <summary>
        /// 本次加载记录的目标平台。
        /// </summary>
        public abstract PlatformType Platform { get; }

        /// <summary>
        /// 本次加载记录的目标渠道。
        /// </summary>
        public abstract ChannelType Channel { get; }

        /// <summary>
        /// 异步加载并解析 ConfigRuntimeSO。
        /// </summary>
        /// <returns>
        /// 加载完成的异步任务。
        /// </returns>
        public abstract UniTask LoadAsync();

        /// <summary>
        /// 按泛型类型取 SDK Plugin 配置实例；未启用或类型不匹配返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// SDK Plugin 所需配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        public abstract T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;

        /// <summary>
        /// 按类型对象取 SDK Plugin 配置实例（非泛型版）；未启用或 type 为 null 返回 null。
        /// </summary>
        /// <param name="type">
        /// SDK Plugin 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        public abstract ISDKPluginConfig GetSDKPluginConfig(Type type);

        /// <summary>
        /// 按泛型类型取 Kit 配置实例；未启用或类型不匹配返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// Kit 所需配置类型，须实现 IKitConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        public abstract T GetKitConfig<T>() where T : class, IKitConfig;

        /// <summary>
        /// 按类型对象取 Kit 配置实例（非泛型版）；未启用或 type 为 null 返回 null。
        /// </summary>
        /// <param name="type">
        /// Kit 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        public abstract IKitConfig GetKitConfig(Type type);

        /// <summary>
        /// 当前已加载的所有启用 SDK Plugin 配置集合；LoadAsync 完成后可读，未加载时返回空集合。
        /// </summary>
        /// <returns>
        /// 当前启用的 Plugin 配置只读集合。
        /// </returns>
        public abstract IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs();

    }
}
