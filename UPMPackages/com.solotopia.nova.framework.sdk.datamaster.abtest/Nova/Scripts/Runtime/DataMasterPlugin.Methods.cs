/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterPlugin.Methods.cs
 * author:    taoye
 * created:   2026/7/3
 * descrip:   DataMasterPlugin 私有方法（登录事件订阅与服务端配置拉取）。
 ***************************************************************/

using System.Linq;
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
        /// 解析当前设备 ID。取值口径与 NetBuilder.BuildHeader / Save.ResolveDeviceId 一致：
        /// 优先通过 Nova.SDK 注册的 IDeviceIdProvider 取值；未注册或返回空串时回退 SystemInfo.deviceUniqueIdentifier。
        /// </summary>
        /// <returns>当前设备 ID 字符串。</returns>
        private string ResolveDeviceId()
        {
            if (Nova.SDK.TryGet<IDeviceIdProvider>(out IDeviceIdProvider deviceIdProvider))
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
