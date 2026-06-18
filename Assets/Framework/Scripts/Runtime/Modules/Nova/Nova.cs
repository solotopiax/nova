/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Nova.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   Nova框架根节点组件
 *            框架树形结构根节点，负责统一管理各子模块
 *            所有子组件继承自 NovaComponent，并在 Awake 阶段自动注册
 *            设计目标：
 *            1. 高度解耦：根节点只负责获取和访问组件，不直接实现业务逻辑
 *            2. 低维护成本：新增组件仅需继承 NovaComponent 并注册，无需修改 Nova
 *            3. 模块独立：各子组件间无强依赖，通过事件/消息或接口通信
 *            4. 框架统一入口：提供全局访问点，方便其他模块调用
 ***************************************************************/

using System;
using System.Globalization;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Nova 框架根节点组件。
    /// </summary>
    public sealed partial class Nova : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// 初始化全局助手、运行环境配置和系统回调。
        /// </summary>
        protected override void Awake()
        {
            // 单例保护：场景中只允许存在一个 Nova 实例。
            // 必须在 base.Awake()（注册组件）之前检查，避免重复注册。
            if (Self != null && Self != this)
            {
                Log.Warning(LogTag.Base, "检测到重复的 Nova 实例，销毁多余对象：{0}。", gameObject.name);
                Destroy(gameObject);
                return;
            }

            base.Awake();
            Self = this;

            // Txt 助手。
            try
            {
                var txtHelper = Util.TypeCreator.Create<ITxtHelper>(m_CurTxtHelperTypeName);
                txtHelper.Initialize();
                Txt.SetHelper(txtHelper);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(Txt.Format("[Nova] 初始化 TxtHelper 失败：{0}", e));
            }

            // Log 助手。
            try
            {
                var logHelper = Util.TypeCreator.Create<ILogHelper>(m_CurLogHelperTypeName);
                logHelper.Initialize();
                Log.SetHelper(logHelper);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(Txt.Format("[Nova] 初始化 LogHelper 失败：{0}", e));
            }

            // 必须在任何 ReferencePool.Get / ReferencePool.Put 之前完成此步骤。
            // 若跳过或顺序错位，EnsureHelper() 会抛 InvalidOperationException。
            // Nova.Awake 早于所有其他 FrameworkComponent（由 base.Awake() 内的注册顺序保证），
            // 业务代码与其他模块禁止在 Nova.Awake 之前调用 ReferencePool。
            // Ref 助手。
            try
            {
                var refHelper = Util.TypeCreator.Create<IReferenceHelper>(m_CurReferenceHelperTypeName);
                refHelper.Initialize(IsStrictCheck());
                ReferencePool.SetHelper(refHelper);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Base, "初始化 ReferenceHelper 失败：{0}。", e);
            }

            // 设置全局文化信息（避免地区差异导致的格式化问题）。
            SetCultureInfo(CultureInfo.InvariantCulture);

            // 运行帧率设定。
            Application.targetFrameRate = m_FrameRate;

            // 运行速率设定。
            Time.timeScale = m_GameSpeed;

            // 后台运行设定。
            Application.runInBackground = m_RunInBackground;

            // 屏幕常亮设定。
            Screen.sleepTimeout = m_NeverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

            // 注册内存不足预警回调。
            Application.lowMemory += OnLowMemory;
        }

        /// <summary>
        /// 开始（获取所有注册的 FrameworkComponent 并赋值到静态属性）。
        /// </summary>
        private void Start()
        {
            App = FrameworkComponentsGroup.GetComponent<AppComponent>();
            Asset = FrameworkComponentsGroup.GetComponent<AssetComponent>();
            Config = FrameworkComponentsGroup.GetComponent<ConfigComponent>();
            Prefab = FrameworkComponentsGroup.GetComponent<PrefabComponent>();
            Event = FrameworkComponentsGroup.GetComponent<EventComponent>();
            Table = FrameworkComponentsGroup.GetComponent<TableComponent>();
            Localization = FrameworkComponentsGroup.GetComponent<LocalizationComponent>();
            UI = FrameworkComponentsGroup.GetComponent<UIComponent>();
            Network = FrameworkComponentsGroup.GetComponent<NetworkComponent>();
            Procedure = FrameworkComponentsGroup.GetComponent<ProcedureComponent>();
            ObjectPool = FrameworkComponentsGroup.GetComponent<ObjectPoolComponent>();
            Persist = FrameworkComponentsGroup.GetComponent<PersistComponent>();
            Sound = FrameworkComponentsGroup.GetComponent<SoundComponent>();
            Vibrate = FrameworkComponentsGroup.GetComponent<VibrateComponent>();
            SDK = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            Debug = FrameworkComponentsGroup.GetComponent<DebugComponent>();

            // 组件空引用保护。
            ValidateComponent(App, nameof(AppComponent));
            ValidateComponent(Asset, nameof(AssetComponent));
            ValidateComponent(Config, nameof(ConfigComponent));
            ValidateComponent(Prefab, nameof(PrefabComponent));
            ValidateComponent(Event, nameof(EventComponent));
            ValidateComponent(Table, nameof(TableComponent));
            ValidateComponent(Localization, nameof(LocalizationComponent));
            ValidateComponent(UI, nameof(UIComponent));
            ValidateComponent(Network, nameof(NetworkComponent));
            ValidateComponent(Procedure, nameof(ProcedureComponent));
            ValidateComponent(ObjectPool, nameof(ObjectPoolComponent));
            ValidateComponent(Persist, nameof(PersistComponent));
            ValidateComponent(Sound, nameof(SoundComponent));
            ValidateComponent(Vibrate, nameof(VibrateComponent));
            ValidateComponent(SDK, nameof(SDKComponent));
            ValidateComponent(Debug, nameof(DebugComponent));

            // 引擎版本信息。
            Log.Debug(LogTag.Base, "UNITY 版本号：{0}。", Application.unityVersion);

            // 框架版本信息。
            Log.Debug(LogTag.Base, "NOVA 版本号：{0}。", Version);

            // 应用版本信息。
            Log.Debug(LogTag.Base, "APP 版本号：{0}。", Application.version);
        }

        /// <summary>
        /// 更新（每帧调度所有管理器的更新）。
        /// </summary>
        private void Update()
        {
            FrameworkManagersGroup.Update();
        }

        /// <summary>
        /// 销毁（清理管理器、注销回调、清空静态引用）。
        /// </summary>
        private void OnDestroy()
        {
            FrameworkManagersGroup.Shutdown();

            // 注销内存不足预警回调。
            Application.lowMemory -= OnLowMemory;

            // 清空所有静态组件引用，防止持有已销毁对象。
            ClearStaticReferences();
            FrameworkComponentsGroup.Clear();
        }

        /// <summary>
        /// App 挂起和恢复挂起时的回调。
        /// </summary>
        /// <param name="pause">是否为挂起。</param>
        private void OnApplicationPause(bool pause)
        {
            Log.Debug(LogTag.Base, pause ? "APP 挂起。" : "APP 恢复。");
        }

        /// <summary>
        /// App 退出时的回调。
        /// </summary>
        private void OnApplicationQuit()
        {
            Log.Debug(LogTag.Base, "APP 退出。");
        }

        /// <summary>
        /// 暂停游戏。
        /// 缓存当前速率后将 GameSpeed 设为 0。
        /// </summary>
        public void PauseGame()
        {
            if (IsGamePaused)
            {
                return;
            }

            m_GameSpeedCache = GameSpeed;
            GameSpeed = 0f;
        }

        /// <summary>
        /// 恢复游戏。
        /// 将 GameSpeed 恢复为暂停前缓存的速率。
        /// </summary>
        public void ResumeGame()
        {
            if (!IsGamePaused)
            {
                return;
            }

            GameSpeed = m_GameSpeedCache;
        }

        /// <summary>
        /// 重置为正常游戏速度。
        /// </summary>
        public void ResetNormalGameSpeed()
        {
            if (IsNormalGameSpeed)
            {
                return;
            }

            GameSpeed = 1f;
        }

        /// <summary>
        /// 退出应用。
        /// 先输出日志，再调用 Application.Quit；编辑器下同时停止播放。
        /// </summary>
        public void QuitApplication()
        {
            Log.Debug(LogTag.Base, "退出应用。");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
