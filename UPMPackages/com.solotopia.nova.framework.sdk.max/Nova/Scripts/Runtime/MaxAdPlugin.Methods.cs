/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// 获取 MAX SDK 初始化回调返回的国家或地区代码。
        /// </summary>
        /// <returns>MAX SDK 返回的国家或地区代码；尚未初始化或未返回时为空字符串。</returns>
        public override string GetCountryCode() => m_CountryCode ?? string.Empty;

        /// <summary>
        /// 查询 MAX 初始化完成时是否已取得用户明确的广告隐私授权决定。
        /// </summary>
        /// <returns>已明确授权或拒绝时返回 true；初始化前或尚未设置时返回 false。</returns>
        public override bool IsUserConsentSet() => m_IsUserConsentSet;

        /// <summary>
        /// 查询 MAX 初始化完成时缓存的用户广告隐私授权结果。
        /// 必须结合 IsUserConsentSet() 区分“拒绝”和“尚未设置”。
        /// </summary>
        /// <returns>用户已明确同意时返回 true；拒绝、尚未设置或初始化前返回 false。</returns>
        public override bool HasUserConsent() => m_HasUserConsent;

        /// <summary>
        /// 等待 MAX 初始化期间的 Consent Flow 结束。
        /// MAX 未展示 Consent Flow 时随初始化完成；展示时在用户完成同意或拒绝后完成。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Consent Flow 完成任务。</returns>
        public override UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default)
        {
            return m_PrivacyFlowCompletionSource.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 一次性校验完整配置后注册各有效格式，缓存冻结计划中的真实 ID。
        /// 无效格式返回配置错误并跳过注册，不能把分组表达式传给原厂。
        /// </summary>
        /// <param name="config">包含全屏原始分组和 Banner 真实 ID 的配置。</param>
        private void RegisterChannelPlacements(MaxAdChannelConfig config)
        {
            var validation = MaxAdGroupConfigValidator.Validate(config);
            foreach (var item in validation)
            {
                if (!item.Value.IsValid)
                {
                    string message = string.Join("；", item.Value.Errors);
                    RegisterAdConfigFailure(item.Key, message);
                    Log.Warning(LogTag.Max, $"MAX {item.Key} 配置无效：{message}");
                    continue;
                }
                switch (item.Key)
                {
                    case AdFormat.Rewarded: RegisterAdGroups(item.Key, config.RVPlacementGroups); break;
                    case AdFormat.Interstitial: RegisterAdGroups(item.Key, config.InterPlacementGroups); break;
                    case AdFormat.AppOpen: RegisterAdGroups(item.Key, config.AppOpenPlacementGroups); break;
                    case AdFormat.Banner:
                        var options = new List<AdUnitOptions>(item.Value.EffectivePlacementIds.Count);
                        foreach (string id in item.Value.EffectivePlacementIds) options.Add(new AdUnitOptions(id));
                        if (options.Count > 0) RegisterAdUnits(AdFormat.Banner, options);
                        break;
                }
            }
            m_RVPlacementIds = GetRegisteredPlacementIds(AdFormat.Rewarded);
            m_InterPlacementIds = GetRegisteredPlacementIds(AdFormat.Interstitial);
            m_AppOpenPlacementIds = GetRegisteredPlacementIds(AdFormat.AppOpen);
            m_BannerPlacementIds = GetRegisteredPlacementIds(AdFormat.Banner);
            m_BannerAutoRefreshIntervalSeconds = config.BannerAutoRefreshIntervalSeconds;
        }

        /// <summary>
        /// 在初始化主流程中缓存 MAX 结果、授权状态和调试设置，并通知上层。
        /// 广告事件注册完成后才开放加载和结束隐私等待。
        /// </summary>
        /// <param name="sdkConfig">MAX 返回的初始化结果，包含国家代码和真实成功状态。</param>
        /// <param name="cfg">当前渠道配置，包含 CreativeDebugger 与 MediationDebugger 开关。</param>
        private void OnSdkInitializedCallback(MaxSdkBase.SdkConfiguration sdkConfig, MaxAdChannelConfig cfg)
        {
            bool succeeded = sdkConfig != null && sdkConfig.IsSuccessfullyInitialized;

            // 缓存国家代码，供后续数据上报使用。
            m_CountryCode = sdkConfig?.CountryCode;

            // 授权结果结合是否已设置判断，避免把“尚未设置”当作“拒绝”。
            m_IsUserConsentSet = MaxSdk.IsUserConsentSet();
            m_HasUserConsent = m_IsUserConsentSet && MaxSdk.HasUserConsent();
            Log.Debug(LogTag.Max, $"MAX 返回的国家代码：{m_CountryCode}");

            // 原厂成功后应用创意调试器开关，并注册各格式的广告事件。
            if (succeeded)
            {
                MaxSdk.SetCreativeDebuggerEnabled(cfg.CreativeDebuggerEnabled);
                RegisterCallbacks();

                // 测试环境下显示调解调试器，只有 Android/iOS 真机生效。
                if (cfg.MediationDebuggerEnabled)
                {
                    MaxSdk.ShowMediationDebugger();
                }
            }

            // 初始化步骤全部完成后再开放加载，隐私等待者可立即发起广告请求。
            m_IsSdkInitialized = succeeded;
            m_PrivacyFlowCompletionSource.TrySetResult();
            RaiseInitResult(succeeded);
        }

        /// <summary>
        /// 按格式核对回调 ID 属于冻结注册计划，不查询或修改组状态。
        /// </summary>
        /// <param name="format">回调所属广告格式。</param>
        /// <param name="placementId">厂商回调携带的真实广告位 ID。</param>
        /// <returns>格式与真实 ID 同时匹配已注册计划时为 true。</returns>
        private bool IsMaxPlacementRegistered(AdFormat format, string placementId)
        {
            if (IsDisposed || string.IsNullOrEmpty(placementId)) return false;
            IReadOnlyList<string> ids = format switch
            {
                AdFormat.Rewarded => m_RVPlacementIds,
                AdFormat.Interstitial => m_InterPlacementIds,
                AdFormat.AppOpen => m_AppOpenPlacementIds,
                AdFormat.Banner => m_BannerPlacementIds,
                _ => null,
            };
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], placementId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// 过滤未注册或释放后的回调；未注册回调记录警告，不会创建槽位或推进请求。
        /// </summary>
        /// <param name="format">回调所属格式。</param>
        /// <param name="placementId">回调广告位。</param>
        /// <param name="callback">用于定位诊断的回调名称。</param>
        /// <returns>回调属于本渠道有效计划时为 true。</returns>
        private bool AcceptMaxCallback(AdFormat format, string placementId, string callback)
        {
            if (IsMaxPlacementRegistered(format, placementId)) return true;
            if (!IsDisposed)
                Log.Warning(LogTag.Max, $"忽略未注册回调：format={format}，placement={placementId}，callback={callback}，原因=格式或真实广告位不属于当前注册计划。");
            return false;
        }

        /// <summary>
        /// 核对回调格式、真实广告位及终态等待，不匹配时直接拒绝回调。
        /// </summary>
        /// <param name="format">原厂回调所属的全屏广告格式。</param>
        /// <param name="placementId">原厂回调携带的真实广告位 ID。</param>
        /// <returns>广告位匹配且该格式仍有终态等待时为 true。</returns>
        private bool IsCurrentFullscreenShow(AdFormat format, string placementId)
        {
            if (!string.Equals(m_ActiveFullscreenPlacementId, placementId, StringComparison.Ordinal))
            {
                return false;
            }

            return format switch
            {
                AdFormat.Rewarded => m_RVTcs != null,
                AdFormat.Interstitial => m_InterTcs != null,
                AdFormat.AppOpen => m_AppOpenTcs != null,
                _ => false,
            };
        }

        /// <summary>
        /// 领取匹配终态的完成源，并在业务通知前清空成员状态。
        /// 调用方完成此局部句柄，避免业务重入后误结束新展示的等待。
        /// </summary>
        /// <param name="format">终态所属的全屏广告格式。</param>
        /// <param name="placementId">终态携带的真实广告位 ID。</param>
        /// <param name="completion">本次终态对应的完成源；不匹配时为 null。</param>
        /// <returns>首次匹配终态时为 true，未知或已结束的终态为 false。</returns>
        private bool TryFinishFullscreenShow(AdFormat format, string placementId, out UniTaskCompletionSource<AdResult> completion)
        {
            completion = null;
            if (!IsCurrentFullscreenShow(format, placementId))
            {
                return false;
            }

            switch (format)
            {
                case AdFormat.Rewarded:
                    completion = m_RVTcs;
                    m_RVTcs = null;
                    break;
                case AdFormat.Interstitial:
                    completion = m_InterTcs;
                    m_InterTcs = null;
                    break;
                case AdFormat.AppOpen:
                    completion = m_AppOpenTcs;
                    m_AppOpenTcs = null;
                    break;
            }
            m_ActiveFullscreenPlacementId = null;
            return true;
        }

        /// <summary>
        /// 将 MAX Show 同步异常作为明确失败收尾，等待句柄与基类占用只结束一次。
        /// </summary>
        /// <param name="format">异常所属格式。</param>
        /// <param name="placementId">实际请求展示的真实 ID。</param>
        /// <param name="exception">原厂调用同步抛出的异常。</param>
        private void FinishShowException(AdFormat format, string placementId, Exception exception)
        {
            string message = exception.Message;
            Log.Warning(LogTag.Max, $"原厂展示调用异常：format={format}，placement={placementId}，message={message}。");
            PostAdCallbackToMainThread(() =>
            {
                if (!TryFinishFullscreenShow(format, placementId, out var completion))
                {
                    return;
                }
                var showProps = AbandonFullscreenImpression(format, placementId);
                var result = new AdResult { Success = false, Format = format, PlacementId = placementId, ErrorMessage = message };
                if (format == AdFormat.Rewarded) m_RVRewarded = false;
                try
                {
                    RaiseShowFailed(result, showProps);
                }
                finally
                {
                    completion.TrySetResult(result);
                }
            });
        }
#endif
    }
}
