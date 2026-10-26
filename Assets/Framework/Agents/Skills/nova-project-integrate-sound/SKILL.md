---
name: nova-project-integrate-sound
description: Use when 项目组要正式导出已有 Sound 配置的数据和访问代码，或接入大厅 BGM、按钮音效并验证实际播放、停止与资源释放生命周期时使用。
---

# Nova 接入业务声音

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。确定性导出先通过 `nova_project_action` 对 `nova.project.sound.export` 执行 `describe`；只有 Action 报告配置问题时才读 Exporter 文档。确认 `SoundSettings`、数据格式、单元与组壳时读 `Docs/Runtime/Modules/Sound/SoundSettings.md`、`Docs/Runtime/Modules/Sound/SoundUnitSetting.md`、`Docs/Runtime/Modules/Sound/SoundGroupShell.md` 与 `Docs/Editor/Inspectors/SoundComponentInspector/SoundComponentInspector.md`；按 ConfigMaster 的有效 YooAsset 配置定位 Collector 时读 `Docs/Editor/Config/ConfigMasterSO.md`、`Docs/Editor/Config/Definitions/YooAssetEditorConfigsOverride.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md`；运行时加载、播放和生命周期验证时读 `Docs/Runtime/Modules/Sound/SoundComponent.md`、`Docs/Runtime/Modules/Sound/SoundManager.md`、`Docs/Runtime/Modules/Sound/ISoundRow.md`，仅在本次传入参数对象时再读 `Docs/Runtime/Modules/Sound/PlaySoundParams.md`。不要递归加载全部 Sound、Luban、YooAsset 或 UI 文档。

## 仅导出已有数据和代码

用户只要求导出已有 Sound 配置的 `all` / `code` 产物时，选择合同中的 `taskProfiles[id=export-code-data]`，对应 `sound.export`。只读取当前项目根、已保存活动场景、唯一 `Nova` / `SoundComponent`、现有 `SoundSettings`、目标 Unit、真实源文件和正式输出范围；不要求 AudioClip、Collector、业务 Owner、运行时加载或播放探针。`data` 单独导出不属于此 profile，不能借它降低当前代码导出的编译要求。

追加冻结 `activeConfigMasterAndGeneratedNamespace`：从 `ProjectSettings/Nova/Globals.json` 读取 `configMasterGuid` / `configMasterPathHint`，经当前 Action 的 `describe/plan` 与 Unity Editor / MCP 只读配置查阅核对实际可加载 `ConfigMasterSO` 的 GUID、资产路径和顶层 `Namespace`。Luban 的 `RuntimeProvider.GetNamespace()` 读取这个顶层值，不读取 `ConfigRuntimeSO.Namespace`，也不调用 `DimensionalResolver.ResolveNamespace`；可按需读取 `Docs/Editor/EditorUtil/EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md` 核对来源。缺少绑定或身份不一致时 `blocked`，不得依据场景、第一个候选 Master 或旧生成代码补猜。已绑定 Master 明确空值时允许 global namespace，并冻结该预期。

Plan 必须记录上述身份与命名空间期望，Execute 在任何写入前复核；身份、值或适用目标漂移时重新计划，不能沿用旧 `PlanId`。以当前 live Action 返回的字段和已发布实现为准；当前宿主若未提供相应冻结或复核门，代码导出为 `blocked`，不得称该防护已实现或已验证。验收同时核对生成命名空间、完整类型名和既有消费者与冻结预期一致；SHA 与编译分别保留，不能替代类型身份兼容性检查。

对同一 `nova.project.sound.export` 执行 live `describe → plan → execute`，冻结 `scope`、可选 `sourcePaths` 和 Action 完整写入集。完整接入的全链确认输入适用于集成分支；本分支只冻结 `profile.requiredInputs`、当前 Action 的 `PlanId/writeSet`，危险副作用的原确认门、全部禁止范围、锁和幂等规则仍继承，不能由 profile 降低。等待此次 Unity 编译和脚本重载后，以 Core 的 `recovery_token` 进行只读 `verify`；写入已开始或重载导致 `partial` 时不重放 Execute。核对全部计划输出及 SHA、范围外状态、真实编译和本轮清场均符合冻结范围，才报告“声音数据/代码导出成功”；没有完整产物或编译证据仍为 `partial`，不承诺声音播放或完整业务接入。本分支验收后结束，下面仅适用于完整声音接入。

导出已经写入后，输入范围漂移、产物读取异常或取消会返回 `partial` 与已有 Receipt。保留已取得的产物证据，使用恢复令牌只读核验；不能按写前 `blocked` 处理，也不能重新执行导出补齐证据。

## 完整接入的冻结输入与阻断门

