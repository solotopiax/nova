# Config 设计态编辑与共享保存

`EditorUtil.Config.DesignTimeEditing` 是 ConfigWindow 和固定字段 Action 共用的 internal Editor 服务。真源仍为 ConfigMaster；Runtime 只消费 Exporter 生成的 ConfigRuntime。服务不依赖 Window，窗口以草稿检测和保存通知回调接入。

## 函数与参数

| 函数 | 输入、默认与实际来源 | 行为及失败边界 |
|---|---|---|
| `NormalizeWorkingCopy(workingCopy, workingCopySO, coordinate)` | 非持久化 ConfigMaster 副本、仅绑定该副本的单目标 SerializedObject、显式三维坐标；无可省略默认值 | 提交副本字段，复用 `DimensionProjector.NormalizeForSave` 和 `ResourceKeyStore.Validate`；错误绑定在提交前拒绝；不写真实资产、不生成源码。归一与密钥校验分别保留失败阶段。 |
| `SaveWorkingCopy(master, workingCopy, workingCopySO, coordinate, options, beforeCommit=null)` | 真实持久化 Master、独立副本、绑定副本的 SerializedObject、显式坐标、非 null options；回调默认无 | 先归一、再回调复核、最后 CopySerialized 与保存。还原真实资产名称和 hideFlags。Copy 开始后的异常携带 `WriteStarted=true`，不能声称无修改；不自动回滚。 |
| `RegisterDraftGuard(guard)` / `HasUnsavedDraft(master)` | 草稿检测委托、目标 Master | 同一委托不重复登记；任何检测返回 true 或异常都拒绝覆盖。窗口实际检查 dirty、SerializedProperty 未提交、待保存和待切坐标。 |
| `RegisterSavedListener(listener)` / `NotifyMasterSaved(master)` | 保存后刷新委托、已保存 Master | 通知干净窗口与既有 `Events.ActiveConfigMasterSaved`；监听异常只返回安全 warning，不重做保存，不推翻已落盘结果。 |

`SaveOptions.SaveAllAssets` 和 `CompleteLegacyMigration` 的默认值均为 false：只保存目标资产、不删除旧资源密钥文件。ConfigWindow 为兼容原流程显式传两个 true；批量 Action 使用默认选项。迁移收尾失败通过 `SaveResult.MigrationWarning` 表示，`Committed` 仍为 true。窗口保留 `CommitWorkingCopyToAsset(rebuildWorkingCopy=true)` 的调用语义；关窗和切坐标传 false 时，保存通知不会偷偷重建该窗口副本。

## 固定字段服务与 Action

`EditorUtil.Config.CoordinatePatchService` 在 clone 上完成新值检查、逻辑组冲突判断、广播、保存归一预演和真实序列化叶子差异校验；`ConfigPatchCoordinateAction` 仅封装请求、冻结、提交和只读核验。实际调用入口为 `nova.project.config.patch-coordinate`，复用已有 [配置 Skill](../../../../Agents/Skills/nova-project-configure-runtime/SKILL.md)。

| 输入 | 精确含义与默认 |
|---|---|
| `masterGuid` | Assets 内真实可写 ConfigMaster 的 32 位十六进制 Unity GUID；必填，不猜其他 Master。 |
| `patches` | 1 至 64 个坐标项，每项至少一字段；不存在默认批量范围。 |
| `platform/channel/developMode` | 当前枚举的大小写精确名称，平台和渠道不能为 None；设计态不要求等于 Active BuildTarget。 |
| `Namespace` | 可选 `{value:string}`；有效 C# 命名空间或明确空字符串。不改 asmdef，不证明业务程序集契约同步。 |
| `AppConfigs` | 可选固定对象，只接受 AppID、AppAesKey、AppAesIV、CustomConfigCmdName、CustomName 五字段。 |
| 每个字段包装 | 省略或 null 包装保留；`{value:""}` 清空；缺 value 或 value null 拒绝。字符串最多4096字符、不含控制字符。非空 App AES Key/IV 各为 UTF-8 16字节，与 NetService 的实际协议检查一致。 |

### 保存当前渠道与开发模式

同一 `nova.project.config.patch-coordinate` 复用互斥 `operation="coordinate-switch"` 分支，不新增 Action。旧固定字段请求省略 `operation/coordinate`，原 `patches` 输入、广播、保存和 Receipt schema 1 继续有效；未知或空 operation 拒绝。坐标切换须省略或传 null `patches`，完整提供 `coordinate.platform/channel/developMode`；混用空数组、缺轴、数字或非精确枚举均拒绝。

