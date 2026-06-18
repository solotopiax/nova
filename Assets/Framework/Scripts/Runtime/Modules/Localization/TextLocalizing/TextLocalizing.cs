/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizing.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   文本本地化组件
 ***************************************************************/

using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文本本地化组件。
    /// 挂载在 TextMeshProUGUI 节点上，通过事件驱动自动刷新本地化文本和字体。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [AddComponentMenu("UI/Nova/TextLocalizing")]
    public sealed partial class TextLocalizing : MonoBehaviour
    {
        /// <summary>
        /// 获取文本组件引用并缓存管理器引用。
        /// </summary>
        private void Awake()
        {
            m_TextMeshProUGUI = GetComponent<TextMeshProUGUI>();
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_LocalizationManager = FrameworkManagersGroup.GetManager<ILocalizationManager>();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
        }

        /// <summary>
        /// 启用时订阅事件并刷新文本和字体。
        /// </summary>
        private void OnEnable()
        {
            if (m_TextMeshProUGUI == null)
            {
                Log.Warning(LogTag.Localization, "TextLocalizing 所在节点 '{0}' 无有效的 TextMeshProUGUI 组件。", gameObject.name);
                return;
            }

            if (m_EventManager != null)
            {
                m_EventManager.Subscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
                m_IsSubscribed = true;
            }

            RefreshText();
            RefreshFont();
        }

        /// <summary>
        /// 禁用时取消事件订阅，并释放已加载的字体和材质资源。
        /// </summary>
        private void OnDisable()
        {
            if (m_IsSubscribed && m_EventManager != null)
            {
                m_EventManager.Unsubscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
                m_IsSubscribed = false;
            }

            m_LoadedFontHandle?.Release();
            m_LoadedFontHandle = null;
            m_LoadedMaterialHandle?.Release();
            m_LoadedMaterialHandle = null;
        }

        /// <summary>
        /// 运行时动态设置本地化键名并立即刷新文本。
        /// </summary>
        /// <param name="keyName">本地化键名。</param>
        public void SetKeyName(string keyName)
        {
            if (m_LocalizingKeyName == keyName)
            {
                return;
            }

            m_LocalizingKeyName = keyName;
            RefreshText();
        }

        /// <summary>
        /// 运行时动态设置字体标记并立即刷新字体。
        /// </summary>
        /// <param name="fontMark">字体标记。</param>
        public void SetFontMark(string fontMark)
        {
            if (m_LocalizingFontMark == fontMark)
            {
                return;
            }

            m_LocalizingFontMark = fontMark;
            RefreshFont();
        }
    }
}
