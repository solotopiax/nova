/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoMemoryProtectionView.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   MemoryProtectionDemo 独立钱包的内存防护交互演示
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NovaFramework.Memory.Protection.Samples.Runtime
{
    /// <summary>内存数据防护：独立钱包的受保护读写、普通存档及编辑器故障与轮换演示。</summary>
    public sealed class DemoMemoryProtectionView : BaseDemoView
    {
        [SerializeField] private TextMeshProUGUI m_MemoryStatusText;
        [SerializeField] private TextMeshProUGUI m_MemoryBalancesText;
        [SerializeField] private Button m_MemoryLoadButton;
        [SerializeField] private Button m_MemoryNewButton;
        [SerializeField] private Button m_MemoryGoldAddButton;
        [SerializeField] private Button m_MemoryGoldSpendButton;
        [SerializeField] private Button m_MemoryDiamondsAddButton;
        [SerializeField] private Button m_MemoryDiamondsSpendButton;
        [SerializeField] private Button m_MemorySaveButton;
        [SerializeField] private Button m_MemoryFailureButton;
        [SerializeField] private Button m_MemoryRotateButton;
        private DemoMemoryProtectionWallet m_MemoryWallet;

        /// <summary>首次创建时设置标题并绑定序列化控件。</summary>
        /// <param name="userData">框架传入的页面上下文。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            SetTitle("内存数据防护");
            InitializeMemoryProtectionDemo();
        }

        /// <summary>每次打开都从演示存档建立新的受保护字段并订阅通知。</summary>
        /// <param name="userData">框架传入的页面上下文。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            OpenMemoryProtectionDemo();
        }

        /// <summary>关闭时释放本页字段与通知，支持对象池重复打开。</summary>
        /// <param name="isShutdown">是否由框架关闭触发。</param>
        /// <param name="userData">框架传入的关闭上下文。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CloseMemoryProtectionDemo();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>首次创建时绑定已序列化的演示控件，不在运行时创建页面或修改 Config。</summary>
        private void InitializeMemoryProtectionDemo()
        {
            BindMemoryButton(m_MemoryLoadButton, LoadMemoryWallet, "普通 DTO → ProtectedLong / ProtectedInt");
            BindMemoryButton(m_MemoryNewButton, NewMemoryWallet, "新建独立演示钱包：1000 金币 / 100 钻石");
            BindMemoryButton(m_MemoryGoldAddButton, () => ChangeMemoryGold(100), "TryRead → TryWrite(gold + 100)");
            BindMemoryButton(m_MemoryGoldSpendButton, () => ChangeMemoryGold(-100), "TryRead → TryWrite(gold - 100)");
            BindMemoryButton(m_MemoryDiamondsAddButton, () => ChangeMemoryDiamonds(10), "TryRead → TryWrite(diamonds + 10)");
            BindMemoryButton(m_MemoryDiamondsSpendButton, () => ChangeMemoryDiamonds(-10), "TryRead → TryWrite(diamonds - 10)");
            BindMemoryButton(m_MemorySaveButton, SaveMemoryWallet, "TryRead → 普通 DTO → FileFragment.Save");
            BindMemoryButton(m_MemoryFailureButton, SimulateMemoryFailure, "仅 Editor：模拟后端校验失败，触发字段隔离");
            BindMemoryButton(m_MemoryRotateButton, RotateMemoryFields, "仅 Editor：单独轮换本页字段并复核余额");
        }

        /// <summary>为固定按钮绑定一次事件并展示就近 API 提示。</summary>
        private void BindMemoryButton(Button button, UnityAction action, string hint)
        {
            if (button == null) return;
            button.onClick.AddListener(action);
            SetButtonApiHint(button, hint);
        }

        /// <summary>每次打开重新绑定本页钱包与通知，不继承上一次关闭的字段。</summary>
        private void OpenMemoryProtectionDemo()
        {
            CloseMemoryProtectionDemo();
            MemoryProtection.TamperDetected += OnMemoryTamperDetected;
            if (Nova.Persist?.FileFragment != null)
                m_MemoryWallet = new DemoMemoryProtectionWallet(Nova.Persist.FileFragment);
            LoadMemoryWallet();
        }

        /// <summary>页面退出只清理本页字段和订阅，不重置全局防护或删除演示存档。</summary>
        private void CloseMemoryProtectionDemo()
        {
            MemoryProtection.TamperDetected -= OnMemoryTamperDetected;
            m_MemoryWallet?.Dispose();
            m_MemoryWallet = null;
        }

        /// <summary>从独立分类读取普通 DTO；坏结构只反馈失败，不把它覆盖成默认余额。</summary>
        private void LoadMemoryWallet()
        {
            if (m_MemoryWallet == null)
            {
                AppendFeedback("MemoryProtection：FileFragment 尚未就绪，请等待 MemoryProtectionDemo 预加载完成。", FeedbackLevel.Warn);
                RefreshMemoryStatus();
                return;
            }
            try
            {
                bool loaded = m_MemoryWallet.Load();
                AppendFeedback(loaded ? "FileFragment → ProtectedLong / ProtectedInt：演示钱包已加载。"
                    : "FileFragment：尚无演示存档，已创建 1000 金币 / 100 钻石的内存钱包，未自动保存。", FeedbackLevel.Info);
            }
            catch (Exception)
            {
                AppendFeedback("FileFragment：演示存档读取失败，未覆盖原存档。", FeedbackLevel.Warn);
            }
            RefreshMemoryStatus();
        }

        /// <summary>用户显式恢复演示初始值，只操作固定示例钱包，不代表真实余额恢复策略。</summary>
        private void NewMemoryWallet()
        {
            m_MemoryWallet?.CreateNew();
            AppendFeedback("ProtectedLong / ProtectedInt：已新建演示内存钱包，点击保存才写入演示存档。", FeedbackLevel.Info);
            RefreshMemoryStatus();
        }

        /// <summary>按按钮金额增减金币，失败时停止本次结算。</summary>
        private void ChangeMemoryGold(long delta)
        {
            bool success = m_MemoryWallet?.TryChangeGold(delta) == true;
            AppendFeedback("ProtectedLong.TryRead / TryWrite：金币变更" + (success ? "成功。" : "被拒绝，余额不足或字段不可用。"),
                success ? FeedbackLevel.Success : FeedbackLevel.Warn);
            RefreshMemoryStatus();
        }

        /// <summary>按按钮金额增减钻石，失败不使用输出零值继续结算。</summary>
        private void ChangeMemoryDiamonds(int delta)
        {
            bool success = m_MemoryWallet?.TryChangeDiamonds(delta) == true;
            AppendFeedback("ProtectedInt.TryRead / TryWrite：钻石变更" + (success ? "成功。" : "被拒绝，余额不足或字段不可用。"),
                success ? FeedbackLevel.Success : FeedbackLevel.Warn);
            RefreshMemoryStatus();
        }

        /// <summary>只有两个字段均通过校验才保存普通 DTO；异常只显示固定提示，不自动重试。</summary>
        private void SaveMemoryWallet()
        {
            try
            {
                bool saved = m_MemoryWallet?.TrySave() == true;
                AppendFeedback(saved ? "TryRead → FileFragment.Save：返回 true；存档只有普通整数，跨启动读取需另行验证。"
                    : "TryRead → FileFragment.Save：未保存，字段校验失败或存储未确认成功。",
                    saved ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (Exception)
            {
                AppendFeedback("FileFragment.Save：保存状态未确认，请核对后再决定操作。", FeedbackLevel.Warn);
            }
            RefreshMemoryStatus();
        }

        /// <summary>仅通过 Editor 桥让演示金币的下一次后端校验失败，展示真实字段隔离与通知路径。</summary>
        private void SimulateMemoryFailure()
        {
            bool injected = DemoMemoryProtectionDiagnostics.Editor?.InjectValidationFailure(m_MemoryWallet?.Gold) == true;
            if (injected) m_MemoryWallet.TryRead(out _, out _);
            AppendFeedback(injected ? "Editor：已模拟后端校验失败，请尝试结算和保存，观察隔离拦截。"
                : "Editor：当前没有可用的受保护演示字段。", injected ? FeedbackLevel.Info : FeedbackLevel.Warn);
            RefreshMemoryStatus();
        }

        /// <summary>仅通过 Editor 桥对本页字段调用真实单字段轮换；不修改全局定时器或发送虚构批次结果。</summary>
        private void RotateMemoryFields()
        {
            bool rotated = DemoMemoryProtectionDiagnostics.Editor?.RotateFields(m_MemoryWallet?.Gold, m_MemoryWallet?.Diamonds) == true;
            AppendFeedback(rotated ? "Editor：两个演示字段轮换并校验通过，余额保持不变。"
                : "Editor：本次轮换未通过或当前未启用防护与轮换。", rotated ? FeedbackLevel.Success : FeedbackLevel.Warn);
            RefreshMemoryStatus();
        }

        /// <summary>只展示本页固定逻辑字段的脱敏通知，不接管其他页面或业务的异常。</summary>
        private void OnMemoryTamperDetected(MemoryTamperInfo info)
        {
            if (info.ProtectedFieldId != DemoMemoryProtectionWallet.GoldFieldId &&
                info.ProtectedFieldId != DemoMemoryProtectionWallet.DiamondsFieldId) return;
            AppendFeedback("MemoryProtection.TamperDetected：演示字段已隔离，结算与保存已停止。", FeedbackLevel.Warn);
        }

        /// <summary>刷新保护状态、示例余额和诊断按钮；缺插件仍允许正常读写及保存。</summary>
        private void RefreshMemoryStatus()
        {
            bool readable = false;
            long gold = 0;
            int diamonds = 0;
            if (m_MemoryWallet != null) readable = m_MemoryWallet.TryRead(out gold, out diamonds);
            bool protectedValues = m_MemoryWallet?.Gold?.IsProtected == true && m_MemoryWallet.Diamonds?.IsProtected == true;
            bool quarantined = m_MemoryWallet?.Gold?.IsQuarantined == true || m_MemoryWallet?.Diamonds?.IsQuarantined == true;
            if (m_MemoryStatusText != null)
            {
                string status = quarantined ? "字段已隔离，结算与保存被拦截" : protectedValues ? "内存防护已启用" : "普通数值兼容模式";
                m_MemoryStatusText.text = status + "\nACTk：" + (string.IsNullOrEmpty(MemoryProtection.ActkVersion) ? "未检测到" : MemoryProtection.ActkVersion)
                    + "；自动轮换：" + (MemoryProtection.IsActive && MemoryProtection.KeyRotationEnabled ? "开启（60 秒周期）" : "未启用");
                m_MemoryStatusText.color = protectedValues ? Color.green : Color.yellow;
            }
            if (m_MemoryBalancesText != null)
                m_MemoryBalancesText.text = readable ? $"演示金币：{gold}    演示钻石：{diamonds}" : "演示余额不可用，不把读取失败的 0 当作余额。";
            bool editorDiagnostics = Application.isEditor && DemoMemoryProtectionDiagnostics.Editor != null;
            if (m_MemoryFailureButton != null) m_MemoryFailureButton.interactable = editorDiagnostics && protectedValues;
            if (m_MemoryRotateButton != null) m_MemoryRotateButton.interactable = editorDiagnostics && protectedValues && MemoryProtection.KeyRotationEnabled;
        }
    }
}
