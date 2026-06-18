/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/02
 * descrip:   DemoFirebaseView 演示 View — 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Firebase.Samples.Runtime
{
    /// <summary>
    /// DemoFirebaseView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoFirebaseView
    {
        /// <summary>
        /// 点击后从 FirebasePlugin 获取 Analytics Instance ID。
        /// </summary>
        [SerializeField] private Button m_GetInstanceIdButton;

        /// <summary>
        /// 点击后从 FirebasePlugin 获取 Token。
        /// </summary>
        [SerializeField] private Button m_GetTokenButton;

        /// <summary>
        /// 待发送到 Firebase 的打点事件名输入框。
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
        /// 点击后将事件名和当前参数发送到 Firebase。
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
