/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoTGAView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   DemoTGAView 演示 View - 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Tga.Samples.Runtime
{
    /// <summary>
    /// DemoTGAView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoTGAView
    {
        /// <summary>
        /// 点击后执行示例登录流程。
        /// </summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 点击后从 TGAPlugin 获取设备 ID。
        /// </summary>
        [SerializeField] private Button m_GetDeviceIdButton;

        /// <summary>
        /// 点击后从 TGAPlugin 获取访客 ID。
        /// </summary>
        [SerializeField] private Button m_GetDistinctIdButton;

        /// <summary>
        /// 点击后从 TGAPlugin 获取预置属性。
        /// </summary>
        [SerializeField] private Button m_GetPresetPropertiesButton;

        /// <summary>
        /// 待发送到 TGA 的打点事件名输入框。
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
        /// 点击后将事件名和当前参数发送到 TGA。
        /// </summary>
        [SerializeField] private Button m_SendEventButton;

        /// <summary>
        /// 点击后对当前事件名调用 TGA 事件计时。
        /// </summary>
        [SerializeField] private Button m_TimeEventButton;

        /// <summary>
        /// 点击后立即 Flush TGA 本地缓存。
        /// </summary>
        [SerializeField] private Button m_FlushButton;

        /// <summary>
        /// 事件参数预览文本；当前 Demo 通过底部反馈区显示添加结果，运行时保持隐藏。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_EventParamsPreviewText;

        /// <summary>
        /// 用户属性 key 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_UserPropertyKeyInput;

        /// <summary>
        /// 用户属性 value 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_UserPropertyValueInput;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserSet。
        /// </summary>
        [SerializeField] private Button m_UserSetButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserSetOnce。
        /// </summary>
        [SerializeField] private Button m_UserSetOnceButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserAdd。
        /// </summary>
        [SerializeField] private Button m_UserAddButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserAppend。
        /// </summary>
        [SerializeField] private Button m_UserAppendButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserUnset。
        /// </summary>
        [SerializeField] private Button m_UserUnsetButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.UserDelete。
        /// </summary>
        [SerializeField] private Button m_UserDeleteButton;

        /// <summary>
        /// 公共属性 key 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_SuperPropertyKeyInput;

        /// <summary>
        /// 公共属性 value 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_SuperPropertyValueInput;

        /// <summary>
        /// 点击后调用 TGAPlugin.SetSuperProperty。
        /// </summary>
        [SerializeField] private Button m_SetSuperPropertyButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.ClearSuperProperties。
        /// </summary>
        [SerializeField] private Button m_ClearSuperPropertiesButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.SetDynamicSuperProperty。
        /// </summary>
        [SerializeField] private Button m_SetDynamicSuperPropertyButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.GetDynamicSuperProperties。
        /// </summary>
        [SerializeField] private Button m_GetDynamicSuperPropertiesButton;

        /// <summary>
        /// 点击后调用 TGAPlugin.ClearDynamicSuperProperties。
        /// </summary>
        [SerializeField] private Button m_ClearDynamicSuperPropertiesButton;

        private readonly Dictionary<string, object> m_EventParams = new Dictionary<string, object>();
    }
}
