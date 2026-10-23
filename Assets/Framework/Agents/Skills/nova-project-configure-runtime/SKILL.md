---
name: nova-project-configure-runtime
description: Use when 项目组要在已确认的 Platform、Channel、DevelopMode 三维坐标配置 Nova ConfigMaster，并导出和编译验证对应 ConfigRuntimeSO 时使用。
---

# Nova 配置运行时快照

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在需要确认设计态边界时读 `Docs/Editor/Config/ConfigMasterSO.md`；批量固定字段编辑或保存当前渠道/开发模式时读 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DesignTimeEditing.md`；窗口编辑或保存时读 `Docs/Editor/Windows/ConfigWindow.md`；执行导出时读 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md` 和 `Docs/Runtime/Modules/Config/ConfigRuntimeSO.md`；面板编辑或非规范 Master 整理时按需读 [面板编辑参考](references/panel-editing.md)；复用既有 Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。若冻结字段触及已安装 SDK/Kit，先确认实际解析的 package id、版本、source 与根路径，并先读取其首个存在的 `Nova/Doc/INDEX.md`、`Nova/Docs/INDEX.md` 或 `Nova/DOCS/INDEX.md`；随后只按该 INDEX 读取目标 Config、路由、资源和平台前置。不要递归读取其他 SDK、Kit、YooAsset 或 CDN 文档。

只有用户要求完整三平台或配置后的 Player 验收时，按需读取 [三平台验收参考](../nova-project-build-player/references/platform-validation.md) 的坐标、场景快照与串行恢复部分。

## 冻结输入与决策门

冻结一个 `ConfigMasterSO`、Unity 当前 Active BuildTarget 实时映射出的非 `None` Platform、一个 `Channel × DevelopMode` 坐标、精确的字段变更集、目标 `ConfigRuntimeSO` 路径，以及是否明确要求写入活动场景的 DevelopMode / Channel 快照。不要把旧 Config 资产中的平台值、其他坐标、远端 JSON、CDN 凭据或 Sample 默认值当作本次输入。

先读取持久化工作区、已有 Master/Runtime、坐标矩阵和 Batch 参数，给出确定的来源与计划；可读取的身份、路径和数值无需新手抄写。完整三平台授权下主动发现可复用的三平台配置，逐平台执行当前 Operation 并复核；业务渠道/模式存在多个合理候选时才要求用户决策，不从相邻格猜值。

- 没有确认的 Master、目标 Runtime 资产或对应坐标矩阵时保持 `blocked`；新建 Master 或 Runtime 资产必须先确认资产路径、坐标和写入范围。
- Runtime 导出 Platform 必须等于 Unity 当前 Active BuildTarget 映射值；仅 Android、iOS、WebGL 可用。未映射或请求不一致时先切换 Unity BuildTarget，再重新计划。设计态固定字段批量编辑可明确选择其他有效平台，不自动切换 BuildTarget。
- 默认编辑已冻结单一坐标。用户明确要求批量时，使用下方固定字段分支，确认全部坐标和掩码展开的物理写集；不从相邻格猜值。
- 固定字段保存优先使用 `nova.project.config.patch-coordinate`；其它已有面板字段与维度优先使用 `inspect-panel` / `patch-panels`；未开放字段仍使用 ConfigWindow 的 `Nova/Open Config`。该窗口的“导出”成功后还会写活动场景快照。未授权写场景时，通过 `nova.project.config.export-runtime` 导出；底层复用 `EditorUtil.Config.Exporter.Export(...)`，不把 C# API 当作 MCP 旁路。

## 保存当前渠道与开发模式

用户要求切换或保存当前配置选择时，复用 `nova.project.config.patch-coordinate` 的互斥分支：`masterGuid`、`operation="coordinate-switch"`、完整 `coordinate={platform,channel,developMode}`，`patches` 省略或 null。平台、渠道与模式使用精确枚举名称；平台必须等于当前 Active BuildTarget 映射的 Android、iOS 或 WebGL，渠道不能为 None。实际参数先读 live describe。

该分支只保存 Master 的 `CurrentChannel` / `CurrentDevelopMode`，冻结 BuildTarget、完整文件与序列化状态，并拒绝请求外归一变化；非规范 Master 先走独立整理分支再 Plan。已满足时不写资产；草稿、漂移、重放、未知 operation 或混用 patches 均阻断。Verify 同时核对文件、完整状态、实际选择与冻结 BuildTarget；写后失败保留 partial 回执，不自动回滚。

