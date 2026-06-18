/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdPlugin.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   Google AdMob 广告渠道插件骨架，继承 AdChannelPluginBase
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    /// <summary>
    /// Google AdMob 广告渠道插件。
    /// 继承 AdChannelPluginBase，暂不实现任何渠道逻辑，仅用于验证配置流程。
    /// </summary>
    [AdChannel(typeof(AdMobAdChannelConfig))]
    public sealed class AdMobAdPlugin : AdChannelPluginBase
    {
        /// <summary>
        /// 插件唯一名称。
        /// </summary>
        public override string Name => "AdMobAdPlugin";

        /// <summary>
        /// 当前渠道枚举标识，返回 Google AdMob。
        /// </summary>
        public override AdChannelType Channel => AdChannelType.AdMob;

        /// <summary>
        /// 渠道 SDK 初始化；暂不实现，未调用 RegisterAdUnits 即默认不支持任何 AdFormat。
        /// </summary>
        /// <param name="config">渠道配置。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>完成的异步任务。</returns>
        protected override UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)
            => UniTask.CompletedTask;
    }
}
