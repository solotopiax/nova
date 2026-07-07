/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterPlugin.cs
 * author:    taoye
 * created:   2026/7/3
 * descrip:   DataMaster SDK 插件主文件（public/override 方法）。
 *            继承 SDKPluginBase，不实现业务能力接口；ABTest 能力（二级寻址
 *            读参、曝光打点、实验事件上报）经具体类型公开方法暴露，业务通过
 *            SDKManager.Get<DataMasterPlugin>() 调用。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using StarlusSDK.DataMaster;
using UnityEngine;

namespace NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime
{
    /// <summary>
    /// DataMaster SDK 插件，继承 SDKPluginBase。
    /// 负责桥接厂商 DataMaster 单例：初始化本地库、订阅登录事件拉取服务端配置，
    /// 并对业务暴露 ABTest 读参 / 曝光打点 / 实验事件上报能力。
    /// 厂商 DataMaster 为 MonoBehaviour 单例，首次访问 Instance 时自建常驻 GameObject。
    /// </summary>
    public sealed partial class DataMasterPlugin : SDKPluginBase
    {
        /// <summary>
        /// 缓存的运行期配置，OnInitializeAsync 时由注入的 ISDKPluginConfig 强转得到。
        /// </summary>
        private DataMasterPluginConfig m_Config;

        /// <summary>
        /// 事件管理器引用，用于订阅 / 退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 厂商 DataMaster 是否已完成 Initialize；未完成时不触发服务端拉取。
        /// </summary>
        private bool m_Initialized;

        /// <summary>
        /// 用户属性字典，供服务端分流与规则匹配；业务经 SetUserProperty 填充，登录拉取时透传。
        /// </summary>
        private readonly Dictionary<string, object> m_UserProperties = new Dictionary<string, object>();

        /// <summary>
        /// 最近一次登录的用户 ID，供简化版事件上报构造用户上下文。
        /// </summary>
        private string m_CurrentUserId;

        /// <summary>
        /// 已落库主题名（topic_name）缓存，OnInitializeAsync 时从默认配置 Params.Keys 取得。
        /// 业务读参 / 曝光 / 读原始 JSON 所需的 topicId 实际即此 topic_name（非 experiment.topicId），
        /// 由服务端生成、业务无法预知；但默认配置随包发布、其 Params.Keys 即业务可读主题全集
        /// （README：默认配置 schema 是本地参数名来源），故初始化时一次性缓存即可。
        /// </summary>
        private IReadOnlyList<string> m_TopicNames;

        /// <summary>
        /// 插件友好名，用于诊断日志与 Inspector 显示。
        /// </summary>
        public override string Name => "DataMaster";

        /// <summary>
        /// 声明本插件所需配置类型，SDKManager 按此从 IConfigManager 拉取 DataMasterPluginConfig 注入。
        /// </summary>
        protected override Type ConfigType => typeof(DataMasterPluginConfig);

        /// <summary>
        /// 服务端配置拉取成功事件，在 RefreshFromServer 落库成功后主线程触发。
        /// 业务可监听此事件清缓存并重新读取关心的参数以响应远程变更。
        /// </summary>
        public event Action OnConfigRefreshed;

        /// <summary>
        /// 服务端配置拉取失败事件，参数为错误信息，在 RefreshFromServer 请求失败后主线程触发。
        /// 业务可监听此事件感知拉取失败并提示或重试。
        /// </summary>
        public event Action<string> OnConfigRefreshFailed;

        /// <summary>
        /// 服务端配置拉取发起事件，参数为本次拉取传参摘要（userId / deviceId / userProperties）。
        /// 在 RefreshFromServer 调用前触发，业务可据此在 UI 同步显示本次拉取上下文。
        /// </summary>
        public event Action<string> OnRefreshTriggered;

