/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigEnsurePluginInstancesAction.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   ConfigMaster SDK/Kit 配置实例与启用状态收敛 Action
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    [AgentAction(
        "nova.project.config.ensure-plugin-instances",
        "确保 Config 插件实例",
        "config",
        AgentActionOperationType.Ensure,
        Description = "根据已确认的 Config 插件类型补齐 ConfigMaster 中缺失的插件实例，不替换已有配置。",
        Effects = AgentActionEffect.WorkspaceRead |
                  AgentActionEffect.WorkspaceWrite |
                  AgentActionEffect.UnityRead |
                  AgentActionEffect.UnityWrite,
        RequiredEvidence = AgentActionEvidence.Static,
        Idempotency = AgentActionIdempotency.EnsureState,
        RequiresConfirmation = true,
        RequiresEditMode = true,
        Locks = new[] { "unity-editor", "configmaster-assets" })]
    internal sealed class ConfigEnsurePluginInstancesAction : AgentActionHandler<ConfigEnsurePluginInstancesAction.Request>
    {
        [Serializable]
        public sealed class Request
        {
            [AgentActionRequired] public string masterGuid;
            [AgentActionRequired] public string kind;
            [AgentActionRequired] public string typeFullName;
            [AgentActionRequired] public string scope;
            public Coordinate coordinate;
            public bool enable;
        }

        [Serializable]
        public sealed class Coordinate
        {
            [AgentActionRequired] public string platform;
            [AgentActionRequired] public string channel;
            [AgentActionRequired] public string developMode;
        }

        [Serializable]
        private sealed class CellView
        {
            public string kind;
            public string platform;
            public string channel;
            public string developMode;
            public bool matrixRowMissing;
            public bool modeSlotMissing;
        }

        [Serializable]
        private sealed class EnableChangeView
        {
            public string kind;
            public bool currentEnabled;
            public int currentOccurrences;
            public bool targetEnabled;
        }

        [Serializable]
        private sealed class PlanView
        {
            public string masterGuid;
            public string assetPath;
            public string assetSha256;
            public string requestedKind;
            public string[] resolvedKinds;
            public string typeFullName;
            public TypeIdentity[] typeIdentities;
            public string scope;
            public int targetCellCount;
            public CellView[] missingCells;
            public EnableChangeView[] enableChanges;
            public bool hasChanges;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string masterGuid;
            public string assetPath;
            public string plannedAssetSha256;
            public string typeFullName;
            public string scope;
            public Coordinate coordinate;
            public bool enable;
            public string[] resolvedKinds;
            public TypeIdentity[] typeIdentities;
        }

        [Serializable]
        private sealed class TypeIdentity
        {
            public string kind;
            public string typeFullName;
            public string assemblyIdentity;
            public string moduleVersionId;
        }

        [Serializable]
        private sealed class FrozenDiff
        {
            public CellView[] missingCells;
            public EnableChangeView[] enableChanges;
        }

        private sealed class ResolvedKind
        {
            public string Name;
            public Type ConfigType;
            public TypeIdentity Identity;
        }

        private sealed class TargetCell
        {
            public PlatformType Platform;
            public ChannelType Channel;
            public DevelopMode Mode;
        }

        private sealed class State
        {
            public string AssetPath;
            public string AssetSha256;
            public string DiffJson;
            public Receipt Receipt;
        }

        /// <summary>
        /// 严格校验资产、类别、类型全名、作用域和 coordinate 专属坐标字段。
        /// </summary>
        protected override bool TryValidateRequest(Request request, out string error)
        {
            error = null;
            if (request == null || !IsGuid(request.masterGuid))
            {
                error = "masterGuid 必须是 32 位十六进制 Unity GUID。";
                return false;
            }
            if (request.kind != "sdk" && request.kind != "kit" && request.kind != "all")
            {
                error = "kind 只能是 sdk、kit 或 all。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.typeFullName) || request.typeFullName.Length > 512 || request.typeFullName != request.typeFullName.Trim())
            {
                error = "typeFullName 必须是非空、无首尾空白且不超过 512 字符的类型全名。";
                return false;
            }
            if (request.scope != "coordinate" && request.scope != "matrix")
            {
                error = "scope 只能是 coordinate 或 matrix。";
                return false;
            }
            if (request.scope == "coordinate")
            {
                return TryParseCoordinate(request, out _, out _, out _, out error);
            }
            if (request.coordinate != null)
            {
                error = "scope=matrix 时不得提供 coordinate，避免坐标被误解为过滤条件。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 只读冻结目标资产、Scanner 白名单类型和全部目标格，并精确列出缺失格与 enable 变化。
        /// </summary>
        public override Task<AgentActionHandlerPlan> PlanAsync(Request request, AgentActionExecutionContext context)
        {
            if (!TryLoadMaster(request.masterGuid, out ConfigMasterSO master, out string assetPath, out string error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            }
            if (!TryResolveKinds(request.kind, request.typeFullName, out ResolvedKind[] kinds, out error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            }
            if (!TryBuildTargets(request, out TargetCell[] targets, out error))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = error });
            }
            if (EditorUtility.IsDirty(master))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "ConfigMaster 存在未保存改动，无法冻结可复核的资产 SHA-256。" });
            }
            string assetAbsolute = Path.Combine(GenerateActionCommon.ProjectRoot, assetPath);
            if (!File.Exists(assetAbsolute))
            {
                return Task.FromResult(new AgentActionHandlerPlan { Status = "blocked", Summary = "ConfigMaster 资产文件不存在，无法冻结 SHA-256。" });
            }
            string assetSha256 = GenerateActionCommon.ComputeFileHash(assetAbsolute, context.CancellationToken);
            FrozenDiff diff = BuildDiff(master, kinds, targets, request.typeFullName, request.enable);
            string diffJson = Util.Json.Serialize(diff);

            var receipt = new Receipt
            {
                masterGuid = request.masterGuid,
                assetPath = assetPath,
                plannedAssetSha256 = assetSha256,
                typeFullName = request.typeFullName,
                scope = request.scope,
                coordinate = request.coordinate == null ? null : new Coordinate
                {
                    platform = request.coordinate.platform,
                    channel = request.coordinate.channel,
                    developMode = request.coordinate.developMode,
                },
                enable = request.enable,
                resolvedKinds = kinds.Select(kind => kind.Name).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                typeIdentities = kinds.Select(kind => kind.Identity).OrderBy(value => value.kind, StringComparer.Ordinal).ToArray(),
            };
            var view = new PlanView
            {
                masterGuid = request.masterGuid,
                assetPath = assetPath,
                assetSha256 = assetSha256,
                requestedKind = request.kind,
                resolvedKinds = receipt.resolvedKinds,
                typeFullName = request.typeFullName,
                typeIdentities = receipt.typeIdentities,
                scope = request.scope,
                targetCellCount = targets.Length * kinds.Length,
                missingCells = diff.missingCells,
                enableChanges = diff.enableChanges,
                hasChanges = diff.missingCells.Length > 0 || diff.enableChanges.Length > 0,
            };
            string receiptJson = Util.Json.Serialize(receipt);
            return Task.FromResult(new AgentActionHandlerPlan
            {
                Status = "ready",
                Summary = view.hasChanges
                    ? $"将补齐 {view.missingCells.Length} 个插件配置格，并应用 {view.enableChanges.Length} 项 enable 变化。"
                    : "目标插件实例与 enable 状态已满足，无需写入。",
                DataJson = Util.Json.Serialize(view),
                State = new State
                {
                    AssetPath = assetPath,
                    AssetSha256 = assetSha256,
                    DiffJson = diffJson,
                    Receipt = receipt,
                },
                RecoveryPayloadJson = receiptJson,
                WriteSet = view.hasChanges ? new[] { assetPath } : Array.Empty<string>(),
                Evidence = new[]
                {
                    "类型仅通过 TypeCache 元数据解析；Plan 不构造消费项目插件实例。",
                    "已冻结 ConfigMaster SHA-256、类型程序集完整身份、ModuleVersionId 与精确 diff。",
                    request.scope == "matrix"
                        ? "matrix 明确覆盖全部有效 PlatformType × ChannelType × DevelopMode 格。"
                        : "coordinate 仅覆盖请求中的单个三维坐标。",
                },
            });
        }

        /// <summary>
        /// 通过 ConfigMaster 与 Scanner 的公开 API 幂等补齐实例、规范化 enable 列表并保存资产。
        /// </summary>
        public override Task<AgentActionResult> ExecuteAsync(object state, AgentActionExecutionContext context)
        {
            if (!(state is State typed) || typed.Receipt == null || string.IsNullOrEmpty(typed.AssetPath) ||
                string.IsNullOrEmpty(typed.AssetSha256) || string.IsNullOrEmpty(typed.DiffJson))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config 插件 Ensure 的内部计划状态无效。"));
            }
            if (!TryLoadMaster(typed.Receipt.masterGuid, out ConfigMasterSO master, out string assetPath, out string error) ||
                !string.Equals(assetPath, typed.AssetPath, StringComparison.Ordinal))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "ConfigMaster 路径已漂移，请重新 Plan。"));
            }
            if (EditorUtility.IsDirty(master))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster 在 Plan 后出现未保存改动，拒绝执行。"));
            }
            string currentHash = GenerateActionCommon.ComputeFileHash(
                Path.Combine(GenerateActionCommon.ProjectRoot, assetPath), context.CancellationToken);
            if (!string.Equals(currentHash, typed.AssetSha256, StringComparison.Ordinal))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster SHA-256 已漂移，请重新 Plan。"));
            }
            if (!TryResolveKinds(typed.Receipt.typeIdentities, out ResolvedKind[] kinds, out error) ||
                !TryBuildTargets(typed.Receipt, out TargetCell[] targets, out error))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error));
            }
            FrozenDiff currentDiff = BuildDiff(master, kinds, targets, typed.Receipt.typeFullName, typed.Receipt.enable);
            if (!string.Equals(Util.Json.Serialize(currentDiff), typed.DiffJson, StringComparison.Ordinal))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "ConfigMaster 精确 diff 已漂移，请重新 Plan。"));
            }

            int addedInstances = 0;
            int addedRows = 0;
            foreach (TargetCell target in targets)
            {
                if (!master.TryGetEntry(target.Platform, target.Channel, out PlatformChannelEntry entry))
                {
                    entry = new PlatformChannelEntry { Platform = target.Platform, Channel = target.Channel };
                    master.EditorAddEntry(entry);
                    addedRows++;
                }
                foreach (ResolvedKind kind in kinds)
                {
                    bool added = kind.Name == "sdk"
                        ? EditorUtil.Config.SDKPluginScanner.EnsureInstance(entry, target.Mode, kind.ConfigType)
                        : EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, target.Mode, kind.ConfigType);
                    if (added) addedInstances++;
                }
            }

            int enableChanges = 0;
            foreach (ResolvedKind kind in kinds)
            {
                List<string> enabled = kind.Name == "sdk" ? master.EnabledSDKs : master.EnabledKits;
                if (enabled == null)
                {
                    if (!typed.Receipt.enable) continue;
                    enabled = new List<string>();
                    if (kind.Name == "sdk") master.EnabledSDKs = enabled;
                    else master.EnabledKits = enabled;
                }
                int oldCount = enabled.Count(value => value == typed.Receipt.typeFullName);
                enabled.RemoveAll(value => value == typed.Receipt.typeFullName);
                if (typed.Receipt.enable) enabled.Add(typed.Receipt.typeFullName);
                if (oldCount != (typed.Receipt.enable ? 1 : 0)) enableChanges++;
            }

            bool changed = addedRows > 0 || addedInstances > 0 || enableChanges > 0;
            if (changed)
            {
                EditorUtility.SetDirty(master);
                AssetDatabase.SaveAssetIfDirty(master);
            }

            string receiptJson = Util.Json.Serialize(typed.Receipt);
            AgentActionResult result = AgentActionResult.Create(null, "success", changed
                ? $"已新增 {addedRows} 个矩阵行、{addedInstances} 个插件配置实例，并应用 {enableChanges} 项 enable 变化。"
                : "目标状态已满足，未写入 ConfigMaster。");
            result.DataJson = Util.Json.Serialize(new
            {
                changed,
                addedRows,
                addedInstances,
                enableChanges,
                assetPath = AssetDatabase.GUIDToAssetPath(typed.Receipt.masterGuid),
            });
            result.ReceiptJson = receiptJson;
            result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add(changed
                ? "已对 ConfigMaster 调用 SetDirty 与 SaveAssetIfDirty。"
                : "执行时再次确认目标状态，无需脏标记或保存。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 重新按 GUID 加载 ConfigMaster，并逐格核对实例存在与 enable 精确状态。
        /// </summary>
        public override Task<AgentActionResult> VerifyAsync(string receiptJson, AgentActionExecutionContext context)
        {
            Receipt receipt;
            try
            {
                receipt = Util.Json.Deserialize<Receipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", "Config 插件 Ensure Receipt 无法解析：" + exception.Message));
            }
            if (!TryValidateReceipt(receipt, out string error) ||
                !TryLoadMaster(receipt.masterGuid, out ConfigMasterSO master, out string assetPath, out error) ||
                !string.Equals(assetPath, receipt.assetPath, StringComparison.Ordinal) ||
                !TryResolveKinds(receipt.typeIdentities, out ResolvedKind[] resolvedKinds, out error) ||
                !TryBuildTargets(receipt, out TargetCell[] targets, out error))
            {
                return Task.FromResult(AgentActionResult.Create(null, "blocked", error ?? "Receipt 中的 ConfigMaster 路径已漂移。"));
            }

            var missing = new List<string>();
            foreach (TargetCell target in targets)
            {
                master.TryGetEntry(target.Platform, target.Channel, out PlatformChannelEntry entry);
                foreach (ResolvedKind kind in resolvedKinds)
                {
                    bool present = HasInstance(entry, target.Mode, kind, out _);
                    if (!present) missing.Add($"{kind.Name}:{target.Platform}/{target.Channel}/{target.Mode}");
                }
            }

            var enableMismatches = new List<string>();
            foreach (string kind in receipt.resolvedKinds)
            {
                List<string> enabled = kind == "sdk" ? master.EnabledSDKs : master.EnabledKits;
                int count = enabled == null ? 0 : enabled.Count(value => value == receipt.typeFullName);
                if (count != (receipt.enable ? 1 : 0))
                {
                    enableMismatches.Add($"{kind}: expected={(receipt.enable ? 1 : 0)}, actual={count}");
                }
            }

            bool success = missing.Count == 0 && enableMismatches.Count == 0;
            AgentActionResult result = AgentActionResult.Create(null, success ? "success" : "partial", success
                ? "重新加载 ConfigMaster 后，全部目标格与 enable 状态均符合 Receipt。"
                : $"重新加载核对未完成：缺失 {missing.Count} 格，enable 不匹配 {enableMismatches.Count} 项。");
            result.DataJson = Util.Json.Serialize(new
            {
                assetPath,
                targetCellCount = targets.Length * resolvedKinds.Length,
                missingCells = missing.ToArray(),
                enableMismatches = enableMismatches.ToArray(),
            });
            result.ReceiptJson = receiptJson;
            if (success) result.EvidenceKinds = AgentActionEvidence.Static;
            result.Evidence.Add("Verify 已重新按 masterGuid 加载资产并逐格核对，不会补写或重放 Execute。" );
            return Task.FromResult(result);
        }

        /// <summary>
        /// 以稳定顺序计算 Plan/Execute 共用的精确差异，避免执行时隐式扩大写入面。
        /// </summary>
        private static FrozenDiff BuildDiff(
            ConfigMasterSO master,
            ResolvedKind[] kinds,
            TargetCell[] targets,
            string typeFullName,
            bool enable)
        {
            var missing = new List<CellView>();
            foreach (TargetCell target in targets)
            {
                bool rowMissing = !master.TryGetEntry(target.Platform, target.Channel, out PlatformChannelEntry entry);
                foreach (ResolvedKind kind in kinds)
                {
                    bool instanceMissing = !HasInstance(entry, target.Mode, kind, out bool modeMissing);
                    if (!instanceMissing) continue;
                    missing.Add(new CellView
                    {
                        kind = kind.Name,
                        platform = target.Platform.ToString(),
                        channel = target.Channel.ToString(),
                        developMode = target.Mode.ToString(),
                        matrixRowMissing = rowMissing,
                        modeSlotMissing = modeMissing,
                    });
                }
            }

            EnableChangeView[] enableChanges = kinds
                .Select(kind =>
                {
                    List<string> enabled = kind.Name == "sdk" ? master.EnabledSDKs : master.EnabledKits;
                    int count = enabled == null ? 0 : enabled.Count(item => item == typeFullName);
                    return new EnableChangeView
                    {
                        kind = kind.Name,
                        currentEnabled = count > 0,
                        currentOccurrences = count,
                        targetEnabled = enable,
                    };
                })
                .Where(change => change.currentOccurrences != (change.targetEnabled ? 1 : 0))
                .OrderBy(change => change.kind, StringComparer.Ordinal)
                .ToArray();

            return new FrozenDiff
            {
                missingCells = missing
                    .OrderBy(cell => cell.kind, StringComparer.Ordinal)
                    .ThenBy(cell => cell.platform, StringComparer.Ordinal)
                    .ThenBy(cell => cell.channel, StringComparer.Ordinal)
                    .ThenBy(cell => cell.developMode, StringComparer.Ordinal)
                    .ToArray(),
                enableChanges = enableChanges,
            };
        }

        /// <summary>
        /// 通过 TypeCache 元数据精确解析类型；不为扫描执行任何消费项目类型构造器。
        /// </summary>
        private static bool TryResolveKinds(string requestedKind, string typeFullName, out ResolvedKind[] kinds, out string error)
        {
            var result = new List<ResolvedKind>();
            if (requestedKind == "sdk" || requestedKind == "all")
            {
                Type[] matches = FindConfigTypes<ISDKPluginConfig>(typeFullName);
                if (matches.Length > 1)
                {
                    kinds = null;
                    error = "SDK 类型元数据中存在多个相同 typeFullName，无法安全选择程序集。";
                    return false;
                }
                if (matches.Length == 1) result.Add(CreateResolvedKind("sdk", matches[0]));
            }
            if (requestedKind == "kit" || requestedKind == "all")
            {
                Type[] matches = FindConfigTypes<IKitConfig>(typeFullName);
                if (matches.Length > 1)
                {
                    kinds = null;
                    error = "Kit 类型元数据中存在多个相同 typeFullName，无法安全选择程序集。";
                    return false;
                }
                if (matches.Length == 1) result.Add(CreateResolvedKind("kit", matches[0]));
            }
            if (result.Count == 0)
            {
                kinds = null;
                error = $"当前 {requestedKind} Scanner 白名单未发现类型：{typeFullName}。";
                return false;
            }
            kinds = result.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            error = null;
            return true;
        }

        /// <summary>
        /// 按 Receipt 冻结的程序集完整身份与 MVID 重新解析类型；任一漂移都 fail-closed。
        /// </summary>
        private static bool TryResolveKinds(TypeIdentity[] identities, out ResolvedKind[] kinds, out string error)
        {
            if (identities == null || identities.Length == 0)
            {
                kinds = null;
                error = "Receipt 缺少类型程序集身份。";
                return false;
            }
            var result = new List<ResolvedKind>();
            foreach (TypeIdentity identity in identities)
            {
                Type[] matches = identity.kind == "sdk"
                    ? FindConfigTypes<ISDKPluginConfig>(identity.typeFullName)
                    : identity.kind == "kit"
                        ? FindConfigTypes<IKitConfig>(identity.typeFullName)
                        : Array.Empty<Type>();
                Type match = matches.Length == 1 ? matches[0] : null;
                if (match == null ||
                    !string.Equals(match.Assembly.FullName, identity.assemblyIdentity, StringComparison.Ordinal) ||
                    !string.Equals(match.Module.ModuleVersionId.ToString("D"), identity.moduleVersionId, StringComparison.Ordinal))
                {
                    kinds = null;
                    error = $"{identity.kind}:{identity.typeFullName} 的程序集身份已漂移或存在歧义。";
                    return false;
                }
                result.Add(CreateResolvedKind(identity.kind, match));
            }
            if (result.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != result.Count)
            {
                kinds = null;
                error = "Receipt 包含重复 kind 类型身份。";
                return false;
            }
            kinds = result.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            error = null;
            return true;
        }

        private static Type[] FindConfigTypes<TContract>(string typeFullName)
        {
            return TypeCache.GetTypesDerivedFrom<TContract>()
                .Where(type => IsSafeConfigType(type) && string.Equals(type.FullName, typeFullName, StringComparison.Ordinal))
                .OrderBy(type => type.Assembly.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.Module.ModuleVersionId)
                .ToArray();
        }

        private static bool IsSafeConfigType(Type type)
        {
            return type != null && !type.IsAbstract && !type.IsInterface && type.IsSerializable &&
                   !type.Assembly.GetReferencedAssemblies().Any(name => name.Name == "nunit.framework") &&
                   (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null);
        }

        private static ResolvedKind CreateResolvedKind(string kind, Type type)
        {
            return new ResolvedKind
            {
                Name = kind,
                ConfigType = type,
                Identity = new TypeIdentity
                {
                    kind = kind,
                    typeFullName = type.FullName,
                    assemblyIdentity = type.Assembly.FullName,
                    moduleVersionId = type.Module.ModuleVersionId.ToString("D"),
                },
            };
        }

        /// <summary>
        /// 根据 coordinate 或 matrix 契约生成稳定目标格列表。
        /// </summary>
        private static bool TryBuildTargets(Request request, out TargetCell[] targets, out string error)
        {
            if (request.scope == "coordinate")
            {
                if (!TryParseCoordinate(request, out PlatformType platform, out ChannelType channel, out DevelopMode mode, out error))
                {
                    targets = null;
                    return false;
                }
                targets = new[] { new TargetCell { Platform = platform, Channel = channel, Mode = mode } };
                return true;
            }
            targets = BuildMatrixTargets();
            error = null;
            return true;
        }

        /// <summary>
        /// 根据 Receipt 生成稳定目标格列表。
        /// </summary>
        private static bool TryBuildTargets(Receipt receipt, out TargetCell[] targets, out string error)
        {
            if (receipt.scope == "coordinate")
            {
                if (!TryParseCoordinate(receipt.coordinate,
                        out PlatformType platform, out ChannelType channel, out DevelopMode mode, out error))
                {
                    targets = null;
                    return false;
                }
                targets = new[] { new TargetCell { Platform = platform, Channel = channel, Mode = mode } };
                return true;
            }
            if (receipt.scope != "matrix")
            {
                targets = null;
                error = "Receipt scope 只能是 coordinate 或 matrix。";
                return false;
            }
            targets = BuildMatrixTargets();
            error = null;
            return true;
        }

        /// <summary>
        /// 枚举全部有效平台、渠道和开发模式，构造 matrix 的完整笛卡尔积。
        /// </summary>
        private static TargetCell[] BuildMatrixTargets()
        {
            return (from PlatformType platform in Enum.GetValues(typeof(PlatformType))
                    where platform != PlatformType.None
                    from ChannelType channel in Enum.GetValues(typeof(ChannelType))
                    from DevelopMode mode in Enum.GetValues(typeof(DevelopMode))
                    orderby platform, channel, mode
                    select new TargetCell { Platform = platform, Channel = channel, Mode = mode }).ToArray();
        }

        /// <summary>
        /// 判断目标格是否已有指定 Scanner 类型实例，同时报告 mode 包装项是否缺失。
        /// </summary>
        private static bool HasInstance(
            PlatformChannelEntry entry,
            DevelopMode mode,
            ResolvedKind kind,
            out bool modeMissing)
        {
            modeMissing = true;
            if (entry == null) return false;
            if (kind.Name == "sdk")
            {
                DevelopModeSDKEntry slot = entry.SDKConfigsByMode?.FirstOrDefault(item => item != null && item.Mode == mode);
                if (slot == null) return false;
                modeMissing = false;
                return slot.SDKConfigs != null && slot.SDKConfigs.Any(item => item != null && item.GetType() == kind.ConfigType);
            }
            DevelopModeKitEntry kitSlot = entry.KitConfigsByMode?.FirstOrDefault(item => item != null && item.Mode == mode);
            if (kitSlot == null) return false;
            modeMissing = false;
            return kitSlot.KitConfigs != null && kitSlot.KitConfigs.Any(item => item != null && item.GetType() == kind.ConfigType);
        }

        /// <summary>
        /// 校验并解析 coordinate 请求。
        /// </summary>
        private static bool TryParseCoordinate(
            Request request,
            out PlatformType platform,
            out ChannelType channel,
            out DevelopMode mode,
            out string error)
        {
            return TryParseCoordinate(request.coordinate, out platform, out channel, out mode, out error);
        }

        /// <summary>
        /// 严格按枚举名称解析三维坐标：Platform 拒绝 None，Channel 允许 None，并拒绝数字字符串。
        /// </summary>
        private static bool TryParseCoordinate(
            Coordinate coordinate,
            out PlatformType platform,
            out ChannelType channel,
            out DevelopMode mode,
            out string error)
        {
            platform = default;
            channel = default;
            mode = default;
            error = null;
            if (coordinate == null)
            {
                error = "scope=coordinate 时必须提供 coordinate。";
                return false;
            }
            string platformText = coordinate.platform;
            string channelText = coordinate.channel;
            string modeText = coordinate.developMode;
            if (!Enum.TryParse(platformText, false, out platform) || !Enum.IsDefined(typeof(PlatformType), platform) || platform == PlatformType.None || platformText != platform.ToString())
            {
                error = "coordinate.platform 必须是有效且非 None 的 PlatformType 名称。";
                return false;
            }
            if (!Enum.TryParse(channelText, false, out channel) || !Enum.IsDefined(typeof(ChannelType), channel) || channelText != channel.ToString())
            {
                error = "coordinate.channel 必须是有效的 ChannelType 名称。";
                return false;
            }
            if (!Enum.TryParse(modeText, false, out mode) || !Enum.IsDefined(typeof(DevelopMode), mode) || modeText != mode.ToString())
            {
                error = "coordinate.developMode 必须是有效的 DevelopMode 名称。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 按 Unity GUID 精确加载 ConfigMasterSO。
        /// </summary>
        private static bool TryLoadMaster(string guid, out ConfigMasterSO master, out string assetPath, out string error)
        {
            assetPath = AssetDatabase.GUIDToAssetPath(guid);
            master = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(assetPath);
            error = master == null ? "masterGuid 未指向可加载的 ConfigMasterSO。" : null;
            return master != null;
        }

        /// <summary>
        /// 校验 Receipt 自包含字段，防止 Verify 接受超出原计划的 kind 或资产范围。
        /// </summary>
        private static bool TryValidateReceipt(Receipt receipt, out string error)
        {
            error = null;
            if (receipt == null || !IsGuid(receipt.masterGuid) || string.IsNullOrWhiteSpace(receipt.typeFullName) ||
                string.IsNullOrWhiteSpace(receipt.assetPath) || string.IsNullOrWhiteSpace(receipt.plannedAssetSha256))
            {
                error = "Config 插件 Ensure Receipt 缺少有效资产或类型标识。";
                return false;
            }
            if (receipt.resolvedKinds == null || receipt.resolvedKinds.Length == 0 ||
                receipt.resolvedKinds.Any(kind => kind != "sdk" && kind != "kit") ||
                receipt.resolvedKinds.Distinct(StringComparer.Ordinal).Count() != receipt.resolvedKinds.Length)
            {
                error = "Config 插件 Ensure Receipt 的 resolvedKinds 无效。";
                return false;
            }
            if (receipt.typeIdentities == null || receipt.typeIdentities.Length != receipt.resolvedKinds.Length ||
                receipt.typeIdentities.Any(identity => identity == null ||
                    identity.typeFullName != receipt.typeFullName ||
                    string.IsNullOrWhiteSpace(identity.assemblyIdentity) ||
                    !Guid.TryParse(identity.moduleVersionId, out _)) ||
                !receipt.typeIdentities.Select(identity => identity.kind)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(receipt.resolvedKinds.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                error = "Config 插件 Ensure Receipt 的类型程序集身份无效。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 判断字符串是否为 Unity 资产使用的 32 位十六进制 GUID。
        /// </summary>
        private static bool IsGuid(string value)
        {
            return value != null && value.Length == 32 && value.All(Uri.IsHexDigit);
        }
    }
}
