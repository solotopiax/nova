/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetWebPreloadRegistry.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   Web 平台资源预下载适配注册入口
 ***************************************************************/

using System;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 供平台 SDK 在场景加载前注册资源预下载适配；业务仍通过 IAssetManager 使用下载器。
    /// 平台策略由各包创建，框架不依赖微信 SDK，也不暴露 YooAsset 内部策略类型。
    /// </summary>
    public static class AssetWebPreloadRegistry
    {
        private static Func<UnityWebRequestCreator, object> s_StrategyFactory;

        /// <summary>
        /// 注册平台策略工厂；重复注册覆盖旧工厂，传入 null 注销。
        /// 工厂必须为每个包创建独立策略，并使用传入的请求创建器保留 Nova 请求超时。
        /// </summary>
        /// <param name="strategyFactory">接收请求创建器并返回 YooAsset 平台预下载策略的工厂。</param>
        public static void Register(Func<UnityWebRequestCreator, object> strategyFactory)
        {
            s_StrategyFactory = strategyFactory;
        }

        /// <summary>
        /// 将已注册平台策略注入当前包的 WebNetwork 参数；未注册或工厂返回 null 时保持原行为。
        /// </summary>
        /// <param name="parameters">目标包的 WebNetwork 文件系统参数。</param>
        /// <param name="requestCreator">保留当前 AssetManager 超时配置的请求创建器。</param>
        /// <returns>实际注入策略时返回 true。</returns>
        internal static bool TryConfigure(FileSystemParameters parameters, UnityWebRequestCreator requestCreator)
        {
            object strategy = s_StrategyFactory?.Invoke(requestCreator);
            if (strategy == null)
            {
                return false;
            }

            parameters.AddParameter(EFileSystemParameter.WebPreloadStrategy, strategy);
            return true;
        }
    }
}
