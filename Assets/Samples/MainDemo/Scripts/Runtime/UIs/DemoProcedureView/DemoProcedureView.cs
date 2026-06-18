/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoProcedureView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.10 — Procedure 模块演示视图（只读快照型）。
 *            展示已注册流程列表与当前活跃流程。
 *            API：Nova.Procedure.GetProcedure<T>() / HasProcedure<T>()
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Procedure 模块演示视图，展示已注册流程列表及当前流程快照。
    /// 继承 BaseDemoView 三段式骨架，提供刷新按钮以重新读取流程状态。
    /// </summary>
    public sealed class DemoProcedureView : BaseDemoView
    {
        /// <summary>
        /// 当前流程名称显示文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CurrentProcedureText;

        /// <summary>
        /// 刷新快照按钮。
        /// </summary>

        [SerializeField] private Button m_RefreshButton;

        /// <summary>
        /// 流程列表容器（每行 1 个 TextMeshProUGUI）。
        /// </summary>

        [SerializeField] private Transform m_ProcedureListRoot;

        /// <summary>
        /// 流程列表行模板（disable 状态）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ProcedureItemTemplate;

        /// <summary>
        /// 已实例化的流程列表行，刷新时销毁重建。
        /// </summary>
        private readonly List<TextMeshProUGUI> m_ProcedureItems = new List<TextMeshProUGUI>();

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Procedure 演示");

            if (m_ProcedureItemTemplate != null)
            {
                m_ProcedureItemTemplate.gameObject.SetActive(false);
            }

            if (m_RefreshButton != null)
            {
                m_RefreshButton.onClick.AddListener(OnRefreshButtonClick);
                SetButtonApiHint(m_RefreshButton, "Nova.Procedure.GetProcedure<T>() / HasProcedure<T>()");
            }
        }

        /// <summary>
        /// 视图打开：自动刷新流程快照。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshSnapshot();
        }

        /// <summary>
        /// 刷新按钮点击：重新读取流程列表并刷新 UI。
        /// </summary>
        private void OnRefreshButtonClick()
        {
            RefreshSnapshot();
        }

        /// <summary>
        /// 刷新流程快照：通过 Util.Assembly 枚举 ProcedureBase 子类，调用 HasProcedure 检查。
        /// </summary>
        private void RefreshSnapshot()
        {
            ClearProcedureItems();

            if (Nova.Procedure == null)
            {
                AppendFeedback("Nova.Procedure 不可用", FeedbackLevel.Error);
                return;
            }

            string currentName = Nova.Procedure.CurrentProcedure?.GetType().Name ?? "（无）";

            if (m_CurrentProcedureText != null)
            {
                m_CurrentProcedureText.text = "当前流程：" + currentName;
            }

            string[] typeNames = Util.Assembly.GetTypeNames(typeof(ProcedureBase), typeof(ProcedureBase).Assembly);

            if (typeNames == null || typeNames.Length == 0)
            {
                AppendFeedback("Nova.Procedure -> 0 procedures（MainDemo 无 Procedure）", FeedbackLevel.Warn);
                return;
            }

            for (int i = 0; i < typeNames.Length; i++)
            {
                string typeName = typeNames[i];
                System.Type t = Util.Assembly.GetType(typeName);
                if (t == null)
                {
                    continue;
                }

                ProcedureBase proc = Nova.Procedure.GetProcedure(t);
                bool exists = proc != null;
                bool isCurrent = exists && proc.GetType().Name == currentName;
                AddProcedureItem(t.Name, isCurrent, exists);
            }

            AppendFeedback($"Nova.Procedure.GetProcedure<T>() -> {typeNames.Length} types / Current={currentName}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 在流程列表容器中添加一行流程条目。
        /// </summary>
        /// <param name="typeName">流程类型名称。</param>
        /// <param name="isCurrent">是否为当前活跃流程。</param>
        /// <param name="exists">是否已注册（HasProcedure 为 true）。</param>
        private void AddProcedureItem(string typeName, bool isCurrent, bool exists)
        {
            if (m_ProcedureItemTemplate == null || m_ProcedureListRoot == null)
            {
                return;
            }

            TextMeshProUGUI item = Instantiate(m_ProcedureItemTemplate, m_ProcedureListRoot);
            item.gameObject.SetActive(true);

            string prefix = isCurrent ? "[当前] " : (exists ? "" : "[未注册] ");
            item.text = prefix + typeName;
            item.color = isCurrent
                ? new Color32(0x4C, 0xAF, 0x50, 0xFF)
                : exists
                    ? new Color32(0xCC, 0xCC, 0xCC, 0xFF)
                    : new Color32(0xFF, 0xB3, 0x00, 0xFF);

            m_ProcedureItems.Add(item);
        }

        /// <summary>
        /// 销毁所有已实例化的流程列表行并清空缓存列表。
        /// </summary>
        private void ClearProcedureItems()
        {
            for (int i = 0; i < m_ProcedureItems.Count; i++)
            {
                if (m_ProcedureItems[i] != null)
                {
                    Destroy(m_ProcedureItems[i].gameObject);
                }
            }

            m_ProcedureItems.Clear();
        }
    }
}
