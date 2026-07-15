/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterPlugin.Methods.cs
 * author:    taoye
 * created:   2026/7/3
 * descrip:   DataMasterPlugin 私有方法（登录事件订阅与服务端配置拉取）。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
#if NOVA_STARLUS_DATAMASTER
using StarlusSDK.DataMaster;
#endif
using UnityEngine;

namespace NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime
{
    public sealed partial class DataMasterPlugin
    {
        /// <summary>
        /// 拉取 EventManager 并订阅 SDKEventData.UserLogin 事件。
        /// 订阅后用户登录时触发 OnUserLogin 回调，据此发起服务端配置拉取。
        /// </summary>
        private void SubscribeEvents()
        {
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
        }

        /// <summary>
        /// SDKEventData.UserLogin 事件处理器：拿到已登录用户 ID 后发起服务端配置拉取。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login) || string.IsNullOrEmpty(login.UserId))
            {
                return;
            }
            m_CurrentUserId = login.UserId;
            TriggerRefresh(login.UserId);
        }

        /// <summary>
        /// 以指定用户 ID 发起服务端配置拉取（携带已设置的用户属性用于分流）。
        /// 厂商 RefreshFromServer 为回调式异步，此处 fire-and-forget；拉取成功后触发 OnConfigRefreshed。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        private void TriggerRefresh(string userId)
        {
            if (!m_Initialized)
            {
                return;
            }

            RefreshFrameworkUserProperties();
            string deviceId = ResolveDeviceId();
            string props = m_UserProperties.Count == 0
                ? "(空)"
                : string.Join(", ", m_UserProperties.Select(kv => string.Concat(kv.Key, "=", kv.Value)));
            string refreshLine = Txt.Format(
                "DataMaster 拉取传参：userId={0}, deviceId={1}, userProperties(count={2})=[{3}]",
                userId, deviceId, m_UserProperties.Count, props);
            Log.Debug(LogTag.SDK, refreshLine);
            OnRefreshTriggered?.Invoke(refreshLine);
#if NOVA_STARLUS_DATAMASTER
            DataMaster.Instance.RefreshFromServer(
                userId,
                deviceId,
                m_UserProperties,
                onSuccess: () =>
                {
                    Log.Debug(LogTag.SDK, "DataMaster 服务端配置拉取成功。");
                    OnConfigRefreshed?.Invoke();
                },
                onError: err =>
                {
                    Log.Warning(LogTag.SDK, Txt.Format("DataMaster 服务端配置拉取失败：{0}", err));
                    OnConfigRefreshFailed?.Invoke(err);
                });
#endif
        }

        /// <summary>
        /// 在每次服务端刷新请求发出前，强制更新框架管理的分流属性。
        /// 项目通过 SetUserProperty 写入的 country_code 与其他自定义 key 保持不变。
        /// </summary>
        private void RefreshFrameworkUserProperties()
        {
#if NOVA_STARLUS_DATAMASTER
            DataMasterUserContextBuilder.UpdateRefreshProperties(
                m_UserProperties,
                GetAppVersionCode(),
                GetInstallTimeMs());
#else
            m_UserProperties["app_version"] = GetAppVersionCode();
            m_UserProperties["install_time"] = GetInstallTimeMs();
#endif
        }

        /// <summary>
        /// 订阅框架归因插件，并异步补获可能已在订阅前缓存的归因结果。
        /// 等待过程不阻塞 DataMaster 初始化。
        /// </summary>
        private void SubscribeAttribution(CancellationToken initializationToken)
        {
            if (Nova.SDK == null || !Nova.SDK.TryGet<IAttributionPlugin>(out m_AttributionPlugin))
            {
                return;
            }

            m_AttributionPlugin.OnAttributionResolved += OnAttributionResolved;
            m_AttributionCancellation = CancellationTokenSource.CreateLinkedTokenSource(initializationToken);
            CaptureAttributionAsync(m_AttributionCancellation.Token).Forget();
        }

        /// <summary>
        /// 等待归因插件的当前缓存或下一次回调，并保存最新结果。
        /// </summary>
        private async UniTask CaptureAttributionAsync(CancellationToken ct)
        {
            try
            {
                AttributionData attribution = await m_AttributionPlugin.GetAttributionAsync(ct);
                if (!ct.IsCancellationRequested)
                {
                    m_Attribution = attribution;
                }
            }
            catch (OperationCanceledException)
            {
                // 插件释放时的正常取消。
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.SDK, "DataMaster 获取归因上下文失败，本次保留空归因字段：{0}", e.Message);
            }
        }

        /// <summary>
        /// 接收归因插件最新结果，后续事件会使用该快照。
        /// </summary>
        private void OnAttributionResolved(AttributionData attribution)
        {
            m_Attribution = attribution;
        }

#if NOVA_STARLUS_DATAMASTER
        /// <summary>
        /// 采集当前框架状态，为单次事件创建全新的厂商用户上下文。
        /// </summary>
        private DMUserContext BuildUserContext(Dictionary<string, object> extraContext)
        {
            m_UserProperties.TryGetValue("country_code", out object countryValue);
            ChannelType channel = Nova.Config != null ? Nova.Config.Channel : ChannelType.None;
            Language language = Nova.Localization != null
                ? Nova.Localization.Language
                : Language.Unspecified;

            return DataMasterUserContextBuilder.Build(
                new DataMasterUserContextSnapshot
                {
                    PlayerId = m_CurrentUserId,
                    DeviceId = ResolveDeviceId(),
                    MediaSource = m_Attribution?.MediaSource,
                    CampaignName = m_Attribution?.Campaign,
                    InstallChannel = channel == ChannelType.None ? null : channel.ToString(),
                    InstallTimeMs = GetInstallTimeMs(),
                    CountryCode = countryValue?.ToString(),
                    LanguageCode = language == Language.Unspecified
                        ? null
                        : LanguageMetadata.GetFlag(language),
                    AppVersion = GetAppVersionCode(),
                    OsVersion = ResolveOsName(),
                },
                extraContext);
        }

        /// <summary>
        /// 把 Unity 运行平台映射为 DataMaster 当前约定的平台名称。
        /// </summary>
        private static string ResolveOsName()
        {
            return Application.platform switch
            {
                RuntimePlatform.Android => "Android",
                RuntimePlatform.IPhonePlayer => "iOS",
                _ => null,
            };
        }
#endif

        /// <summary>
        /// 解析当前设备 ID。取值口径与 NetBuilder.BuildHeader / Save.ResolveDeviceId 一致：
        /// 优先通过 Nova.SDK 注册的 IDeviceIdProvider 取值；未注册或返回空串时回退 SystemInfo.deviceUniqueIdentifier。
        /// </summary>
        /// <returns>当前设备 ID 字符串。</returns>
        private string ResolveDeviceId()
        {
            if (Nova.SDK != null &&
                Nova.SDK.TryGet<IDeviceIdProvider>(out IDeviceIdProvider deviceIdProvider))
            {
                string id = deviceIdProvider.GetDeviceID();
                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }
            return SystemInfo.deviceUniqueIdentifier;
        }
    }
}
