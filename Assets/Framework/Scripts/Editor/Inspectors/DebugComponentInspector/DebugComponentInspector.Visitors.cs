/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   Debug 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class DebugComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 磁盘检测配置数组属性。
        /// </summary>
        private SerializedProperty m_DiskCheckingConfigs;

        /// <summary>
        /// Debugger 激活类型属性。
        /// </summary>
        private SerializedProperty m_DebuggerActiveType;

        /// <summary>
        /// Console 最大日志条数属性。
        /// </summary>
        private SerializedProperty m_MaximumConsoleEntries;

        /// <summary>
        /// DebugManager 类型名称属性。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// DebugManager 可选实现类型名称列表。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// 调试器配置文件相对工程根目录的路径。
        /// </summary>
        private const string c_ConfigFileRelPath = "../Library/Nova/DebugConfig.json";

        /// <summary>
        /// AAB 安装脚本在 UPM 包内的路径。
        /// </summary>
        private const string c_InstallAABScriptPackagePath = "Packages/com.solotopia.nova.framework/Scripts/Editor/Tools/Deploys/install_aab.py";

        /// <summary>
        /// APK 安装脚本在 UPM 包内的路径。
        /// </summary>
        private const string c_InstallAPKScriptPackagePath = "Packages/com.solotopia.nova.framework/Scripts/Editor/Tools/Deploys/install_apk.py";

        /// <summary>
        /// AAB 默认输出目录相对工程根目录的路径。
        /// </summary>
        private const string c_AABOutputRelDir = "../Build/Android/AAB";

        /// <summary>
        /// APK 默认输出目录相对工程根目录的路径。
        /// </summary>
        private const string c_APKOutputRelDir = "../Build/Android/APK";

        /// <summary>
        /// Android 签名文件绝对路径。
        /// </summary>
        private string m_AndroidSignaturePath;

        /// <summary>
        /// Android 签名密钥库密码。
        /// </summary>
        private string m_AndroidSignaturePass;

        /// <summary>
        /// Android 签名 Alias 名称。
        /// </summary>
        private string m_AndroidSignatureAlias;

        /// <summary>
        /// Android 签名 Alias 密码。
        /// </summary>
        private string m_AndroidSignatureAliasPass;

        /// <summary>
        /// bundletool.jar 绝对路径。
        /// </summary>
        private string m_BundleToolPath;

        /// <summary>
        /// adb 可执行文件绝对路径。
        /// </summary>
        private string m_ADBPath;

        /// <summary>
        /// 上次选择 AAB 文件时所在目录（用于下次打开文件面板时定位）。
        /// </summary>
        private string m_HistoryAABPath;

        /// <summary>
        /// 上次选择 APK 文件时所在目录。
        /// </summary>
        private string m_HistoryAPKPath;

        /// <summary>
        /// 编译时是否清空缓存。
        /// </summary>
        private bool m_BuildClearCache;
    }
}