冻结唯一的项目根、活动场景、`Nova` 根和既有 `SoundComponent`；激活 `ConfigMaster` 的稳定身份、`YooAssetEditorConfigsMask`、目标 `Platform / Channel / DevelopMode` 与 `EditorUtil.Config.DimensionalResolver.ResolveYooAsset` 解析出的有效 `YooAssetSettingsPath` / `BundleCollectorSettingPath`；`SoundSettings` 的数据格式、源目录、目标 Unit、源文件 / Sheet / 行、导出范围；目标 `ISoundRow.Name`、`AssetLocation`、`GroupName`、循环、优先级、音量与空间混合度；真实 `AudioClip` 的所有权、Collector、Package 和唯一地址；组壳名称、AgentCount、Mute、Volume、可选 Mixer 路由；目标业务触发点、本 Owner 的 BGM / SFX 请求停止与资源释放责任，以及实际播放成功探针。事实不唯一或缺项时阻断依赖步骤；授权范围内尚未实现的调用与生命周期按下方规则补齐，无法取得实际播放证据时最高为 `partial`。

- 本 Skill 只接入已确认的声音表、真实 AudioClip、声音组和目标业务触发。触发代码优先复用项目已有 Owner；它尚不存在且本次授权包含接入声音时，可在已冻结的项目自有源码新增必要调用与停止/释放胶水，复用下方公开 API，不新建加载 Owner 或 Framework Manager。新页面与持久绑定由命中的 UI Operation 完成。音频制作、占位音频、Framework Sound API / Manager / Inspector 改造属于 `not_applicable` 或其他 Operation。
- `Name` 在所有已加载 Sound Unit 中必须唯一；不得因重名选择“最后一个”行。`GroupName` 必须对应已确认组壳，且 AgentCount 为正、验证时未静音。
- `Nova.Sound.PlaySound(name, params)` 的首参固定为声音表 `Name`；`PlaySoundByAsset(assetLocation, params = null)` 的首参固定为资源地址，必须唯一反查表行，缺失或歧义时拒绝。所有播放入口统一消费表内组、地址、循环、优先级、音量和空间混合度，禁止改用硬编码组或资源绕表。多个名称可共用音频，但必须按名称选择。
- 迁移旧 `PlaySound(group, assetLocation, params)` 到按名称或按唯一地址入口；原 `PlaySoundParams.Loop / Priority / VolumeInSoundGroup` 迁入源表的 `Loop / Priority / Volume` 并正式导出。参数对象只保留 `Time / MuteInSoundGroup / FadeInSeconds / Pitch / PanStereo`，仍可表达表外单次上下文；组音量、暂停、停止等独立运行命令继续使用原 API。
- `SoundComponent`、`SoundSettings`、组壳、Mixer、AudioClip、Collector 和 Prefab / Scene 引用只能通过 Unity Editor / MCP 修改；禁止手写 YAML、`.meta` 或生成的 JSON / `.bytes` / C#。源目录、孤儿 Unit 清理或替换非生成音频资产未确认时，不进入会扩大写入集的 Inspector 路径。
- `EditorUtil.Sound.Exporter.ExportData` / `ExportCode` / `ExportAll` 仅更新确认范围。`export.sound.data` / `export.sound.code` 会处理全部 `SoundUnitsSettings`，只能在该全量范围已确认时调用；不得默认使用 `export.excel.all`。
- 定位或修改 Collector 前，冻结激活 `ConfigMaster`、`YooAssetEditorConfigsMask` 与目标 `Platform / Channel / DevelopMode`。目标与 Master 当前坐标一致时，复用 `nova.project.config.inspect-bundle-collector(masterGuid)`；该 Action 内部用 `ResolveYooAsset` 与 `YooAssetInjector.LoadBundleCollector` 解析并加载当前坐标的有效路径，包含 Override。目标与当前坐标不同时，只按已确认的 `ResolveYooAsset(master, platform, channel, mode)` 路径契约定向检查，不把当前坐标 Action 的结果当成目标坐标结果，也不用任意 C# 旁路。坐标缺失、有效路径为空、路径漂移或加载结果与已确认 Collector 不一致时返回 `blocked`。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的声音源行、Unit 与导出范围 | 项目已确认的源编辑入口；导出调用 `nova.project.sound.export` | 指定的 Sound 数据、类型与映射产物 | Action Verify 成功，且变更仅位于冻结输出集 |
| 已确认真实 AudioClip、Collector、Package、地址与有效 ConfigMaster 坐标 | 当前坐标用 `nova.project.config.inspect-bundle-collector` 定位；其他坐标按显式 `ResolveYooAsset` 契约检查；Unity Editor / MCP 更新 AudioClip、Collector 和引用 | 可由当前项目加载的 AudioClip 地址 | 激活 Master、掩码、坐标、解析路径、Collector、Package、地址与目标表行一致 |
| 已确认 `SoundComponent`、组壳和 Mixer | Unity Editor / MCP 的 `SoundComponentInspector` | 目标组壳、代理容量、静音 / 音量和可选 Mixer 路由 | 目标场景启动后 `Nova.Sound.HasSoundGroup(group)` 为真 |
| 已确认的既有业务触发和生命周期 | 项目既有业务入口；`Nova.Sound.PlaySound(name, params)` 或唯一反查表的 `PlaySoundByAsset(assetLocation, params)`；`StopSound` / `ReleaseAssetBySerialID` | BGM 进入/退出或 SFX 单次触发链 | 绑定不重复，BGM 不叠播，本 Owner 显式结束、替换或关闭时停止并释放请求 |
| 已确认的 Play 验证样例 | `Nova.Sound.LoadAsync`、`HasSoundGroup`、`PlaySound`、`StopSound`、`ReleaseAssetBySerialID` 与 Unity Editor 自动化通道 Play Mode / AudioSource 检查 | 目标请求对应的实际播放、停止与释放 | 实际 AudioSource 播放及目标 Owner 的停止、句柄释放证据；serialID 仅作请求关联 |