跨平台任务先通过受控 Unity 构建目标入口显式切换 BuildTarget，再重新查询并计划本分支；`CurrentPlatform` 是实时只读属性，没有序列化写入。选择保存后，按已授权目标分别完成 Runtime 正式导出、必要场景快照和 Unity 编译，才能交付对应完整配置任务。

## 固定字段批量分支

复用 `patch-coordinate`，不新增 Skill。请求省略 `operation` / `coordinate`，绑定一个 `masterGuid` 和 1 至 64 个 `patches` 坐标项；每项必须含至少一个固定字段。支持 `Namespace`，以及 `AppConfigs` 内的 `AppID`、`AppAesKey`、`AppAesIV`、`CustomConfigCmdName`、`CustomName`。字段值使用 `{ "value": "..." }`：省略或 null 包装保留，`value=""` 明确清空，包装内 `value=null` 拒绝。实际坐标和参数以 live describe 为准。

当前面板未选轴共享一个逻辑组；请求一个坐标可能修改同组多个物理格。Plan 展示全部字段地址，同组同字段出现不同请求值会拒绝。服务只接受当前规范矩阵，拒绝请求外字段、数组、管理引用和新增 Namespace Override 等结构变化；非规范 Master 先走下方独立整理分支，再重新计划字段修改。

全部预演在 clone 上进行。Execute 重新检查文件、完整序列化状态、精确写集、资产脏态及窗口草稿，只保存目标 Master；不修改当前坐标、掩码、BuildTarget、Runtime、场景或资源密钥源码。空值允许逐步配置，但剩余问题须保留；非空 App AES Key/IV 须各为 UTF-8 16 字节，Namespace 须为有效 C# 标识符路径。计划和 Receipt 不输出字段值或逐密钥摘要，凭据仍遵守共同授权门。

Verify 只读核对完整状态与保存文件摘要；写入开始后失败返回 `partial`，不自动回滚或重放。完成 Master 批量保存仅是本 Skill 的中间步骤，最终仍按单一目标坐标完成 Runtime 导出与编译。

## 已有面板与维度分支

字段或面板不止固定六字段时，复用 `nova.project.config.inspect-panel` / `nova.project.config.patch-panels`，不为SDK、参数或坐标新增Skill。详细请求与真实范围按需读 [面板编辑参考](references/panel-editing.md)。

面板查询请求为 `masterGuid` 与 `target={platform,channel,developMode,panel,configType?}`。固定面板是 `AppConfigs / PrivacyConfigs / Namespace / HybridEditorConfigs / YooAssetEditorConfigs / CDNEditorConfigs / SDK / Kit`；SDK/Kit用实际已保存Config实例的精确 `FullName`，其它面板不传类型。坐标名称精确且Platform/Channel非None。读取真实字段path/type/tooltip/当前值/掩码/同组坐标；默认未知不能推测，凭据仅hasValue。

面板写入为1至64个 `changes[{target,fields?,dimensions?}]`。`fields[{path,valueJson}]` 使用live inspect返回的可写相对路径及严格JSON值文本；`dimensions[{axis,enabled}]` 使用明确的 `Platform / Channel / DevelopMode` 轴。维度先执行既有分裂/合并，然后按最终逻辑组广播字段；关闭轴时target提供保留来源，不能偷偷选邻格。同一逻辑组字段冲突拒绝，完整物理差异以Plan为准。

Privacy 初始 Key/IV 可逐步填写；原 Key/IV 按 UTF-8 各为16字节时，`patch-panels` 会在完整字段广播和归一后逐物理坐标检查，两值必须保持相同。更换、清空、维度合并或从其它格初始化覆盖原有效凭据均阻断；同值及其它隐私字段可编辑。此保护只作用于 `PanelEditing.BuildPreview` / `patch-panels`，手工窗口和共享保存保持现行为。需要保留旧数据的 Key/IV 轮换路由同一 `nova-project-integrate-persistence` 的独立 Privacy 分支，按当前macOS Editor三存储、当前Privacy逻辑组、正规配置保存/导出与独立恢复范围执行，不能改用普通配置入口绕过。

只编辑已有序列化结构和live editable字段，支持值转换的数组/对象以实际Schema为准，不承诺全部复杂类型。SDK/Kit缺实例先用ensure-plugin-instances；不由通用字段入口创建新管理引用类型、不绕过UnityEvent或只读限制。非规范结构或请求外归一变化走下方独立整理分支；脏资产或窗口草稿不能自动丢弃，先保留并解决冲突，不能手写YAML。

Plan/Execute复核完整资产和精确物理写集，只保存Master；Verify只读，不导出、不改变BuildTarget、不运行游戏。成功仍只是本Skill的配置中间阶段，Runtime导出与编译单独完成。

