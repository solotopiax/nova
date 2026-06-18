/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameLoginView.Visitors.cs
 * author:    taoye
 * created:   2026/06/01
 * descrip:   Login Kit 演示 View — 字段与属性
 ***************************************************************/

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// Login Kit 演示 View，展示登录与登出 API 的调用方式。
    /// </summary>
    public sealed partial class DemoGameLoginView
    {
        /// <summary>
        /// openId 输入框；用户在此填入第三方平台返回的用户唯一标识。
        /// </summary>

        [SerializeField] private TMP_InputField m_OpenIdInput;

        /// <summary>
        /// forceNewAccount 开关；勾选后登录时强制注册新账号。
        /// </summary>

        [SerializeField] private Toggle m_ForceNewAccountToggle;

        /// <summary>
        /// 登录按钮；点击后触发 Nova.Network.Kit<Login>().Async(openId, forceNewAccount) 调用。
        /// </summary>

        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 清空按钮；点击后触发 Nova.Network.Kit<Login>().Clear() 调用，清空本地 UID。
        /// </summary>

        [SerializeField] private Button m_ClearButton;

        /// <summary>
        /// 删除账号按钮；点击后触发 Nova.Network.Kit<Login>().DeleteAsync() 调用，
        /// 删除当前登录账号并自动清空本地 UID。
        /// </summary>

        [SerializeField] private Button m_DeleteButton;
    }
}
