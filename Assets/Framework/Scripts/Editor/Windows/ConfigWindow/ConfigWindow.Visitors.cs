/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.Visitors.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Nova 全局环境配置窗口字段声明
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static NovaFramework.Editor.EditorUtil.Environment.HybridCLRChecker;
using static NovaFramework.Editor.EditorUtil.Environment.LubanChecker;
using static NovaFramework.Editor.EditorUtil.Environment.Python3Checker;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 菜单路径。
        /// </summary>
        private const string c_MenuPath = "Nova/Open Config";

        /// <summary>
        /// 窗口标题。
        /// </summary>
        private const string c_WindowTitle = "Nova · Config";

        /// <summary>
        /// 窗口最小宽度。
        /// </summary>
        private const float c_WindowMinWidth = 1000f;

        /// <summary>
        /// 窗口最小高度。
        /// </summary>
        private const float c_WindowMinHeight = 650f;

        /// <summary>
        /// 顶部工具栏高度。
        /// </summary>
        private const float c_TopBarHeight = 60f;

        /// <summary>
        /// 左侧树宽度。
        /// </summary>
        private const float c_LeftTreeWidth = 260f;

        /// <summary>
        /// macOS 安装命令（dotnet-install.sh，推荐安装兼容区间上限版本，精确锁定 c_MaxDotnetVersion）。
        /// </summary>
        private const string c_InstallCmdMac = "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version " + c_MaxDotnetVersion;

        /// <summary>
        /// Windows 安装命令（dotnet-install.ps1 官方脚本，推荐安装兼容区间上限版本，精确锁定 c_MaxDotnetVersion）。
        /// </summary>
        private const string c_InstallCmdWin = "&([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Version " + c_MaxDotnetVersion;

        /// <summary>
        /// Linux 安装命令（dotnet-install.sh，推荐安装兼容区间上限版本，精确锁定 c_MaxDotnetVersion）。
        /// </summary>
        private const string c_InstallCmdLinux = "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version " + c_MaxDotnetVersion;

        /// <summary>
        /// .NET 官方下载页 URL（上限版本所属主版本 .NET 10 下载页）。
        /// </summary>
        private const string c_DotnetDownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";

        /// <summary>
        /// 右侧详情面板滚动位置。
        /// </summary>
        private Vector2 m_RightPanelScrollPos;

        /// <summary>
        /// 左侧树滚动位置。
        /// </summary>
        private Vector2 m_LeftScrollPos;

        /// <summary>
        /// 左侧一级组"环境检测"折叠状态。
        /// </summary>
        private bool m_GroupExpandedEnvironment = true;

        /// <summary>
        /// 左侧一级组"通用配置"折叠状态。
        /// </summary>
        private bool m_GroupExpandedCommon = true;

        /// <summary>
        /// 应用配置面板是否有未保存的改动（窗口关闭即丢弃，不持久化）。
        /// </summary>
        private bool m_IsDirty;

        /// <summary>
        /// 左侧一级组"SDK 配置"折叠状态。
        /// </summary>
        private bool m_GroupExpandedSDK = true;

        /// <summary>
        /// 左侧一级组"Kit 配置"折叠状态。
        /// </summary>
        private bool m_GroupExpandedKit = true;

        /// <summary>
        /// Luban 环境检查结果。
        /// </summary>
        private EnvironmentCheckResult m_LubanCheckResult;

        /// <summary>
        /// Python3 环境检查结果。
        /// </summary>
        private Python3CheckResult m_Python3CheckResult;

        /// <summary>
        /// HybridCLR 环境检查结果。
        /// </summary>
        private HybridCLRCheckResult m_HybridCLRCheckResult;

        /// <summary>
        /// 主标题样式（窗口顶部居中粗体大标题）。
        /// </summary>
        private GUIStyle m_MainTitleStyle;

        /// <summary>
        /// 标题样式（大标题）。
        /// </summary>
        private GUIStyle m_TitleStyle;

        /// <summary>
        /// 小节标题样式。
        /// </summary>
        private GUIStyle m_SectionTitleStyle;

        /// <summary>
        /// 状态标签样式（就绪）。
        /// </summary>
        private GUIStyle m_StatusReadyStyle;

        /// <summary>
        /// 状态标签样式（失败）。
        /// </summary>
        private GUIStyle m_StatusErrorStyle;

        /// <summary>
        /// 命令行代码样式。
        /// </summary>
        private GUIStyle m_CodeStyle;

        /// <summary>
        /// 普通说明文字样式。
        /// </summary>
        private GUIStyle m_DescStyle;

        /// <summary>
        /// 当前激活的真实 ConfigMasterSO 资产引用；m_WorkingCopy 的来源与落盘目标。
        /// </summary>
        private ConfigMasterSO m_Master;

        /// <summary>
        /// 基于 m_Master Instantiate 的内存暂存副本；所有面板编辑均写入此副本，
        /// 点击保存时通过 EditorUtility.CopySerialized 写回 m_Master。
        /// hideFlags = DontSave，不入 AssetDatabase。
        /// </summary>
        private ConfigMasterSO m_WorkingCopy;

        /// <summary>
        /// 与 m_WorkingCopy 绑定的 SerializedObject；面板绘制时通过此对象读写。
        /// </summary>
        private SerializedObject m_MasterSO;

        /// <summary>
        /// 左树当前选中项。
        /// </summary>
        private LeftTreeItem m_SelectedItem = LeftTreeItem.LubanEnv;

        /// <summary>
        /// 左树选中的 SDK Plugin 类型，仅当 m_SelectedItem == SDKNode 时有效。
        /// </summary>
        private Type m_SelectedPluginType;

        /// <summary>
        /// 扫描到的 Plugin Config 条目缓存（含类型 + DisplayName + Tooltip）。
        /// </summary>
        private List<EditorUtil.Config.SDKPluginScanner.PluginConfigEntry> m_PluginTypeCache = new();

        /// <summary>
        /// 扫描到的 Kit Config 条目缓存（含类型 + DisplayName）。
        /// </summary>
        private List<EditorUtil.Config.KitConfigScanner.KitConfigEntry> m_KitTypeCache = new();

        /// <summary>
        /// 上次看到的 CurrentChannel 值，用于触发 Repaint 的轮询对比。
        /// </summary>
        private ChannelType m_LastKnownChannel;

        /// <summary>
        /// ReInjectYooAsset 缓存守卫：上次注入时的 YooAssetSettingsPath 解析结果。
        /// 与后三个坐标字段共同构成四元组，全等则跳过注入（修复 3）。
        /// </summary>
        private string m_CachedInjectSettingsPath;

        /// <summary>
        /// ReInjectYooAsset 缓存守卫：上次注入时的平台。
        /// </summary>
        private PlatformType m_CachedInjectPlatform;

        /// <summary>
        /// ReInjectYooAsset 缓存守卫：上次注入时的渠道。
        /// </summary>
        private ChannelType m_CachedInjectChannel;

        /// <summary>
        /// ReInjectYooAsset 缓存守卫：上次注入时的开发模式。
        /// </summary>
        private DevelopMode m_CachedInjectMode;

        /// <summary>
        /// 坐标切换延迟标志：为 true 时表示本帧 TryApply 已记录待应用的新坐标，
        /// 待 DrawRightPanel 绘制完成（当前编辑字段在旧坐标格子下完成失焦提交）后再应用。
        /// </summary>
        private bool m_HasPendingCoordSwitch;

        /// <summary>
        /// 待应用的目标平台（延迟切坐标，见 m_HasPendingCoordSwitch）。
        /// </summary>
        private PlatformType m_PendingPlatform;

        /// <summary>
        /// 待应用的目标渠道（延迟切坐标，见 m_HasPendingCoordSwitch）。
        /// </summary>
        private ChannelType m_PendingChannel;

        /// <summary>
        /// 待应用的目标开发模式（延迟切坐标，见 m_HasPendingCoordSwitch）。
        /// </summary>
        private DevelopMode m_PendingDevelopMode;

        /// <summary>
        /// HybridCLR 面板：DLL 条目"选择文件夹"按钮宽度。
        /// </summary>
        private const float c_PickButtonWidth = 64f;

        /// <summary>
        /// HybridCLR 面板：DLL 条目"打开文件夹"按钮宽度。
        /// </summary>
        private const float c_RevealButtonWidth = 96f;

        /// <summary>
        /// HybridCLR 面板：AOT 元数据 DLL 列表的 ReorderableList 控件。
        /// </summary>
        private ReorderableList m_HybridCLRAotMetadataDllsList;

        /// <summary>
        /// HybridCLR 面板：业务 DLL 列表的 ReorderableList 控件。
        /// </summary>
        private ReorderableList m_HybridCLRGameDllsList;

        /// <summary>
        /// HybridCLR 面板：AOT 元数据 DLL 列表各条目的折叠状态（按 index，默认全部收缩）。
        /// </summary>
        private List<bool> m_AotDllFoldouts = new List<bool>();

        /// <summary>
        /// HybridCLR 面板：业务 DLL 列表各条目的折叠状态（按 index，默认全部收缩）。
        /// </summary>
        private List<bool> m_GameDllFoldouts = new List<bool>();

        /// <summary>
        /// 左侧树一级组枚举。
        /// </summary>
        private enum LeftTreeGroup
        {
            /// <summary>
            /// 环境检测组。
            /// </summary>
            Environment,

            /// <summary>
            /// 通用配置组。
            /// </summary>
            Common,

            /// <summary>
            /// SDK 配置组。
            /// </summary>
            SDK,

            /// <summary>
            /// Kit 配置组。
            /// </summary>
            Kit,
        }

        /// <summary>
        /// 左侧树二级节点枚举。
        /// </summary>
        private enum LeftTreeItem
        {
            /// <summary>
            /// Luban 环境检测面板（环境检测组下）。
            /// </summary>
            LubanEnv,

            /// <summary>
            /// Python3 环境检测面板（环境检测组下）。
            /// </summary>
            Python3Env,

            /// <summary>
            /// HybridCLR 环境检测面板（环境检测组下）。
            /// </summary>
            HybridCLREnv,

            /// <summary>
            /// 应用配置面板（通用配置组下）。
            /// </summary>
            AppConfig,

            /// <summary>
            /// 名字空间配置面板（通用配置组下）。
            /// </summary>
            NamespaceConfig,

            /// <summary>
            /// SDK Plugin 节点面板。
            /// </summary>
            SDKNode,

            /// <summary>
            /// HybridCLR 配置面板（通用配置组下）。
            /// </summary>
            HybridCLRConfig,

            /// <summary>
            /// YooAsset 配置面板（通用配置组下）。
            /// </summary>
            YooAssetConfig,

            /// <summary>
            /// Kit 配置节点面板。
            /// </summary>
            KitNode,
        }
    }
}
