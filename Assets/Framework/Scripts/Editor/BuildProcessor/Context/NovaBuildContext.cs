/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaBuildContext.cs
 * author:    yingzheng
 * created:   2026/4/23
 * descrip:   Nova 构建上下文，贯穿整个构建流程，由中央调度器创建后传递给每个 SDK 处理器
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 构建上下文。
    /// 贯穿整个构建流程，由 NovaBuildProcessor 创建并传递给每个 SDK 处理器，
    /// 用于在各处理器之间共享构建状态和平台相关资源。
    /// </summary>
    public sealed class NovaBuildContext
    {
        /// <summary>
        /// Unity 构建报告，包含本次构建的完整信息。
        /// </summary>
        public BuildReport Report { get; set; }

        /// <summary>
        /// 构建目标平台。
        /// </summary>
        public BuildTarget Target { get; set; }

        /// <summary>
        /// 默认主 Activity 类名，未注册时作为 ActivityName 的返回值。
        /// </summary>
        private const string c_DefaultActivityName = "com.unity3d.player.UnityPlayerActivity";

        /// <summary>
        /// 已注册的主 Activity 类名，未注册时为 null。
        /// </summary>
        private string m_ActivityName;

        /// <summary>
        /// 注册主 Activity 类名。先到先得，已注册后忽略后续注册并输出 Warning。
        /// Firebase 等高优先级处理器先调用，AF 等低优先级处理器直接读 ActivityName。
        /// </summary>
        /// <param name="name">主 Activity 的完整类名。</param>
        public void RegisterActivityName(string name)
        {
            if (string.IsNullOrEmpty(m_ActivityName))
                m_ActivityName = name;
            else
                Log.Warning(LogTag.Editor, $"[NovaBuildContext] ActivityName 已注册为 {m_ActivityName}，忽略重复注册：{name}。");
        }

        /// <summary>
        /// 当前构建的主 Activity 类名。未注册时返回默认值 "com.unity3d.player.UnityPlayerActivity"。
        /// </summary>
        public string ActivityName => string.IsNullOrEmpty(m_ActivityName) ? c_DefaultActivityName : m_ActivityName;

        /// <summary>
        /// 已注册的全部 Manifest 规则集列表。
        /// </summary>
        private readonly List<ManifestRuleSet> m_ManifestRuleSets = new List<ManifestRuleSet>();

        /// <summary>
        /// 注册一组 Manifest 规则。SDK 处理器在预处理回调中调用，框架统一在所有处理器执行完后 Apply。
        /// </summary>
        /// <param name="ruleSet">要注册的规则集，为 null 时直接忽略。</param>
        public void AddManifestRules(ManifestRuleSet ruleSet) { if (ruleSet != null) m_ManifestRuleSets.Add(ruleSet); }

        /// <summary>
        /// 当前已注册的全部 Manifest 规则集，供 NovaBuildProcessor 统一 Apply 使用。
        /// </summary>
        public IReadOnlyList<ManifestRuleSet> ManifestRuleSets => m_ManifestRuleSets;

        /// <summary>
        /// 已注册的全部 ProGuard 规则列表，sdkName 用于在输出文件中标注来源。
        /// </summary>
        private readonly List<(string sdkName, string rules)> m_ProguardRules = new List<(string, string)>();

        /// <summary>
        /// 注册一组 ProGuard 规则。SDK 处理器在预处理回调中调用，框架统一在所有处理器执行完后写入文件。
        /// </summary>
        /// <param name="sdkName">SDK 名称，用于在输出文件中标注来源。</param>
        /// <param name="rules">ProGuard 规则文本，为空时直接忽略。</param>
        public void AddProguardRules(string sdkName, string rules)
        {
            if (!string.IsNullOrEmpty(rules))
                m_ProguardRules.Add((sdkName, rules));
        }

        /// <summary>
        /// 当前已注册的全部 ProGuard 规则列表，供 NovaBuildProcessor 统一写入使用。
        /// </summary>
        public IReadOnlyList<(string sdkName, string rules)> ProguardRules => m_ProguardRules;

        /// <summary>
        /// Android Manifest XML 操作对象。
        /// 由 NovaBuildProcessor 在 Android 预处理阶段创建并注入，
        /// 处理器可通过此对象读写主 AndroidManifest.xml。
        /// </summary>
        public CustomAndroidManifest AndroidManifest { get; set; }

#if UNITY_IOS
        /// <summary>
        /// iOS Xcode 工程对象，用于修改工程配置。
        /// </summary>
        public PBXProject XProj { get; set; }

        /// <summary>
        /// iOS Info.plist 文档，用于读写应用配置项。
        /// </summary>
        public PlistDocument XPlist { get; set; }

        /// <summary>
        /// iOS Info.plist 根字典，XPlist.root 的快捷引用。
        /// </summary>
        public PlistElementDict XPlistDict { get; set; }

        /// <summary>
        /// iOS Entitlements 文件，用于读写应用权能配置。
        /// </summary>
        public PlistDocument XEntitlements { get; set; }

        /// <summary>
        /// iOS Xcode 主 Target 的 GUID。
        /// </summary>
        public string TargetGuid { get; set; }

        /// <summary>
        /// Entitlements 文件相对于 Xcode 工程根目录的路径。
        /// </summary>
        public string RelativeEntitlementFilePath { get; set; }
#endif
    }
}