## 非规范 Master 独立整理分支

普通 patch 发现请求外归一变化时，不自动扩大字段计划。复用 `nova.project.config.normalize-master`，单独绑定真实 `masterGuid` 与明确的 `platform/channel/developMode` 锚点；锚点决定合并轴时保留的存量分支，可沿用本任务已确认的坐标，不能猜窗口当前值或相邻格。向用户解释需保留哪个分支及整份 Master 的完整脱敏差异，不要求新手重新抄写可读取事实；现有授权覆盖全部差异时继续，范围或保留分支存在歧义时先解决决策。

先 live describe，再独立 Plan/Execute/只读 Verify。归一预演复用既有 clone 归一与资源密钥校验，并核对重复归一的幂等性；完整资产、meta、语义状态、草稿与差异均重新检查。仅保存一个 Master，不切当前坐标/BuildTarget、不导出 Runtime、不生成资源密钥源码、不清理旧迁移文件；已规范时不保存。无法无歧义地整理则保持 blocked，不通过通用字段入口绕过。写后失败为 partial，不能重放。整理成功后重新 inspect 目标面板或 validate-coordinate，再生成新 patch 计划；整理的静态保存证据不代表本Skill所需的 Runtime 导出与编译验收。

## SDK / Kit 定向分支

仅当冻结字段触及已安装的 `com.solotopia.nova.framework.sdk.*` 或 `com.solotopia.nova.framework.kit.*` 时进入本分支。目标包的 INDEX、精确 Config 类型、路由/资源归属与平台前置不完整时保持 `blocked`，不靠包名、Plugin 类型名或相邻坐标猜测。

- SDK：通过 `nova.project.config.inspect-plugin-types(kind=sdk)` 定位可用的 `ISDKPluginConfig`。需要补实例或启用时，使用 `nova.project.config.ensure-plugin-instances`，绑定 Config 类型 `FullName`、`kind=sdk`、精确 `scope` / `coordinate` 和 `enable`。它内部复用 `SDKPluginScanner.EnsureInstance` 并维护 `ConfigMaster.EnabledSDKs`；后者是唯一启用源，不得以 `SDKComponent.m_PluginEntries`、能力族面板或 Plugin 类型名代替。实例已有字段值先通过 `inspect-panel(panel=SDK,configType=<精确Config类型>)` 查询，再用 `patch-panels` 修改已开放字段。
- Kit：通过 `nova.project.config.inspect-plugin-types(kind=kit)` 定位可用的 `IKitConfig`，补实例或启用同样调用 `ensure-plugin-instances(kind=kit)`，内部复用 `KitConfigScanner.EnsureInstance` 并维护 `ConfigMaster.EnabledKits`。Kit 值属于单一 `Platform × Channel × DevelopMode` 格，不是全局 Kit 配置，也不得从相邻格复制猜测。
- `EnabledSDKs` / `EnabledKits` 的成员变更是 Master 级写入，仍须在字段变更集内逐个冻结；Scanner 的发现或补实例不证明厂商 SDK、路由、资源或目标平台已经可用。

## 受控 Action Adapter

执行前先用 `nova_project_action(operation="describe", action_id=...)` 获取当前 Registry Request Schema，不从本文猜字段。当前 MCP 已开放：

| 目的 | Action ID | 关键请求 |
|---|---|---|
| 校验并读取单一坐标 | `nova.project.config.validate-coordinate` | `masterGuid/platform/channel/developMode` |
| 保存当前渠道与开发模式 | `nova.project.config.patch-coordinate` | `masterGuid/operation=coordinate-switch/coordinate`；当前平台，省略 patches |
| 批量编辑固定字段并保存 | `nova.project.config.patch-coordinate` | `masterGuid/patches[]`；字段使用 `{value}` 包装 |
| 查阅已有配置面板 | `nova.project.config.inspect-panel` | `masterGuid/target` |
| 编辑面板字段与维度 | `nova.project.config.patch-panels` | `masterGuid/changes[]` |
| 补齐已登记 IAP Store | `nova.project.config.ensure-panel-references` | `masterGuid/target/collectionPath/typeFullNames[]`；真实列表及已安装白名单 |
| 独立整理非规范 Master | `nova.project.config.normalize-master` | `masterGuid/platform/channel/developMode`；明确存量分支锚点 |
| 扫描 SDK/Kit Config 类型 | `nova.project.config.inspect-plugin-types` | `kind=sdk|kit|all` |
| 补齐并可选启用插件实例 | `nova.project.config.ensure-plugin-instances` | `masterGuid/kind/typeFullName/scope/coordinate?/enable` |
| 定位 Bundle Collector | `nova.project.config.inspect-bundle-collector` | `masterGuid` |
| 导出 Runtime 快照 | `nova.project.config.export-runtime` | `masterGuid/platform/channel/developMode/savePath` |

