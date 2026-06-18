/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppsflyerView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoAppsflyerView 演示 View — 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Appsflyer.Samples.Runtime
{
    /// <summary>
    /// DemoAppsflyerView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoAppsflyerView
    {
        /// <summary>
        /// 点击后从 AppsFlyerPlugin 获取 AppsFlyer ID。
        /// </summary>
        [SerializeField] private Button m_GetAppsFlyerIdButton;

        /// <summary>
        /// 点击后从 AppsFlyerPlugin 获取归因数据。
        /// </summary>
        [SerializeField] private Button m_GetConversionDataButton;

        /// <summary>
        /// 待发送到 AppsFlyer 的打点事件名输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventNameInput;

        /// <summary>
        /// 待添加打点参数的 key 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventParamKeyInput;

        /// <summary>
        /// 待添加打点参数的 value 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventParamValueInput;

        /// <summary>
        /// 点击后将当前 key/value 输入加入待发送打点参数。
        /// </summary>
        [SerializeField] private Button m_AddEventParamButton;

        /// <summary>
        /// 点击后清空当前待发送打点参数。
        /// </summary>
        [SerializeField] private Button m_ClearEventParamsButton;

        /// <summary>
        /// 点击后将事件名和当前参数发送到 AppsFlyer。
        /// </summary>
        [SerializeField] private Button m_SendEventButton;

        /// <summary>
        /// 点击后执行示例登录流程。
        /// </summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 用于显示当前待发送打点参数的预览文本。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_EventParamsPreviewText;

        private readonly Dictionary<string, object> m_EventParams = new Dictionary<string, object>();
    }
}
