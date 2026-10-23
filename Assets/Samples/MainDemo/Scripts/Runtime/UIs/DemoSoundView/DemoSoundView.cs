/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoSoundView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.13 — Sound 模块演示视图（交互触发型）。
 *            演示声音播放/停止/暂停/恢复，以及 name 重载播放与资源释放。
 *            API：Nova.Sound.PlaySound(group, location) / PlaySound(name) /
 *                 PlaySound(name, params) / StopSound / PauseSound / ResumeSound / ReleaseAssetBySerialID
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Sound 模块演示视图，演示声音播放（group/name 重载）、停止、暂停与恢复。
    /// 继承 BaseDemoView 三段式骨架，包含组/location 选择与多个操作按钮。
    /// </summary>
    public sealed class DemoSoundView : BaseDemoView
    {
        /// <summary>
        /// 声音组输入框（BGM / SFX）。
        /// </summary>

        [SerializeField] private TMP_InputField m_GroupInput;

        /// <summary>
        /// 声音 Asset 地址输入框。
        /// </summary>

        [SerializeField] private TMP_InputField m_LocationInput;

        /// <summary>
        /// Play 按钮，调用 PlaySound(group, location) 播放声音。
        /// </summary>

        [SerializeField] private Button m_PlayButton;

        /// <summary>
        /// PlayByName 按钮，调用 PlaySound(name) 按名称查表播放。
        /// </summary>

        [SerializeField] private Button m_PlayByNameButton;

        /// <summary>
        /// PlayByNameWithParams 按钮，调用 PlaySound(name, PlaySoundParams) 带参数按名称播放。
        /// </summary>

        [SerializeField] private Button m_PlayByNameWithParamsButton;

        /// <summary>
        /// Stop 按钮，停止当前声音并释放本视图持有的资源。
        /// </summary>

        [SerializeField] private Button m_StopButton;

        /// <summary>
        /// Pause 按钮，调用 PauseSound 暂停当前声音。
        /// </summary>

        [SerializeField] private Button m_PauseButton;

        /// <summary>
        /// Resume 按钮，调用 ResumeSound 恢复当前声音。
        /// </summary>

        [SerializeField] private Button m_ResumeButton;

        /// <summary>
        /// 当前播放 SerialID 显示文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CurrentSerialIDText;

        /// <summary>
        /// 默认声音组名。
        /// </summary>
        private const string c_DefaultGroup = "BGM";

        /// <summary>
        /// 兜底声音 Asset 地址（Demo_Sound 未加载时使用）。
        /// </summary>
        private const string c_FallbackLocation = "bgm_main";

        /// <summary>
        /// PlaySound(name) 演示固定 name（来自 Demo_Sound 数据表，BGM 组）。
        /// </summary>
        private const string c_DemoSoundName = "Demo_BgmMain";

        /// <summary>
        /// PlaySound(name) 带参数演示固定 name（来自 Demo_Sound 数据表，SFX 组）。
        /// </summary>
        private const string c_DemoSoundNameSfx = "Demo_SfxClick";

        /// <summary>
        /// 无效 SerialID 哨兵值。
        /// </summary>
        private const int c_InvalidSerialID = -1;

        /// <summary>
        /// 本视图持有的声音请求 SerialID；暂停或自然结束后仍保留，显式结束时停止并释放。
        /// c_InvalidSerialID 表示本视图没有持有声音请求。
        /// </summary>
        private int m_CurrentSerialID = c_InvalidSerialID;

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题，填充默认值。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Sound 演示");

            if (m_GroupInput != null)
            {
                m_GroupInput.text = c_DefaultGroup;
            }

            if (m_LocationInput != null)
            {
                m_LocationInput.text = c_FallbackLocation;
            }

            if (m_PlayButton != null)
            {
                m_PlayButton.onClick.AddListener(OnPlayButtonClick);
                SetButtonApiHint(m_PlayButton, "Nova.Sound.PlaySound(group, location)");
            }

            if (m_PlayByNameButton != null)
            {
                m_PlayByNameButton.onClick.AddListener(OnPlayByNameButtonClick);
                SetButtonApiHint(m_PlayByNameButton, "Nova.Sound.PlaySound(name)");
            }

            if (m_PlayByNameWithParamsButton != null)
            {
                m_PlayByNameWithParamsButton.onClick.AddListener(OnPlayByNameWithParamsButtonClick);
                SetButtonApiHint(m_PlayByNameWithParamsButton, "Nova.Sound.PlaySound(name, PlaySoundParams)");
            }

            if (m_StopButton != null)
            {
                m_StopButton.onClick.AddListener(OnStopButtonClick);
                SetButtonApiHint(m_StopButton, "Nova.Sound.StopSound(serialID)");
            }

            if (m_PauseButton != null)
            {
                m_PauseButton.onClick.AddListener(OnPauseButtonClick);
                SetButtonApiHint(m_PauseButton, "Nova.Sound.PauseSound(serialID)");
            }

            if (m_ResumeButton != null)
            {
                m_ResumeButton.onClick.AddListener(OnResumeButtonClick);
                SetButtonApiHint(m_ResumeButton, "Nova.Sound.ResumeSound(serialID)");
            }
        }

        /// <summary>
        /// 视图打开：重置 SerialID 显示。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_CurrentSerialID = c_InvalidSerialID;
            RefreshSerialIDText();
        }

        /// <summary>
        /// 视图关闭：停止并释放当前声音请求，包含暂停、自然结束或尚在加载中的请求。
        /// </summary>
        /// <param name="isShutdown">是否因管理器关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            if (m_CurrentSerialID != c_InvalidSerialID)
            {
                StopAndReleaseOwnedSound(m_CurrentSerialID);
                m_CurrentSerialID = c_InvalidSerialID;
            }

            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 发起声音请求并接管 SerialID；替换时停止并释放此前由本视图持有的声音。
        /// </summary>
        private void OnPlayButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            string group = m_GroupInput != null && !string.IsNullOrWhiteSpace(m_GroupInput.text) ? m_GroupInput.text : c_DefaultGroup;
            string location = m_LocationInput != null && !string.IsNullOrWhiteSpace(m_LocationInput.text) ? m_LocationInput.text : c_FallbackLocation;

            if (!Nova.Sound.HasSoundGroup(group))
            {
                AppendFeedback($"Nova.Sound.PlaySound(\"{group}\", \"{location}\") -> 声音组 \"{group}\" 未注册（请检查 SoundComponent.SoundGroupShells）", FeedbackLevel.Error);
                return;
            }

            int serialID = Nova.Sound.PlaySound(group, location);
            if (serialID <= 0)
            {
                AppendFeedback($"Nova.Sound.PlaySound(\"{group}\", \"{location}\") -> 失败（声音组不存在或资源无效）", FeedbackLevel.Error);
                return;
            }

            ReplaceCurrentSound(serialID);
            RefreshSerialIDText();
            AppendFeedback($"Nova.Sound.PlaySound(\"{group}\", \"{location}\") -> 已发起播放请求 SerialID={serialID}", FeedbackLevel.Info);
        }

        /// <summary>
        /// 按固定 name 发起声音请求；接受新 SerialID 时停止并释放此前持有的声音。
        /// </summary>
        private void OnPlayByNameButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            int serialID = Nova.Sound.PlaySound(c_DemoSoundName);
            if (serialID <= 0)
            {
                AppendFeedback($"Nova.Sound.PlaySound(\"{c_DemoSoundName}\") -> 失败（表未加载或声音组不存在）", FeedbackLevel.Error);
                return;
            }

            ReplaceCurrentSound(serialID);
            RefreshSerialIDText();
            AppendFeedback($"Nova.Sound.PlaySound(\"{c_DemoSoundName}\") -> 已发起播放请求 SerialID={serialID}", FeedbackLevel.Info);
        }

        /// <summary>
        /// 按名带参数请求播放并停止、释放此前声音；PlaySoundParams 所有权交给 Manager，不自行重复归还。
        /// </summary>
        private void OnPlayByNameWithParamsButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            PlaySoundParams param = PlaySoundParams.Create();
            param.VolumeInSoundGroup = 0.8f;
            int serialID = Nova.Sound.PlaySound(c_DemoSoundNameSfx, param);
            if (serialID <= 0)
            {
                AppendFeedback($"Nova.Sound.PlaySound(\"{c_DemoSoundNameSfx}\", params) -> 失败（表未加载或声音组不存在）", FeedbackLevel.Error);
                return;
            }

            ReplaceCurrentSound(serialID);
            RefreshSerialIDText();
            AppendFeedback($"Nova.Sound.PlaySound(\"{c_DemoSoundNameSfx}\", params{{volumeInGroup=0.8}}) -> 已发起播放请求 SerialID={serialID}", FeedbackLevel.Info);
        }

        /// <summary>
        /// Stop 按钮点击：停止并释放当前 SerialID 的声音，请求自然结束后仍执行资源释放。
        /// </summary>
        private void OnStopButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            if (m_CurrentSerialID == c_InvalidSerialID)
            {
                AppendFeedback("当前无播放中的声音", FeedbackLevel.Warn);
                return;
            }

            int serialID = m_CurrentSerialID;
            bool stopped = StopAndReleaseOwnedSound(serialID);
            m_CurrentSerialID = c_InvalidSerialID;
            RefreshSerialIDText();
            AppendFeedback($"Nova.Sound.StopSound({serialID}, 0f) -> stopped={stopped}；已调用 ReleaseAssetBySerialID({serialID}) 释放本视图资源", stopped ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// Pause 按钮点击：暂停当前 SerialID 的声音。
        /// </summary>
        private void OnPauseButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            if (m_CurrentSerialID == c_InvalidSerialID)
            {
                AppendFeedback("当前无播放中的声音", FeedbackLevel.Warn);
                return;
            }

            Nova.Sound.PauseSound(m_CurrentSerialID);
            AppendFeedback($"Nova.Sound.PauseSound({m_CurrentSerialID}) -> ok", FeedbackLevel.Success);
        }

        /// <summary>
        /// Resume 按钮点击：恢复当前 SerialID 的声音。
        /// </summary>
        private void OnResumeButtonClick()
        {
            if (Nova.Sound == null)
            {
                AppendFeedback("Nova.Sound 不可用", FeedbackLevel.Error);
                return;
            }

            if (m_CurrentSerialID == c_InvalidSerialID)
            {
                AppendFeedback("当前无播放中的声音", FeedbackLevel.Warn);
                return;
            }

            bool resumed = Nova.Sound.ResumeSound(m_CurrentSerialID);
            AppendFeedback($"Nova.Sound.ResumeSound({m_CurrentSerialID}) -> resumed={resumed}", resumed ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 接管新播放标识并立即停止、释放旧声音，避免连续播放丢失本视图的资源释放责任。
        /// </summary>
        /// <param name="serialID">本次 PlaySound 返回的有效请求标识；正值不表示声音已成功播放。</param>
        private void ReplaceCurrentSound(int serialID)
        {
            int previous = m_CurrentSerialID;
            m_CurrentSerialID = serialID;
            if (previous != c_InvalidSerialID && previous != serialID)
            {
                StopAndReleaseOwnedSound(previous);
            }
        }

        /// <summary>
        /// 结束本视图持有的单个声音请求：先立即停止，再通过公开门面释放资源，不影响其他声音。
        /// StopSound 不负责释放已加载资源；加载中的请求先登记停止，后续结果由 Manager 释放而不播放。
        /// 本方法仅供结束、替换和关闭使用，Pause/Resume 保留请求与资源以便继续播放。
        /// </summary>
        /// <param name="serialID">本视图持有的有效声音请求标识；正值不保证资源已经加载或成功播放。</param>
        /// <returns>停止请求是否被声音系统接受；资源释放门面无返回值，不以此结果证明句柄已释放。</returns>
        private bool StopAndReleaseOwnedSound(int serialID)
        {
            if (serialID == c_InvalidSerialID || Nova.Sound == null)
            {
                return false;
            }

            bool stopped = Nova.Sound.StopSound(serialID, 0f);
            Nova.Sound.ReleaseAssetBySerialID(serialID);
            return stopped;
        }

        /// <summary>
        /// 刷新当前 SerialID 显示文本。
        /// </summary>
        private void RefreshSerialIDText()
        {
            if (m_CurrentSerialIDText == null)
            {
                return;
            }

            m_CurrentSerialIDText.text = m_CurrentSerialID == c_InvalidSerialID ? "SerialID：无" : $"SerialID：{m_CurrentSerialID}";
        }
    }
}
