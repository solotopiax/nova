/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoPersistView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.12 — Persist 模块演示视图（交互触发型）。
 *            演示 PlayerPrefsManager 键值对读写操作。
 *            API：Nova.Persist.PlayerPrefs.SetInt/GetInt/HasItem/RemoveItem/Save
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Persist 模块演示视图，演示 PlayerPrefsManager 键值读写、存在性查询、删除与显式保存。
    /// 继承 BaseDemoView 三段式骨架，提供 key/value 输入框与五个操作按钮。
    /// </summary>
    public sealed class DemoPersistView : BaseDemoView
    {
        /// <summary>
        /// 键名输入框。
        /// </summary>

        [SerializeField] private TMP_InputField m_KeyInput;

        /// <summary>
        /// 值输入框（int）。
        /// </summary>

        [SerializeField] private TMP_InputField m_ValueInput;

        /// <summary>
        /// Set 按钮，调用 SetInt 写入键值对。
        /// </summary>

        [SerializeField] private Button m_SetButton;

        /// <summary>
        /// Get 按钮，调用 GetInt 读取键值。
        /// </summary>

        [SerializeField] private Button m_GetButton;

        /// <summary>
        /// Has 按钮，调用 HasItem 检查键是否存在。
        /// </summary>

        [SerializeField] private Button m_HasButton;

        /// <summary>
        /// Delete 按钮，调用 RemoveItem 删除键。
        /// </summary>

        [SerializeField] private Button m_DeleteButton;

        /// <summary>
        /// Save 按钮，显式保存 Demo 分类；默认 Unity 后端会提交整个进程的偏好缓存。
        /// </summary>

        [SerializeField] private Button m_SaveButton;

        /// <summary>
        /// 键值列表显示容器。
        /// </summary>

        [SerializeField] private Transform m_KvListRoot;

        /// <summary>
        /// 键值列表行模板（disable 状态）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_KvItemTemplate;

        /// <summary>
        /// 演示使用的 classify 分组名。
        /// </summary>
        private const string c_DemoClassify = "Demo";

        /// <summary>
        /// 已实例化的键值列表行，刷新时销毁重建。
        /// </summary>
        private readonly List<TextMeshProUGUI> m_KvItems = new List<TextMeshProUGUI>();

        /// <summary>
        /// 视图首次初始化时一次注册五个按钮事件，设置标题与就近 API 提示。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Persist 演示");

            if (m_KvItemTemplate != null)
            {
                m_KvItemTemplate.gameObject.SetActive(false);
            }

            if (m_SetButton != null)
            {
                m_SetButton.onClick.AddListener(OnSetButtonClick);
                SetButtonApiHint(m_SetButton, "Nova.Persist.PlayerPrefs.SetInt(classify, key, v)");
            }

            if (m_GetButton != null)
            {
                m_GetButton.onClick.AddListener(OnGetButtonClick);
                SetButtonApiHint(m_GetButton, "Nova.Persist.PlayerPrefs.GetInt(classify, key)");
            }

            if (m_HasButton != null)
            {
                m_HasButton.onClick.AddListener(OnHasButtonClick);
                SetButtonApiHint(m_HasButton, "Nova.Persist.PlayerPrefs.HasItem(classify, key)");
            }

            if (m_DeleteButton != null)
            {
                m_DeleteButton.onClick.AddListener(OnDeleteButtonClick);
                SetButtonApiHint(m_DeleteButton, "Nova.Persist.PlayerPrefs.RemoveItem(classify, key)");
            }

            if (m_SaveButton != null)
            {
                m_SaveButton.onClick.AddListener(OnSaveButtonClick);
                SetButtonApiHint(m_SaveButton, "Nova.Persist.PlayerPrefs.Save(classify)\n默认后端保存整个进程的偏好缓存");
            }
        }

        /// <summary>
        /// 视图打开：刷新键值列表。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            RefreshKvList();
        }

        /// <summary>
        /// Set 按钮点击：将 KeyInput 与 ValueInput 作为 int 写入 PlayerPrefs。
        /// </summary>
        private void OnSetButtonClick()
        {
            if (Nova.Persist?.PlayerPrefs == null)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs 不可用", FeedbackLevel.Error);
                return;
            }

            string key = m_KeyInput != null ? m_KeyInput.text : string.Empty;
            string valueStr = m_ValueInput != null ? m_ValueInput.text : "0";

            if (string.IsNullOrWhiteSpace(key))
            {
                AppendFeedback("键名不能为空", FeedbackLevel.Warn);
                return;
            }

            if (!int.TryParse(valueStr, out int intValue))
            {
                AppendFeedback($"值 \"{valueStr}\" 无法解析为 int", FeedbackLevel.Warn);
                return;
            }

            Nova.Persist.PlayerPrefs.SetInt(c_DemoClassify, key, intValue);
            AppendFeedback($"Nova.Persist.PlayerPrefs.SetInt(\"{c_DemoClassify}\", \"{key}\", {intValue}) -> ok", FeedbackLevel.Success);
            RefreshKvList();
        }

        /// <summary>
        /// Get 按钮点击：读取 KeyInput 对应的 int 值并打印到反馈区。
        /// </summary>
        private void OnGetButtonClick()
        {
            if (Nova.Persist?.PlayerPrefs == null)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs 不可用", FeedbackLevel.Error);
                return;
            }

            string key = m_KeyInput != null ? m_KeyInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                AppendFeedback("键名不能为空", FeedbackLevel.Warn);
                return;
            }

            bool exists = Nova.Persist.PlayerPrefs.HasItem(c_DemoClassify, key);
            int value = Nova.Persist.PlayerPrefs.GetInt(c_DemoClassify, key, 0);
            AppendFeedback($"Nova.Persist.PlayerPrefs.GetInt(\"{c_DemoClassify}\", \"{key}\") -> {value} (exists={exists})", exists ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// Has 按钮点击：检查 KeyInput 对应的键是否存在。
        /// </summary>
        private void OnHasButtonClick()
        {
            if (Nova.Persist?.PlayerPrefs == null)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs 不可用", FeedbackLevel.Error);
                return;
            }

            string key = m_KeyInput != null ? m_KeyInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                AppendFeedback("键名不能为空", FeedbackLevel.Warn);
                return;
            }

            bool exists = Nova.Persist.PlayerPrefs.HasItem(c_DemoClassify, key);
            AppendFeedback($"Nova.Persist.PlayerPrefs.HasItem(\"{c_DemoClassify}\", \"{key}\") -> {exists}", exists ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// Delete 按钮点击：删除 KeyInput 对应的键。
        /// </summary>
        private void OnDeleteButtonClick()
        {
            if (Nova.Persist?.PlayerPrefs == null)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs 不可用", FeedbackLevel.Error);
                return;
            }

            string key = m_KeyInput != null ? m_KeyInput.text : string.Empty;

            if (string.IsNullOrWhiteSpace(key))
            {
                AppendFeedback("键名不能为空", FeedbackLevel.Warn);
                return;
            }

            bool existed = Nova.Persist.PlayerPrefs.HasItem(c_DemoClassify, key);
            Nova.Persist.PlayerPrefs.RemoveItem(c_DemoClassify, key);
            AppendFeedback($"Nova.Persist.PlayerPrefs.RemoveItem(\"{c_DemoClassify}\", \"{key}\") -> removed={existed}", existed ? FeedbackLevel.Success : FeedbackLevel.Warn);
            RefreshKvList();
        }

        /// <summary>
        /// 仅由保存按钮触发 Demo 分类保存；默认 Unity 后端仍提交全进程偏好缓存。
        /// 返回值仅报告底层调用完成情况，不证明跨启动耐久性；异常只反馈固定提示，不输出数据或异常正文、不自动重试。
        /// </summary>
        private void OnSaveButtonClick()
        {
            if (Nova.Persist?.PlayerPrefs == null)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs 不可用", FeedbackLevel.Error);
                return;
            }

            AppendFeedback("Nova.Persist.PlayerPrefs.Save(\"Demo\")：默认 Unity 后端会保存整个进程的偏好缓存，包含其它尚未保存的偏好修改。", FeedbackLevel.Info);
            bool saved;
            try
            {
                saved = Nova.Persist.PlayerPrefs.Save(c_DemoClassify);
            }
            catch (System.Exception)
            {
                AppendFeedback("Nova.Persist.PlayerPrefs.Save(\"Demo\") -> 保存发生异常；持久化状态未确认，请核对后再决定是否重试。", FeedbackLevel.Error);
                return;
            }

            AppendFeedback($"Nova.Persist.PlayerPrefs.Save(\"{c_DemoClassify}\") -> 返回 {saved}；仅为底层保存报告，重启后读取需另行验证。",
                saved ? FeedbackLevel.Info : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 刷新当前 Demo classify 下的所有键值列表。
        /// </summary>
        private void RefreshKvList()
        {
            ClearKvItems();

            if (Nova.Persist?.PlayerPrefs == null || m_KvItemTemplate == null || m_KvListRoot == null)
            {
                return;
            }

            string[] keys = Nova.Persist.PlayerPrefs.GetAllItemNames(c_DemoClassify);
            if (keys == null || keys.Length == 0)
            {
                return;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                int val = Nova.Persist.PlayerPrefs.GetInt(c_DemoClassify, k, 0);

                TextMeshProUGUI item = Instantiate(m_KvItemTemplate, m_KvListRoot);
                item.gameObject.SetActive(true);
                item.text = $"{k} = {val}";
                item.color = new Color32(0xCC, 0xCC, 0xCC, 0xFF);
                m_KvItems.Add(item);
            }
        }

        /// <summary>
        /// 销毁所有已实例化的键值列表行并清空缓存列表。
        /// </summary>
        private void ClearKvItems()
        {
            for (int i = 0; i < m_KvItems.Count; i++)
            {
                if (m_KvItems[i] != null)
                {
                    Destroy(m_KvItems[i].gameObject);
                }
            }

            m_KvItems.Clear();
        }
    }
}
