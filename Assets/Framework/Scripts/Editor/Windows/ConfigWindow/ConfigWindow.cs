/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Nova 全局环境配置窗口
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using static NovaFramework.Editor.EditorUtil.Environment.LubanChecker;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 全局环境配置窗口，集中展示与管理框架层级的各类环境检测和全局配置信息。
    /// </summary>
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 菜单入口：打开环境配置窗口。
        /// </summary>
        [MenuItem(c_MenuPath)]
        public static void Open()
        {
            ConfigWindow window = GetWindow<ConfigWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
        }

        /// <summary>
        /// 打开应用配置面板并切换到启动 Guard 报告的平台、渠道与模式。
        /// </summary>
        /// <param name="master">当前 Demo 的设计态 ConfigMasterSO 来源。</param>
        /// <param name="platform">运行时导出的目标平台。</param>
        /// <param name="channel">运行时导出的目标渠道。</param>
        /// <param name="developMode">运行时导出的开发模式。</param>
        public static void OpenAppConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode)
        {
            OpenConfigSection(master, platform, channel, developMode, LeftTreeItem.AppConfig, null);
        }

        /// <summary>
        /// 打开隐私配置面板并切换到指定平台、渠道与模式。
        /// </summary>
        /// <param name="master">设计态 ConfigMasterSO 来源。</param>
        /// <param name="platform">目标平台。</param>
        /// <param name="channel">目标渠道。</param>
        /// <param name="developMode">目标开发模式。</param>
        public static void OpenPrivacyConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode)
        {
            OpenConfigSection(master, platform, channel, developMode, LeftTreeItem.PrivacyConfig, null);
        }

        /// <summary>
        /// 打开名字空间配置面板并切换到启动 Guard 报告的平台、渠道与模式。
        /// </summary>
        public static void OpenNamespaceConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode)
        {
            OpenConfigSection(master, platform, channel, developMode, LeftTreeItem.NamespaceConfig, null);
        }

        /// <summary>
        /// 打开指定 SDK 配置面板并切换到启动 Guard 报告的平台、渠道与模式。
        /// </summary>
        public static void OpenSDKConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode, Type configType)
        {
            OpenConfigSection(master, platform, channel, developMode, LeftTreeItem.SDKNode, configType);
        }

        /// <summary>
        /// 打开指定 Kit 配置面板并切换到启动 Guard 报告的平台、渠道与模式。
        /// </summary>
        public static void OpenKitConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode, Type configType)
        {
            OpenConfigSection(master, platform, channel, developMode, LeftTreeItem.KitNode, configType);
        }

        /// <summary>
        /// 按 Guard 报告的坐标统一绑定设计态来源、编辑平台、渠道/模式与左树目标。
        /// </summary>
        private static ConfigWindow OpenConfigSection(ConfigMasterSO master, PlatformType platform,
            ChannelType channel, DevelopMode developMode, LeftTreeItem target, Type configType)
        {
            ConfigWindow window = GetWindow<ConfigWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
            if (master != null && !ReferenceEquals(window.m_Master, master))
            {
                window.RebindMaster(master);
            }

            if (window.m_WorkingCopy != null)
            {
                if (platform != PlatformType.None) window.m_EditingPlatform = platform;
                window.m_WorkingCopy.CurrentChannel = channel;
                window.m_WorkingCopy.CurrentDevelopMode = developMode;
                window.m_LastKnownChannel = channel;
            }
            window.m_GroupExpandedCommon = target == LeftTreeItem.AppConfig ||
                                           target == LeftTreeItem.PrivacyConfig ||
                                           target == LeftTreeItem.NamespaceConfig ||
                                           window.m_GroupExpandedCommon;
            window.m_GroupExpandedSDK = target == LeftTreeItem.SDKNode || window.m_GroupExpandedSDK;
            window.m_GroupExpandedKit = target == LeftTreeItem.KitNode || window.m_GroupExpandedKit;
            window.m_SelectedItem = target;
            window.m_SelectedPluginType = configType;
            window.Focus();
            window.Repaint();
            return window;
        }

        /// <summary>
        /// 通过 Luban 环境检测结果打开窗口并自动导航到 Luban 面板（Pipeline 调用入口）。
        /// </summary>
        /// <param name="result">Luban 环境检测结果。</param>
        public static void OpenLubanSection(EnvironmentCheckResult result)
        {
            ConfigWindow window = GetWindow<ConfigWindow>(false, c_WindowTitle, true);
            window.minSize = new Vector2(c_WindowMinWidth, c_WindowMinHeight);
            window.m_LubanCheckResult = result;
            window.m_SelectedItem = LeftTreeItem.LubanEnv;
        }

    }
}
