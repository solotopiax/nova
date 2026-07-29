/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTableView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.6 — Table Luban 表格读取演示 View（只读型）
 *            职责：逐项验证 MainDemo 的全部 Luban 表与演示数据。
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
    /// 演示 Nova.Table.HasTable 和 GetTable，并逐项验证 MainDemo 的四张 Luban 表。
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
        /// Demo 中预期加载的表格数量。
        /// </summary>
        private const int c_ExpectedTableCount = 4;

        /// <summary>
        /// 四张表验证报告需要的预览区域高度。
        /// </summary>
        private const float c_TablePreviewHeight = 220f;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Table");

            if (m_TablePreviewText != null)
            {
                m_TablePreviewText.rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical, c_TablePreviewHeight);
            }

            if (m_HasTableButton != null)
            {
                m_HasTableButton.onClick.AddListener(OnHasTableButtonClick);
                SetButtonText(m_HasTableButton, "检查加载状态");
                SetButtonApiHint(m_HasTableButton, "Nova.Table.HasTable<T>() × 4");
            }

            if (m_GetTableButton != null)
            {
                m_GetTableButton.onClick.AddListener(OnGetTableButtonClick);
                SetButtonText(m_GetTableButton, "校验全部数据");
                SetButtonApiHint(m_GetTableButton, "Nova.Table.GetTable<T>() × 4");
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

            OnGetTableButtonClick();
        }

        /// <summary>
        /// HasTable 按钮点击回调，逐项查询全部 Demo 表是否存在。
        /// </summary>
        private void OnHasTableButtonClick()
        {
            if (Nova.Table == null)
            {
                AppendFeedback("Nova.Table.HasTable -> TableComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            var report = new StringBuilder();
            int loadedCount = 0;
            loadedCount += AppendTableAvailability<TbDemo_Item>(report, "TbDemo_Item");
            loadedCount += AppendTableAvailability<TbListA>(report, "TbListA");
            loadedCount += AppendTableAvailability<TbListB>(report, "TbListB");
            loadedCount += AppendTableAvailability<TbMap1>(report, "TbMap1");

            SetPreview(report);
            bool passed = loadedCount == c_ExpectedTableCount && Nova.Table.Count == c_ExpectedTableCount;
            AppendFeedback(
                $"HasTable 全表检查 -> {loadedCount}/{c_ExpectedTableCount}，运行时注册 {Nova.Table.Count} 张表",
                passed ? FeedbackLevel.Success : FeedbackLevel.Error);
        }

        /// <summary>
        /// GetTable 按钮点击回调，读取并校验全部 Demo 表与完整演示数据。
        /// </summary>
        private void OnGetTableButtonClick()
        {
            if (Nova.Table == null)
            {
                AppendFeedback("Nova.Table.GetTable -> TableComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            var report = new StringBuilder();
            int passedCount = 0;
            passedCount += ValidateDemoItemTable(report);
            passedCount += ValidateListATable(report);
            passedCount += ValidateListBTable(report);
            passedCount += ValidateMapTable(report);

            SetPreview(report);
            AppendFeedback(
                $"Table 全量数据校验 -> {passedCount}/{c_ExpectedTableCount} 张表通过",
                passedCount == c_ExpectedTableCount ? FeedbackLevel.Success : FeedbackLevel.Error);
        }

        /// <summary>
        /// 在按钮主文本上设置演示操作名称。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="text">按钮显示文本。</param>
        private static void SetButtonText(Button button, string text)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// 查询指定表是否已经注册，并向报告追加清晰的单表结果。
        /// </summary>
        /// <typeparam name="T">Luban 生成表类型。</typeparam>
        /// <param name="report">结果报告。</param>
        /// <param name="tableName">表格显示名称。</param>
        /// <returns>存在时返回一，否则返回零。</returns>
        private static int AppendTableAvailability<T>(StringBuilder report, string tableName)
            where T : class, ITable
        {
            bool hasTable = Nova.Table.HasTable<T>();
            report.AppendLine($"[{(hasTable ? "通过" : "失败")}] {tableName} | HasTable={hasTable}");
            return hasTable ? 1 : 0;
        }

        /// <summary>
        /// 校验 Demo_Item 的十行完整演示数据。
        /// </summary>
        /// <param name="report">结果报告。</param>
        /// <returns>通过时返回一，否则返回零。</returns>
        private static int ValidateDemoItemTable(StringBuilder report)
        {
            TbDemo_Item table = Nova.Table.GetTable<TbDemo_Item>();
            int[] ids = { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010 };
            string[] names =
            {
                "demo_sword", "demo_shield", "demo_bow", "demo_staff", "demo_potion",
                "demo_helmet", "demo_armor", "demo_boots", "demo_ring", "demo_necklace",
            };
            int[] prices = { 100, 200, 150, 300, 50, 120, 250, 80, 400, 500 };

            bool passed = table != null && table.DataList.Count == c_MaxPreviewRows;
            if (passed)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    Demo_Item row = table[i];
                    if (row.Id != ids[i] || row.Name != names[i] || row.Icon != "icon_coin" || row.Price != prices[i])
                    {
                        passed = false;
                        break;
                    }
                }
            }

            AppendValidationLine(report, passed, "TbDemo_Item", table?.DataList.Count ?? 0,
                "1001/demo_sword/100 -> 1010/demo_necklace/500");
            return passed ? 1 : 0;
        }

        /// <summary>
        /// 校验 ListA 的完整演示数据。
        /// </summary>
        /// <param name="report">结果报告。</param>
        /// <returns>通过时返回一，否则返回零。</returns>
        private static int ValidateListATable(StringBuilder report)
        {
            TbListA table = Nova.Table.GetTable<TbListA>();
            ListA row = table != null && table.DataList.Count == 1 ? table[0] : null;
            bool passed = row != null && row.ID == 1 && row.Desc.Contains("TableListA") &&
                          row.IntValue == 32768 && Mathf.Abs(row.FloatValue - 3.1415925f) < 0.000001f &&
                          row.BoolValue && row.StringValue == "A";
            AppendValidationLine(report, passed, "TbListA", table?.DataList.Count ?? 0,
                "ID=1, Int=32768, Float=3.1415925, Bool=True, String=A");
            return passed ? 1 : 0;
        }

        /// <summary>
        /// 校验 ListB 的完整演示数据。
        /// </summary>
        /// <param name="report">结果报告。</param>
        /// <returns>通过时返回一，否则返回零。</returns>
        private static int ValidateListBTable(StringBuilder report)
        {
            TbListB table = Nova.Table.GetTable<TbListB>();
            ListB row = table != null && table.DataList.Count == 1 ? table[0] : null;
            bool passed = row != null && row.ID == 1 && row.Desc.Contains("TableListB") &&
                          row.IntValue == 32768 && Mathf.Abs(row.FloatValue - 3.1415925f) < 0.000001f &&
                          row.BoolValue && row.StringValue == "A";
            AppendValidationLine(report, passed, "TbListB", table?.DataList.Count ?? 0,
                "ID=1, Int=32768, Float=3.1415925, Bool=True, String=A");
            return passed ? 1 : 0;
        }

        /// <summary>
        /// 校验 Map1 的键查询与完整演示数据。
        /// </summary>
        /// <param name="report">结果报告。</param>
        /// <returns>通过时返回一，否则返回零。</returns>
        private static int ValidateMapTable(StringBuilder report)
        {
            TbMap1 table = Nova.Table.GetTable<TbMap1>();
            Map1 row = table?.GetOrDefault("XXXXX");
            bool passed = table != null && table.DataList.Count == 1 && row != null &&
                          row.Name == "XXXXX" && row.Desc == "XXXXX" && row.Value == "XXXXX";
            AppendValidationLine(report, passed, "TbMap1", table?.DataList.Count ?? 0,
                "Key=XXXXX, Desc=XXXXX, Value=XXXXX");
            return passed ? 1 : 0;
        }

        /// <summary>
        /// 向数据报告追加统一格式的单表校验结果。
        /// </summary>
        /// <param name="report">结果报告。</param>
        /// <param name="passed">是否通过。</param>
        /// <param name="tableName">表格名称。</param>
        /// <param name="rowCount">实际行数。</param>
        /// <param name="sample">代表数据摘要。</param>
        private static void AppendValidationLine(
            StringBuilder report, bool passed, string tableName, int rowCount, string sample)
        {
            report.AppendLine($"[{(passed ? "通过" : "失败")}] {tableName} | {rowCount} 行 | {sample}");
        }

        /// <summary>
        /// 把完整验证报告写入 Demo 预览区。
        /// </summary>
        /// <param name="report">结果报告。</param>
        private void SetPreview(StringBuilder report)
        {
            if (m_TablePreviewText != null)
            {
                m_TablePreviewText.text = report.ToString().TrimEnd();
            }
        }
    }
}
