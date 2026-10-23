/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationTableUiView.cs
 * author:    taoye
 * created:   2026/10/15
 * descrip:   Integration 4.5 — 从已加载的物品表刷新业务界面
 ***************************************************************/

using System.Globalization;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 按明确的物品编号读取 TbDemo_Item，并将当前行绑定到物品详情。
    /// 每次打开重新读表；不在页面中加载表，也不以固定数据替代缺失行。
    /// </summary>
    public sealed class DemoIntegrationTableUiView : BaseDemoView
    {
        /// <summary>物品编号输入框；每次打开重置为明确的演示编号。</summary>
        [SerializeField] private TMP_InputField m_ItemIdInput;

        /// <summary>读取当前输入编号并刷新详情的按钮。</summary>
        [SerializeField] private Button m_RefreshButton;

        /// <summary>展示表中编号、名称、图标地址与价格的详情文本。</summary>
        [SerializeField] private TextMeshProUGUI m_ItemDetailsText;

        /// <summary>展示读取成功或输入、表格、行编号错误的状态文本。</summary>
        [SerializeField] private TextMeshProUGUI m_StatusText;

        /// <summary>每次打开使用的明确演示编号，不代表表格第一行。</summary>
        private const int c_DefaultItemId = 1001;

        /// <summary>
        /// 首次创建实例时设置标题并注册本页面按钮；池化重开不重复注册。
        /// </summary>
        /// <param name="userData">用户数据，本页面不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            SetTitle("物品详情");

            if (m_RefreshButton != null)
            {
                m_RefreshButton.onClick.AddListener(OnRefreshButtonClick);
                TextMeshProUGUI label = m_RefreshButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = "读取物品";
                }
            }
        }

        /// <summary>
        /// 每次打开都重置输入并重新查询演示编号，避免池化实例显示上次选中数据。
        /// </summary>
        /// <param name="userData">用户数据，本页面不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            if (m_ItemIdInput != null)
            {
                m_ItemIdInput.SetTextWithoutNotify(c_DefaultItemId.ToString(CultureInfo.InvariantCulture));
            }

            RefreshItem(c_DefaultItemId);
        }

        /// <summary>
        /// 清空本页面临时显示并交回基类关闭；本实例按钮监听留给池化重开复用。
        /// </summary>
        /// <param name="isShutdown">是否由 UI 管理器关闭。</param>
        /// <param name="userData">关闭数据，本页面不使用。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            if (m_ItemDetailsText != null)
            {
                m_ItemDetailsText.text = string.Empty;
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = string.Empty;
            }

            base.OnClose(isShutdown, userData);
        }

        /// <summary>解析输入整数；错误输入清空旧详情，合法编号再查当前已加载表。</summary>
        private void OnRefreshButtonClick()
        {
            if (m_ItemIdInput == null || !int.TryParse(m_ItemIdInput.text,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
            {
                ShowReadFailure("请输入有效的整数编号。");
                return;
            }

            RefreshItem(itemId);
        }

        /// <summary>检查指定表并按编号匹配唯一行，再更新详情与读取状态。</summary>
        /// <param name="itemId">要读取的物品编号，不能当作 List 表的行索引。</param>
        private void RefreshItem(int itemId)
        {
            if (Nova.Table == null || !Nova.Table.HasTable<TbDemo_Item>())
            {
                ShowReadFailure("未加载物品数据。");
                return;
            }

            TbDemo_Item table = Nova.Table.GetTable<TbDemo_Item>();
            if (table == null)
            {
                ShowReadFailure("未加载物品数据。");
                return;
            }

            if (!TryFindItemById(table, itemId, out Demo_Item item, out bool duplicate))
            {
                ShowReadFailure(duplicate ? $"物品编号重复：{itemId}。" : $"未找到物品编号：{itemId}。");
                return;
            }

            if (m_ItemDetailsText != null)
            {
                m_ItemDetailsText.text = $"编号：{item.Id}\n名称：{item.Name}\n图标地址：{item.Icon}\n价格：{item.Price}";
            }

            string message = $"已读取物品编号：{itemId}。";
            if (m_StatusText != null)
            {
                m_StatusText.text = message;
            }

            AppendFeedback(message, FeedbackLevel.Success);
        }

        /// <summary>
        /// 扫描 List 表的 Id 字段并要求唯一匹配；不会将物品编号用作行索引。
        /// </summary>
        /// <param name="table">已确认可用的物品表。</param>
        /// <param name="itemId">需要匹配的物品编号。</param>
        /// <param name="item">唯一匹配行；没有或重复匹配时为空。</param>
        /// <param name="duplicate">是否发现至少两行使用相同编号。</param>
        /// <returns>只有找到一行匹配时返回 true。</returns>
        private static bool TryFindItemById(TbDemo_Item table, int itemId, out Demo_Item item, out bool duplicate)
        {
            item = null;
            duplicate = false;
            for (int i = 0; i < table.DataList.Count; i++)
            {
                Demo_Item row = table.DataList[i];
                if (row == null || row.Id != itemId)
                {
                    continue;
                }

                if (item != null)
                {
                    item = null;
                    duplicate = true;
                    return false;
                }

                item = row;
            }

            return item != null;
        }

        /// <summary>清空上次成功详情，并把本次错误显示到状态区和反馈区。</summary>
        /// <param name="message">本次读取失败的用户可读原因。</param>
        private void ShowReadFailure(string message)
        {
            if (m_ItemDetailsText != null)
            {
                m_ItemDetailsText.text = string.Empty;
            }

            if (m_StatusText != null)
            {
                m_StatusText.text = message;
            }

            AppendFeedback(message, FeedbackLevel.Error);
        }
    }
}
