/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView 演示 View — 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// DemoIAPView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoIAPView
    {
        /// <summary>
        /// 登录按钮；点击后置灰并展开 4 个支付按钮（复用脚手架生成的示例按钮槽位）。
        /// </summary>
        [SerializeField] private Button m_SampleButton;

        /// <summary>
        /// 普通支付（消耗品）商品表行 id 列表，对应 ConfigRuntime 中 type=Consumable 的两条商品。
        /// </summary>
        private static readonly long[] s_NormalProductIds = { 1L, 2L };

        /// <summary>
        /// 订阅支付商品表行 id 列表，对应 ConfigRuntime 中 type=Subscription 的两条商品。
        /// </summary>
        private static readonly long[] s_SubscriptionProductIds = { 3L, 4L };

        /// <summary>
        /// 恢复购买按钮运行时名称。
        /// </summary>
        private const string c_RestorePurchasesButtonName = "RestorePurchasesButton";

        /// <summary>
        /// 运行时实例化的支付按钮列表，登录后由示例按钮克隆生成，用于统一管理。
        /// </summary>
        private readonly List<Button> m_PayButtons = new List<Button>();

        /// <summary>
        /// 是否已登录；登录后禁止重复展开支付按钮。
        /// </summary>
        private bool m_LoggedIn;

        /// <summary>
        /// 是否正在登录；防止快速连点在 await 返回前重复发起登录。
        /// </summary>
        private bool m_LoginInProgress;

        /// <summary>
        /// IAP 胶水层；View 只调用此对象，不直接访问 IAP 插件。
        /// </summary>
        private DemoIAPBridge m_IapBridge;
    }
}
