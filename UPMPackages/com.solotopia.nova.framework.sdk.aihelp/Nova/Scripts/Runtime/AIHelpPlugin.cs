/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AIHelpPlugin.cs
 * author:    taoye
 * created:   2026/7/9
 * descrip:   AIHelp SDK 插件主文件（生命周期 / 初始化 + 全部对外接口）。继承
 *            SDKPluginBase，桥接 AIHelp 智能客服 / 帮助中心；业务经
 *            SDKManager.Get<AIHelpPlugin>() 调用。引用 vendor 一律
 *            global::AIHelp.* 全限定以避 CS0234 命名空间遮蔽。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AIHelp.Runtime
{
    /// <summary>
    /// AIHelp SDK 插件，继承 SDKPluginBase。负责桥接 AIHelp 智能客服 / 帮助中心：
    /// 初始化 vendor SDK、自动订阅登录事件同步用户，并对业务暴露拉起客服 / 帮助中心、
    /// 用户信息同步、语言切换、未读数查询、推送 token 设置等能力。
    /// </summary>
    [SDKPluginConfigType(typeof(AIHelpPluginConfig))]
    public sealed partial class AIHelpPlugin : SDKPluginBase
    {
        /// <summary>
        /// 缓存的运行期配置，OnInitializeAsync 时由注入的 ISDKPluginConfig 强转得到。
        /// </summary>
        private AIHelpPluginConfig m_Config;

        /// <summary>
        /// 事件管理器引用，用于订阅 / 退订 SDKEventData.UserLogin。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// vendor SDK 是否已完成 Initialize；未完成时公开方法早退，避免向未初始化的 SDK 发指令。
        /// </summary>
        private bool m_InitOver;

        /// <summary>
        /// 插件友好名，用于诊断日志与 Inspector 显示。
        /// </summary>
        public override string Name => "AIHelp";

        /// <summary>
        /// 声明本插件所需配置类型，SDKManager 按此从 IConfigManager 拉取 AIHelpPluginConfig 注入。
        /// </summary>
        protected override Type ConfigType => typeof(AIHelpPluginConfig);

        /// <summary>
        /// vendor MessageArrival 事件桥接；FetchUnreadMessageCount() 的未读消息数结果经此回传
        /// （参数为原始 JSON）。
        /// </summary>
        public event Action<string> OnMessageArrived;

        /// <summary>
        /// vendor UnreadTaskCount 事件桥接；FetchUnreadTaskCount() 的未读工单数结果经此回传
        /// （参数为原始 JSON）。
        /// </summary>
        public event Action<string> OnUnreadTaskCountChanged;

        /// <summary>
        /// 异步初始化：强转并校验配置，按 ServerCmdName 从 netcmd 表解析域名，开启日志，初始化 vendor SDK，
        /// 注册 vendor 事件监听并订阅登录事件。配置缺失（ServerCmdName / AppId 为空）或域名解析失败时
        /// 记 Warning 并跳过初始化，插件降级为不可用（后续公开方法空操作）。
        /// </summary>
        /// <param name="config">SDKManager 注入的 AIHelpPluginConfig。</param>
        /// <param name="ct">取消令牌，本插件初始化为同步逻辑，暂不使用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            m_Config = config as AIHelpPluginConfig;
            if (m_Config == null || string.IsNullOrEmpty(m_Config.ServerCmdName) || string.IsNullOrEmpty(m_Config.AppId))
            {
                Log.Warning(LogTag.SDK, "AIHelp 配置缺失（ServerCmdName / AppId 为空），初始化跳过。");
                return UniTask.CompletedTask;
            }

            INetworkManager networkManager = FrameworkManagersGroup.GetManager<INetworkManager>();
            INetworkCmdRow cmdRow = networkManager?.ResolveNetCmdRow(m_Config.ServerCmdName);
            string url = cmdRow != null ? networkManager.ResolveNetCmdUrl(cmdRow) : null;
            string domain = ExtractDomain(url);
            if (string.IsNullOrEmpty(domain))
            {
                Log.Warning(LogTag.SDK, $"AIHelp 域名解析失败（netcmd 指令 {m_Config.ServerCmdName} 未找到或 URL 为空），初始化跳过。");
                return UniTask.CompletedTask;
            }

            global::AIHelp.AIHelpSupport.enableLogging(m_Config.EnableLogging);
            global::AIHelp.AIHelpSupport.Initialize(domain, m_Config.AppId, m_Config.InitialLanguage ?? string.Empty);
            m_InitOver = true;

            RegisterAIHelpEventListeners();
            SubscribeEvents();
            Log.Debug(LogTag.SDK, "AIHelp 初始化完成。");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步释放：退订登录事件、注销 vendor 事件监听并关闭页面。
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
            if (m_InitOver)
            {
                UnregisterAIHelpEventListeners();
                global::AIHelp.AIHelpSupport.Close();
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 用户登录：把用户信息同步给 AIHelp。框架会在收到 SDKEventData.UserLogin 时自动以 uid 调用；
        /// 需要携带 name / serverId / tags / customData 时由业务显式调用本重载。
        /// </summary>
        /// <param name="uid">用户唯一标识。</param>
        /// <param name="name">用户名称（可选）。</param>
        /// <param name="serverId">用户所在服务器 ID（可选）。</param>
        /// <param name="userTags">用户标签（可选），需提前在 AIHelp 后台配置对应标签。</param>
        /// <param name="customDataJsonString">自定义 Json 用户数据（可选），格式 {"key":"value"}。</param>
        public void Login(string uid, string name = null, string serverId = null, List<string> userTags = null, string customDataJsonString = null)
        {
            if (!m_InitOver)
            {
                return;
            }
            var userConfigBuilder = new global::AIHelp.UserConfig.Builder();
            if (name != null)
            {
                userConfigBuilder.SetUserName(name);
            }
            if (serverId != null)
            {
                userConfigBuilder.SetServerId(serverId);
            }
            if (userTags != null)
            {
                userConfigBuilder.SetUserTags(string.Join(",", userTags));
            }
            if (customDataJsonString != null)
            {
                userConfigBuilder.SetCustomData(customDataJsonString);
            }
            var loginConfigBuilder = new global::AIHelp.LoginConfig.Builder();
            loginConfigBuilder.SetUserId(uid);
            loginConfigBuilder.SetUserConfig(userConfigBuilder.Build());
            global::AIHelp.AIHelpSupport.Login(loginConfigBuilder.Build());
        }

        /// <summary>
        /// 更新用户信息（不改变登录态）。
        /// </summary>
        /// <param name="name">用户名称（可选）。</param>
        /// <param name="serverId">用户所在服务器 ID（可选）。</param>
        /// <param name="userTags">用户标签（可选）。</param>
        /// <param name="customDataJsonString">自定义 Json 用户数据（可选）。</param>
        public void UpdateUserInfo(string name = null, string serverId = null, List<string> userTags = null, string customDataJsonString = null)
        {
            if (!m_InitOver)
            {
                return;
            }
            var userConfigBuilder = new global::AIHelp.UserConfig.Builder();
            if (name != null)
            {
                userConfigBuilder.SetUserName(name);
            }
            if (serverId != null)
            {
                userConfigBuilder.SetServerId(serverId);
            }
            if (userTags != null)
            {
                userConfigBuilder.SetUserTags(string.Join(",", userTags));
            }
            if (customDataJsonString != null)
            {
                userConfigBuilder.SetCustomData(customDataJsonString);
            }
            global::AIHelp.AIHelpSupport.UpdateUserInfo(userConfigBuilder.Build());
        }

        /// <summary>
        /// 重置用户信息：用户退出登录时调用，清除 AIHelp 侧登录用户信息以保证游客/用户信息准确。
        /// </summary>
        public void ResetUserInfo()
        {
            if (!m_InitOver)
            {
                return;
            }
            global::AIHelp.AIHelpSupport.ResetUserInfo();
        }

        /// <summary>
        /// 设置 AIHelp 语言。语言码对照见官方文档；框架多语言切换时可调用，也可业务手动调用。
        /// </summary>
        /// <param name="languageCode">语言码，如 en、zh-CN。为空则忽略。</param>
        public void SetLanguage(string languageCode)
        {
            if (!m_InitOver || string.IsNullOrEmpty(languageCode))
            {
                return;
            }
            global::AIHelp.AIHelpSupport.UpdateSDKLanguage(languageCode);
        }

        /// <summary>
        /// 拉起在线客服 / 帮助中心页面。
        /// </summary>
        /// <param name="entranceId">页面入口 ID（AIHelp 后台配置）。</param>
        /// <param name="welcomeMessage">欢迎语文案（仅在线客服页面有效，可选）。</param>
        /// <returns>是否成功拉起。</returns>
        public bool Show(string entranceId, string welcomeMessage = null)
        {
            if (!m_InitOver || string.IsNullOrEmpty(entranceId))
            {
                return false;
            }
            var builder = new global::AIHelp.ApiConfig.Builder();
            builder.SetEntranceId(entranceId);
            if (!string.IsNullOrEmpty(welcomeMessage))
            {
                builder.SetWelcomeMessage(welcomeMessage);
            }
            return global::AIHelp.AIHelpSupport.Show(builder.Build());
        }

        /// <summary>
        /// 展示单条 FAQ。
        /// </summary>
        /// <param name="faqId">FAQ ID。</param>
        /// <param name="moment">是否/何时展示进入会话入口。</param>
        public void ShowSingleFAQ(string faqId, global::AIHelp.ConversationMoment moment)
        {
            if (!m_InitOver || string.IsNullOrEmpty(faqId))
            {
                return;
            }
            global::AIHelp.AIHelpSupport.ShowSingleFAQ(faqId, moment);
        }

        /// <summary>
        /// 以 AIHelp 内置浏览器展示 URL。
        /// </summary>
        /// <param name="url">目标 URL。</param>
        public void ShowUrl(string url)
        {
            if (!m_InitOver || string.IsNullOrEmpty(url))
            {
                return;
            }
            global::AIHelp.AIHelpSupport.ShowUrl(url);
        }

        /// <summary>
        /// 主动查询未读消息数量。结果经 <see cref="OnMessageArrived"/> 事件异步回传
        /// （对应 vendor EventType.MessageArrival，回传 JSON 含未读消息数；vendor 内部有频率限制）。
        /// </summary>
        public void FetchUnreadMessageCount()
        {
            if (!m_InitOver)
            {
                return;
            }
            global::AIHelp.AIHelpSupport.FetchUnreadMessageCount();
        }

        /// <summary>
        /// 主动查询未读工单数量。结果经 <see cref="OnUnreadTaskCountChanged"/> 事件异步回传
        /// （对应 vendor EventType.UnreadTaskCount）。
        /// </summary>
        public void FetchUnreadTaskCount()
        {
            if (!m_InitOver)
            {
                return;
            }
            global::AIHelp.AIHelpSupport.FetchUnreadTaskCount();
        }

        /// <summary>
        /// 设置推送 token 与平台。插件不依赖 Firebase，由业务把已获取的 token 传入。
        /// </summary>
        /// <param name="pushToken">推送 token。</param>
        /// <param name="platform">推送平台。</param>
        public void SetPushToken(string pushToken, global::AIHelp.PushPlatform platform)
        {
            if (!m_InitOver || string.IsNullOrEmpty(pushToken))
            {
                return;
            }
            global::AIHelp.AIHelpSupport.SetPushTokenAndPlatform(pushToken, platform);
        }

        /// <summary>
        /// 设置上传日志文件路径（Persistent 下绝对路径）。目前仅支持 .log/.bytes/.txt/.zip。
        /// </summary>
        /// <param name="logPath">日志文件绝对路径。</param>
        public void SetUploadLogPath(string logPath)
        {
            if (!m_InitOver || string.IsNullOrEmpty(logPath))
            {
                return;
            }
            global::AIHelp.AIHelpSupport.SetUploadLogPath(logPath);
        }

        /// <summary>
        /// AIHelp 页面是否正在展示中。
        /// </summary>
        /// <returns>展示中返回 true；未初始化返回 false。</returns>
        public bool IsShowing()
        {
            return m_InitOver && global::AIHelp.AIHelpSupport.IsAIHelpShowing();
        }

        /// <summary>
        /// 获取 AIHelp SDK 版本号。
        /// </summary>
        /// <returns>版本号；未初始化返回空串。</returns>
        public string GetSDKVersion()
        {
            return m_InitOver ? global::AIHelp.AIHelpSupport.GetSDKVersion() : string.Empty;
        }

        /// <summary>
        /// 关闭当前 AIHelp 页面。
        /// </summary>
        public void Close()
        {
            if (!m_InitOver)
            {
                return;
            }
            global::AIHelp.AIHelpSupport.Close();
        }
    }
}
