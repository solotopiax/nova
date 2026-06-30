/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.RequiredLibraries.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   PlugPals 工具 —— 必须三方库审计
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            private static bool IsSolotopiaPackageName(string packageName)
            {
                return packageName.StartsWith("com.solotopia.", System.StringComparison.Ordinal);
            }

            private static bool IsUnityPackageName(string packageName)
            {
                return packageName.StartsWith("com.unity.", System.StringComparison.Ordinal);
            }

            /// <summary>
            /// 基于包 dependencies 与窗口内存 registry 包列表，检测缺失库与待自动配 scope 的命中依赖。
            /// </summary>
            /// <param name="dependencies">待装包的 dependencies（包名 -> 版本）。</param>
            /// <param name="installedPackageNames">本地已安装包名集合。</param>
            /// <param name="knownRegistryPackages">内存 registry 包列表（包名 -> 命中来源）。</param>
            /// <param name="nova">待装包的展示元数据（提供 displayName/purchaseUrl）。</param>
            /// <param name="projectScopedRegistries">项目 manifest 当前已配置的 scopedRegistries；用户手工配的 OpenUPM 等私有仓库在此放行，避免误判为缺库。</param>
            /// <param name="dependentName">待装包名（用于缺库信息回溯）。</param>
            /// <param name="dependentDisplayName">待装包显示名。</param>
            internal static DependencyCheckResult CheckDependencies(
                IReadOnlyDictionary<string, string> dependencies,
                ISet<string> installedPackageNames,
                IReadOnlyDictionary<string, RegistrySource> knownRegistryPackages,
                NovaPackageMetadata nova,
                IReadOnlyList<ScopedRegistry> projectScopedRegistries,
                string dependentName,
                string dependentDisplayName)
            {
                var result = new DependencyCheckResult();
                if (dependencies == null || dependencies.Count == 0)
                {
                    return result;
                }

                foreach (System.Collections.Generic.KeyValuePair<string, string> dependency in dependencies.OrderBy(kvp => kvp.Key, System.StringComparer.OrdinalIgnoreCase))
                {
                    string dependencyName = dependency.Key;
                    if (string.IsNullOrEmpty(dependencyName) ||
                        IsSolotopiaPackageName(dependencyName) ||
                        IsUnityPackageName(dependencyName) ||
                        IsCoveredByDeclaredRegistries(dependencyName, nova) ||
                        IsCoveredByProjectScopedRegistries(dependencyName, projectScopedRegistries) ||
                        (installedPackageNames != null && installedPackageNames.Contains(dependencyName)))
                    {
                        continue;
                    }

                    if (knownRegistryPackages != null && knownRegistryPackages.TryGetValue(dependencyName, out RegistrySource source))
                    {
                        result.ToAutoScope[dependencyName] = source;
                        continue;
                    }

                    RequiredLibraryGuide guide = ResolveRequiredLibraryGuideFromNova(nova, dependencyName);
                    result.Missing.Add(new MissingRequiredLibraryInfo
                    {
                        PackageName = dependencyName,
                        RequiredVersion = dependency.Value,
                        DisplayName = string.IsNullOrEmpty(guide?.displayName) ? dependencyName : guide.displayName,
                        PurchaseUrl = guide?.purchaseUrl,
                        DependentPackageName = dependentName,
                        DependentPackageDisplayName = string.IsNullOrEmpty(dependentDisplayName) ? dependentName : dependentDisplayName
                    });
                }

                return result;
            }

            private static RequiredLibraryGuide ResolveRequiredLibraryGuideFromNova(NovaPackageMetadata nova, string dependencyName)
            {
                if (nova?.requiredLibraries == null || string.IsNullOrEmpty(dependencyName))
                {
                    return null;
                }

                nova.requiredLibraries.TryGetValue(dependencyName, out RequiredLibraryGuide guide);
                return guide;
            }

            /// <summary>
            /// 判断依赖名是否被待装包 nova.scopedRegistries 声明的任一 scope 前缀覆盖（com.x 覆盖 com.x.y）。
            /// 命中表示该依赖由包自带声明的私有仓库提供，依赖检测应放行——既不判缺库也不自动配 scope，
            /// 交由注册的 scoped registry + UPM 传递解析自动拉取。
            /// </summary>
            private static bool IsCoveredByDeclaredRegistries(string dependencyName, NovaPackageMetadata nova)
            {
                return IsCoveredByAnyScopedRegistry(dependencyName, nova?.scopedRegistries);
            }

            /// <summary>
            /// 判断依赖名是否被项目 manifest.json 当前已配置的 scopedRegistries 任一 scope 前缀覆盖。
            /// 命中表示用户已为该依赖手工配置好私有仓库（如 OpenUPM 提供的 com.google.external-dependency-manager），
            /// 依赖检测应放行——既不判缺库也不自动配 scope，避免误改用户配置，交由 UPM 解析拉取。
            /// </summary>
            private static bool IsCoveredByProjectScopedRegistries(string dependencyName, IReadOnlyList<ScopedRegistry> projectScopedRegistries)
            {
                return IsCoveredByAnyScopedRegistry(dependencyName, projectScopedRegistries);
            }

            /// <summary>
            /// scope 前缀覆盖判定共享 helper：dependencyName 等于 scope 或以 scope + "." 起始即视为命中。
            /// </summary>
            private static bool IsCoveredByAnyScopedRegistry(string dependencyName, IEnumerable<ScopedRegistry> registries)
            {
                if (registries == null || string.IsNullOrEmpty(dependencyName))
                {
                    return false;
                }

                foreach (ScopedRegistry registry in registries)
                {
                    if (registry?.scopes == null)
                    {
                        continue;
                    }

                    foreach (string scope in registry.scopes)
                    {
                        if (string.IsNullOrEmpty(scope))
                        {
                            continue;
                        }

                        if (dependencyName == scope ||
                            dependencyName.StartsWith(scope + ".", System.StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            internal static DependencyCheckResult CheckDependenciesForTest(
                IReadOnlyDictionary<string, string> dependencies,
                ISet<string> installedPackageNames,
                IReadOnlyDictionary<string, RegistrySource> knownRegistryPackages,
                NovaPackageMetadata nova)
            {
                return CheckDependencies(dependencies, installedPackageNames, knownRegistryPackages, nova, null, "com.solotopia.test.package", "Test Package");
            }

            internal static DependencyCheckResult CheckDependenciesForTest(
                IReadOnlyDictionary<string, string> dependencies,
                ISet<string> installedPackageNames,
                IReadOnlyDictionary<string, RegistrySource> knownRegistryPackages,
                NovaPackageMetadata nova,
                IReadOnlyList<ScopedRegistry> projectScopedRegistries)
            {
                return CheckDependencies(dependencies, installedPackageNames, knownRegistryPackages, nova, projectScopedRegistries, "com.solotopia.test.package", "Test Package");
            }
        }
    }
}
