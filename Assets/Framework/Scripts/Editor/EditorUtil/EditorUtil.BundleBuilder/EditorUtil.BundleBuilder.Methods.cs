/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.BundleBuilder.Methods.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   EditorUtil.BundleBuilder 内部辅助方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class BundleBuilder
        {
            /// <summary>
            /// 正式构建 Sample 的标准资源包前，核验本包默认采集规则覆盖到的已配置 DLL 目标。
            /// 只检查实际目标文件与 Unity 导入类型，不生成、复制或修复资产；Editor 模拟构建与 Player 的首次 AOT 生成不走此门。
            /// 非 Sample 工作区、其他 Package 及自定义采集规则保留既有构建语义，不额外执行规则回调。
            /// </summary>
            /// <param name="packageName">本次标准 AssetBundle 的 Package 名称。</param>
            /// <param name="target">本次资源构建的明确平台，用于解析对应维度与路径占位符。</param>
            /// <exception cref="InvalidOperationException">本包覆盖到的 DLL 目标缺失、为空或未作为 TextAsset 导入。</exception>
            internal static void ValidateSampleDllTargets(string packageName, BuildTarget target)
            {
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                string masterPath = master == null ? string.Empty : AssetDatabase.GetAssetPath(master);
                if (!masterPath.StartsWith("Assets/Samples/", StringComparison.Ordinal))
                {
                    return;
                }

                PlatformType platform = Config.ActivePlatform.FromBuildTarget(target);
                if (platform == PlatformType.None)
                {
                    return;
                }

                // 与真正构建使用同一份已注入 Collector，不按目标平台另找配置或修改工作区绑定。
                BundleCollectorPackage package = BundleCollectorSettingData.Setting.GetPackage(packageName);
                if (package.IgnoreRuleName != nameof(NormalIgnoreRule))
                {
                    return;
                }

                Config.DimensionalResolver.HybridCLRResult hybrid = Config.DimensionalResolver.ResolveHybridCLR(
                    master, platform, master.CurrentChannel, master.CurrentDevelopMode);
                HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);
                CollectMissingSampleDllTargets(hybrid.AotMetadataDlls, package, target, missing);
                CollectMissingSampleDllTargets(hybrid.StartupGameDlls, package, target, missing);
                CollectMissingSampleDllTargets(hybrid.RunningGameDlls, package, target, missing);
                if (missing.Count > 0)
                {
                    List<string> paths = new List<string>(missing);
                    paths.Sort(StringComparer.Ordinal);
                    throw new InvalidOperationException(
                        $"{c_LogPrefix} Sample 资源包 '{packageName}' 缺少可用的 DLL 目标：\n  {string.Join("\n  ", paths)}\n" +
                        "导入时准备的空目录只供 Editor 演示使用。正式资源构建前请完成当前平台所需的 HybridCLR 生成、编译与 DLL 复制；" +
                        "AOT 副本应来自相应 Player 构建后的裁剪产物。");
                }
            }

            /// <summary>
            /// 收集已配置且落在本包默认启用主收集器中的无效 DLL 目标，不读取或要求构建源产物仍然存在。
            /// 自定义过滤和激活规则由原管线执行，避免为了预检重复调用用户规则。
            /// </summary>
            /// <param name="entries">当前目标平台的某一组 DLL 映射。</param>
            /// <param name="package">本次真正构建的 Package。</param>
            /// <param name="target">用于替换路径中 ActiveBuildTarget 的构建平台。</param>
            /// <param name="missing">缺失、空文件或未导入 TextAsset 的目标集合。</param>
            private static void CollectMissingSampleDllTargets(
                IReadOnlyList<DllMasterAssetEntry> entries,
                BundleCollectorPackage package,
                BuildTarget target,
                ISet<string> missing)
            {
                foreach (DllMasterAssetEntry entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.TargetLocation))
                    {
                        continue;
                    }

                    string resolved = entry.TargetLocation.Replace("{ActiveBuildTarget}", target.ToString());
                    string fullPath = Path.GetFullPath(Path.Combine(global::HybridCLR.Editor.SettingsUtil.ProjectDir, resolved));
                    string assetPath = FileUtil.GetProjectRelativePath(fullPath).Replace('\\', '/');
                    if (string.IsNullOrEmpty(assetPath) || !IsCoveredByDefaultSampleCollector(package, assetPath))
                    {
                        continue;
                    }

                    if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0 ||
                        AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(TextAsset))
                    {
                        missing.Add(assetPath);
                    }
                }
            }

            /// <summary>
            /// 只用默认规则声明判断目标路径是否属于本包，不实例化或调用自定义规则。
            /// 文件收集器要求路径完全一致，目录收集器要求完整目录边界，避免同名前缀误匹配。
            /// </summary>
            /// <param name="package">目标 Package 配置。</param>
            /// <param name="assetPath">待检查的项目内 DLL 目标路径。</param>
            /// <returns>默认启用主收集器明确覆盖目标时为 true。</returns>
            private static bool IsCoveredByDefaultSampleCollector(BundleCollectorPackage package, string assetPath)
            {
                foreach (BundleCollectorGroup group in package.Groups)
                {
                    if (group.ActiveRuleName != nameof(EnableGroup))
                    {
                        continue;
                    }

                    foreach (BundleCollector collector in group.Collectors)
                    {
                        if (collector.CollectorType != ECollectorType.MainAssetCollector ||
                            collector.FilterRuleName != nameof(CollectAll))
                        {
                            continue;
                        }

                        string root = collector.CollectPath.Replace('\\', '/').TrimEnd('/');
                        if (assetPath == root ||
                            (AssetDatabase.IsValidFolder(root) && assetPath.StartsWith(root + "/", StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// 解析最终类名：参数为空时回退到默认实现的 FullName。
            /// </summary>
            /// <param name="className">外部传入的全类型名。</param>
            /// <param name="fallback">为空时使用的默认类型。</param>
            /// <returns>解析后的全类型名。</returns>
            private static string ResolveClassName(string className, Type fallback)
            {
                return string.IsNullOrEmpty(className) ? fallback.FullName : className;
            }

            /// <summary>
            /// 按全类型名实例化指定接口实现；找不到时记录 Warning 并返回 null。
            /// </summary>
            /// <typeparam name="T">服务接口类型。</typeparam>
            /// <param name="className">实现类的全类型名。</param>
            /// <returns>实例或 null。</returns>
            private static T CreateInstanceOrNull<T>(string className) where T : class
            {
                if (string.IsNullOrEmpty(className)) return null;
                System.Collections.Generic.List<Type> classTypes = EditorAssemblyUtility.GetAssignableTypes(typeof(T));
                Type classType = classTypes.Find(x => x.FullName.Equals(className));
                if (classType == null)
                {
                    Log.Warning(LogTag.Editor, "{0} 未找到 {1} 实现：{2}", c_LogPrefix, typeof(T).Name, className);
                    return null;
                }
                return (T)Activator.CreateInstance(classType);
            }

            /// <summary>
            /// 解析包裹内置着色器资源包名（与 ScriptableBuildPipelineViewer.GetBuiltinShaderBundleName 行为一致）。
            /// </summary>
            /// <param name="packageName">包裹名。</param>
            /// <returns>内置着色器资源包名。</returns>
            private static string ResolveBuiltinShaderBundleName(string packageName)
            {
                bool uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
                BundlePackRuleResult packRuleResult = DefaultBundlePackRule.CreateShadersPackRuleResult();
                return packRuleResult.GetBundleName(packageName, uniqueBundleName);
            }
        }
    }
}
