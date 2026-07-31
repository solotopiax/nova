/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGA SDK 插件主文件（public/override 方法）
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using ThinkingData.Analytics;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    /// <summary>
    /// TGA（ThinkingAnalytics）SDK 插件，继承 SDKPluginBase，实现埋点服务与 TDA 专有能力。
    /// 负责 SDK 初始化、事件上报、用户属性管理及公共属性管理。
    /// 通过独立的 TGADynamicSuperPropertyListener 监听器接收 TDA SDK 的动态公共属性回调。
    /// </summary>
    public sealed partial class TGAPlugin : SDKPluginBase, ITrackPlugin, IDeviceIdProvider
    {
    
        /// <summary>
        /// 异步初始化 TGA SDK。
        /// config 为 SDKManager 按 ConfigType 自动从 IConfigManager 注入的 TGAPluginConfig；
        /// 内部按其 AppID / Mode / TimeZone / LogEnable / ServerCmdName / ReportCmdName /
        /// IsTestUser / AssignDeviceIdToDistinctId 字段初始化 TDA SDK 与登录上报链路。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的 TGAPluginConfig 配置。</param>
        /// <param name="ct">取消令牌，TGA 同步初始化路径不使用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            try
            {
                // 缓存运行时配置与上报服务。配置缺失时使用 SDK 默认值，后续只对必要字段做硬校验。
                m_ReportNetService = new TGAReportNetService();
                m_RuntimeConfig = config as TGAPluginConfig;
                string appId = m_RuntimeConfig?.AppID ?? string.Empty;
                TDMode mode = m_RuntimeConfig?.Mode ?? TDMode.Normal;
                TDTimeZone timeZone = m_RuntimeConfig?.TimeZone ?? TDTimeZone.Local;
                bool logEnable = m_RuntimeConfig?.LogEnable ?? false;
                string serverCmdName = m_RuntimeConfig?.ServerCmdName ?? string.Empty;
                bool isTestUser = m_RuntimeConfig?.IsTestUser ?? true;
                bool assignDeviceIdToDistinctId = m_RuntimeConfig?.AssignDeviceIdToDistinctId ?? false;

                // AppId 与上报地址是 TGA SDK 初始化的最低要求，缺少任意一项都不能继续初始化。
                if (string.IsNullOrEmpty(appId))
                {
                    Log.Warning(LogTag.TGA, "TGA AppId 为空，SDK 初始化跳过。");
                    return UniTask.CompletedTask;
                }

                if (string.IsNullOrEmpty(serverCmdName))
                {
                    Log.Warning(LogTag.TGA, "TGA ServerCmdName 为空，SDK 初始化跳过。");
                    return UniTask.CompletedTask;
                }

                // ServerCmdName 只保存表名入口，实际 URL 走 Network 模块解析，保持配置与环境地址解耦。
                INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(serverCmdName);
                string serverUrl = cmdRow != null ? Nova.Network?.ResolveNetCmdUrl(cmdRow) : null;
                if (string.IsNullOrEmpty(serverUrl))
                {
                    Log.Warning(LogTag.TGA, $"按 ServerCmdName「{serverCmdName}」解析上报地址失败，TGA SDK 初始化跳过。");
                    return UniTask.CompletedTask;
                }

                // 将 Nova 配置直写到 ThinkingAnalytics 配置对象，避免在初始化路径中再做枚举转换。
                TDConfig tdConfig = new TDConfig(appId, serverUrl);
                tdConfig.mode = mode;
                tdConfig.timeZone = timeZone;
                TDAnalytics.EnableLog(logEnable);
                TDAnalytics.SetNetworkType(TDNetworkType.All);
                TDAnalytics.Init(tdConfig);
                
                // 初始化后将 DeviceId 设置为 DistinctId。
                ApplyDeviceIdAsDistinctId(assignDeviceIdToDistinctId);
                // 初始化框架默认属性
                InitFrameworkProperties(isTestUser);

                // 动态公共属性依赖 Unity 回调对象，需要挂到 SDKComponent 下随框架生命周期管理。
                var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
                m_DynamicSuperPropertyListener = CreateDynamicSuperPropertyListener(sdkComponent != null ? sdkComponent.transform : null);
                TDAnalytics.SetDynamicSuperProperties(m_DynamicSuperPropertyListener);
                TDAnalytics.EnableAutoTrack(TDAutoTrackEventType.All);

                // 发布 TGA 标识供其他 SDK 消费，同时启动异步桥接，不阻塞 TGA 自身初始化完成。
                PublishTGAIdentifiers();
                RegisterFetchDataAsync(ct).Forget();

                // 登录事件到来后再绑定 AccountId，并触发 TGA 标识上报到业务服务器。
                m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
                m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);

                Log.Debug(LogTag.TGA, "初始化完成。");
            }
            catch (Exception e)
            {
                Log.Error(LogTag.TGA, $"初始化异常：{e}");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步释放 TGA SDK 资源。
        /// 先退订 SDKEventData.UserLogin 事件，再返回完成任务；TGA SDK 无显式 Shutdown API。
        /// </summary>
        /// <param name="ct">取消令牌，TGA 无释放逻辑，此处不使用。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            if (m_EventManager != null)
            {
                m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
                m_EventManager = null;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 上报无附加参数的埋点事件。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        public void TrackEvent(string eventName)
        {
            TDAnalytics.Track(eventName, new Dictionary<string, object>());
        }

        /// <summary>
        /// 上报携带自定义参数的埋点事件。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="parameters">事件附加参数字典。</param>
        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            TDAnalytics.Track(eventName, parameters);
        }

        /// <summary>
        /// 实现 ITrackPlugin 契约，将通用埋点事件载荷转发至 TDA SDK 上报。
        /// evt 为 null 或 evt.Name 为空时静默返回，不抛出异常。
        /// </summary>
        /// <param name="evt">通用埋点事件载荷。</param>
        public void TrackEvent(TrackEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.Name)) return;
            TDAnalytics.Track(evt.Name, evt.Parameters ?? new Dictionary<string, object>());
        }

        /// <summary>
        /// 上报携带 JSON 字符串参数的埋点事件。
        /// properties 为空字符串时等同于无参数上报。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="properties">JSON 格式的事件属性字符串，可为空。</param>
        public void TrackEvent(string eventName, string properties)
        {
            TDAnalytics.TrackStr(eventName, properties);
        }

        /// <summary>
        /// 上报携带自定义时间的埋点事件。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="parameters">事件附加参数字典。</param>
        /// <param name="time">事件发生时间。</param>
        /// <param name="timeZone">事件时间对应的时区。</param>
        public void TrackEvent(string eventName, Dictionary<string, object> parameters, DateTime time, TimeZoneInfo timeZone)
        {
            TDAnalytics.Track(eventName, parameters, time, timeZone);
        }

        /// <summary>
        /// 设置单条字符串类型用户属性，内部委托 UserSet 实现。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性字符串值。</param>
        public void SetUserProperty(string key, string value)
        {
            UserSet(key, value);
        }

        /// <summary>
        /// 设置当前用户的账号 ID，内部委托 Login 实现。
        /// </summary>
        /// <param name="userId">用户唯一标识。</param>
        public void SetUserId(string userId)
        {
            Login(userId);
        }

        /// <summary>
        /// 设置账号 ID，关联访客 ID 与账号。
        /// </summary>
        /// <param name="accountID">账号唯一标识。</param>
        public void Login(string accountID)
        {
            m_AccountId = accountID;
            TDAnalytics.Login(accountID);
            Log.Debug(LogTag.TGA, $"TGAPlugin.Login => {accountID}");

        }

        /// <summary>
        /// 清除账号 ID，恢复为访客模式。
        /// </summary>
        public void Logout()
        {
            m_AccountId = null;
            TDAnalytics.Logout();
            Log.Debug(LogTag.TGA, "TGAPlugin.Logout");
        }

        /// <summary>
        /// 设置访客ID
        /// </summary>
        /// <param name="distinctId">访客 ID 字符串。</param>
        public void SetDistinctId(string distinctId)
        {
            TDAnalytics.SetDistinctId(distinctId);
        }

        /// <summary>
        /// 获取 TDA 分配的访客 ID。
        /// </summary>
        /// <returns>访客 ID 字符串。</returns>
        public string GetDistinctId()
        {
            return TDAnalytics.GetDistinctId();
        }

        /// <summary>
        /// 获取设备唯一标识符。
        /// </summary>
        /// <returns>设备 ID 字符串；TDA 未就绪时可能返回 null。</returns>
        public string GetDeviceId()
        {
            return TDAnalytics.GetDeviceId();
        }

        /// <summary>
        /// IDeviceIdProvider 契约：返回 TDA 提供的设备唯一标识。
        /// </summary>
        /// <returns>设备 ID 字符串；TDA 未就绪时返回空串。</returns>
        string IDeviceIdProvider.GetDeviceID()
        {
            return TDAnalytics.GetDeviceId() ?? string.Empty;
        }

        /// <summary>
        /// 使用毫秒级 Unix 时间戳校准 SDK 时间。
        /// </summary>
        /// <param name="timestamp">毫秒级 Unix 时间戳。</param>
        public void CalibrateTime(long timestamp)
        {
            TDAnalytics.CalibrateTime(timestamp);
        }

        /// <summary>
        /// 通过 NTP 服务器校准 SDK 时间。
        /// </summary>
        /// <param name="ntpServer">NTP 服务器地址。</param>
        public void CalibrateTimeWithNtp(string ntpServer)
        {
            TDAnalytics.CalibrateTimeWithNtp(ntpServer);
        }

        /// <summary>
        /// 对指定事件开始计时，上报时自动附加耗时属性。
        /// </summary>
        /// <param name="eventName">需要计时的事件名称。</param>
        public void TimeEvent(string eventName)
        {
            TDAnalytics.TimeEvent(eventName);
        }

        /// <summary>
        /// 立即将本地缓存的事件数据上报到服务端。
        /// </summary>
        public void Flush()
        {
            TDAnalytics.Flush();
        }

        /// <summary>
        /// 启用或禁用 SDK 数据采集功能。
        /// </summary>
        /// <param name="enable">true 表示启用，false 表示禁用。</param>
        public void EnableTracking(bool enable)
        {
            TDAnalytics.SetTrackStatus(enable ? TDTrackStatus.Normal : TDTrackStatus.Pause);
        }

        /// <summary>
        /// 设置 SDK 打点状态（0=Normal，1=Pause，2=Stop）。
        /// </summary>
        /// <param name="status">打点状态枚举值，映射 TDTrackStatus。</param>
        public void SetTrackStatus(int status)
        {
            TDAnalytics.SetTrackStatus((TDTrackStatus)status);
        }

        /// <summary>
        /// 获取 SDK 预置属性字典。
        /// </summary>
        /// <returns>预置属性键值对字典。</returns>
        public Dictionary<string, object> GetPresetProperties()
        {
            return TDAnalytics.GetPresetProperties().ToDictionary();
        }

        /// <summary>
        /// 获取设备本地地区代码。
        /// </summary>
        /// <returns>地区代码字符串。</returns>
        public string GetLocalRegion()
        {
            return TDAnalytics.GetLocalRegion();
        }

        /// <summary>
        /// 更新框架层静态公共事件属性；SDK 已初始化时立即合并进 SDK 静态公共属性。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void SetFrameworkSuperProperty(string key, object value)
        {
            m_FrameworkSuperProperties[key] = value;
        }

        /// <summary>
        /// 更新框架层动态公共事件属性，下次 SDK 回调时生效。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void SetFrameworkDynamicProperty(string key, object value)
        {
            m_FrameworkDynamicProperties[key] = value;
        }

        /// <summary>
        /// 更新框架层 UserSet 属性，下次调用 UserSet 时自动合并。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void SetFrameworkUserProperty(string key, object value)
        {
            m_FrameworkUserSetProperties[key] = value;
        }

        /// <summary>
        /// 更新框架层 UserSetOnce 属性，下次调用 UserSetOnce 时自动合并。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void SetFrameworkUserSetOnceProperty(string key, object value)
        {
            m_FrameworkUserSetOnceProperties[key] = value;
        }
    }
}
#endif
