---
name: nova-project-configure-runtime
description: Use when 项目组要在已确认的 Platform、Channel、DevelopMode 三维坐标配置 Nova ConfigMaster，并导出和编译验证对应 ConfigRuntimeSO 时使用。
---

# Nova 配置运行时快照

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在需要确认设计态边界时读 `Docs/Editor/Config/ConfigMasterSO.md`；编辑或保存 `ConfigMasterSO` 时读 `Docs/Editor/Windows/ConfigWindow.md`；执行导出时读 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md` 和 `Docs/Runtime/Modules/Config/ConfigRuntimeSO.md`；复用既有 Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。不要递归读取 SDK、Kit、YooAsset 或 CDN 文档，除非已冻结的字段实际触及它们。

## 冻结输入与决策门

冻结一个 `ConfigMasterSO`、一个非 `None` 的 `Platform × Channel × DevelopMode` 坐标、精确的字段变更集、目标 `ConfigRuntimeSO` 路径，以及是否明确要求写入活动场景的 DevelopMode / Channel 快照。不要把当前窗口选择、其他坐标、远端 JSON、CDN 凭据或 Sample 默认值当作本次输入。

- 没有确认的 Master、目标 Runtime 资产或对应坐标矩阵时保持 `blocked`；新建 Master 或 Runtime 资产必须先确认资产路径、坐标和写入范围。
- 一次只编辑冻结坐标；跨平台、跨渠道、跨开发模式批量改动须作为新的确认，不从相邻格复制猜测。
- 使用 ConfigWindow 的 `Nova/Open Config` 保存设计态；该窗口的“导出”成功后还会写活动场景快照。若未明确授权写场景，使用 `EditorUtil.Config.Exporter.Export(...)` 或参数已冻结的 Pipify `export.config`，并保持该额外副作用排除在外。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 Master、三维坐标与字段变更 | `Nova/Open Config` 的 ConfigWindow 保存流 | 指定坐标的 `ConfigMasterSO` 设计态配置 | 保存后的资产与冻结坐标、字段变更一致 |
| 已冻结的目标 Runtime 路径 | `EditorUtil.Config.Exporter.Export(master, platform, channel, mode, savePath)`；复用既有 Batch 时用 `export.config` 的显式参数 | 对应 `ConfigRuntimeSO` 快照 | Exporter 返回目标资产，且 Platform、Channel、DevelopMode 与本次输入一致 |
| 已确认的活动场景快照更新 | 仅使用 ConfigWindow 的“导出”附加动作 | 活动场景中 Nova / FrameworkComponent 的启动快照 | 场景变更在确认的范围内；未授权时不得产生该 Artifact |

`export.config` 使用固化的三维参数导出 Runtime，不会修改 ConfigMaster 当前选择；不要把它当作编辑设计态资产的入口。

## 执行与验证边界

按“保存 Master → 导出单一 Runtime 快照 → Unity 编译”执行。Exporter 会覆盖已存在的目标 `ConfigRuntimeSO`，并可能按已配置的维度解析更新对应 `YooAssetSettings.asset`；因此目标资产和该坐标的解析结果必须在确认的写入集内。

最低成功证据是指定 `ConfigRuntimeSO` 导出成功且 Unity 编译通过。只有资产保存、只有静态 diff、或只运行 Pipify 而没有确认编译时返回 `partial`。不把远端 Config 内容、Bundle、Player、CDN 或真机行为推断为本 Skill 的成功。
