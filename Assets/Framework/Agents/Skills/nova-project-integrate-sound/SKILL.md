---
name: nova-project-integrate-sound
description: Use when 项目组要在现有 Nova 项目中加入或修改大厅 BGM、按钮点击音效或其他已确认业务声音，并需完成音频资源、Sound 表、声音组、业务触发与实际播放验证时使用。
---

# Nova 接入业务声音

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。只在当前决策分支读取对应页面：确认 `SoundSettings`、数据格式、单元与组壳时读 `Docs/Runtime/Modules/Sound/SoundSettings.md`、`Docs/Runtime/Modules/Sound/SoundUnitSetting.md`、`Docs/Runtime/Modules/Sound/SoundGroupShell.md` 与 `Docs/Editor/Inspectors/SoundComponentInspector/SoundComponentInspector.md`；按 ConfigMaster 的有效 YooAsset 配置定位 Collector 时读 `Docs/Editor/Config/ConfigMasterSO.md`、`Docs/Editor/Config/Definitions/YooAssetEditorConfigsOverride.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md`；导出已确认声音表时读 `Docs/Editor/EditorUtil/EditorUtil.Sound/EditorUtil.Sound.Exporter.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；运行时加载、播放和生命周期验证时读 `Docs/Runtime/Modules/Sound/SoundComponent.md`、`Docs/Runtime/Modules/Sound/SoundManager.md`、`Docs/Runtime/Modules/Sound/ISoundRow.md`，仅在本次传入参数对象时再读 `Docs/Runtime/Modules/Sound/PlaySoundParams.md`。不要递归加载全部 Sound、Luban、YooAsset 或 UI 文档。

## 冻结输入与阻断门

冻结唯一的项目根、活动场景、`Nova` 根和既有 `SoundComponent`；激活 `ConfigMaster` 的稳定身份、`YooAssetEditorConfigsMask`、目标 `Platform / Channel / DevelopMode` 与 `EditorUtil.Config.DimensionalResolver.ResolveYooAsset` 解析出的有效 `YooAssetSettingsPath` / `BundleCollectorSettingPath`；`SoundSettings` 的数据格式、源目录、目标 Unit、源文件 / Sheet / 行、导出范围；目标 `ISoundRow.Name`、`AssetLocation`、`GroupName`、循环、优先级与音量；真实 `AudioClip` 的所有权、Collector、Package 和唯一地址；组壳名称、AgentCount、Mute、Volume、可选 Mixer 路由；既有业务触发点、BGM 停止或 SFX 释放责任，以及实际播放成功探针。任一输入不唯一、缺失或会改变写入范围时返回 `blocked`。

- 本 Skill 只接入已确认的声音表、真实 AudioClip、声音组和既有业务触发。音频制作、占位音频、创建新页面 / UIGroup / UI 注册、Framework Sound API / Manager / Inspector 改造属于 `not_applicable` 或其他 Operation。
- `Name` 在所有已加载 Sound Unit 中必须唯一；不得因重名选择“最后一个”行。`GroupName` 必须对应已确认组壳，且 AgentCount 为正、验证时未静音。
- `SoundComponent`、`SoundSettings`、组壳、Mixer、AudioClip、Collector 和 Prefab / Scene 引用只能通过 Unity Editor / MCP 修改；禁止手写 YAML、`.meta` 或生成的 JSON / `.bytes` / C#。源目录、孤儿 Unit 清理或替换非生成音频资产未确认时，不进入会扩大写入集的 Inspector 路径。
- `EditorUtil.Sound.Exporter.ExportData` / `ExportCode` / `ExportAll` 仅更新确认范围。`export.sound.data` / `export.sound.code` 会处理全部 `SoundUnitsSettings`，只能在该全量范围已确认时调用；不得默认使用 `export.excel.all`。
- 定位或修改 Collector 前，必须冻结激活 `ConfigMaster`、`YooAssetEditorConfigsMask` 与目标 `Platform / Channel / DevelopMode`，再用 `ResolveYooAsset` 取得有效路径并用 `YooAsset.Editor.SettingLoader.LoadSettingDataAtPath<BundleCollectorSetting>` 加载。掩码非全局、坐标缺失、解析路径为空、路径漂移，或加载结果与已确认 Collector 不一致时返回 `blocked`。不得调用 `YooAssetInjector.LoadBundleCollector`，它只读取顶层配置而不会解析 Override。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的声音源行、Unit 与导出范围 | 项目已确认的源编辑入口；`EditorUtil.Sound.Exporter.ExportData` / `ExportCode` / `ExportAll` | 指定的 Sound 数据、类型与映射产物 | 导出暂存发布成功，且变更仅位于冻结输出集 |
| 已确认真实 AudioClip、Collector、Package、地址与有效 ConfigMaster 坐标 | 先以 `EditorUtil.Config.DimensionalResolver.ResolveYooAsset` 解析有效 `BundleCollectorSettingPath`，再以 `YooAsset.Editor.SettingLoader.LoadSettingDataAtPath<BundleCollectorSetting>` 定位既有 Collector；Unity Editor / MCP 更新 AudioClip、Collector 和引用 | 可由当前项目加载的 AudioClip 地址 | 激活 Master、掩码、坐标、解析路径、Collector、Package、地址与目标表行一致 |
| 已确认 `SoundComponent`、组壳和 Mixer | Unity Editor / MCP 的 `SoundComponentInspector` | 目标组壳、代理容量、静音 / 音量和可选 Mixer 路由 | 目标场景启动后 `Nova.Sound.HasSoundGroup(group)` 为真 |
| 已确认的既有业务触发和生命周期 | 项目既有业务入口；`Nova.Sound.PlaySound` / `StopSound` / `ReleaseAssetBySerialID` | BGM 进入/退出或 SFX 单次触发链 | 绑定不重复，BGM 不叠播，停止 / 释放责任明确 |
| 已确认的 Play 验证样例 | `Nova.Sound.LoadAsync`、`HasSoundGroup`、`PlaySound`、`StopSound` 与 Unity MCP Play Mode / AudioSource 检查 | 目标请求对应的实际播放与停止 | 实际 AudioSource 播放、可听确认或等价可观察探针；serialID 仅作请求关联 |

## 实施与验证边界

1. 先确认当前场景已完成 `SoundComponent.Start()` 初始化；它不会自动加载声音表。没有真实 AudioClip、唯一地址、加载入口、组、触发点、停止生命周期或成功探针时返回 `blocked`，不先写占位配置或调用播放。
2. 先在冻结的激活 Master 与 `Platform / Channel / DevelopMode` 上执行 `ResolveYooAsset`，确认有效 `BundleCollectorSettingPath` 加载出的 Collector 与已确认 Collector 完全一致，再更新冻结的源行、资源 / Collector、组壳和既有业务触发。表模式先验证跨 Unit 的 `Name` 唯一性、`AssetLocation` 和 `GroupName`，再按确认范围导出；不得手改生成物或清理无关输出。
3. BGM 必须冻结进入播放、退出停止和重入不叠播策略；按钮音效必须冻结已有 Button / View 的单次触发与解绑生命周期。若创建 `PlaySoundParams`，交给 `PlaySound` 后不再由调用方回收。
4. Play Mode 中先 `await Nova.Sound.LoadAsync()`，再确认目标 `HasSoundGroup(group)`。`LoadAsync()==true 也不是播放成功证据`：空 Unit 也可能返回成功。`serialID 不是播放成功证据`：缺 Name、缺组或代理不足仍可能得到请求编号。
5. 触发冻结的业务入口，等待异步 AudioClip 加载后检查实际 AudioSource 播放、可听确认或等价探针；随后验证 BGM 停止或本次 SFX 的停止 / 释放责任。只有导出、编译、返回 serialID 或静态绑定时不得报告 `success`。
6. 只有路由、加载、目标组、业务触发、实际 AudioSource 播放和停止生命周期都成立才报告 `success`。输入已确认且已执行允许步骤，但只能取得导出 / 编译证据、无法进入 Play 或无法取得播放 / 停止证据时报告 `partial`；框架开发或音频制作需求报告 `not_applicable`。

不默认新建 UI、替换未确认的音频资产、修改无关 Sound Unit / Collector / Package、构建 Bundle / Player、发布 CDN、使用凭据或执行 Git 操作。删除、外部写入、替换非生成资产、Git commit / push 均需要本 Skill 之外的精确确认。
