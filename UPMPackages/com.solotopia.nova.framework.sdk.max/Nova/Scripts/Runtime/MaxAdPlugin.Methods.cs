/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 私有方法
 ***************************************************************/

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
        /// MAX SDK 初始化完成回调，在 InitializeSdk 异步返回后由 MaxSdkCallbacks.OnSdkInitializedEvent 触发。
        /// 负责打印国家代码、应用静音设置、启用调试开关、注册各广告格式回调，最后通知上层初始化完成。
        /// </summary>
        /// <param name="sdkConfig">MAX SDK 返回的初始化配置，含国家代码和初始化状态。</param>
        /// <param name="cfg">当前渠道配置，含日志开关和 CreativeDebugger 开关。</param>
        /// <param name="initTcs">等待初始化完成的异步挂起句柄。</param>
        private void OnSdkInitializedCallback(MaxSdkBase.SdkConfiguration sdkConfig, MaxAdChannelConfig cfg, UniTaskCompletionSource<bool> initTcs)
        {
            // 缓存国家代码，供后续数据上报使用
            m_CountryCode = sdkConfig.CountryCode;

            // 授权结果必须结合是否已设置判断，避免把“尚未设置”误认为“拒绝”
            m_IsUserConsentSet = MaxSdk.IsUserConsentSet();
            m_HasUserConsent = m_IsUserConsentSet && MaxSdk.HasUserConsent();
            m_PrivacyFlowCompletionSource.TrySetResult();

            Log.Debug(LogTag.Max, $"MAX 返回的国家代码：{m_CountryCode}");

            // 控制创意调试器（CreativeDebugger）开关
            MaxSdk.SetCreativeDebuggerEnabled(cfg.CreativeDebuggerEnabled);

            // 注册各广告格式的 MaxSdk 事件回调
            RegisterCallbacks();

            // 测试环境下显示 MAX 调解调试器界面（仅 Android/iOS 真机生效）
            if (cfg.MediationDebuggerEnabled)
            {
                MaxSdk.ShowMediationDebugger();
            }

            // 通知上层初始化完成
            RaiseInitResult(true);

            // 解除 await 挂起
            initTcs.TrySetResult(true);
        }
#endif
    }
}
