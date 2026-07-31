/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 私有辅助方法
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using ThinkingData.Analytics;
using UnityEngine;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 创建 TGADynamicSuperPropertyListener 宿主 GameObject 并挂载到指定父节点。
        /// 绑定当前 Plugin 作为回调目标，返回 listener 实例供 TDAnalytics.SetDynamicSuperProperties 使用。
        /// parent 为 null 时 listener 置于场景根节点。
        /// </summary>
        /// <param name="parent">父节点 Transform，通常为 SDKComponent.transform；可为 null。</param>
        /// <returns>已绑定当前 Plugin 的 TGADynamicSuperPropertyListener 实例。</returns>
        private TGADynamicSuperPropertyListener CreateDynamicSuperPropertyListener(Transform parent)
        {
            var listenerGo = new GameObject("TGADynamicSuperPropertyListener");
            if (parent != null)
            {
                listenerGo.transform.SetParent(parent, false);
            }
            var listener = listenerGo.AddComponent<TGADynamicSuperPropertyListener>();
            listener.Bind(this);
            return listener;
        }

        /// <summary>
        /// 发布 TGA 设备 ID / 访客 ID 至 SDKPluginBase 数据槽位。
        /// 供依赖 TGA 身份标识的其他 Plugin（如 AppsFlyerPlugin.setAdditionalData）通过 FetchDataAsync 消费。
        /// 必须在 TDAnalytics.Init 之后调用，否则 GetDeviceId / GetDistinctId 返回空。
        /// </summary>
        private void PublishTGAIdentifiers()
        {
            PublishData(SDKDataKeys.TGADevicesId, TDAnalytics.GetDeviceId());
            PublishData(SDKDataKeys.TGADistinctId, TDAnalytics.GetDistinctId());
        }

        /// <summary>
        /// 按配置将 TGA DeviceId 设置为 DistinctId。
        /// </summary>
        /// <param name="enabled">是否启用 DeviceId 同步 DistinctId。</param>
        private void ApplyDeviceIdAsDistinctId(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            string deviceId = TDAnalytics.GetDeviceId();
            if (string.IsNullOrEmpty(deviceId))
            {
                Log.Warning(LogTag.TGA, "TGA DeviceId 为空，跳过 DistinctId 设置。");
                return;
            }

            TDAnalytics.SetDistinctId(deviceId);
        }

        /// <summary>
        /// 获取首次安装版本号；本地尚无记录时使用当前 Application.version 写入 FileFragment 并立即落盘。
        /// </summary>
        /// <returns>首次安装版本号；持久化管理器不可用时返回当前 Application.version。</returns>
        private string GetOrCreateFirstVersion()
        {
            string currentVersion = Application.version;
            IFileFragmentManager persistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
            if (persistManager == null)
            {
                return currentVersion;
            }

            if (persistManager.HasItem(c_TGAPersistClassifyName, c_FirstVersionPersistItemName))
            {
                string savedVersion = persistManager.GetString(c_TGAPersistClassifyName, c_FirstVersionPersistItemName, currentVersion);
                return string.IsNullOrEmpty(savedVersion) ? currentVersion : savedVersion;
            }

            persistManager.SetString(c_TGAPersistClassifyName, c_FirstVersionPersistItemName, currentVersion);
            persistManager.Save(c_TGAPersistClassifyName);
            return currentVersion;
        }

        /// <summary>
        /// 异步等待 AppsFlyerId 发布并写入 TGA UserSetOnce 属性。
        /// 必须以 Fire-and-Forget 方式调用（.Forget()），不可在 OnInitializeAsync 中 await：
        /// TGA 桶按 Priority 先于 AppsFlyer 桶执行，直接 await 会导致 TGA 桶永远等不到 AppsFlyer 桶发布数据而死锁。
        /// IAttributionPlugin 不可用或 ct 取消时静默跳过；异常仅记日志，不向上抛。
        /// </summary>
        /// <param name="ct">取消令牌，串联到 FetchDataAsync 调用，Plugin 释放时随之取消。</param>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private void RegisterFetchDataAsync(CancellationToken ct)
        {
            RegisterAppsFlyerIdAsync(ct).Forget();
            RegisterThirdPartyLoginAsync(ct).Forget();
        }

        /// <summary>
        /// 等待 SDK 统一初始化完成后再查询其他插件，避免 TGA 因 Priority 更早而拿不到后续插件。
        /// </summary>
        /// <param name="ct">取消令牌，串联到 InitializeTask 等待。</param>
        /// <returns>SDKComponent；组件不存在或等待取消时由调用方处理。</returns>
        private async UniTask<SDKComponent> WaitForSDKInitializedAsync(CancellationToken ct)
        {
            var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                return null;
            }

            await UniTask.Yield(cancellationToken: ct);
            await sdkComponent.InitializeTask.AttachExternalCancellation(ct);
            return sdkComponent;
        }

        /// <summary>
        /// 异步等待 AppsFlyerId 发布并写入 TGA UserSetOnce 属性。
        /// </summary>
        /// <param name="ct">取消令牌，串联到 FetchDataAsync 调用。</param>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid RegisterAppsFlyerIdAsync(CancellationToken ct)
        {
            try
            {
                SDKComponent sdkComponent = await WaitForSDKInitializedAsync(ct);
                if (sdkComponent == null || !sdkComponent.TryGet<IAttributionPlugin>(out var attribution))
                {
                    return;
                }

                var afId = (string)await attribution.FetchDataAsync(SDKDataKeys.AppsFlyerId, ct);
                if (!string.IsNullOrEmpty(afId))
                {
                    SetFrameworkUserSetOnceProperty("nova_appsflyer_id", afId);
                    UserSetOnce(new Dictionary<string, object>());
                    Log.Debug(LogTag.TGA, $"TGA nova_appsflyer_id 已设置：{afId}");
                }
            }
            catch (OperationCanceledException)
            {
                // 取消属正常退出路径，不记日志。
            }
            catch (Exception e)
            {
                Log.Error(LogTag.TGA, $"RegisterAppsFlyerIdAsync 异常：{e}");
            }
        }

        /// <summary>
        /// 异步等待第三方登录数据发布并写入 TGA UserSet 属性。
        /// 第三方登录依赖用户操作，可能永远不发布，因此必须独立 Fire-and-Forget。
        /// </summary>
        /// <param name="ct">取消令牌，串联到 FetchDataAsync 调用。</param>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid RegisterThirdPartyLoginAsync(CancellationToken ct)
        {
            try
            {
                SDKComponent sdkComponent = await WaitForSDKInitializedAsync(ct);
                if (sdkComponent == null)
                {
                    return;
                }

                IReadOnlyList<IAuthPlugin> authPlugins = sdkComponent.GetAll<IAuthPlugin>();
                for (int i = 0; i < authPlugins.Count; i++)
                {
                    RegisterThirdPartyLoginFromAuthPluginAsync(authPlugins[i], ct).Forget();
                }
            }
            catch (OperationCanceledException)
            {
                // 取消属正常退出路径，不记日志。
            }
            catch (Exception e)
            {
                Log.Error(LogTag.TGA, $"RegisterThirdPartyLoginAsync 异常：{e}");
            }
        }

        /// <summary>
        /// 从账号插件的数据槽消费第三方登录 ID 与渠道名，并写入 TGA UserSet 属性。
        /// 不按具体 Facebook / Google / Apple 包类型或插件名查询，保持 SDK 子包之间零依赖。
        /// </summary>
        /// <param name="authPlugin">账号插件实例。</param>
        /// <param name="ct">取消令牌，串联到 FetchDataAsync 调用。</param>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid RegisterThirdPartyLoginFromAuthPluginAsync(IAuthPlugin authPlugin, CancellationToken ct)
        {
            if (authPlugin == null)
            {
                return;
            }

            try
            {
                var openId = (string)await authPlugin.FetchDataAsync(SDKDataKeys.OpenId, ct);
                var thirdPlatform = (string)await authPlugin.FetchDataAsync(SDKDataKeys.ThirdPlatform, ct);
                if (string.IsNullOrEmpty(openId) || string.IsNullOrEmpty(thirdPlatform))
                {
                    return;
                }

                SetFrameworkUserProperty("nova_openid", openId);
                SetFrameworkUserProperty("nova_third_platform", thirdPlatform);
                UserSet(new Dictionary<string, object>());
                Log.Debug(LogTag.TGA, $"TGA 第三方登录属性已设置：platform={thirdPlatform}, openId={openId}");
            }
            catch (OperationCanceledException)
            {
                // 取消属正常退出路径，不记日志。
            }
            catch (Exception e)
            {
                Log.Error(LogTag.TGA, $"RegisterThirdPartyLoginAsync 异常：{e}");
            }
        }

        /// <summary>
        /// 填充框架层属性默认值并将静态公共属性一次性注入 SDK。
        /// </summary>
        /// <param name="isTestUser">是否为测试用户。</param>
        private void InitFrameworkProperties(bool isTestUser)
        {
            // 设置静态公共事件属性
            SetFrameworkSuperProperty("nova_first_version",GetOrCreateFirstVersion());

            // 设置用户 UserSet 属性
            SetFrameworkUserProperty("nova_test",isTestUser);

            // 更新设置 UserSet 属性到云端
            UserSet(new Dictionary<string, object>());
            

            // 设置用户 UserSetOnce 属性
        
      
            // 更新设置 UserSetOnce 属性到云端
            //UserSetOnce(new Dictionary<string, object>());


            if (m_FrameworkSuperProperties.Count > 0)
            {
                TDAnalytics.SetSuperProperties(new Dictionary<string, object>(m_FrameworkSuperProperties));
            }
        }

        /// <summary>
        /// 构建动态公共属性字典的当前快照，避免外部持有引用后修改内部状态。
        /// 若动态属性缓存为空则返回空字典。
        /// </summary>
        /// <returns>动态公共属性快照字典。</returns>
        private Dictionary<string, object> BuildDynamicSuperPropertiesSnapshot()
        {
            lock (m_DynamicSuperProperties)
            {
                if (m_DynamicSuperProperties.Count == 0)
                {
                    return new Dictionary<string, object>();
                }

                return new Dictionary<string, object>(m_DynamicSuperProperties);
            }
        }

        /// <summary>
        /// 判断给定值是否为支持的数值类型（int、long、float、double）。
        /// </summary>
        /// <param name="value">待判断的值。</param>
        /// <returns>值为数值类型时返回 true，否则返回 false。</returns>
        private bool IsNumericValue(object value)
        {
            return value is int || value is long || value is float || value is double;
        }

        /// <summary>
        /// SDKEventData.UserLogin 事件处理器；调用 TGA 的 SetUserId（内部 → Login → TDAnalytics.Login），
        /// 同时把 login.UserId 通过 PublishData 发布到 SDKDataKeys.TGAAccountId 槽位，
        /// 然后以 Fire-and-Forget 方式触发 ReportOnLoginAsync 走异步上报流程。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login))
            {
                return;
            }
            SetUserId(login.UserId);
            PublishData(SDKDataKeys.TGAAccountId, login.UserId);
            ReportOnLoginAsync().Forget();
        }

        /// <summary>
        /// 登录后异步上报 TGA 标识至服务端：先 await FetchDataAsync 等待 TGADistinctId / TGAAccountId 数据槽位就绪，
        /// 直接拿 fetch 返回值作为协议参数，与 AppsFlyerPlugin.ReportOnLoginAsync 同构。
        /// 把"初始化结果"与"登录结果"统一为"先 await 拿值、再用值"的可等待过程。
        /// 数据槽位由本插件自身发布（TGADistinctId 在 PublishTGAIdentifiers，TGAAccountId 在 OnUserLogin）；
        /// m_ReportNetService 或 m_RuntimeConfig 为 null（守卫早返回路径）时静默跳过；
        /// CancellationToken 暂用 default；OperationCanceledException 静默吞，其他异常仅记日志不上抛。
        /// </summary>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid ReportOnLoginAsync()
        {
            if (m_ReportNetService == null || m_RuntimeConfig == null)
            {
                return;
            }
            try
            {
                object distinctIdObj = await FetchDataAsync(SDKDataKeys.TGADistinctId, default);
                object accountIdObj = await FetchDataAsync(SDKDataKeys.TGAAccountId, default);
                string distinctId = distinctIdObj as string ?? string.Empty;
                string accountId = accountIdObj as string ?? string.Empty;
                m_ReportNetService.Async(m_RuntimeConfig.ReportCmdName, distinctId, accountId).Forget();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.TGA, $"ReportOnLoginAsync 上报异常：{ex}");
            }
        }

        /// <summary>
        /// 合并框架属性与业务属性，业务属性优先覆盖同名框架属性。
        /// framework 为空或无元素时直接返回 business；business 为空或无元素时直接返回 framework。
        /// </summary>
        /// <param name="framework">框架层属性字典，优先级低。</param>
        /// <param name="business">业务层属性字典，优先级高。</param>
        /// <returns>合并后的属性字典，同名 key 以业务属性值为准。</returns>
        private Dictionary<string, object> MergeProperties(Dictionary<string, object> framework, Dictionary<string, object> business)
        {
            if (framework == null || framework.Count == 0)
            {
                return business ?? new Dictionary<string, object>();
            }

            if (business == null || business.Count == 0)
            {
                return new Dictionary<string, object>(framework);
            }

            var merged = new Dictionary<string, object>(framework);
            foreach (var kv in business)
            {
                merged[kv.Key] = kv.Value;
            }

            return merged;
        }
    }
}
#endif
