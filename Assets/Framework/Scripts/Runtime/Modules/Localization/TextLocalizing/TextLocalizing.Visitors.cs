/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizing.Visitors.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   文本本地化组件-访问器
 ***************************************************************/

using UnityEngine;
using TMPro;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文本本地化组件。
    /// </summary>
    public sealed partial class TextLocalizing : MonoBehaviour
    {
        /// <summary>
        /// 本地化键名，对应语言 JSON 中的 Key。
        /// </summary>
        [Tooltip("本地化键名，对应语言 JSON 中的 Key")]
        [SerializeField]
        private string m_LocalizingKeyName;
        public string KeyName => m_LocalizingKeyName;

        /// <summary>
        /// 字体标记（如 "Main"、"Special"），用于匹配字体配置。
        /// </summary>
        [Tooltip("字体标记，用于匹配 ILocalizationFontRow 中的 Mark 字段")]
        [SerializeField]
        private string m_LocalizingFontMark;
        public string FontMark => m_LocalizingFontMark;

        /// <summary>
        /// TextMeshProUGUI 组件缓存。
        /// </summary>
        private TextMeshProUGUI m_TextMeshProUGUI;

        /// <summary>
        /// 字体刷新前缓存的原始字号（用于 FontSizeScaleRatio 缩放）。
        /// </summary>
        private float m_OriginalFontSize = -1f;

        /// <summary>
        /// 是否已订阅本地化刷新事件（用于安全取消订阅）。
        /// </summary>
        private bool m_IsSubscribed;

        /// <summary>
        /// 事件管理器引用。
        /// </summary>
        private IEventManager m_EventManager;

        /// <summary>
        /// 本地化管理器引用。
        /// </summary>
        private ILocalizationManager m_LocalizationManager;

        /// <summary>
        /// 资源管理器引用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// 当前已加载的字体资源句柄，语言切换时释放旧句柄。
        /// </summary>
        private IAssetHandle<UnityEngine.Object> m_LoadedFontHandle;

        /// <summary>
        /// 当前已加载的材质资源句柄，语言切换时释放旧句柄。
        /// </summary>
        private IAssetHandle<Material> m_LoadedMaterialHandle;
    }
}
