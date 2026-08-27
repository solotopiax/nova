---
name: nova-project-configure-runtime
description: Use when 项目组要在已确认的 Platform、Channel、DevelopMode 三维坐标配置 Nova ConfigMaster，并导出和编译验证对应 ConfigRuntimeSO 时使用。
---

# Nova 配置运行时快照

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在需要确认设计态边界时读 `Docs/Editor/Config/ConfigMasterSO.md`；编辑或保存 `ConfigMasterSO` 时读 `Docs/Editor/Windows/ConfigWindow.md`；执行导出时读 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md` 和 `Docs/Runtime/Modules/Config/ConfigRuntimeSO.md`；复用既有 Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。若冻结字段触及已安装 SDK/Kit，先确认实际解析的 package id、版本、source 与根路径，并先读取其首个存在的 `Nova/Doc/INDEX.md`、`Nova/Docs/INDEX.md` 或 `Nova/DOCS/INDEX.md`；随后只按该 INDEX 读取目标 Config、路由、资源和平台前置。不要递归读取其他 SDK、Kit、YooAsset 或 CDN 文档。

## 冻结输入与决策门

冻结一个 `ConfigMasterSO`、Unity 当前 Active BuildTarget 实时映射出的非 `None` Platform、一个 `Channel × DevelopMode` 坐标、精确的字段变更集、目标 `ConfigRuntimeSO` 路径，以及是否明确要求写入活动场景的 DevelopMode / Channel 快照。不要把旧 Config 资产中的平台值、其他坐标、远端 JSON、CDN 凭据或 Sample 默认值当作本次输入。

- 没有确认的 Master、目标 Runtime 资产或对应坐标矩阵时保持 `blocked`；新建 Master 或 Runtime 资产必须先确认资产路径、坐标和写入范围。
- Platform 必须等于 Unity 当前 Active BuildTarget 映射值；仅 Android、iOS、WebGL 可用。未映射或请求平台不一致时先要求项目组切换 Unity BuildTarget，不通过 ConfigWindow、Pipify 参数或 Action 自动切换平台。
- 一次只编辑冻结坐标；跨平台、跨渠道、跨开发模式批量改动须作为新的确认，不从相邻格复制猜测。
- 使用 ConfigWindow 的 `Nova/Open Config` 保存设计态；该窗口的“导出”成功后还会写活动场景快照。若未明确授权写场景，使用 `EditorUtil.Config.Exporter.Export(...)` 或参数已冻结的 Pipify `export.config`，并保持该额外副作用排除在外。

## SDK / Kit 定向分支

仅当冻结字段触及已安装的 `com.solotopia.nova.framework.sdk.*` 或 `com.solotopia.nova.framework.kit.*` 时进入本分支。目标包的 INDEX、精确 Config 类型、路由/资源归属与平台前置不完整时保持 `blocked`，不靠包名、Plugin 类型名或相邻坐标猜测。

- SDK：用 `SDKPluginScanner.ScanAll()` 定位可用的 `ISDKPluginConfig`，以 Config 类型 `FullName` 写入 `ConfigMaster.EnabledSDKs`。这是唯一启用源；不得以 `SDKComponent.m_PluginEntries`、能力族面板或 Plugin 类型名代替。需要补实例时，仅对冻结 `PlatformChannelEntry` 与 `DevelopMode` 调用 `SDKPluginScanner.EnsureInstance(entry, mode, configType)`，再编辑该单格的值。
- Kit：用 `KitConfigScanner.ScanAll()` 定位可用的 `IKitConfig`，以 Config 类型 `FullName` 写入 `ConfigMaster.EnabledKits`。需要补实例时，仅对冻结 `PlatformChannelEntry` 与 `DevelopMode` 调用 `KitConfigScanner.EnsureInstance(entry, mode, configType)`；Kit 值属于单一 `Platform × Channel × DevelopMode` 格，不是全局 Kit 配置，也不得从相邻格复制猜测。
- `EnabledSDKs` / `EnabledKits` 的成员变更是 Master 级写入，仍须在字段变更集内逐个冻结；Scanner 的发现或补实例不证明厂商 SDK、路由、资源或目标平台已经可用。

## 受控 Action Adapter

执行前先用 `nova_project_action(operation="describe", action_id=...)` 获取当前 Registry Request Schema，不从本文猜字段。当前 MCP 已开放：

| 目的 | Action ID | 关键请求 |
|---|---|---|
| 校验单一坐标 | `nova.project.config.validate-coordinate` | `masterGuid/platform/channel/developMode` |
| 扫描 SDK/Kit Config 类型 | `nova.project.config.inspect-plugin-types` | `kind=sdk|kit|all` |
| 补齐并可选启用插件实例 | `nova.project.config.ensure-plugin-instances` | `masterGuid/kind/typeFullName/scope/coordinate?/enable` |
| 定位 Bundle Collector | `nova.project.config.inspect-bundle-collector` | `masterGuid` |
| 导出 Runtime 快照 | `nova.project.config.export-runtime` | `masterGuid/platform/channel/developMode/savePath` |

统一调用 `plan`。只读 Inspect Action 的 ready 计划可直接调用 `execute`，写入型 Ensure/Export 必须先展示精确 write set 并取得用户确认。Execute 必须同时传 `action_id` 与 `plan_id`；需要确认时再传 `confirmation_token=plan_id`。断线、编译或 domain reload 后只使用 `recovery_token` 调 `verify`，不得重放 Execute。

这些 Action 不负责任意字段编辑或活动场景快照。未被 Action 覆盖的已确认字段变更仍通过 ConfigWindow/受控 Unity 编辑完成；不得退化为任意 C# 字符串执行。`export-runtime` 只证明静态配置和目标 Artifact，Skill 的最终 `success` 仍需等待 Unity 编译通过。

## 执行与验证边界

按“保存 Master → 导出单一 Runtime 快照 → Unity 编译”执行。Exporter 会覆盖已存在的目标 `ConfigRuntimeSO`，并可能按已配置的维度解析更新对应 `YooAssetSettings.asset`；因此目标资产和该坐标的解析结果必须在确认的写入集内。导出时只验证本次坐标的 `EnabledSDKConfigs` / `EnabledKitConfigs` 快照，不把其他坐标、Bundle、Player、CDN 或厂商运行时结果推断为成功。

最低成功证据是指定 `ConfigRuntimeSO` 导出成功且 Unity 编译通过。只有资产保存、只有静态 diff、或只运行 Pipify 而没有确认编译时返回 `partial`。不把远端 Config 内容、Bundle、Player、CDN 或真机行为推断为本 Skill 的成功。