统一调用 `plan`。只读 Inspect Action 的 ready 计划可直接调用 `execute`，写入型 Ensure/Export 必须先展示精确 write set 并取得用户确认。Execute 必须同时传 `action_id` 与 `plan_id`；需要确认时再传 `confirmation_token=plan_id`。断线、编译或 domain reload 后只使用 `recovery_token` 调 `verify`，不得重放 Execute。

`validate-coordinate` 额外返回 `coordinateState`：Master 当前坐标、已有 App 槽坐标、Namespace/App 掩码勾选轴、按请求解析的 Namespace 和该物理格 App 字段。App AES Key/IV 仅报告 `hasValue`，不返回值；空掩码轴表示共享，`unavailable` 表示掩码缺失。`availableCoordinates` 只证明 App 槽存在，不证明 SDK/Kit/Privacy、导出或运行就绪。读取直接使用既有槽，不补结构；每次 Execute/Verify 读取当前值，不能把 Plan 当固定运行快照。

这些 Action 不负责任意字段或活动场景快照。固定字段以外的已有面板配置优先走 `inspect-panel` / `patch-panels`；未开放字段、实例类型变化和其它结构仍通过 ConfigWindow/对应专门入口完成，不得退化为任意 C# 字符串执行。`patch-coordinate` 只证明 Master 保存，`export-runtime` 只证明静态配置和目标 Artifact，Skill 的最终 `success` 仍需等待 Unity 编译通过。

`ensure-plugin-instances` 还会预演完整维度矩阵：已有矩阵不一致、补实例或启用后仍缺格时会在写入前拒绝。`scope=matrix` 会扩大到全部有效坐标，必须与本次已授权范围一致；不能为消除报错自动升级单格范围。`inspect-bundle-collector` 只接收 `masterGuid`，读取该 Master 的当前坐标，不接收任意目标坐标；其结果只能证明路径与资产定位，不证明 Collector 成员或地址正确。

## 执行与验证边界

按“保存 Master → 导出单一 Runtime 快照 → Unity 编译”执行。Exporter 会覆盖已存在的目标 `ConfigRuntimeSO`，并可能按已配置的维度解析更新对应 `YooAssetSettings.asset`；因此目标资产和该坐标的解析结果必须在确认的写入集内。导出时只验证本次坐标的 `EnabledSDKConfigs` / `EnabledKitConfigs` 快照，不把其他坐标、Bundle、Player、CDN 或厂商运行时结果推断为成功。

`export-runtime` Action 与 Pipify `export.config` 均不回写 Master 当前 Channel/DevelopMode，也不调用场景快照写入器。需要保存当前选择时使用上方 `coordinate-switch`；需要构建时核对 Master 当前坐标、Runtime 与已保存入口场景的 Asset/App 渠道。ConfigWindow 导出会写当前活动场景，仍须保存并检查全部启用场景；场景准备在已授权范围内经既有窗口/受控 Unity 编辑完成。每平台显式切换、坐标保存、导出与构建按单写者串行进行，批次外独立保留 Master、Runtime、Scene 与 PlayerSettings 基线；Batch Plan 不提供这些状态的完整冻结。

最低成功证据是指定 `ConfigRuntimeSO` 导出成功且 Unity 编译通过。只有资产保存、只有静态 diff、或只运行 Pipify 而没有确认编译时返回 `partial`。不把远端 Config 内容、Bundle、Player、CDN 或真机行为推断为本 Skill 的成功。

Action 调用带冻结时间和 `saveOnlyTouchedAssets=true` 的内部 Export 重载，默认 `exportResourceKeys=false`；它不生成客户端资源密钥源码或相应 link.xml。ConfigWindow/Pipify 使用的公开五参数 Export 会导出这些附加产物并可能更新源码路径追踪。加密配置变更若要求客户端生效，必须另行核对并授权该完整生成链；只完成窄 Action 不能宣称密钥部署完成。`Channel=None` 也不能用于完整配置闭环：坐标校验/插件补齐和底层 Export 都拒绝它，Config Export 的请求校验也直接拒绝 None，其他 Generate 解析器的语义保持原状。

临时三平台验收结束后按基线恢复本轮设置、场景与 Runtime 覆盖，保留用户明确要求交付的配置；持久配置任务保留目标修改。只清理本轮可归属的临时文件、对象与测试数据，并报告清场结果。
