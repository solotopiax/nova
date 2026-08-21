---
name: nova-project-integrate-vibration
description: Use when 项目组要在现有 Nova 项目中接入已确认的 Emphasis 或 Custom 振动数据、业务触发与停止生命周期，并需完成真机反馈验证时使用。
---

# Nova 接入业务振动

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

以下资料仅在当前决策分支按需读取。

再读取 `references/contract.json`。确定性导出先通过 `nova_project_action` 对 `nova.project.vibration.export` 执行 `describe`；只有 Action 报告配置问题时才读 Exporter 文档。确认 Emphasis / Custom 双区域、`DataFormat`、Unit 和行字段时读 `Docs/Runtime/Modules/Vibrate/VibrateSettings.md`、`VibrateUnitSetting.md`、`IVibrateRow.md`；配置场景入口时读 `Docs/Editor/Inspectors/VibrateComponentInspector/VibrateComponentInspector.md`；运行时加载、播放、停止和平台能力核验时读 `Docs/Runtime/Modules/Vibrate/VibrateComponent.md`、`VibrateManager.md`、`IVibrateManager.md` 与 `VibrateType.md`。不要递归加载全部 Vibrate、Luban、Pipify 或第三方插件文档。

## 冻结输入与阻断门

冻结唯一项目根、活动场景、`Nova` 根和既有 `VibrateComponent`；目标区域（至少一个明确的 `Emphasis` 或 `Custom`）、对应源目录、源文件 / Sheet / 行、`VibrateUnitSetting`、共享 `DataFormat`、数据与代码输出范围；每个目标组的 `Name`、`Order`、`PreDuration` 与区域字段（Emphasis 的 `Amplitude` / `Frequency` / `Interval`，或 Custom 的 `Intensity` / `Sharpness` / `Duration`）；运行时 `AssetLocation`、所属 Collector / Package、现有业务触发点、唯一的停止责任、目标平台、真机和可取得的用户体感反馈。

- Emphasis 与 Custom 是独立区域、独立源目录和独立发布事务。不得把它们合并为同一表、同一 Unit 或同一次未确认的全量写入；只处理本次冻结的区域。
- `VibrateUnitSetting` 固定为 List 模式，运行时按 `Name` 分组并按 `Order` 排序。目标组所属区域、预期行集合、排序、数据地址或启动时序不明确时返回 `blocked`，不得猜测最后一个候选。
- `VibrateComponent`、`VibrateSettings`、Unit、Asset 地址、Collector、Prefab 和 Scene 引用只能经 Unity Editor 自动化通道 修改；禁止手写 Unity YAML、`.meta` 或生成的 JSON、`.bytes`、C#。
- 不修改 NiceVibrations 源码、包内容、编译开关或 Nova Framework 的 Vibrate API。第三方插件接入、Framework / Inspector 改造属于 `not_applicable` 或其他 Operation。
- `EditorUtil.Vibrate.ExportEmphasisData` / `ExportEmphasisCode` 与 `ExportCustomData` / `ExportCustomCode` 只适用于已精确匹配 Unit 的单文件；`ExportEmphasisAll` / `ExportCustomAll` 及各 Pipify Step 会覆盖该区域全部 Unit，只有完整区域范围已确认时才能调用。不得默认使用 `export.excel.all`。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 Emphasis 或 Custom 源数据、行和 Unit | 项目已确认的数据源编辑入口 | 指定区域的源数据 | 变更只覆盖冻结的文件、Sheet、字段和行 |
| 已冻结的 `VibrateComponent`、`VibrateSettings`、Unit、地址和 Collector | Unity Editor / MCP 的 `VibrateComponentInspector` | 可由当前项目加载的区域数据配置 | 场景中的 `Nova`、组件、两区 Unit、DataFormat、AssetLocation 和资源归属一致 |
| 已冻结的单文件或完整区域导出范围 | `nova.project.vibration.export` | 指定区域的数据、类型和映射产物 | Action Verify 成功，变更仅位于冻结输出集；另一地区域未被修改 |
| 已冻结的既有业务入口和停止责任 | 项目既有业务触发编辑入口 | 振动触发与唯一停止生命周期 | 绑定不重复，离开、取消或替换时仅由确认责任方调用停止 |
| 已冻结的真机平台、样例组和反馈探针 | `Nova.Vibrate.LoadAsync` / `Play` / `PlayCustom` / `PlayEmphasis` / `StopAll`，以及 Unity Editor 自动化通道 Play Mode / 设备检查 | 真机上可感知的预期振动及停止 | `LoadAsync` 成功、设备能力与启用状态可观察、触发与停止日志关联，且测试者确认实际体感 |

## 实施与验证边界

1. 先确认 `VibrateComponent.Start()` 已初始化，目标区域的 Unit 与 `AssetLocation` 可由当前运行时资产链消费。没有唯一数据源、Unit、地址、业务触发、停止责任、目标平台或真机反馈探针时返回 `blocked`，不先写占位数据或调用播放。
2. 仅更新冻结的区域数据、Unit / 资源映射和既有业务入口。先检查每个区域内的 `Name`、`Order` 和字段完整性，再通过 `nova.project.vibration.export` 选择精确单文件或完整区域范围；导出失败立即停止，不清理另一地区域或无关生成物，也不退化为任意 C#、反射或临时 Pipify。
3. 触发路径必须先在运行时完成 `await Nova.Vibrate.LoadAsync()`。`LoadAsync()==true` 只证明数据加载链返回成功，不证明组名命中、设备支持或用户已感到振动；按名 `PlayCustom` / `PlayEmphasis` 未命中会警告后静默返回，不能把调用已发生当作播放成功。
4. 对预设需求使用 `Nova.Vibrate.Play()` 或 `Play(VibrateType)`；对已确认数据组仅使用相应的 `PlayCustom(name)` 或 `PlayEmphasis(name)`。冻结重入策略，避免未停止的组合叠加；在确认的退出、取消或替换点调用 `Nova.Vibrate.StopAll()`，不新增第二个停止责任方。
5. 在目标真机上确认 `Nova.Vibrate.IsSupported` 与启用状态，执行冻结样例并由实际测试者确认体感，再验证 `StopAll()` 后不再持续振动。Editor Play Mode、日志、代码编译、导出结果或 `IsSupported` 单独都不是物理振动成功证据。
6. 只有源数据、导出、运行时加载、正确业务触发、唯一停止生命周期和真机体感反馈均成立才报告 `success`。允许步骤已完成但没有真实设备反馈时最高为 `partial`；输入不唯一或不能安全写入时为 `blocked`；要求改 NiceVibrations / Framework API 或不属于消费端业务接入时为 `not_applicable`。

不默认新建 UI、替换未确认资源、修改无关 Unit / Collector / Package、构建 Bundle / Player、安装应用、发布 CDN、使用凭据或执行 Git 操作。删除、外部写入、替换非生成资产、构建或安装到设备、Git commit / push 都需要本 Skill 之外的精确确认。