        /// <summary>
        /// 异步初始化：强转并校验配置，解析默认配置文本，初始化厂商 DataMaster 本地库，订阅登录事件。
        /// 配置缺失（AppId / AesKey 为空）时记 Warning 并跳过初始化，插件降级为不可用状态（后续读参返回兜底值）。
        /// </summary>
        /// <param name="config">SDKManager 注入的 DataMasterPluginConfig。</param>
        /// <param name="ct">取消令牌，本插件初始化为同步逻辑，暂不使用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            m_Config = config as DataMasterPluginConfig;
            if (m_Config == null || string.IsNullOrEmpty(m_Config.AppId) || string.IsNullOrEmpty(m_Config.AesKey))
            {
                Log.Warning(LogTag.SDK, "DataMaster 配置缺失（AppId / AesKey 为空），初始化跳过。");
                return UniTask.CompletedTask;
            }

            string defaultJson = m_Config.DefaultConfig != null ? m_Config.DefaultConfig.text : string.Empty;
            DMGetParamsResponse defaultConfig = DataMaster.Instance.ParseConfigJson(defaultJson);
            // 缓存默认配置的 Params.Keys 作为业务可读主题名全集（topic_name，即读参所需的 topicId）。
            var topicNames = new List<string>();
            if (defaultConfig?.Params != null)
            {
                topicNames.AddRange(defaultConfig.Params.Keys);
            }
            m_TopicNames = topicNames;
            DataMaster.Instance.Initialize(m_Config.AppId, defaultConfig, m_Config.AesKey);
            m_Initialized = true;

            SubscribeEvents();
            Log.Debug(LogTag.SDK, "DataMaster 初始化完成。");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步释放：退订登录事件；厂商 DataMaster 单例常驻，无显式 shutdown 接口。
        /// </summary>
        /// <param name="ct">取消令牌，本插件不使用。</param>
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
        /// 设置一条用户属性，用于服务端分流与规则匹配（如等级、国家、是否 VIP）。
        /// 同一 key 多次赋值以最后一次为准；在登录触发拉取前设置方能参与本次分流。
        /// </summary>
        /// <param name="key">属性名，不可为空。</param>
        /// <param name="value">属性值，支持基础类型 / 对象。</param>
        public void SetUserProperty(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            m_UserProperties[key] = value;
        }

        /// <summary>
        /// 一次性设齐服务端分流所需的两条必传属性：app_version 与 install_time。
        /// 业务在触发拉取（登录）前调用一次即可，无需自行合成版本号 / 记录安装时间。
        /// app_version 取 <see cref="GetAppVersionCode"/>（整数版本号，number 类型），
        /// install_time 取 <see cref="GetInstallTimeMs"/>（首次启动毫秒时间戳）。
        /// 其余分流属性（如 country_code）仍由业务按需 <see cref="SetUserProperty"/>。
        /// </summary>
        public void ApplyRequiredUserProperties()
        {
            m_UserProperties["app_version"] = GetAppVersionCode();
            m_UserProperties["install_time"] = GetInstallTimeMs();
        }

        /// <summary>
        /// 取整数版本号（app_version，number 类型），全平台通用。
        /// 把 <see cref="Application.version"/> 的 x.y.z 三段合成为一个 int：
        /// major*1_000_000 + minor*1_000 + patch，如 "1.0.0" → 1000000、"1.10.3" → 1010003；
        /// 非数字段按 0 计，结果 ≤ 0 时兜底 1（每段 &lt; 1000 时唯一且可比较，major &lt; 2000 时 int 不溢出）。
        /// </summary>
        /// <returns>整数版本号。</returns>
        public int GetAppVersionCode()
        {
            string[] parts = Application.version.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out var c) ? c : 0;
            int code = major * 1000000 + minor * 1000 + patch;
            return code > 0 ? code : 1;
        }

        /// <summary>
        /// 取本设备首次启动的毫秒时间戳作为 install_time 的近似值（number / long，13 位）。
        /// 首次调用以当前 UTC 时间记入 PlayerPrefs 持久化，之后读取存值，保证单设备取值稳定。
        /// 说明：Unity 无「真实首次安装时间」的跨平台 API，故以「本地首次启动」近似；
        /// 需精确安装时间的业务可自行覆盖（登录前 SetUserProperty("install_time", 真实值)）。
        /// </summary>
        /// <returns>首次启动毫秒时间戳。</returns>
        public long GetInstallTimeMs()
        {
            const string key = "Nova_DataMaster_InstallTimeMs";
            // PlayerPrefs 无 long 重载，毫秒时间戳超 int 范围，以字符串存取。
            string saved = PlayerPrefs.GetString(key, string.Empty);
            long t = 0;
            if (!string.IsNullOrEmpty(saved) && long.TryParse(saved, out long parsed))
            {
                t = parsed;
            }
            if (t <= 0)
            {
                t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PlayerPrefs.SetString(key, t.ToString());
                PlayerPrefs.Save();
            }
            return t;
        }

