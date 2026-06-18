/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoSDKView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.15 — SDK 模块演示视图（只读快照型）。
 *            展示已注册 SDK 插件列表（MainDemo 默认 0 个插件）。
 *            API：Nova.SDK.GetAll<ISDKPlugin>() / TryGet<TPlugin>(out p)
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// SDK 模块演示视图，展示已注册插件的名称、优先级、可用状态与类型。
    /// MainDemo 默认无任何 SDKPlugin，以零插件态展示正常信息流。
    /// 继承 BaseDemoView 三段式骨架，提供刷新按钮与插件列表。
    /// </summary>
    public sealed class DemoSDKView : BaseDemoView
    {
        /// <summary>
        /// 刷新插件列表按钮。
        /// </summary>

        [SerializeField] private Button m_RefreshButton;

        /// <summary>
        /// 插件列表容器。
        /// </summary>

        [SerializeField] private Transform m_PluginListRoot;

        /// <summary>
        /// 插件列表行模板（disable 状态）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_PluginItemTemplate;

        /// <summary>
        /// 已实例化的插件列表行，刷新时销毁重建。
        /// </summary>
        private readonly List<TextMeshProUGUI> m_PluginItems = new List<TextMeshProUGUI>();

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("SDK 演示");

            if (m_PluginItemTemplate != null)
            {
                m_PluginItemTemplate.gameObject.SetActive(false);
            }

            if (m_RefreshButton != null)
            {
                m_RefreshButton.onClick.AddListener(OnRefreshButtonClick);
                SetButtonApiHint(m_RefreshButton, "Nova.SDK.GetAll<ISDKPlugin>() / TryGet<TPlugin>(out plugin)");
            }
        }

        /// <summary>
        /// 视图打开：自动刷新插件快照。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshPluginList();
        }

        /// <summary>
        /// 刷新按钮点击：重新读取插件列表。
        /// </summary>
        private void OnRefreshButtonClick()
        {
            RefreshPluginList();
        }

        /// <summary>
        /// 刷新插件列表，调用 GetAll 获取所有 ISDKPlugin 并渲染到列表区域。
        /// </summary>
        private void RefreshPluginList()
        {
            ClearPluginItems();

            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用", FeedbackLevel.Error);
                return;
            }

            IReadOnlyList<ISDKPlugin> plugins = Nova.SDK.GetAll<ISDKPlugin>();
            int count = plugins != null ? plugins.Count : 0;

            if (count == 0)
            {
                AppendFeedback("Nova.SDK.GetAll<ISDKPlugin>() -> 0 plugins（MainDemo 未启用任何 SDK 插件）", FeedbackLevel.Warn);
                return;
            }

            for (int i = 0; i < plugins.Count; i++)
            {
                ISDKPlugin plugin = plugins[i];
                if (plugin == null)
                {
                    continue;
                }

                AddPluginItem(plugin);
            }

            AppendFeedback($"Nova.SDK.GetAll<ISDKPlugin>() -> {count} plugins", FeedbackLevel.Success);
        }

        /// <summary>
        /// 向插件列表容器中添加一行插件条目。
        /// </summary>
        /// <param name="plugin">ISDKPlugin 插件实例。</param>
        private void AddPluginItem(ISDKPlugin plugin)
        {
            if (m_PluginItemTemplate == null || m_PluginListRoot == null)
            {
                return;
            }

            TextMeshProUGUI item = Instantiate(m_PluginItemTemplate, m_PluginListRoot);
            item.gameObject.SetActive(true);
            item.text = $"{plugin.Name}  Priority={plugin.Priority}  Available={plugin.IsAvailable}  [{plugin.GetType().Name}]";
            item.color = plugin.IsAvailable ? new Color32(0x4C, 0xAF, 0x50, 0xFF) : new Color32(0xFF, 0xB3, 0x00, 0xFF);
            m_PluginItems.Add(item);
        }

        /// <summary>
        /// 销毁所有已实例化的插件列表行并清空缓存列表。
        /// </summary>
        private void ClearPluginItems()
        {
            for (int i = 0; i < m_PluginItems.Count; i++)
            {
                if (m_PluginItems[i] != null)
                {
                    Destroy(m_PluginItems[i].gameObject);
                }
            }

            m_PluginItems.Clear();
        }
    }
}
