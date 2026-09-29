/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IOSPrivacyManifestBuildProcessor.cs
 * author:    taoye
 * created:   2026/9/28
 * descrip:   将 iOS 隐私声明写入应用级 PrivacyInfo.xcprivacy 与 Info.plist
 ***************************************************************/

#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace NovaFramework.Editor
{
    /// <summary>
    /// iOS 构建时读取当前已导出的隐私配置，写入应用级隐私清单和用途说明。
    /// </summary>
    public sealed class IOSPrivacyManifestBuildProcessor : NovaSDKBuildProcessor
    {
        private const string c_ManifestFileName = "PrivacyInfo.xcprivacy";
        private const string c_ApiTypesKey = "NSPrivacyAccessedAPITypes";
        private const string c_ApiTypeKey = "NSPrivacyAccessedAPIType";
        private const string c_ReasonsKey = "NSPrivacyAccessedAPITypeReasons";

        /// <summary>
        /// 在其他普通构建处理器之后合并应用级隐私声明。
        /// </summary>
        public override int PostprocessPriority => 1000;

        /// <summary>
        /// 把当前 iOS 导出坐标中的用途说明写入 Info.plist，并合并隐私清单理由码。
        /// </summary>
        /// <param name="report">iOS 构建报告。</param>
        /// <param name="context">已加载 Xcode 工程的 Nova 构建上下文。</param>
        public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context)
        {
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            if (runtime == null || runtime.Platform != PlatformType.iOS)
            {
                Log.Warning(LogTag.Editor,
                    "[iOS Privacy] 当前未找到已导出的 iOS ConfigRuntimeSO，跳过隐私清单和用途说明注入。请先在 ConfigWindow 导出 iOS 配置。");
                return;
            }

            PrivacyConfigs privacy = runtime.PrivacyConfigs;
            if (!InfoPlistUsageDescriptionsParser.TryParse(
                    privacy?.InfoPlistUsageDescriptions,
                    out Dictionary<string, string> descriptions,
                    out string usageError))
            {
                throw new BuildFailedException($"InfoPlistUsageDescriptions 无效：{usageError}");
            }

            foreach (KeyValuePair<string, string> entry in descriptions)
            {
                context.XPlistDict.SetString(entry.Key, entry.Value);
            }

            string json = privacy?.PrivacyInfoConfig;
            if (string.IsNullOrWhiteSpace(json)) return;
            if (!PrivacyInfoConfigParser.TryParse(json, out Dictionary<string, List<string>> reasonsByCategory, out string error))
            {
                throw new BuildFailedException($"PrivacyInfoConfig 无效：{error}");
            }

            if (reasonsByCategory.Count == 0) return;
            string manifestPath = System.IO.Path.Combine(report.summary.outputPath, c_ManifestFileName);
            PlistDocument manifest = new PlistDocument();
            if (File.Exists(manifestPath)) manifest.ReadFromFile(manifestPath);

            PlistElementArray apiTypes;
            if (manifest.root.values.TryGetValue(c_ApiTypesKey, out PlistElement existing))
            {
                if (existing is not PlistElementArray existingArray)
                    throw new BuildFailedException($"{manifestPath} 中的 {c_ApiTypesKey} 不是数组，无法安全合并。");
                apiTypes = existingArray;
            }
            else
            {
                apiTypes = manifest.root.CreateArray(c_ApiTypesKey);
            }

            foreach (KeyValuePair<string, List<string>> entry in reasonsByCategory)
            {
                PlistElementDict category = FindCategory(apiTypes, entry.Key) ?? apiTypes.AddDict();
                category.SetString(c_ApiTypeKey, entry.Key);
                PlistElementArray reasons;
                if (category.values.TryGetValue(c_ReasonsKey, out PlistElement existingReasons))
                {
                    if (existingReasons is not PlistElementArray reasonArray)
                        throw new BuildFailedException($"{manifestPath} 中 {entry.Key} 的理由码不是数组，无法安全合并。");
                    reasons = reasonArray;
                }
                else
                {
                    reasons = category.CreateArray(c_ReasonsKey);
                }

                foreach (string reason in entry.Value)
                {
                    if (!ContainsReason(reasons, reason)) reasons.AddString(reason);
                }
            }

            manifest.WriteToFile(manifestPath);
            if (string.IsNullOrEmpty(context.XProj.FindFileGuidByProjectPath(c_ManifestFileName)))
            {
                string fileGuid = context.XProj.AddFile(c_ManifestFileName, c_ManifestFileName);
                context.XProj.AddFileToBuild(context.TargetGuid, fileGuid);
            }

            Log.Debug(LogTag.Editor,
                "[iOS Privacy] 已合并 {0} 个 Required Reason API 类别到应用级 {1}。",
                reasonsByCategory.Count,
                manifestPath);
        }

        /// <summary>
        /// 在现有隐私清单数组中查找同名 API 类别。
        /// </summary>
        /// <param name="apiTypes">已有的 API 类别数组。</param>
        /// <param name="categoryName">待查找类别名。</param>
        /// <returns>匹配的字典；不存在时为 null。</returns>
        private static PlistElementDict FindCategory(PlistElementArray apiTypes, string categoryName)
        {
            foreach (PlistElement item in apiTypes.values)
            {
                if (item is PlistElementDict category
                    && category.values.TryGetValue(c_ApiTypeKey, out PlistElement type)
                    && type is PlistElementString name
                    && string.Equals(name.value, categoryName, StringComparison.Ordinal))
                {
                    return category;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断理由码数组是否已有指定值，避免重复声明。
        /// </summary>
        /// <param name="reasons">已有理由码数组。</param>
        /// <param name="reason">待加入理由码。</param>
        /// <returns>已存在时为 true。</returns>
        private static bool ContainsReason(PlistElementArray reasons, string reason)
        {
            foreach (PlistElement item in reasons.values)
            {
                if (item is PlistElementString existing
                    && string.Equals(existing.value, reason, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
