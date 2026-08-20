/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.AgentOperations.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   PlugPals 面向消费项目 Agent 的计划、执行与验证入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            private const string c_FrameworkPackageName = "com.solotopia.nova.framework";
            private static readonly Dictionary<string, AgentPackagePlanState> s_AgentPackagePlans =
                new Dictionary<string, AgentPackagePlanState>(StringComparer.Ordinal);

            /// <summary>
            /// Agent 包操作请求。只接受最新版本安装、最新版本升级和卸载，不接受指定版本或任意来源地址。
            /// </summary>
            public sealed class ProjectPackageOperationRequest
            {
                /// <summary>
                /// 操作名：install-latest、upgrade-latest 或 uninstall。
                /// </summary>
                public string action;

                /// <summary>
                /// 目标包名。
                /// </summary>
                public string packageName;

                /// <summary>
                /// 可选的已配置 registry URL；仅用于同名包来源消歧，不能传入未配置地址。
                /// </summary>
                public string registryUrl;
            }

            /// <summary>
            /// Agent 包操作计划。ready 只表示可以请求用户确认，不表示已经写入或解析成功。
            /// </summary>
            public sealed class ProjectPackageOperationPlan
            {
                /// <summary>
                /// 一次性计划标识；任一前置状态变化后必须重新计划。
                /// </summary>
                public string planId;

                /// <summary>
                /// 计划状态：ready、blocked 或 not_applicable。
                /// </summary>
                public string status;

                /// <summary>
                /// 规范化后的操作名。
                /// </summary>
                public string action;

                /// <summary>
                /// 目标包名。
                /// </summary>
                public string packageName;

                /// <summary>
                /// 当前已解析版本；未安装时为空。
                /// </summary>
                public string currentVersion;

                /// <summary>
                /// 计划安装或升级到的远端最新版本；卸载时为空。
                /// </summary>
                public string targetVersion;

                /// <summary>
                /// 本次操作使用或清理的 registry URL。
                /// </summary>
                public string selectedRegistryUrl;

                /// <summary>
                /// 面向确认与诊断的原因列表。
                /// </summary>
                public List<string> messages = new List<string>();

                /// <summary>
                /// 卸载前发现的直接消费者包名。
                /// </summary>
                public List<string> consumers = new List<string>();

                /// <summary>
                /// 安装或升级前仍缺失的依赖包名。
                /// </summary>
                public List<string> missingDependencies = new List<string>();
            }

            /// <summary>
            /// 已提交包操作的验证凭据。提交只返回 partial，必须在 UPM 与 domain reload 稳定后验证。
            /// </summary>
            public sealed class ProjectPackageOperationReceipt
            {
                /// <summary>
                /// 操作名。
                /// </summary>
                public string action;

                /// <summary>
                /// 目标包名。
                /// </summary>
                public string packageName;

                /// <summary>
                /// 安装或升级期望版本；卸载时为空。
                /// </summary>
                public string expectedVersion;

                /// <summary>
                /// 安装或升级期望解析到的已配置 registry URL；卸载时为空。
                /// </summary>
                public string expectedRegistryUrl;
            }

            /// <summary>
            /// Agent 包操作执行或验证结果。
            /// </summary>
            public sealed class ProjectPackageOperationResult
            {
                /// <summary>
                /// success、partial、blocked 或 not_applicable。
                /// </summary>
                public string status;

                /// <summary>
                /// 结果说明。
                /// </summary>
                public string message;

                /// <summary>
                /// 需要在 UPM 稳定后交给验证入口的凭据；未提交时为空。
                /// </summary>
                public ProjectPackageOperationReceipt receipt;
            }

            /// <summary>
            /// 为消费项目计划一次 UPM 包操作，不写 manifest，也不触发 Resolve。
            /// </summary>
            /// <param name="request">只包含操作、包名和可选的已配置 registry 来源。</param>
            /// <param name="token">远端查询取消令牌。</param>
            /// <returns>可确认计划或带证据的阻塞/不适用结果。</returns>
            public static async Task<ProjectPackageOperationPlan> PlanProjectPackageOperationAsync(
                ProjectPackageOperationRequest request,
                CancellationToken token)
            {
                ProjectPackageOperationPlan plan = CreateBasePlan(request);
                if (plan.status != "ready")
                {
                    return plan;
                }

                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return BlockPlan(plan, "Unity 正在编译或更新包，请稳定后重新计划。");
                }

                string manifestPath = GetProjectManifestPath();
                string lockPath = GetProjectLockPath();
                ManifestData manifest = ReadManifest(manifestPath);
                PackagesLockData lockData = ReadPackagesLock(lockPath);
                if (manifest == null || lockData?.dependencies == null)
                {
                    return BlockPlan(plan, "无法读取 Packages/manifest.json 或 packages-lock.json。");
                }

                manifest.dependencies ??= new Dictionary<string, string>();
                manifest.dependencies.TryGetValue(plan.packageName, out string directValue);
                lockData.dependencies.TryGetValue(plan.packageName, out PackagesLockEntry lockEntry);
                plan.currentVersion = ResolveInstalledVersion(plan.packageName, lockEntry);

                if (plan.action == "uninstall")
                {
                    return PlanUninstall(plan, manifest, lockData, directValue, manifestPath, lockPath);
                }

                if (!string.IsNullOrEmpty(directValue) && IsNonRegistryValue(directValue, lockEntry))
                {
                    return BlockPlan(plan, "目标包当前来自 file/git/embedded 等非 registry 来源，禁止隐式切换来源。");
                }

                if (plan.action == "install-latest" && !string.IsNullOrEmpty(directValue))
                {
                    return NotApplicablePlan(plan, "目标包已是项目 direct dependency；如需升级请使用 upgrade-latest。");
                }

                if (plan.action == "upgrade-latest" && string.IsNullOrEmpty(directValue))
                {
                    return NotApplicablePlan(plan, "目标包不是项目 direct dependency，不能执行升级。");
                }

                if (plan.action == "upgrade-latest" &&
                    (lockEntry == null || lockEntry.source != "registry" || string.IsNullOrEmpty(lockEntry.url)))
                {
                    return BlockPlan(plan, "目标包缺少完整的 registry 解析来源，无法证明升级保持同一来源。");
                }

                if (!string.IsNullOrEmpty(request.registryUrl) &&
                    !string.IsNullOrEmpty(lockEntry?.url) &&
                    request.registryUrl != lockEntry.url)
                {
                    return BlockPlan(plan, "registryUrl 只能消歧，不能把已解析包切换到另一来源。");
                }

                RegistryCatalog catalog = await LoadRegistryCatalogAsync(token);
                if (catalog.errors.Count > 0)
                {
                    return BlockPlan(plan, "未能完整读取全部已配置 registry，无法安全判断来源或依赖。" + JoinErrors(catalog.errors));
                }

                if (catalog.entries.Count == 0)
                {
                    return BlockPlan(plan, "已配置 registry 中没有可用包数据。");
                }

                RegistryPackageCandidate candidate = SelectRegistryCandidate(request.registryUrl, plan.packageName, lockEntry, catalog, plan.messages);
                if (candidate == null)
                {
                    return BlockPlan(plan, "无法唯一确定目标包的已配置 registry 来源。" + JoinErrors(catalog.errors));
                }

                plan.selectedRegistryUrl = candidate.source.Url;
                plan.targetVersion = candidate.entry.LatestVersion;
                if (plan.action == "upgrade-latest" && CompareSemVer(plan.targetVersion, plan.currentVersion) <= 0)
                {
                    return NotApplicablePlan(plan, "远端最新版本未高于当前版本，禁止降级或重复写入。");
                }

                DependencyCheckResult dependencyCheck = BuildAgentDependencyCheck(manifest, candidate.entry, catalog.knownPackages);
                if (dependencyCheck.Missing.Count > 0)
                {
                    plan.missingDependencies.AddRange(dependencyCheck.Missing.Select(item => item.PackageName));
                    return BlockPlan(plan, "依赖预检未通过；不会写入 manifest。" + JoinErrors(catalog.errors));
                }

                plan.messages.Add($"将 {plan.packageName} 写为远端最新版本 {plan.targetVersion}，随后提交一次 UPM Resolve。");
                StoreReadyPlan(plan, manifestPath, lockPath, candidate, catalog, dependencyCheck);
                return plan;
            }

            /// <summary>
            /// 执行一次已经向用户展示并确认的计划。计划只可消费一次，前置状态漂移时拒绝写入。
            /// </summary>
            /// <param name="planId">PlanProjectPackageOperationAsync 返回的一次性计划标识。</param>
            /// <param name="confirmed">用户是否已确认该计划中的精确包名、动作、来源和版本。</param>
            /// <param name="token">执行前远端最新版本复核的取消令牌。</param>
            /// <returns>提交后为 partial；未通过确认或失效检查时为 blocked。</returns>
            public static async Task<ProjectPackageOperationResult> ExecutePlannedProjectPackageOperationAsync(
                string planId,
                bool confirmed,
                CancellationToken token)
            {
                if (!confirmed)
                {
                    return CreateResult("blocked", "未获得针对本计划的明确确认。", null);
                }

                if (string.IsNullOrEmpty(planId) || !s_AgentPackagePlans.TryGetValue(planId, out AgentPackagePlanState state))
                {
                    return CreateResult("blocked", "计划不存在、已消费或已因 domain reload 失效，请重新计划。", null);
                }

                s_AgentPackagePlans.Remove(planId);
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return CreateResult("blocked", "Unity 正在编译或更新包，请重新计划。", null);
                }

                string currentFingerprint = ComputePackageStateFingerprint(state.manifestPath, state.lockPath);
                if (!string.Equals(currentFingerprint, state.stateFingerprint, StringComparison.Ordinal))
                {
                    return CreateResult("blocked", "manifest 或 packages-lock 已变化，旧计划失效。", null);
                }

                if (state.plan.action != "uninstall")
                {
                    RegistryCatalog latestCatalog = await LoadRegistryCatalogAsync(token);
                    if (latestCatalog.errors.Count > 0)
                    {
                        return CreateResult("blocked", "未能完整复核全部已配置 registry，旧计划失效。" + JoinErrors(latestCatalog.errors), null);
                    }

                    RegistryPackageCandidate latestCandidate = SelectRegistryCandidate(
                        state.candidate.source.Url,
                        state.plan.packageName,
                        null,
                        latestCatalog,
                        null);
                    if (latestCandidate == null || latestCandidate.entry.LatestVersion != state.plan.targetVersion)
                    {
                        return CreateResult("blocked", "远端最新版本或 registry 可达性已变化，旧计划失效。", null);
                    }
                }

                if (state.plan.action == "uninstall")
                {
                    ManifestData manifest = ReadManifest(state.manifestPath);
                    ApplyUninstallManifestChanges(
                        manifest,
                        state.plan.selectedRegistryUrl,
                        state.entry,
                        state.registryUrlsNeededByOthers,
                        false);
                    SaveManifest(state.manifestPath, manifest);
                    ResolvePackages();
                }
                else
                {
                    bool submitted = InstallPackage(
                        state.manifestPath,
                        state.candidate.source.Url,
                        state.candidate.source.Name,
                        state.candidate.entry,
                        state.knownPackages);
                    if (!submitted)
                    {
                        return CreateResult("blocked", "执行期依赖预检未通过，未提交 UPM Resolve。", null);
                    }
                }

                ProjectPackageOperationReceipt receipt = new ProjectPackageOperationReceipt
                {
                    action = state.plan.action,
                    packageName = state.plan.packageName,
                    expectedVersion = state.plan.targetVersion,
                    expectedRegistryUrl = state.plan.selectedRegistryUrl,
                };
                return CreateResult("partial", "manifest 已提交，等待 UPM Resolve 与 domain reload 完成后验证。", receipt);
            }

            /// <summary>
            /// 在 UPM Resolve 与 domain reload 稳定后只读验证操作结果；不会自动重试或再次写入。
            /// </summary>
            /// <param name="receipt">执行入口返回的验证凭据。</param>
            /// <returns>达到精确版本/移除条件时 success，尚未稳定时 partial。</returns>
            public static ProjectPackageOperationResult VerifyProjectPackageOperation(ProjectPackageOperationReceipt receipt)
            {
                if (receipt == null || string.IsNullOrEmpty(receipt.packageName))
                {
                    return CreateResult("blocked", "验证凭据为空。", null);
                }

                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return CreateResult("partial", "Unity 仍在编译或更新包。", receipt);
                }

                ManifestData manifest = ReadManifest(GetProjectManifestPath());
                PackagesLockData lockData = ReadPackagesLock(GetProjectLockPath());
                string directValue = null;
                PackagesLockEntry lockEntry = null;
                manifest?.dependencies?.TryGetValue(receipt.packageName, out directValue);
                lockData?.dependencies?.TryGetValue(receipt.packageName, out lockEntry);

                if (receipt.action == "uninstall")
                {
                    bool removed = string.IsNullOrEmpty(directValue) && lockEntry == null;
                    return removed
                        ? CreateResult("success", "目标包已从 direct manifest 与解析图移除。", receipt)
                        : CreateResult("partial", "目标包仍存在于 manifest 或解析图；不会自动重放卸载。", receipt);
                }

                string installedVersion = ResolveInstalledVersion(receipt.packageName, lockEntry);
                bool exact = directValue == receipt.expectedVersion &&
                             installedVersion == receipt.expectedVersion &&
                             lockEntry?.source == "registry" &&
                             lockEntry.url == receipt.expectedRegistryUrl;
                return exact
                    ? CreateResult("success", "manifest 与 packages-lock 已解析到计划的精确 registry 版本。", receipt)
                    : CreateResult("partial", "UPM 尚未解析到计划的精确 registry 版本；不会自动重放写操作。", receipt);
            }

            /// <summary>
            /// 应用卸载计划的纯 manifest 变更；Agent 可选择保留来源不明的旧顶层依赖。
            /// </summary>
            /// <param name="manifest">待修改的 manifest 模型。</param>
            /// <param name="registryUrl">主包来源 registry URL，可为空。</param>
            /// <param name="entry">当前已安装包元数据。</param>
            /// <param name="registryUrlsNeededByOthers">仍被其它已安装包共用的私有 registry URL。</param>
            /// <param name="removeLegacyTopLevelDependencies">是否兼容清理旧版 PlugPals 展开的顶层依赖。</param>
            internal static void ApplyUninstallManifestChanges(
                ManifestData manifest,
                string registryUrl,
                PackageDisplayEntry entry,
                ISet<string> registryUrlsNeededByOthers,
                bool removeLegacyTopLevelDependencies)
            {
                if (manifest == null || entry == null)
                {
                    return;
                }

                manifest.dependencies?.Remove(entry.Name);
                if (removeLegacyTopLevelDependencies && manifest.dependencies != null)
                {
                    foreach (string dependencyName in CollectDeclaredRegistryDependencies(entry).Keys)
                    {
                        manifest.dependencies.Remove(dependencyName);
                    }
                }

                CleanupScopedRegistry(manifest, registryUrl);
                if (entry.Nova?.scopedRegistries == null)
                {
                    return;
                }

                foreach (ScopedRegistry registry in entry.Nova.scopedRegistries)
                {
                    if (registry == null || string.IsNullOrEmpty(registry.url) ||
                        (registryUrlsNeededByOthers != null && registryUrlsNeededByOthers.Contains(registry.url)))
                    {
                        continue;
                    }

                    RemoveDeclaredScopedRegistryByUrl(manifest, registry.url);
                }
            }

            /// <summary>
            /// 建立基础计划并拒绝非法动作、非法包名和 Framework 自身。
            /// </summary>
            private static ProjectPackageOperationPlan CreateBasePlan(ProjectPackageOperationRequest request)
            {
                var plan = new ProjectPackageOperationPlan
                {
                    planId = Guid.NewGuid().ToString("N"),
                    status = "ready",
                    action = request?.action?.Trim(),
                    packageName = request?.packageName?.Trim(),
                };

                if (plan.action != "install-latest" && plan.action != "upgrade-latest" && plan.action != "uninstall")
                {
                    return BlockPlan(plan, "action 只允许 install-latest、upgrade-latest 或 uninstall。");
                }

                if (string.IsNullOrEmpty(plan.packageName))
                {
                    return BlockPlan(plan, "packageName 不能为空。");
                }

                return plan.packageName == c_FrameworkPackageName
                    ? BlockPlan(plan, "禁止通过消费端包管理 Skill 修改 Nova Framework 自身。")
                    : plan;
            }

            /// <summary>
            /// 生成卸载计划，要求目标为 direct dependency 且当前解析图中没有其它消费者。
            /// </summary>
            private static ProjectPackageOperationPlan PlanUninstall(
                ProjectPackageOperationPlan plan,
                ManifestData manifest,
                PackagesLockData lockData,
                string directValue,
                string manifestPath,
                string lockPath)
            {
                if (string.IsNullOrEmpty(directValue))
                {
                    return NotApplicablePlan(plan, "目标包不是项目 direct dependency，不能直接卸载传递依赖。");
                }

                plan.consumers.AddRange(CollectLockConsumers(lockData, plan.packageName));
                if (plan.consumers.Count > 0)
                {
                    return BlockPlan(plan, "仍有已解析包依赖目标包，禁止卸载。");
                }

                List<PackageDisplayEntry> installedEntries = LoadInstalledPackageEntries();
                PackageDisplayEntry entry = installedEntries.FirstOrDefault(item => item.Name == plan.packageName);
                if (entry == null)
                {
                    return BlockPlan(plan, "无法读取当前已安装包的 package.json 元数据，拒绝猜测卸载清理范围。");
                }

                RegistrySource source = FindProjectRegistrySource(manifest, plan.packageName);
                plan.selectedRegistryUrl = source?.Url;
                plan.messages.Add("将移除目标 direct dependency；保留来源不明的旧顶层依赖，并只清理确认不再共享的 registry。");

                var state = new AgentPackagePlanState
                {
                    plan = plan,
                    manifestPath = manifestPath,
                    lockPath = lockPath,
                    stateFingerprint = ComputePackageStateFingerprint(manifestPath, lockPath),
                    entry = entry,
                    registryUrlsNeededByOthers = CollectRegistryUrlsDeclaredByOtherInstalled(installedEntries, plan.packageName),
                };
                s_AgentPackagePlans[plan.planId] = state;
                return plan;
            }

            /// <summary>
            /// 加载全部已配置 registry；单个来源失败会留下证据，但不会丢弃其它已成功来源。
            /// </summary>
            private static async Task<RegistryCatalog> LoadRegistryCatalogAsync(CancellationToken token)
            {
                RegistriesConfig config = LoadRegistries();
                var catalog = new RegistryCatalog();
                await LoadRegistryIntoCatalogAsync(catalog, config.externalUrl, config.externalName, token);
                await LoadRegistryIntoCatalogAsync(catalog, config.internalUrl, config.internalName, token);
                return catalog;
            }

            /// <summary>
            /// 将单个 registry 的远端最新包信息加入计划目录。
            /// </summary>
            private static async Task LoadRegistryIntoCatalogAsync(
                RegistryCatalog catalog,
                string registryUrl,
                string registryName,
                CancellationToken token)
            {
                if (string.IsNullOrWhiteSpace(registryUrl))
                {
                    return;
                }

                try
                {
                    VerdaccioPackageInfo[] packages = await FetchRemotePackagesAsync(registryUrl, c_RegistryApiPath, token);
                    List<PackageDisplayEntry> entries = BuildDisplayEntries(packages ?? Array.Empty<VerdaccioPackageInfo>(), registryUrl);
                    var source = new RegistrySource { Url = registryUrl, Name = registryName };
                    foreach (PackageDisplayEntry entry in entries)
                    {
                        catalog.entries.Add(new RegistryPackageCandidate { entry = entry, source = source });
                        if (!catalog.knownPackages.ContainsKey(entry.Name))
                        {
                            catalog.knownPackages[entry.Name] = source;
                        }
                    }
                }
                catch (Exception exception)
                {
                    catalog.errors.Add($"{registryName}({registryUrl}): {exception.Message}");
                }
            }

            /// <summary>
            /// 根据显式已配置来源或当前 lock 来源选出唯一候选，避免同名仓库静默切换。
            /// </summary>
            private static RegistryPackageCandidate SelectRegistryCandidate(
                string requestedRegistryUrl,
                string packageName,
                PackagesLockEntry lockEntry,
                RegistryCatalog catalog,
                List<string> messages)
            {
                List<RegistryPackageCandidate> candidates = catalog.entries
                    .Where(item => item.entry.Name == packageName)
                    .ToList();
                if (!string.IsNullOrEmpty(requestedRegistryUrl))
                {
                    candidates = candidates.Where(item => item.source.Url == requestedRegistryUrl).ToList();
                }
                else if (!string.IsNullOrEmpty(lockEntry?.url))
                {
                    candidates = candidates.Where(item => item.source.Url == lockEntry.url).ToList();
                }

                if (candidates.Count != 1)
                {
                    messages?.Add(candidates.Count == 0 ? "已配置 registry 中未找到目标包。" : "多个 registry 提供同名包，需要指定已配置的 registryUrl 消歧。");
                    return null;
                }

                return candidates[0];
            }

            /// <summary>
            /// 使用当前 manifest、解析图和已成功 registry 目录执行不产生 UI 的依赖预检。
            /// </summary>
            private static DependencyCheckResult BuildAgentDependencyCheck(
                ManifestData manifest,
                PackageDisplayEntry entry,
                IReadOnlyDictionary<string, RegistrySource> knownPackages)
            {
                var installedNames = new HashSet<string>(StringComparer.Ordinal);
                Dictionary<string, string> installedVersions = ReadInstalledVersions();
                if (installedVersions != null)
                {
                    installedNames.UnionWith(installedVersions.Keys);
                }
                if (manifest.dependencies != null)
                {
                    installedNames.UnionWith(manifest.dependencies.Keys);
                }

                return CheckDependencies(
                    entry.Dependencies,
                    installedNames,
                    knownPackages,
                    entry.Nova,
                    manifest.scopedRegistries,
                    entry.Name,
                    entry.DisplayName);
            }

            /// <summary>
            /// 保存 ready 计划的执行所需快照；该快照只存在于当前 Editor domain。
            /// </summary>
            private static void StoreReadyPlan(
                ProjectPackageOperationPlan plan,
                string manifestPath,
                string lockPath,
                RegistryPackageCandidate candidate,
                RegistryCatalog catalog,
                DependencyCheckResult dependencyCheck)
            {
                s_AgentPackagePlans[plan.planId] = new AgentPackagePlanState
                {
                    plan = plan,
                    manifestPath = manifestPath,
                    lockPath = lockPath,
                    stateFingerprint = ComputePackageStateFingerprint(manifestPath, lockPath),
                    candidate = candidate,
                    entry = candidate.entry,
                    knownPackages = new Dictionary<string, RegistrySource>(catalog.knownPackages, StringComparer.Ordinal),
                    dependencyCheck = dependencyCheck,
                };
            }

            /// <summary>
            /// 从 packages-lock 图收集仍直接声明目标依赖的消费者。
            /// </summary>
            internal static List<string> CollectLockConsumers(PackagesLockData lockData, string packageName)
            {
                var consumers = new List<string>();
                if (lockData?.dependencies == null || string.IsNullOrEmpty(packageName))
                {
                    return consumers;
                }

                foreach (KeyValuePair<string, PackagesLockEntry> pair in lockData.dependencies)
                {
                    if (pair.Key != packageName && pair.Value?.dependencies != null && pair.Value.dependencies.ContainsKey(packageName))
                    {
                        consumers.Add(pair.Key);
                    }
                }

                consumers.Sort(StringComparer.Ordinal);
                return consumers;
            }

            /// <summary>
            /// 读取 packages-lock.json；失败时返回 null 并保留只读失败语义。
            /// </summary>
            private static PackagesLockData ReadPackagesLock(string path)
            {
                try
                {
                    return File.Exists(path) ? Util.Json.Deserialize<PackagesLockData>(File.ReadAllText(path)) : null;
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.Editor, "PlugPals Agent 读取 packages-lock.json 失败: {0}", exception.Message);
                    return null;
                }
            }

            /// <summary>
            /// 从当前已注册包的 resolvedPath/package.json 读取卸载所需的已安装元数据。
            /// </summary>
            private static List<PackageDisplayEntry> LoadInstalledPackageEntries()
            {
                var entries = new List<PackageDisplayEntry>();
                UnityEditor.PackageManager.PackageInfo[] packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (packages == null)
                {
                    return entries;
                }

                foreach (UnityEditor.PackageManager.PackageInfo package in packages)
                {
                    string packageJsonPath = IOPath.Combine(package.resolvedPath, "package.json");
                    if (!File.Exists(packageJsonPath))
                    {
                        continue;
                    }

                    try
                    {
                        PackageJsonData data = Util.Json.Deserialize<PackageJsonData>(File.ReadAllText(packageJsonPath));
                        if (data == null || string.IsNullOrEmpty(data.name))
                        {
                            continue;
                        }

                        entries.Add(new PackageDisplayEntry
                        {
                            Name = data.name,
                            DisplayName = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName,
                            LocalVersion = data.version,
                            LatestVersion = data.version,
                            Dependencies = data.dependencies,
                            Nova = data.nova,
                            Status = PackageStatus.Installed,
                            Category = CategorizePackage(data.name),
                        });
                    }
                    catch (Exception exception)
                    {
                        Log.Warning(LogTag.Editor, "PlugPals Agent 读取已安装包元数据失败 {0}: {1}", packageJsonPath, exception.Message);
                    }
                }

                return entries;
            }

            /// <summary>
            /// 查找当前 manifest 中覆盖目标包名的最长 registry scope。
            /// </summary>
            private static RegistrySource FindProjectRegistrySource(ManifestData manifest, string packageName)
            {
                ScopedRegistry best = null;
                int bestLength = -1;
                if (manifest?.scopedRegistries == null)
                {
                    return null;
                }

                foreach (ScopedRegistry registry in manifest.scopedRegistries)
                {
                    if (registry?.scopes == null)
                    {
                        continue;
                    }

                    foreach (string scope in registry.scopes)
                    {
                        if (!string.IsNullOrEmpty(scope) && packageName.StartsWith(scope, StringComparison.Ordinal) && scope.Length > bestLength)
                        {
                            best = registry;
                            bestLength = scope.Length;
                        }
                    }
                }

                return best == null ? null : new RegistrySource { Url = best.url, Name = best.name };
            }

            /// <summary>
            /// 解析 lock 条目的实际版本；file/git 来源沿用既有本地版本解析规则。
            /// </summary>
            private static string ResolveInstalledVersion(string packageName, PackagesLockEntry entry)
            {
                return entry == null ? null : ResolveEntryVersion(packageName, entry);
            }

            /// <summary>
            /// 判断 direct manifest 或 lock 是否明确为非 registry 来源。
            /// </summary>
            private static bool IsNonRegistryValue(string directValue, PackagesLockEntry entry)
            {
                return (!string.IsNullOrEmpty(directValue) &&
                        (directValue.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                         directValue.StartsWith("git", StringComparison.OrdinalIgnoreCase) ||
                         directValue.Contains("#"))) ||
                       (entry != null && !string.IsNullOrEmpty(entry.source) && entry.source != "registry");
            }

            /// <summary>
            /// 对 manifest 与 packages-lock 内容生成计划失效指纹。
            /// </summary>
            private static string ComputePackageStateFingerprint(string manifestPath, string lockPath)
            {
                string content = (File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : string.Empty) + "\n---LOCK---\n" +
                                 (File.Exists(lockPath) ? File.ReadAllText(lockPath) : string.Empty);
                using (SHA256 sha256 = SHA256.Create())
                {
                    return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(content)));
                }
            }

            /// <summary>
            /// 返回当前项目 manifest 的绝对路径。
            /// </summary>
            private static string GetProjectManifestPath()
            {
                return IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            }

            /// <summary>
            /// 返回当前项目 packages-lock 的绝对路径。
            /// </summary>
            private static string GetProjectLockPath()
            {
                return IOPath.GetFullPath(IOPath.Combine(Application.dataPath, "..", "Packages", "packages-lock.json"));
            }

            /// <summary>
            /// 把计划标记为 blocked 并移除不可执行的 planId。
            /// </summary>
            private static ProjectPackageOperationPlan BlockPlan(ProjectPackageOperationPlan plan, string message)
            {
                plan.status = "blocked";
                plan.planId = null;
                plan.messages.Add(message);
                return plan;
            }

            /// <summary>
            /// 把计划标记为 not_applicable 并移除不可执行的 planId。
            /// </summary>
            private static ProjectPackageOperationPlan NotApplicablePlan(ProjectPackageOperationPlan plan, string message)
            {
                plan.status = "not_applicable";
                plan.planId = null;
                plan.messages.Add(message);
                return plan;
            }

            /// <summary>
            /// 创建统一结果对象。
            /// </summary>
            private static ProjectPackageOperationResult CreateResult(
                string status,
                string message,
                ProjectPackageOperationReceipt receipt)
            {
                return new ProjectPackageOperationResult { status = status, message = message, receipt = receipt };
            }

            /// <summary>
            /// 把 registry 查询错误拼接为可读的补充证据。
            /// </summary>
            private static string JoinErrors(IReadOnlyList<string> errors)
            {
                return errors == null || errors.Count == 0 ? string.Empty : " 查询异常：" + string.Join("；", errors);
            }

            private sealed class RegistryCatalog
            {
                public readonly List<RegistryPackageCandidate> entries = new List<RegistryPackageCandidate>();
                public readonly Dictionary<string, RegistrySource> knownPackages = new Dictionary<string, RegistrySource>(StringComparer.Ordinal);
                public readonly List<string> errors = new List<string>();
            }

            private sealed class RegistryPackageCandidate
            {
                public PackageDisplayEntry entry;
                public RegistrySource source;
            }

            private sealed class AgentPackagePlanState
            {
                public ProjectPackageOperationPlan plan;
                public string manifestPath;
                public string lockPath;
                public string stateFingerprint;
                public RegistryPackageCandidate candidate;
                public PackageDisplayEntry entry;
                public Dictionary<string, RegistrySource> knownPackages;
                public DependencyCheckResult dependencyCheck;
                public HashSet<string> registryUrlsNeededByOthers;
            }
        }
    }
}