        /// <summary>
        /// 获取指定 Topic 下指定参数的生效值并反序列化为目标类型。
        /// 优先返回服务端下发值，无则返回本地默认值；读取失败或反序列化失败返回 fallback。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="topicId">主题 ID。</param>
        /// <param name="paramName">参数名。</param>
        /// <param name="fallback">兜底值，默认为 default(T)。</param>
        /// <returns>参数生效值，或 fallback。</returns>
        public T GetParamValue<T>(string topicId, string paramName, T fallback = default)
        {
            return DataMaster.Instance.GetParamValue(topicId, paramName, fallback);
        }

        /// <summary>
        /// 获取指定 Topic 下指定参数的生效值（原始 JSON 字符串）。
        /// 优先返回服务端下发值，无则返回本地默认值；无值时返回 null。
        /// </summary>
        /// <param name="topicId">主题 ID。</param>
        /// <param name="paramName">参数名。</param>
        /// <returns>参数生效值的 JSON 字符串，或 null。</returns>
        public string GetParamValueJson(string topicId, string paramName)
        {
            return DataMaster.Instance.GetParamValueJson(topicId, paramName);
        }

        /// <summary>
        /// 标记玩家首次曝光于指定主题的实验，以当前 UTC 时间记录曝光时刻。
        /// 仅当该主题已落库且命中实验（CaseId 非空）、且曝光时间尚未记录时生效。
        /// 应在玩家真正接触到实验功能的位置调用，且通常一次实验只需调用一次。
        /// </summary>
        /// <param name="topicId">主题 ID。</param>
        public void MarkExposure(string topicId)
        {
            DataMaster.Instance.SetTopicExposureTimeMs(topicId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>
        /// 以显式时间戳标记指定主题实验的曝光时刻（高级用法，一般用 MarkExposure）。
        /// 生效条件同 MarkExposure。
        /// </summary>
        /// <param name="topicId">主题 ID。</param>
        /// <param name="exposureTimeMs">曝光时间（Unix 毫秒）。</param>
        public void SetExposureTimeMs(string topicId, long exposureTimeMs)
        {
            DataMaster.Instance.SetTopicExposureTimeMs(topicId, exposureTimeMs);
        }

        /// <summary>
        /// 上报一条实验指标事件（主数值写入 primaryValue，供服务端聚合计算实验指标）。
        /// </summary>
        /// <param name="eventName">事件名，不可为空。</param>
        /// <param name="value">主数值（如金额、次数）。</param>
        /// <param name="userContext">用户上下文，承载玩家画像与业务扩展字段。</param>
        public void LogExperimentEvent(string eventName, double value, DMUserContext userContext)
        {
            DataMaster.Instance.LogEvent(eventName, value, userContext);
        }

        /// <summary>
        /// 上报一条实验指标事件（简化版，自动以当前登录用户与设备构造用户上下文）。
        /// 适合大多数业务：无需引用厂商上下文类型。需要携带更多画像字段时用带 DMUserContext 的重载。
        /// </summary>
        /// <param name="eventName">事件名，不可为空。</param>
        /// <param name="value">主数值（如金额、次数）。</param>
        public void LogExperimentEvent(string eventName, double value)
        {
            var userContext = new DMUserContext
            {
                PlayerId = m_CurrentUserId,
                DeviceId = ResolveDeviceId(),
            };
            DataMaster.Instance.LogEvent(eventName, value, userContext);
        }

        /// <summary>
        /// 获取当前设备 ID（与登录拉取、事件上报所用口径一致）。
        /// 取值口径：优先 Nova.SDK 注册的 IDeviceIdProvider，未注册或返回空时回退 SystemInfo.deviceUniqueIdentifier。
        /// </summary>
        /// <returns>当前设备 ID。</returns>
        public string GetDeviceId()
        {
            return ResolveDeviceId();
        }

        /// <summary>
        /// 枚举业务可读的全部主题名（topic_name，即服务端 response.Params 字典的 key）。
        /// 用途：业务读参 / 曝光 / 读原始 JSON 所需的 topicId 实际即此 topic_name（非 experiment.topicId），
        /// 但 topic_name 由服务端生成、业务无法预知，故由 SDK 提供枚举接口，还原真实接入场景。
        /// 实现：OnInitializeAsync 时从默认配置 Params.Keys 一次性缓存（默认配置随包发布，
        /// 其 schema 是本地参数名来源，Params.Keys 即业务可读主题全集）。
        /// </summary>
        /// <returns>已落库主题名只读列表；未初始化时返回空列表。</returns>
        public IReadOnlyList<string> GetTopicNames()
        {
            return m_TopicNames ?? Array.Empty<string>();
        }

        /// <summary>
        /// 清理 SDK 运行时缓存，回到「仅默认配置」的初始态，配合换 uid 模拟新设备首次分桶。
        /// 清理范围：服务端下发的 new_value、experiment 状态（caseId / 曝光时间）、事件序号 _dmSeq。
        /// 实现清 PlayerPrefs 事件序号 + 反射调厂商私有 ResetLocalDatabase（drop 参数 / 实验表后用默认配置重建）。
        /// 限制：厂商未提供公开清理接口，反射为权宜；DataMaster SDK 版本升级需复核 ResetLocalDatabase 方法名。
        /// 调用后需重新登录拉取（点「模拟登录并拉取」）以新 uid 重新分桶。
        /// </summary>
        public void ClearRuntimeCache()
        {
            PlayerPrefs.DeleteKey("DM_SEQ_CACHE");
            PlayerPrefs.Save();
            var mi = typeof(DataMaster).GetMethod(
                "ResetLocalDatabase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (mi != null)
            {
                // 提示：厂商 ResetLocalDatabase 内部写死一句 Debug.LogError("Tampering or corruption detected!...")，
                // 那是它为「篡改检测」场景写的日志。此处主动复用它清库重建，那句红色 error 会一并打出，
                // 属正常副作用（数据库确实已用默认配置重建），非真错误；Core 源只读不能改，故无法抑制。
                Log.Debug(LogTag.SDK, "DataMaster 清库重建中——紧随其后的一句红色「Tampering or corruption detected」是厂商方法内写死的日志，属正常清库副作用，可忽略。");
                mi.Invoke(DataMaster.Instance, null);
                Log.Debug(LogTag.SDK, "DataMaster 运行时缓存已清理（参数 / 实验表已用默认配置重建）。");
            }
            else
            {
                Log.Warning(LogTag.SDK, "DataMaster ClearRuntimeCache：未找到 ResetLocalDatabase，仅清事件序号。");
            }
        }

        /// <summary>
        /// 调试用：dump 指定主题下所有参数的 default / new（服务端下发）明文，仅 Editor / Development Build 可用。
        /// 用途：Editor 下厂商 EffectiveValue 只读默认值（屏蔽服务端下发），此方法绕过该屏蔽直接查看服务端实际下发内容，便于验证拉取结果。
        /// </summary>
        /// <param name="topicId">主题 ID。</param>
        /// <returns>该主题参数明文信息；非 Editor/Dev 构建返回不可用提示。</returns>
        public string DebugDumpTopic(string topicId)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return DataMaster.Instance.DebugGetTopicInfo(topicId);
#else
            return "调试 dump 仅在 Editor / Development Build 可用。";
#endif
        }

        /// <summary>
        /// 调试用：dump 本地库中「所有」主题及其参数的 default / new 明文，仅 Editor / Development Build 可用。
        /// 用途：拉取后查看服务端实际下发了哪些主题 key（topic_name）与参数结构，据此确认业务读参该传的主题标识。
        /// </summary>
        /// <returns>全部主题的参数明文信息；非 Editor/Dev 构建返回不可用提示。</returns>
        public string DebugDumpAll()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            return DataMaster.Instance.DebugGetAllTopicsInfo();
#else
            return "调试 dump 仅在 Editor / Development Build 可用。";
#endif
        }
    }
}
