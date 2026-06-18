/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
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
    }
}
