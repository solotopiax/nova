/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoDataMasterView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/07/03
 * descrip:   DemoDataMasterView 演示 View — 私有方法（各接口按钮回调）。
 *            使用真实测试物料：参数 show_start_button（Boolean 型）；主题名（topic_name）运行时由 SDK 枚举获取，不硬编码。
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Datamaster.Samples.Runtime
{
    /// <summary>
    /// DemoDataMasterView 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoDataMasterView
    {
        /// <summary>
        /// 测试物料：参数名（是否显示开始按钮，Boolean 型，后台按实验分组下发 true / false）。
        /// 业务读参只需参数名，所属主题名（topic_name）由 SDK 落库后运行时枚举获取，业务不预知、不硬编码。
        /// </summary>
        private const string DemoParamName = "show_start_button";

        /// <summary>
        /// 缓存的 DataMasterPlugin 引用，用于订阅 / 退订拉取事件与调试 dump。
        /// </summary>
        private DataMasterPlugin m_DataMaster;

        /// <summary>
        /// 打印当前设备 ID 到反馈区与 Unity Console。
        /// 走 DataMasterPlugin.GetDeviceId()，与拉取 / 上报口径一致；插件未启用时提示。
        /// </summary>
        private void LogDeviceId()
        {
            string deviceId = m_DataMaster != null ? m_DataMaster.GetDeviceId() : "(DataMaster 未启用，无法取设备 ID)";
            Log.Debug(LogTag.SDK, Txt.Format("DataMasterDemo — 当前设备 ID：{0}", deviceId));
            AppendFeedback($"当前设备 ID：{deviceId}");
        }

        /// <summary>
        /// 订阅 DataMaster 服务端拉取成功 / 失败事件（先退后订防重复）。
        /// 拉取由「登录并拉取」按钮触发，成功后在反馈区显示服务端下发内容，失败显示错误信息。
        /// 未启用插件时静默跳过（不打扰，点击具体按钮时再引导启用）。
        /// </summary>
        private void SubscribeRefreshEvents()
        {
            if (!Nova.SDK.TryGet(out m_DataMaster) || m_DataMaster == null)
            {
                return;
            }
            m_DataMaster.OnConfigRefreshed -= OnConfigRefreshed;
            m_DataMaster.OnConfigRefreshed += OnConfigRefreshed;
            m_DataMaster.OnConfigRefreshFailed -= OnConfigRefreshFailed;
            m_DataMaster.OnConfigRefreshFailed += OnConfigRefreshFailed;
            m_DataMaster.OnRefreshTriggered -= OnRefreshTriggered;
            m_DataMaster.OnRefreshTriggered += OnRefreshTriggered;
        }

        /// <summary>
        /// 退订 DataMaster 拉取事件。
        /// </summary>
        private void UnsubscribeRefreshEvents()
        {
            if (m_DataMaster == null)
            {
                return;
            }
            m_DataMaster.OnConfigRefreshed -= OnConfigRefreshed;
            m_DataMaster.OnConfigRefreshFailed -= OnConfigRefreshFailed;
            m_DataMaster.OnRefreshTriggered -= OnRefreshTriggered;
        }

        /// <summary>
        /// 服务端配置拉取成功回调：dump 该主题的下发内容到反馈区。
        /// 说明：Editor 下厂商 EffectiveValue 只读默认值，故此处走 DebugDumpTopic 明文展示服务端实际下发的 new 值。
        /// </summary>
        private void OnConfigRefreshed()
        {
            // 全量 dump：查看服务端实际下发了哪些主题 key（topic_name）及参数结构，便于确认读参该传的主题标识与参数类型。
            string dump = m_DataMaster != null ? m_DataMaster.DebugDumpAll() : string.Empty;
            AppendFeedback($"服务端配置拉取成功。本地已落库（全部主题）：\n{dump}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 服务端配置拉取失败回调：显示错误信息到反馈区。
        /// </summary>
        /// <param name="error">拉取错误信息。</param>
        private void OnConfigRefreshFailed(string error)
        {
            AppendFeedback($"服务端配置拉取失败：{error}", FeedbackLevel.Error);
        }

        /// <summary>
        /// 拉取发起回调：把本次拉取传参摘要显示到反馈区（与 Unity Console 同步）。
        /// </summary>
        /// <param name="line">拉取传参摘要（userId / deviceId / userProperties）。</param>
        private void OnRefreshTriggered(string line)
        {
            AppendFeedback(line, FeedbackLevel.Info);
        }

        /// <summary>
        /// 绑定按钮点击回调并设置就近 API 提示。
        /// </summary>
        /// <param name="button">目标按钮，可为 null（跳过）。</param>
        /// <param name="onClick">点击回调。</param>
        /// <param name="apiHint">按钮下 ApiHintText 显示的接口签名提示。</param>
        private void BindButton(Button button, UnityAction onClick, string apiHint)
        {
            if (button == null)
            {
                return;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            SetButtonApiHint(button, apiHint);
        }

        /// <summary>
        /// 获取 DataMasterPlugin；未获取到（未在 ConfigMaster 启用）时向反馈区打印引导并返回 false。
        /// </summary>
        /// <param name="dataMaster">输出的插件实例。</param>
        /// <returns>可用返回 true，否则 false。</returns>
        private bool TryGetDataMaster(out DataMasterPlugin dataMaster)
        {
            if (Nova.SDK.TryGet<DataMasterPlugin>(out dataMaster))
            {
                return true;
            }
            AppendFeedback("未获取到 DataMasterPlugin：请在 ConfigMaster 启用并配置该插件（AppId / AesKey / 默认配置）。", FeedbackLevel.Error);
            return false;
        }

        /// <summary>
        /// 运行时动态解析当前已落库主题中含 <see cref="DemoParamName"/> 参数的 topic_name。
        /// 还原真实接入场景：业务只知道自己关心的参数名，所属主题名（topic_name）由服务端生成、
        /// 业务不预知，故从 SDK 枚举已落库主题并按参数名匹配得到。未拉取 / 未命中时提示并返回 false。
        /// </summary>
        /// <param name="dataMaster">已取到的 DataMasterPlugin 实例。</param>
        /// <param name="topicName">输出的 topic_name；未找到时为 null。</param>
        /// <returns>是否解析到。</returns>
        private bool TryResolveTopicName(DataMasterPlugin dataMaster, out string topicName)
        {
            topicName = null;
            var topicNames = dataMaster.GetTopicNames();
            if (topicNames.Count == 0)
            {
                AppendFeedback("本地无已落库主题：请先点「模拟登录并拉取」从服务端获取配置。", FeedbackLevel.Warn);
                return false;
            }
            foreach (var name in topicNames)
            {
                if (!string.IsNullOrEmpty(dataMaster.GetParamValueJson(name, DemoParamName)))
                {
                    topicName = name;
                    return true;
                }
            }
            AppendFeedback($"已落库主题中未找到参数 {DemoParamName}。", FeedbackLevel.Warn);
            return false;
        }

        /// <summary>
        /// 读取实验参数（GetParamValue&lt;bool&gt;，Boolean 型，兜底 false）。
        /// 返回服务端分给当前玩家的开关值（是否显示开始按钮），未拉取 / 未命中时返回兜底 false。
        /// </summary>
        private void OnReadParamClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            if (!TryResolveTopicName(dataMaster, out string topicName))
            {
                return;
            }
            bool showStartButton = dataMaster.GetParamValue(topicName, DemoParamName, false);
            AppendFeedback($"读参 [{topicName}/{DemoParamName}] → {showStartButton}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 读取实验参数（通过 JSON，GetParamValueJson）：返回参数生效值的原始 JSON 字符串（未反序列化）。
        /// </summary>
        private void OnReadJsonClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            if (!TryResolveTopicName(dataMaster, out string topicName))
            {
                return;
            }
            string json = dataMaster.GetParamValueJson(topicName, DemoParamName);
            AppendFeedback(string.IsNullOrEmpty(json)
                ? $"实验参数（JSON）[{topicName}/{DemoParamName}] = (无值)"
                : $"实验参数（JSON）[{topicName}/{DemoParamName}] = {json}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 标记曝光（MarkExposure）。玩家真正看到该实验按钮时调用，服务端据此计入实验分母。
        /// </summary>
        private void OnExposureClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            if (!TryResolveTopicName(dataMaster, out string topicName))
            {
                return;
            }
            dataMaster.MarkExposure(topicName);
            AppendFeedback($"已标记曝光：{topicName}（仅命中实验且首次曝光时生效）", FeedbackLevel.Info);
        }

        /// <summary>
        /// 上报实验事件，并演示只随本次事件发送的 ExtraContext。
        /// UID、设备、归因、渠道、安装时间、国家、语言、版本和平台由框架在调用时实时采集。
        /// </summary>
        private void OnLogEventClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            var extraContext = new Dictionary<string, object>
            {
                ["source"] = "datamaster_demo",
                ["button"] = "play",
            };
            dataMaster.LogExperimentEvent("play_btn_click", 1, extraContext);
            AppendFeedback(
                "已上报实验事件：play_btn_click (value=1, extraContext: source=datamaster_demo, button=play)",
                FeedbackLevel.Info);
        }

        /// <summary>
        /// 清理 SDK 运行时缓存（ClearRuntimeCache）：清掉服务端下发的 new_value、experiment 状态与事件序号，
        /// 回到仅默认配置初始态。配合「模拟登录并拉取」换新 uid 即可模拟新设备首次分桶。
        /// </summary>
        private void OnClearCacheClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            dataMaster.ClearRuntimeCache();
            AppendFeedback("已清理 SDK 运行缓存（new_value / experiment / 事件序号）。点「模拟登录并拉取」以新 uid 重新分桶。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 设置分流用户属性（SetUserProperty），供下次拉取分流匹配。
        /// app_version / install_time 会在每次刷新请求发出前由框架自动更新；
        /// country_code 作为示例分流条件由项目按需设置。
        /// </summary>
        private void OnSetPropertyClick()
        {
            if (!TryGetDataMaster(out DataMasterPlugin dataMaster))
            {
                return;
            }
            dataMaster.SetUserProperty("country_code", "US");
            AppendFeedback(
                "已设置分流属性：country_code=US；app_version / install_time 将在下次拉取前由框架自动更新。",
                FeedbackLevel.Info);
        }

        /// <summary>
        /// 模拟登录并触发服务端拉取：走真实 kit-login 拿到 uid，再 Nova.SDK.Login(uid) 驱动 DataMaster 携带该 uid 拉取。
        /// </summary>
        private void OnLoginRefreshClick()
        {
            LoginThenRefreshAsync().Forget();
        }

        /// <summary>
        /// 真实登录流程：Nova.Network.Kit&lt;Login&gt;().Async 游客登录。
        /// 登录 kit 成功后会在内部自行 Nova.SDK.Login(uid) 广播 UserLogin，DataMaster 订阅该事件即自动携带 uid 拉取；
        /// 业务侧无需（也不应）再手动 Nova.SDK.Login，重复调用会导致重复拉取。
        /// </summary>
        /// <returns>Fire-and-Forget 异步任务。</returns>
        private async UniTaskVoid LoginThenRefreshAsync()
        {
            try
            {
                // demo 走游客登录，不传 openId（openId 仅用于三方账号找已绑 uid，游客口径可留空）。
                // forceNewAccount=true：每次点击都强制注册一个全新用户（拿到新 uid），便于反复演示「新玩家首次分桶」。
                NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, string.Empty, true);
                if (resp.IsSuccess)
                {
                    string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                    if (!string.IsNullOrEmpty(uid))
                    {
                        // 登录 kit 成功后已在内部 Nova.SDK.Login(uid) 广播 UserLogin，DataMaster 订阅该事件即自动拉取；
                        // 此处不可再手动 Nova.SDK.Login，否则重复广播会触发两次 RefreshFromServer。
                        AppendFeedback($"登录成功 uid={uid}，登录 kit 已自动通知 SDK，DataMaster 据此拉取。", FeedbackLevel.Success);
                    }
                    else
                    {
                        AppendFeedback("登录成功但 uid 为空，未触发拉取。", FeedbackLevel.Warn);
                    }
                }
                else
                {
                    AppendFeedback($"登录失败：ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback($"登录异常：{ex.Message}", FeedbackLevel.Error);
            }
        }
    }
}
