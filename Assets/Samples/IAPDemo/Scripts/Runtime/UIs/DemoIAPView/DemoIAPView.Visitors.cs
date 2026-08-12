/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   统一 IAP 演示 View 的通用 Tab、Panel 壳与动态模块字段
 ***************************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 统一 IAP 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoIAPView
    {
        /// <summary>
        /// 登录成功前显示的原尺寸登录按钮。
        /// </summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 登录成功后显示的动态 Tab 与商店 Panel 根节点。
        /// </summary>
        [SerializeField] private GameObject m_AuthenticatedContent;

        /// <summary>
        /// 移动支付通用 Tab 按钮。
        /// </summary>
        [SerializeField] private Button m_MobileTabButton;

        /// <summary>
        /// 第三方支付通用 Tab 按钮。
        /// </summary>
        [SerializeField] private Button m_ThirdPayTabButton;

        /// <summary>
        /// 金券支付通用 Tab 按钮。
        /// </summary>
        [SerializeField] private Button m_VoucherTabButton;

        /// <summary>
        /// 移动支付 Tab 选中指示条。
        /// </summary>
        [SerializeField] private GameObject m_MobileTabIndicator;

        /// <summary>
        /// 第三方支付 Tab 选中指示条。
        /// </summary>
        [SerializeField] private GameObject m_ThirdPayTabIndicator;

        /// <summary>
        /// 金券支付 Tab 选中指示条。
        /// </summary>
        [SerializeField] private GameObject m_VoucherTabIndicator;

        /// <summary>
        /// 不引用 Mobile package 的移动支付 Panel 壳。
        /// </summary>
        [SerializeField] private DemoIAPMobilePanelView m_MobilePanel;

        /// <summary>
        /// 不引用 ThirdPay package 的第三方支付 Panel 壳。
        /// </summary>
        [SerializeField] private DemoIAPThirdPayPanelView m_ThirdPayPanel;

        /// <summary>
        /// 不引用 Voucher package 的金券支付 Panel 壳。
        /// </summary>
        [SerializeField] private DemoIAPVoucherPanelView m_VoucherPanel;

        /// <summary>
        /// 三个可选支付包均未安装时显示的占位提示。
        /// </summary>
        [SerializeField] private TMP_Text m_NoStorePackageText;

        /// <summary>
        /// Tab 选中态深蓝背景色。
        /// </summary>
        private static readonly Color s_ActiveTabBackgroundColor = new Color32(0x1E, 0x40, 0xAF, 0xFF);

        /// <summary>
        /// Tab 未选中态灰蓝背景色。
        /// </summary>
        private static readonly Color s_InactiveTabBackgroundColor = new Color32(0xD7, 0xE0, 0xEA, 0xFF);

        /// <summary>
        /// Tab 未选中态深灰文字色。
        /// </summary>
        private static readonly Color s_InactiveTabTextColor = new Color32(0x33, 0x41, 0x55, 0xFF);

        /// <summary>
        /// 当前运行环境成功发现并初始化的商店模块。
        /// </summary>
        private readonly List<IDemoIAPStoreModule> m_StoreModules = new List<IDemoIAPStoreModule>();

        /// <summary>
        /// 不依赖具体支付包的 Core IAP 桥接层。
        /// </summary>
        private DemoIAPBridge m_IapBridge;

        /// <summary>
        /// 当前生命周期是否已经完成一次商店模块发现。
        /// </summary>
        private bool m_StoreModulesInitialized;

        /// <summary>
        /// 当前演示账号是否已登录。
        /// </summary>
        private bool m_LoggedIn;

        /// <summary>
        /// 当前是否正在等待登录结果。
        /// </summary>
        private bool m_LoginInProgress;
    }
}