```json
{
  "masterGuid": "<actual Unity asset GUID>",
  "operation": "coordinate-switch",
  "coordinate": {"platform": "WebGL", "channel": "Official", "developMode": "Release"}
}
```

示例平台必须替换为当前 `ActivePlatform.Current`。Platform 实时派生自 Unity `EditorUserBuildSettings.activeBuildTarget`，本分支只校验并冻结该构建目标，不写 `CurrentPlatform`、不隐式切 Unity 平台。唯一资产写集为实际变化的 `CurrentChannel` 与 `CurrentDevelopMode`；相同目标不保存，旧矩阵、掩码、配置值、ExportTarget、Runtime、Scene 与 YooAsset 注入均不由此修改。

`CoordinatePatchService.BuildCoordinateSwitchPreview(master, coordinate)` 在 clone 上选择渠道/模式，再走共享 `NormalizeWorkingCopy`；完整前后 JSON 只允许两个选择叶子。若保存归一需要广播、补结构或修改其他配置，Plan 阻断，先通过独立 `normalize-master` 明确来源整理，再重新计划切换。`remainingIssues` 不用于此分支，也不把切坐标成功解释为全部配置就绪。

Plan 冻结完整资产文件/序列化状态、精确叶子、目标与活动 BuildTarget。Execute 单次消费，在重建预演及提交回调中复核完整文件/状态、草稿和活动构建目标，用默认 `SaveOptions` 仅保存目标 Master；保存通知只刷新干净匹配窗口。Receipt schema 1 增加可选分支元信息；旧 Receipt 仍按固定字段路径验证。新 Verify 只读已保存全文件/全状态、实际渠道/模式和冻结 BuildTarget，目标不符、平台漂移或其他持久配置变化为 partial，不保存、不回切、不重放。正式保存后的异常保 partial + Receipt，Runtime 导出及运行验证仍是独立步骤。

例如明确要求把一个逻辑组的 AppID 改为项目实际编号：

```json
{
  "masterGuid": "<actual Unity asset GUID>",
  "patches": [{
    "platform": "Android", "channel": "Official", "developMode": "Release",
    "AppConfigs": {"AppID": {"value": "<project App ID>"}}
  }]
}
```

坐标表示逻辑组取值来源，当前 `PanelDimensionMask` 未勾选轴共享值，因此单项请求可能展开为多个物理格。Plan 必须展示全部 `physicalChanges`，同组同字段的不同请求值拒绝。只允许请求字段的真实叶子替换；请求外属性、数组长度、管理引用、新增 Namespace Override 和归一造成的其他面板变化都拒绝。非规范配置通过独立 `nova.project.config.normalize-master` 明确保留来源和完整整理写集，或由 ConfigWindow 保存；整理后重新计划字段修改，不让字段 Action 隐式修复整个资产。扩展面板及维度编辑见 [PanelEditing](EditorUtil.Config.PanelEditing.md)。

## 冻结、提交与验证

Plan 冻结 GUID、路径、整份资产文件摘要、完整序列化状态摘要和全部物理字段地址；字段值只保存在内存执行态，不进入公开 Plan、Receipt 或审计记录，也不生成逐密钥摘要。其余必填配置缺失可以继续逐步填写，`remainingIssues` 只显示坐标、路径和严重度；`configurationErrorsRemain` 只是当前 Validator 的结果，不证明导出、业务、设备或服务就绪。

固定字段 Execute 重新核对文件、完整状态、预演结果和草稿；提交前再次检查，使用共享保存链仅落盘目标 Master。不改当前坐标、掩码、BuildTarget、Runtime、Scene、YooAsset 或资源密钥源码，不收尾旧密钥迁移。互斥坐标分支仅改上述两个选择字段。无变化时不保存。保存或复核在真实写入开始后失败返回 partial + Receipt，绝不自动重放。

Receipt 只含版本、资产身份、总体摘要、执行标志和物理字段地址。Verify 只读核对已保存文件摘要与完整目标状态，不调用会补结构的领域 Getter、不重新校验/导出、不执行保存。Master 保存成功仍需 [Exporter](EditorUtil.Config.Exporter.md) 按单一实际目标生成 Runtime，并完成对应编译与运行证据。

源码：[共享保存](../../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DesignTimeEditing.cs)、[固定字段服务](../../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.CoordinatePatchService.cs)、[Action](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Ensure/ConfigPatchCoordinateAction.cs)、[窗口适配](../../../../Scripts/Editor/Windows/ConfigWindow/ConfigWindow.AgentEditing.cs)。
