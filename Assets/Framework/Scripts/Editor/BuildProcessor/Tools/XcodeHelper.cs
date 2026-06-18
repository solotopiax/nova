/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  XcodeHelper.cs
 * author:    yingzheng
 * created:   2026/4/24
 * descrip:   iOS 构建后处理工具，封装 Entitlements / XcodeProject / Plist 常用操作
 ***************************************************************/

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// iOS 构建后处理工具。
    /// 封装 XEntitlements、PBXProject、Info.plist 的高频操作，供各 SDK 构建处理器使用，
    /// 所有方法均内置参数校验，调用方无需额外判空。
    /// </summary>
    public static class XcodeHelper
    {
#if UNITY_IOS

        /// <summary>
        /// Entitlements 文件操作工具。
        /// 封装对 XEntitlements（PlistDocument）的读写，支持字符串写入和数组幂等追加。
        /// </summary>
        public static class Entitlements
        {
            /// <summary>
            /// 向 Entitlements 写入字符串类型条目。键已存在时覆盖其值。
            /// </summary>
            /// <param name="entitlements">Entitlements PlistDocument 实例。</param>
            /// <param name="key">条目键名，例如 "application-identifier"。</param>
            /// <param name="value">字符串值。</param>
            public static void SetStringValue(PlistDocument entitlements, string key, string value)
            {
                if (entitlements == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Entitlements] entitlements 为 null，跳过 SetStringValue。");
                    return;
                }
                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Entitlements] key 为空，跳过 SetStringValue。");
                    return;
                }
                entitlements.root[key] = new PlistElementString(value ?? string.Empty);
            }

            /// <summary>
            /// 向 Entitlements 的数组类型条目幂等追加一个字符串值。
            /// 键不存在时自动创建数组；值已存在时跳过，避免重复写入。
            /// </summary>
            /// <param name="entitlements">Entitlements PlistDocument 实例。</param>
            /// <param name="key">数组条目键名，例如 "com.apple.developer.associated-domains"。</param>
            /// <param name="value">要追加的字符串值，例如 "applinks:example.com"。</param>
            public static void EnsureArrayValue(PlistDocument entitlements, string key, string value)
            {
                if (entitlements == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Entitlements] entitlements 为 null，跳过 EnsureArrayValue。");
                    return;
                }
                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Entitlements] key 为空，跳过 EnsureArrayValue。");
                    return;
                }
                var array = EnsureArray(entitlements, key);
                foreach (var item in array.values)
                {
                    if (item is PlistElementString str && str.value == value)
                        return;
                }
                array.values.Add(new PlistElementString(value ?? string.Empty));
            }

            /// <summary>
            /// 确保 Entitlements 中指定键对应的条目为数组类型并返回。
            /// 键不存在或类型不匹配时自动创建新数组。
            /// </summary>
            /// <param name="entitlements">Entitlements PlistDocument 实例。</param>
            /// <param name="key">数组条目键名。</param>
            /// <returns>对应键的 PlistElementArray 实例。</returns>
            public static PlistElementArray EnsureArray(PlistDocument entitlements, string key)
            {
                if (entitlements.root[key] is PlistElementArray existing)
                    return existing;
                var array = new PlistElementArray();
                entitlements.root[key] = array;
                return array;
            }
        }

        /// <summary>
        /// Xcode 工程（PBXProject）操作工具。
        /// 封装 Capability 添加、Framework 引用、Build Property 设置等高频操作。
        /// </summary>
        public static class Project
        {
            /// <summary>
            /// 为指定 Target 添加 Xcode Capability。已添加时 Unity API 本身幂等，可重复调用。
            /// </summary>
            /// <param name="proj">PBXProject 实例。</param>
            /// <param name="targetGuid">主 Target 的 GUID。</param>
            /// <param name="capability">要添加的 Capability 类型，例如 PBXCapabilityType.AssociatedDomains。</param>
            /// <param name="entitlementsRelPath">Entitlements 文件相对于工程根目录的路径。</param>
            public static void AddCapability(PBXProject proj, string targetGuid, PBXCapabilityType capability, string entitlementsRelPath)
            {
                if (proj == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] proj 为 null，跳过 AddCapability。");
                    return;
                }
                if (string.IsNullOrEmpty(targetGuid))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] targetGuid 为空，跳过 AddCapability。");
                    return;
                }
                proj.AddCapability(targetGuid, capability, entitlementsRelPath);
            }

            /// <summary>
            /// 为指定 Target 添加系统 Framework 引用。
            /// </summary>
            /// <param name="proj">PBXProject 实例。</param>
            /// <param name="targetGuid">主 Target 的 GUID。</param>
            /// <param name="framework">Framework 文件名，例如 "AdServices.framework"。</param>
            /// <param name="weak">是否弱引用（Optional），默认 false。</param>
            public static void AddFramework(PBXProject proj, string targetGuid, string framework, bool weak = false)
            {
                if (proj == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] proj 为 null，跳过 AddFramework。");
                    return;
                }
                if (string.IsNullOrEmpty(targetGuid))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] targetGuid 为空，跳过 AddFramework。");
                    return;
                }
                if (string.IsNullOrEmpty(framework))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] framework 为空，跳过 AddFramework。");
                    return;
                }
                string fileGuid = proj.AddFile($"System/Library/Frameworks/{framework}", $"Frameworks/{framework}", PBXSourceTree.Sdk);
                proj.AddFileToBuild(targetGuid, fileGuid);
                if (weak)
                    proj.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", $"-weak_framework {Util.SysIO.Path.GetFileNameWithoutExtension(framework)}");
            }

            /// <summary>
            /// 设置指定 Target 的 Build Setting 属性。
            /// </summary>
            /// <param name="proj">PBXProject 实例。</param>
            /// <param name="targetGuid">主 Target 的 GUID。</param>
            /// <param name="name">Build Setting 键名，例如 "SWIFT_VERSION"。</param>
            /// <param name="value">属性值。</param>
            public static void SetBuildProperty(PBXProject proj, string targetGuid, string name, string value)
            {
                if (proj == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] proj 为 null，跳过 SetBuildProperty。");
                    return;
                }
                if (string.IsNullOrEmpty(targetGuid))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] targetGuid 为空，跳过 SetBuildProperty。");
                    return;
                }
                if (string.IsNullOrEmpty(name))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Project] name 为空，跳过 SetBuildProperty。");
                    return;
                }
                proj.SetBuildProperty(targetGuid, name, value ?? string.Empty);
            }
        }

        /// <summary>
        /// Info.plist 根字典（PlistElementDict）操作工具。
        /// 封装字符串、布尔值写入以及数组幂等追加等高频操作。
        /// </summary>
        public static class Plist
        {
            /// <summary>
            /// 向 Info.plist 根字典写入字符串类型条目。键已存在时覆盖其值。
            /// </summary>
            /// <param name="dict">Info.plist 根字典，即 XPlistDict。</param>
            /// <param name="key">条目键名，例如 "NSAdvertisingAttributionReportEndpoint"。</param>
            /// <param name="value">字符串值。</param>
            public static void SetString(PlistElementDict dict, string key, string value)
            {
                if (dict == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] dict 为 null，跳过 SetString。");
                    return;
                }
                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] key 为空，跳过 SetString。");
                    return;
                }
                dict.SetString(key, value ?? string.Empty);
            }

            /// <summary>
            /// 向 Info.plist 根字典写入布尔类型条目。键已存在时覆盖其值。
            /// </summary>
            /// <param name="dict">Info.plist 根字典，即 XPlistDict。</param>
            /// <param name="key">条目键名。</param>
            /// <param name="value">布尔值。</param>
            public static void SetBoolean(PlistElementDict dict, string key, bool value)
            {
                if (dict == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] dict 为 null，跳过 SetBoolean。");
                    return;
                }
                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] key 为空，跳过 SetBoolean。");
                    return;
                }
                dict.SetBoolean(key, value);
            }

            /// <summary>
            /// 向 Info.plist 的 CFBundleURLTypes 中添加或替换一条 URL Scheme 条目。
            /// 按 CFBundleURLName 查找：存在则整体替换，不存在则追加。
            /// </summary>
            /// <param name="dict">Info.plist 根字典，即 XPlistDict。</param>
            /// <param name="urlName">CFBundleURLName 值，例如 "appsflyer-unity-onelink"。</param>
            /// <param name="urlIdentifier">CFBundleURLIdentifier 值，例如 "appsflyer-unity-onelink"。</param>
            /// <param name="scheme">CFBundleURLSchemes 中的 scheme 字符串。</param>
            public static void AddOrModifyUrlScheme(PlistElementDict dict, string urlName, string urlIdentifier, string scheme)
            {
                if (dict == null || string.IsNullOrEmpty(urlName) || string.IsNullOrEmpty(scheme))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] AddOrModifyUrlScheme 参数无效，跳过。");
                    return;
                }

                if (!dict.values.ContainsKey("CFBundleURLTypes"))
                    dict.CreateArray("CFBundleURLTypes");

                var urlTypes = (PlistElementArray)dict.values["CFBundleURLTypes"];

                var entry = new PlistElementDict();
                entry.SetString("CFBundleURLName", urlName);
                if (!string.IsNullOrEmpty(urlIdentifier))
                    entry.SetString("CFBundleURLIdentifier", urlIdentifier);
                entry.CreateArray("CFBundleURLSchemes").AddString(scheme);

                for (int i = 0; i < urlTypes.values.Count; i++)
                {
                    if (urlTypes.values[i] is PlistElementDict d &&
                        d.values.ContainsKey("CFBundleURLName") &&
                        d.values["CFBundleURLName"].AsString() == urlName)
                    {
                        urlTypes.values[i] = entry;
                        return;
                    }
                }
                urlTypes.values.Add(entry);
            }

            /// <summary>
            /// 向 Info.plist 根字典的数组类型条目幂等追加一个字符串值。
            /// 键不存在时自动创建数组；值已存在时跳过，避免重复写入。
            /// </summary>
            /// <param name="dict">Info.plist 根字典，即 XPlistDict。</param>
            /// <param name="key">数组条目键名，例如 "UIBackgroundModes"。</param>
            /// <param name="value">要追加的字符串值。</param>
            public static void EnsureArrayValue(PlistElementDict dict, string key, string value)
            {
                if (dict == null)
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] dict 为 null，跳过 EnsureArrayValue。");
                    return;
                }
                if (string.IsNullOrEmpty(key))
                {
                    Log.Warning(LogTag.Editor, "[XcodeHelper.Plist] key 为空，跳过 EnsureArrayValue。");
                    return;
                }
                PlistElementArray array;
                if (dict[key] is PlistElementArray existing)
                {
                    array = existing;
                }
                else
                {
                    array = dict.CreateArray(key);
                }
                foreach (var item in array.values)
                {
                    if (item is PlistElementString str && str.value == value)
                        return;
                }
                array.values.Add(new PlistElementString(value ?? string.Empty));
            }
        }

#endif
    }
}