## 完整接入的实施与验证边界

1. 先确认当前场景已完成 `SoundComponent.Start()` 初始化；它不会自动加载声音表。真实 AudioClip、唯一地址、组或归属不明时，阻断依赖该缺项的写入。触发与停止/释放尚未实现时，在已授权项目源码范围补齐唯一 Owner 与可达入口；加载仍接项目现有初始化链。没有实际播放与停止探针时不能完整成功，不先写占位配置或调用播放。
2. 按上面的当前坐标 Action 或显式目标坐标契约，确认有效 `BundleCollectorSettingPath` 定位出的 Collector 与已确认 Collector 完全一致，再更新冻结的源行、资源 / Collector、组壳和既有业务触发。表模式先验证跨 Unit 的 `Name` 唯一性、`AssetLocation` 和 `GroupName`，再通过 `nova.project.sound.export` 按确认范围导出；不得手改生成物、清理无关输出或退化为任意 C#。
3. BGM 必须冻结进入播放、退出停止与释放和重入不叠播策略；按钮音效必须冻结已有 Button / View 的单次触发、解绑与资源释放生命周期。对本 Owner 不再使用的 SerialID，显式结束、替换或关闭时先 `Nova.Sound.StopSound(serialID, 0f)`，再 `Nova.Sound.ReleaseAssetBySerialID(serialID)`；仅 Stop 或自然播放结束不会释放已加载资源。加载中的请求由 Stop 登记停止，异步结果到达后释放且不播放。Pause/Resume 保留 SerialID 与资源；自然结束后仍保留释放责任，直到该 Owner 的结束点。若创建 `PlaySoundParams`，交给 `PlaySound` 或 `PlaySoundByAsset` 后不再由调用方回收，同步返回 `-1` 也已转移所有权。
4. Play Mode 中先 `await Nova.Sound.LoadAsync()`，再确认目标 `HasSoundGroup(group)`。`LoadAsync()==true 也不是播放成功证据`：空 Unit 也可能返回成功。`-1` 表示名称、地址、组或代理容量等同步检查已拒绝，禁止当作有效请求登记。非负 `serialID` 也不是播放成功证据：异步资源加载或后续代理调度仍可能失败。
5. 触发冻结的业务入口，等待异步 AudioClip 加载后检查实际 AudioSource 的循环、优先级、最终音量与 `spatialBlend` 均对应表行（最终音量同时乘组音量），并取得实际播放、可听确认或等价探针；随后验证本 Owner 的 BGM / SFX 停止与句柄释放，覆盖本次涉及的结束、替换、关闭或加载中停止路径。`ReleaseAssetBySerialID` 无返回值，调用记录或 `StopSound` 的 bool 不证明释放完成；通过受控只读诊断或等价探针核对资源句柄。已停止的 helper `AudioSource.clip` 可能仍非空，不能据此判断仍持有句柄，也不承诺 Unity 立即卸载内存。只有导出、编译、返回 serialID 或静态绑定时不得报告 `success`。
6. 只有路由、加载、目标组、业务触发、实际 AudioSource 播放、停止与本 Owner 的资源释放生命周期都成立才报告 `success`。输入已确认且已执行允许步骤，但只能取得导出 / 编译证据、无法进入 Play 或无法取得播放 / 停止 / 释放证据时报告 `partial`；框架开发或音频制作需求报告 `not_applicable`。

不默认新建 UI、替换未确认的音频资产、修改无关 Sound Unit / Collector / Package、构建 Bundle / Player、发布 CDN、使用凭据或执行 Git 操作。删除、外部写入、替换非生成资产、Git commit / push 均需要本 Skill 之外的精确确认。
