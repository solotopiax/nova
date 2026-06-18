/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTableView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.6 — Table Luban 表格读取演示 View（只读型）
 *            职责：演示 Nova.Table.HasTable<TbDemo_Item>() 和 GetTable<TbDemo_Item>()。
 ***************************************************************/

using System.Text;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.6 Table 演示 View（只读型）。
    /// 演示 Nova.Table.HasTable 和 GetTable，使用 TbDemo_Item 表（Demo_Item.xlsx 导出后已替换）。
    /// </summary>
    public sealed class DemoTableView : BaseDemoView
    {
        /// <summary>
        /// HasTable 查询按钮，调用 Nova.Table.HasTable。
        /// </summary>

        [SerializeField] private Button m_HasTableButton;

        /// <summary>
        /// GetTable 读取按钮，调用 Nova.Table.GetTable 并展示预览。
        /// </summary>

        [SerializeField] private Button m_GetTableButton;

        /// <summary>
        /// 表格预览文本组件，展示前若干行数据。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_TablePreviewText;

        /// <summary>
        /// 预览展示的最大行数。
        /// </summary>
        private const int c_MaxPreviewRows = 10;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Table");

            if (m_HasTableButton != null)
            {
                m_HasTableButton.onClick.AddListener(OnHasTableButtonClick);
                SetButtonApiHint(m_HasTableButton, "Nova.Table.HasTable<TbDemoItem>()");
            }

            if (m_GetTableButton != null)
            {
                m_GetTableButton.onClick.AddListener(OnGetTableButtonClick);
                SetButtonApiHint(m_GetTableButton, "Nova.Table.GetTable<TbDemoItem>()");
            }
        }

        /// <summary>
        /// 视图打开钩子，清空预览文本。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            if (m_TablePreviewText != null)
            {
                m_TablePreviewText.text = string.Empty;
            }
        }

        /// <summary>
        /// HasTable 按钮点击回调，查询 TbDemo_Item 是否存在。
        /// </summary>
        private void OnHasTableButtonClick()
        {
            if (Nova.Table == null)
            {
                AppendFeedback("Nova.Table.HasTable -> TableComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            bool has = Nova.Table.HasTable<TbDemo_Item>();
            FeedbackLevel level = has ? FeedbackLevel.Success : FeedbackLevel.Warn;
            AppendFeedback("Nova.Table.HasTable<TbDemo_Item>() -> " + has, level);
        }

        /// <summary>
        /// GetTable 按钮点击回调，读取 TbDemo_Item 并展示预览。
        /// </summary>
        private void OnGetTableButtonClick()
        {
            if (Nova.Table == null)
            {
                AppendFeedback("Nova.Table.GetTable -> TableComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            if (!Nova.Table.HasTable<TbDemo_Item>())
            {
                AppendFeedback("Nova.Table.GetTable<TbDemo_Item> -> 表格未加载，请确认 Table 模块已初始化", FeedbackLevel.Warn);
                return;
            }

            TbDemo_Item table = Nova.Table.GetTable<TbDemo_Item>();

            if (table == null)
            {
                AppendFeedback("Nova.Table.GetTable<TbDemo_Item> -> 返回 null", FeedbackLevel.Error);
                return;
            }

            int count = table.DataList.Count;
            StringBuilder sb = new StringBuilder();
            int previewCount = count < c_MaxPreviewRows ? count : c_MaxPreviewRows;
            for (int i = 0; i < previewCount; i++)
            {
                Demo_Item row = table[i];
                sb.AppendLine(i + ": Id=" + row.Id);
            }

            if (m_TablePreviewText != null)
            {
                m_TablePreviewText.text = sb.ToString().TrimEnd();
            }

            AppendFeedback("Nova.Table.GetTable<TbDemo_Item>() -> " + count + " rows", FeedbackLevel.Success);
        }
    }
}
