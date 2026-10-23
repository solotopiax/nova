/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFrameworkComponentView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.1 — FrameworkComponent 生命周期与全局游戏控制
 *            展示 Awake/Start/OnDestroy 三段生命周期信息卡片，
 *            以及通过 Nova.Self 查询子组件、控制暂停/恢复/速度与显式退出。
 ***************************************************************/

using System.Globalization;
using System.Text;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.1 FrameworkComponent 三段生命周期可视化 View。
    /// 展示 Awake / Start / OnDestroy 三段生命周期说明卡片，
    /// 以及运行时子组件清单与 Nova.Self 的全局暂停、恢复、速度设置和显式退出。
    /// </summary>
    public sealed class DemoFrameworkComponentView : BaseDemoView
    {
        /// <summary>
        /// 「刷新子组件清单」按钮，点击后调用 Nova.Self.GetComponentsInChildren 并输出到反馈区。
        /// </summary>

        [SerializeField] private Button m_RefreshChildrenButton;

        /// <summary>暂停按钮，仅接管本页从运行状态发起的暂停。</summary>
        [SerializeField] private Button m_PauseButton;

        /// <summary>恢复按钮，仅恢复本页拥有的暂停。</summary>
        [SerializeField] private Button m_ResumeButton;

        /// <summary>重置速度按钮，显式将全局速度设为 1。</summary>
        [SerializeField] private Button m_ResetSpeedButton;

        /// <summary>退出按钮，显式调用 Nova.Self.QuitApplication。</summary>
        [SerializeField] private Button m_QuitButton;

        /// <summary>全局速度输入框，只接受大于 0 的有限数；暂停通过独立按钮执行。</summary>
        [SerializeField] private TMP_InputField m_GameSpeedInput;

        /// <summary>应用速度按钮，按有效输入显式设置 Nova.Self.GameSpeed。</summary>
        [SerializeField] private Button m_ApplySpeedButton;

        /// <summary>本次打开时的 Nova 实例，防止旧页面控制后续创建的实例。</summary>
        private Nova m_ControlSelf;

        /// <summary>本次打开时的速度，仅作反馈基线，不用于回滚用户显式速度设置。</summary>
        private float m_EntryGameSpeed;

        /// <summary>本页暂停之前的速度，用于说明 ResumeGame 的缓存恢复结果。</summary>
        private float m_PauseResumeSpeed;

        /// <summary>本页是否从运行状态发起了尚未恢复的暂停。</summary>
        private bool m_OwnsPause;

        /// <summary>本页是否处于当前打开周期，关闭后拒绝按钮回调。</summary>
        private bool m_IsViewOpen;

        /// <summary>是否已发出本页的退出请求，防止重复操作或追加退出后的反馈。</summary>
        private bool m_ExitRequested;

        /// <summary>
        /// 首次实例初始化时注册子组件查询及五个游戏控制按钮，重开时不重复注册。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_RefreshChildrenButton != null)
            {
                m_RefreshChildrenButton.onClick.AddListener(OnRefreshChildrenClick);
            }

            m_PauseButton?.onClick.AddListener(OnPauseClick);
            m_ResumeButton?.onClick.AddListener(OnResumeClick);
            m_ResetSpeedButton?.onClick.AddListener(OnResetSpeedClick);
            m_QuitButton?.onClick.AddListener(OnQuitClick);
            m_ApplySpeedButton?.onClick.AddListener(OnApplySpeedClick);
        }

        /// <summary>
        /// 打开钩子，固定当前 Nova 实例，设置交互提示并展示生命周期与全局速度基线。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            RestoreOwnedPause();
            base.OnOpen(userData);
            m_ControlSelf = Nova.Self;
            m_OwnsPause = false;
            m_IsViewOpen = true;
            m_ExitRequested = false;
            SetTitle("FrameworkComponent 生命周期");
            SetButtonApiHint(m_RefreshChildrenButton, "FrameworkComponent.Awake() / Start() / OnDestroy()");
            SetButtonApiHint(m_PauseButton, "Nova.Self.PauseGame()");
            SetButtonApiHint(m_ResumeButton, "Nova.Self.ResumeGame()");
            SetButtonApiHint(m_ResetSpeedButton, "Nova.Self.ResetNormalGameSpeed()");
            SetButtonApiHint(m_QuitButton, "Nova.Self.QuitApplication()");
            SetButtonApiHint(m_ApplySpeedButton, "Nova.Self.GameSpeed = speed");
            AppendLifecycleInfo();

            if (m_ControlSelf != null)
            {
                m_EntryGameSpeed = m_ControlSelf.GameSpeed;
                UpdateSpeedInput(m_EntryGameSpeed);
                AppendFeedback("当前全局速度: " + m_EntryGameSpeed.ToString("G9", CultureInfo.InvariantCulture) +
                               (m_ControlSelf.IsGamePaused ? "（外部暂停，本页不接管）" : "；可输入 2 验证暂停后恢复 2 倍速"), FeedbackLevel.Info);
            }
        }

        /// <summary>
        /// 关闭时仅恢复同一 Nova 实例上本页拥有的暂停；不回滚用户显式 Apply 或 Reset 的速度设置。
        /// </summary>
        /// <param name="isShutdown">框架关闭标识，原样传给基类。</param>
        /// <param name="userData">视图生命周期载荷，原样传给基类。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_IsViewOpen = false;
            RestoreOwnedPause();
            m_ControlSelf = null;
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 追加三段生命周期说明到反馈区。
        /// </summary>
        private void AppendLifecycleInfo()
        {
            AppendFeedback("Awake: 初始化 Manager（Util.TypeCreator.Create），禁止跨组件获取", FeedbackLevel.Info);
            AppendFeedback("Start: 调用 Manager.Initialize(config)，获取跨组件依赖", FeedbackLevel.Info);
            AppendFeedback("OnDestroy: 调用 Manager.Shutdown()，释放托管/非托管资源", FeedbackLevel.Info);
        }

        /// <summary>
        /// 「刷新子组件清单」点击回调，通过 Nova.Self.GetComponentsInChildren 获取所有子 FrameworkComponent。
        /// </summary>
        private void OnRefreshChildrenClick()
        {
            if (!m_IsViewOpen || m_ExitRequested || !Available)
            {
                return;
            }

            if (Nova.Self == null)
            {
                AppendFeedback("Nova.Self 未就绪", FeedbackLevel.Error);
                return;
            }

            FrameworkComponent[] children = Nova.Self.GetComponentsInChildren<FrameworkComponent>(true);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(children[i].GetType().Name);
            }
            AppendFeedback($"Nova.Self -> {children.Length} children active: [{sb}]", FeedbackLevel.Success);
        }

        /// <summary>从运行状态调用 PauseGame，记录本页暂停归属；已有外部暂停或重复点击不覆盖缓存。</summary>
        private void OnPauseClick()
        {
            if (!TryGetControlSelf(out Nova self)) return;
            if (self.IsGamePaused)
            {
                AppendFeedback(m_OwnsPause ? "本页已暂停，重复点击不覆盖恢复速度" : "当前为外部暂停，本页不接管", FeedbackLevel.Warn);
                return;
            }

            float speed = self.GameSpeed;
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
            {
                AppendFeedback("当前速度不是正有限数，不能记录本页恢复速度", FeedbackLevel.Warn);
                return;
            }

            m_PauseResumeSpeed = speed;
            self.PauseGame();
            m_OwnsPause = self.IsGamePaused;
            UpdateSpeedInput(self.GameSpeed);
            AppendFeedback("Nova.Self.PauseGame() -> 暂停；恢复速度 " + speed.ToString("G9", CultureInfo.InvariantCulture), FeedbackLevel.Success);
        }

        /// <summary>仅恢复同一 Nova 实例上本页拥有的暂停，调用公开 ResumeGame 读取框架缓存。</summary>
        private void OnResumeClick()
        {
            if (!TryGetControlSelf(out Nova self)) return;
            if (!m_OwnsPause || !self.IsGamePaused)
            {
                AppendFeedback("当前没有本页拥有的暂停，不恢复外部暂停", FeedbackLevel.Warn);
                return;
            }

            self.ResumeGame();
            m_OwnsPause = false;
            UpdateSpeedInput(self.GameSpeed);
            AppendFeedback("Nova.Self.ResumeGame() -> " + self.GameSpeed.ToString("G9", CultureInfo.InvariantCulture) +
                           "；本页暂停前 " + m_PauseResumeSpeed.ToString("G9", CultureInfo.InvariantCulture), FeedbackLevel.Success);
        }

        /// <summary>显式重置全局速度为 1；当前外部暂停不由本页解除。</summary>
        private void OnResetSpeedClick()
        {
            if (!TryGetControlSelf(out Nova self) || !CanChangeSpeed(self)) return;
            self.ResetNormalGameSpeed();
            m_OwnsPause = false;
            UpdateSpeedInput(self.GameSpeed);
            AppendFeedback("Nova.Self.ResetNormalGameSpeed() -> " + self.GameSpeed.ToString("G9", CultureInfo.InvariantCulture), FeedbackLevel.Success);
        }

        /// <summary>读取大于 0 的有限速度并显式应用；拒绝空值、非数字、非有限值和暂停旁路。</summary>
        private void OnApplySpeedClick()
        {
            if (!TryGetControlSelf(out Nova self) || !CanChangeSpeed(self)) return;
            string raw = m_GameSpeedInput != null ? m_GameSpeedInput.text : null;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) ||
                float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
            {
                AppendFeedback("速度请输入大于 0 的有限数，例如 2；暂停请使用 Pause 按钮", FeedbackLevel.Warn);
                return;
            }

            self.GameSpeed = speed;
            m_OwnsPause = false;
            UpdateSpeedInput(self.GameSpeed);
            AppendFeedback("Nova.Self.GameSpeed -> " + self.GameSpeed.ToString("G9", CultureInfo.InvariantCulture), FeedbackLevel.Success);
        }

        /// <summary>先反馈并恢复本页暂停，再显式退出应用；退出后不追加反馈或再接受本页控制。</summary>
        private void OnQuitClick()
        {
            if (!TryGetControlSelf(out Nova self)) return;
            m_ExitRequested = true;
            AppendFeedback("Nova.Self.QuitApplication() -> 退出应用；Editor 下停止 Play", FeedbackLevel.Warn);
            RestoreOwnedPause();
            m_IsViewOpen = false;
            self.QuitApplication();
        }

        /// <summary>取得本次打开的同一 Nova 实例；失效、关闭或退出后拒绝控制，并放弃已观察到失效的暂停归属。</summary>
        /// <param name="self">本次页面可控制的 Nova 实例；失败时为 null。</param>
        /// <returns>当前打开周期及 Nova 实例均有效时为 true。</returns>
        private bool TryGetControlSelf(out Nova self)
        {
            self = null;
            if (!m_IsViewOpen || m_ExitRequested || !Available) return false;
            if (m_ControlSelf == null || !ReferenceEquals(Nova.Self, m_ControlSelf))
            {
                m_OwnsPause = false;
                AppendFeedback("Nova.Self 未就绪或实例已改变，请关闭后重新打开此页", FeedbackLevel.Error);
                return false;
            }

            self = m_ControlSelf;
            if (m_OwnsPause && !self.IsGamePaused) m_OwnsPause = false;
            return true;
        }

        /// <summary>只允许运行状态或本页拥有的暂停进行显式速度设置，不解除已观察到的外部暂停。</summary>
        /// <param name="self">已通过本次打开周期及实例身份校验的 Nova。</param>
        /// <returns>允许应用或重置速度时为 true。</returns>
        private bool CanChangeSpeed(Nova self)
        {
            if (!self.IsGamePaused || m_OwnsPause) return true;
            AppendFeedback("当前为外部暂停，不能由本页应用或重置速度", FeedbackLevel.Warn);
            return false;
        }

        /// <summary>在关闭、重开或退出前恢复同一 Nova 实例上本页尚拥有的暂停，然后清空归属。</summary>
        private void RestoreOwnedPause()
        {
            if (m_OwnsPause && m_ControlSelf != null && ReferenceEquals(Nova.Self, m_ControlSelf) && m_ControlSelf.IsGamePaused)
            {
                // Nova 仅有一个暂停缓存；同速度的外部暂停无法从公开状态区分，不承诺检测所有外部调用。
                m_ControlSelf.ResumeGame();
            }
            m_OwnsPause = false;
        }

        /// <summary>将实际全局速度写回输入框，不触发输入事件，也不修改全局速度。</summary>
        /// <param name="speed">需要展示的实际速度。</param>
        private void UpdateSpeedInput(float speed)
        {
            if (m_GameSpeedInput != null)
            {
                m_GameSpeedInput.SetTextWithoutNotify(speed.ToString("G9", CultureInfo.InvariantCulture));
            }
        }
    }
}
