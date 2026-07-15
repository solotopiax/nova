/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoDataMasterView.cs
 * author:    nova-create-sample
 * created:   2026/07/03
 * descrip:   DemoDataMasterView 演示 View — 生命周期与按钮绑定。
 *            每个按钮单独暴露 DataMaster 一个常用接口，点击后就近打印反馈。
 ***************************************************************/

namespace NovaFramework.Sdk.Datamaster.Samples.Runtime
{
    /// <summary>
    /// DemoDataMasterView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 交互区把 DataMaster 常用接口拆成独立按钮：读参 / 读实验参数（JSON） / 曝光 / 上报事件 / 设置分流属性 / 模拟登录拉取。
    /// </summary>
    public sealed partial class DemoDataMasterView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 逐个绑定 DataMaster 接口按钮并设置就近 API 提示。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("DataMaster 演示");

            // 「清理 SDK 缓存」为辅助按钮（模拟新设备），置于流程按钮之前；不影响下方 ABTest 流程顺序。
            BindButton(m_ClearCacheButton, OnClearCacheClick, "ClearRuntimeCache()");
            // 按 ABTest 完整流程顺序绑定：设置分流属性 → 登录并拉取 → 读参 → 读实验参数（JSON） → 曝光 → 上报事件。
            // 分流属性须在触发拉取「之前」设置才能参与本次分桶，故置于登录之前。
            BindButton(m_SetPropertyButton, OnSetPropertyClick, "SetUserProperty(key, value)");
            BindButton(m_LoginRefreshButton, OnLoginRefreshClick, "Kit<Login>().Async(\"\",\"\",true) → kit 自动通知 SDK 拉取");
            BindButton(m_ReadParamButton, OnReadParamClick, "GetParamValue<T>(topicId, paramName, fallback)");
            BindButton(m_ReadJsonButton, OnReadJsonClick, "GetParamValueJson(topicId, paramName)");
            BindButton(m_ExposureButton, OnExposureClick, "MarkExposure(topicId)");
            BindButton(
                m_LogEventButton,
                OnLogEventClick,
                "LogExperimentEvent(eventName, value, extraContext)");
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            // 订阅服务端拉取成功 / 失败事件，使「登录并拉取」的结果与下发内容直接显示在反馈区。
            SubscribeRefreshEvents();

            AppendFeedback("DataMaster 演示已打开，逐个按钮体验各接口。");

            // 进入页面即打印当前设备 ID（反馈区 + Unity Console），口径与 DataMaster 拉取 / 上报一致。
            LogDeviceId();
        }

        /// <summary>
        /// 视图关闭钩子，关闭时由基类清空反馈区。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            // 退订拉取事件，避免 View 复用 / 关闭后仍持有 plugin 回调造成重复触发或泄漏。
            UnsubscribeRefreshEvents();

            base.OnClose(isShutdown, userData);
        }
    }
}
