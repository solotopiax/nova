/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAdView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/03
 * descrip:   DemoAdView 演示 View — 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using TMPro;

namespace NovaFramework.Sdk.Ad.Samples.Runtime
{
    /// <summary>
    /// DemoAdView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoAdView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 设置广告 Demo 标题；交互事件由 prefab 持久化绑定。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Ad 演示");
        }

        /// <summary>
        /// 订阅 AdPlugin.Events，确保 Demo 能看到全局初始化、加载、播放、收益和关闭回调。
        /// </summary>
        private void SubscribeAdEvents()
        {
            if (m_EventsSubscribed)
            {
                return;
            }

            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            adPlugin.Events.InitResult.Subscribe(OnAdInitResult);
            adPlugin.Events.AdLoaded.Subscribe(OnAdLoaded);
            adPlugin.Events.AdLoadFailed.Subscribe(OnAdLoadFailed);
            adPlugin.Events.ShowCompleted.Subscribe(OnAdShowCompleted);
            adPlugin.Events.ShowFailed.Subscribe(OnAdShowFailed);
            adPlugin.Events.AdClosed.Subscribe(OnAdClosed);
            adPlugin.Events.RevenuePaid.Subscribe(OnAdRevenuePaid);
            m_AdPlugin = adPlugin;
            m_EventsSubscribed = true;
        }

        /// <summary>
        /// 取消当前 Demo 持有的 AdPlugin.Events 订阅。
        /// </summary>
        private void UnsubscribeAdEvents()
        {
            if (!m_EventsSubscribed || m_AdPlugin == null)
            {
                return;
            }

            m_AdPlugin.Events.InitResult.Unsubscribe(OnAdInitResult);
            m_AdPlugin.Events.AdLoaded.Unsubscribe(OnAdLoaded);
            m_AdPlugin.Events.AdLoadFailed.Unsubscribe(OnAdLoadFailed);
            m_AdPlugin.Events.ShowCompleted.Unsubscribe(OnAdShowCompleted);
            m_AdPlugin.Events.ShowFailed.Unsubscribe(OnAdShowFailed);
            m_AdPlugin.Events.AdClosed.Unsubscribe(OnAdClosed);
            m_AdPlugin.Events.RevenuePaid.Unsubscribe(OnAdRevenuePaid);
            m_EventsSubscribed = false;
            m_AdPlugin = null;
        }

        /// <summary>
        /// 请求指定广告类型，并把当前类型对应的参数回传输入框内容放入 customProps。
        /// </summary>
        /// <param name="format">要请求的广告类型。</param>
        /// <param name="customDataInput">当前广告类型对应的参数回传输入框。</param>
        private async UniTaskVoid RequestAdAsync(AdFormat format, TMP_InputField customDataInput)
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            try
            {
                AppendFeedback("正在请求" + GetFormatDisplayName(format) + "。");
                AdLoadResult result = await adPlugin.RequestAsync(format, BuildAdCustomProps(format, customDataInput));
                if (result == null || !result.Success)
                {
                    AppendFeedback(GetFormatDisplayName(format) + "请求失败：code=" + (result?.ErrorCode ?? 0) + "，message=" + (result?.ErrorMessage ?? "null"), FeedbackLevel.Error);
                    return;
                }

                AppendFeedback(GetFormatDisplayName(format) + "请求成功：placement=" + result.PlacementId + "，revenue=" + result.Revenue, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback(GetFormatDisplayName(format) + "请求已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback(GetFormatDisplayName(format) + "请求异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 查询指定广告类型是否已经可播放，并把结果输出到反馈区。
        /// </summary>
        /// <param name="format">要检查的广告类型。</param>
        private void CheckAdReady(AdFormat format)
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            bool ready = adPlugin.IsReady(format);
            AppendFeedback(GetFormatDisplayName(format) + " Ready=" + ready, ready ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 播放指定广告类型，并把当前类型对应的参数回传输入框内容放入 customProps。
        /// </summary>
        /// <param name="format">要播放的广告类型。</param>
        /// <param name="customDataInput">当前广告类型对应的参数回传输入框。</param>
        private async UniTaskVoid ShowAdAsync(AdFormat format, TMP_InputField customDataInput)
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            try
            {
                AppendFeedback("正在播放" + GetFormatDisplayName(format) + "。");
                await adPlugin.ShowAsync(format, BuildAdCustomProps(format, customDataInput));
                AppendFeedback(GetFormatDisplayName(format) + "播放调用完成。", FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback(GetFormatDisplayName(format) + "播放已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback(GetFormatDisplayName(format) + "播放失败：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 调用广告聚合插件显示已请求的 Banner。
        /// </summary>
        private void ShowBanner()
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            adPlugin.ShowBanner();
            AppendFeedback("显示 Banner。", FeedbackLevel.Success);
        }

        /// <summary>
        /// 调用广告聚合插件隐藏当前 Banner。
        /// </summary>
        private void HideBanner()
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            adPlugin.HideBanner();
            AppendFeedback("隐藏 Banner。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 调用广告聚合插件销毁当前 Banner。
        /// </summary>
        private void DestroyBanner()
        {
            if (!TryGetAdPlugin(out AdPlugin adPlugin))
            {
                return;
            }

            adPlugin.DestroyBanner();
            AppendFeedback("销毁 Banner。", FeedbackLevel.Warn);
        }

        /// <summary>
        /// 从 Nova.SDK 中获取广告聚合插件；未注册时统一向反馈区输出警告。
        /// </summary>
        /// <param name="adPlugin">找到的广告聚合插件实例。</param>
        /// <returns>找到可用插件时返回 true。</returns>
        private bool TryGetAdPlugin(out AdPlugin adPlugin)
        {
            adPlugin = null;
            if (Nova.SDK != null && Nova.SDK.TryGet(out adPlugin) && adPlugin != null)
            {
                return true;
            }

            AppendFeedback("未找到广告聚合插件，Debug UI 暂不可用。", FeedbackLevel.Warn);
            return false;
        }

        /// <summary>
        /// 构造传给广告 SDK 的调试参数。
        /// 固定写入 Demo 标记、当前渠道和广告类型；输入框有内容时额外写入 debug_custom_data。
        /// </summary>
        /// <param name="format">当前请求或播放的广告类型。</param>
        /// <param name="customDataInput">当前广告类型对应的参数回传输入框。</param>
        /// <returns>传给广告插件的 customProps 字典。</returns>
        private Dictionary<string, object> BuildAdCustomProps(AdFormat format, TMP_InputField customDataInput)
        {
            var props = new Dictionary<string, object>
            {
                ["debug_ui"] = true,
                ["debug_ad_channel"] = m_SelectedAdChannel.ToString(),
                ["debug_format"] = format.ToString(),
            };

            string customData = customDataInput != null ? customDataInput.text : string.Empty;
            if (!string.IsNullOrEmpty(customData))
            {
                props["debug_custom_data"] = customData;
            }

            return props;
        }

        /// <summary>
        /// AdPlugin.Events.InitResult 全局事件回调。
        /// </summary>
        /// <param name="success">初始化是否成功。</param>
        private void OnAdInitResult(bool success)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.InitResult：Success=" + success, success ? FeedbackLevel.Success : FeedbackLevel.Error);
        }

        /// <summary>
        /// AdPlugin.Events.AdLoaded 全局事件回调。
        /// </summary>
        /// <param name="result">广告加载结果。</param>
        private void OnAdLoaded(AdLoadResult result)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.AdLoaded：" + FormatLoadResult(result), FeedbackLevel.Success);
        }

        /// <summary>
        /// AdPlugin.Events.AdLoadFailed 全局事件回调。
        /// </summary>
        /// <param name="result">广告加载结果。</param>
        private void OnAdLoadFailed(AdLoadResult result)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.AdLoadFailed：" + FormatLoadResult(result), FeedbackLevel.Error);
        }

        /// <summary>
        /// AdPlugin.Events.ShowCompleted 全局事件回调。
        /// </summary>
        /// <param name="result">广告展示结果。</param>
        private void OnAdShowCompleted(AdResult result)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.ShowCompleted：" + FormatAdResult(result), FeedbackLevel.Success);
        }

        /// <summary>
        /// AdPlugin.Events.ShowFailed 全局事件回调。
        /// </summary>
        /// <param name="result">广告展示结果。</param>
        private void OnAdShowFailed(AdResult result)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.ShowFailed：" + FormatAdResult(result), FeedbackLevel.Error);
        }

        /// <summary>
        /// AdPlugin.Events.AdClosed 全局事件回调。
        /// </summary>
        /// <param name="result">广告关闭结果。</param>
        private void OnAdClosed(AdResult result)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.AdClosed：" + FormatAdResult(result), FeedbackLevel.Info);
        }

        /// <summary>
        /// AdPlugin.Events.RevenuePaid 全局事件回调。
        /// </summary>
        /// <param name="e">广告收益事件。</param>
        private void OnAdRevenuePaid(AdEvent e)
        {
            AppendFeedback("【全局事件】AdPlugin.Events.RevenuePaid：format=" + e.Format + "，placement=" + e.PlacementId + "，revenue=" + e.Revenue, FeedbackLevel.Success);
        }

        /// <summary>
        /// 格式化广告加载结果，供反馈区展示。
        /// </summary>
        /// <param name="result">广告加载结果。</param>
        /// <returns>可读文本。</returns>
        private static string FormatLoadResult(AdLoadResult result)
        {
            if (result == null)
            {
                return "null";
            }

            return "Success=" + result.Success + "，format=" + result.Format + "，placement=" + result.PlacementId + "，code=" + result.ErrorCode + "，message=" + result.ErrorMessage + "，revenue=" + result.Revenue;
        }

        /// <summary>
        /// 格式化广告展示结果，供反馈区展示。
        /// </summary>
        /// <param name="result">广告展示结果。</param>
        /// <returns>可读文本。</returns>
        private static string FormatAdResult(AdResult result)
        {
            if (result == null)
            {
                return "null";
            }

            return "Success=" + result.Success + "，Completed=" + result.UserCompleted + "，format=" + result.Format + "，placement=" + result.PlacementId + "，message=" + result.ErrorMessage + "，revenue=" + result.Revenue + "，rewardGranted=" + result.RewardGranted;
        }

        /// <summary>
        /// 获取广告类型在反馈区中使用的中文显示名。
        /// </summary>
        /// <param name="format">广告类型。</param>
        /// <returns>广告类型中文显示名。</returns>
        private static string GetFormatDisplayName(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Interstitial:
                    return "插屏广告";
                case AdFormat.Rewarded:
                    return "激励视频广告";
                case AdFormat.Banner:
                    return "条幅广告";
                case AdFormat.AppOpen:
                    return "开屏广告";
                default:
                    return format.ToString();
            }
        }
    }
}
