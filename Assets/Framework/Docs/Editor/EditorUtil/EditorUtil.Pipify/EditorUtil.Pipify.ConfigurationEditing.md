# Pipify Batch 配置自动化

`EditorUtil.Pipify.ConfigurationEditing` 是窗口与 Project Action 共用的设计态配置服务。它管理 Batch 和参数，保存后不运行流水线；实际执行继续使用已有 `nova.project.pipify.run-batch`。

## 可用入口

| Action | 用途 | 最少输入 | 产物 |
|---|---|---|---|
| `nova.project.pipify.inspect-settings` | 检查 Batch、注册状态、当前参数及可编辑字段 Schema | `settingsGuid`；可选 `batchName`、`stepId` | 脱敏只读视图，无文件写入 |
| `nova.project.pipify.configure-batch` | 创建、克隆、重命名或编辑 Batch，增删/复制/排序 Steps，配置基础参数 | `settingsGuid`、`mode`、`batchName`；克隆另需 `sourceBatchName` | 只保存指定 `PipifySettingsSO` |

GUID 应由项目现存资产定位，用户不必记忆。两个 Action 共用现有 `describe → plan → execute → verify` 协议；配置 Action 必须确认 Plan，保存成功不表示 Step、构建、服务器或真机已验证。

## 请求语义

| 字段 | 作用与默认 |
|---|---|
| `mode` | 精确值 `create` / `clone` / `edit` |
| `batchName` | 目标名称，非空且不带首尾空格；区分大小写 |
| `sourceBatchName` | `clone` 必填且不同于目标；`edit` 省略时来源为 `batchName`，提供其它来源名可重命名；`create` 不可提供 |
| `description` | 省略保留描述；`{"value":""}` 明确清空 |
| `steps` | 省略保留目标来源原顺序；存在时表示**完整目标列表**；`[]` 明确移除全部 Steps |
| `steps[].sourceIndex` | 指向编辑前来源 Batch 的零基索引。省略表示新增。相同来源索引可重复使用，形成独立参数条目 |
| `steps[].stepId` | 新条目必须来自当前 Registry；来源条目必须与该索引的 ID 精确一致 |
| `steps[].edits` | 省略或空数组保留保存参数；每个字段最多出现一次 |
| `edits[].fieldName` | 真实 `ParamsType` 的公开实例字段名，不接受属性路径、类型或方法名 |
| `edits[].valueJson` | 一个标准 JSON 基础值的**字符串**。例如布尔为 `"true"`、整数为 `"10"`、字符串为 `"\"文字\""`、枚举为 `"\"LZ4\""` |

`valueJson` 拒绝对象、数组、null、注释、单引号、非标准数字、多个值、隐式类型转换及未定义枚举。空字符串字段使用 `"\"\""`，省略编辑则保留；布尔不能用字符串 `"\"true\""` 替代。

以下请求把来源条目 1 放到第一位，并新增一个导出步骤。完整 `steps` 列表未列出的来源条目会被移除，因此 Agent 应先 inspect，再生成可审阅的目标顺序。

```json
{
  "settingsGuid": "从实际资产读取的32位GUID",
  "mode": "edit",
  "batchName": "日常开发",
  "steps": [
    { "sourceIndex": 1, "stepId": "export.config" },
    { "stepId": "export.excel.all", "edits": [] }
  ]
}
```

Step ID 必须以当前 Registry 检查结果为准；示例不代表所有消费项目均安装了相同 Step。

## 参数、默认值和状态

- 基础参数覆盖与现有窗口一致：`string`、`bool`、`int`、`float`、枚举。字符串支持正文和多行文本，格式仍由真实 Step 执行时处理。
- Registry 参数类型以方法的实际第二参数为准，不依特性的 `ParamsType`。可编辑字段以当前 Registry 和 inspect 返回的 Schema 为准；Array/List、复杂 DTO 与自定义序列化模型仍受下方边界限制，平台只读和条件显示按既有 Policy 判断。
- 新条目复用 `CreateDefaultParams`。框架已核实的参数 DTO 构造默认只在 Plan 生成一次，Execute 使用冻结结果；Config 默认通过只读持久化身份入口解析，在独立 Master 副本读取，并拒绝身份漂移或 Master 草稿；不会修复 Globals 的路径提示。不会读取 Runner 的旧参数迁移入口。
- `[PipifyDynamicDefault]` 仍是动态占位语义：保存空字符串，运行时生成实际版本，不在配置时冻结时间版本号。
- `[PipifyReadOnly]` 字段不可写。当前平台显示与判断取 Unity Active BuildTarget，改变平台需使用平台切换入口。
- `[PipifyVisibleWhen]` 使用传递显隐与防环规则；一次 edits 按最终目标状态判断，因此可以同时打开开关并填写依赖字段。最终隐藏或只读的字段拒绝修改。
- Google Play 审核选项、Config 渠道候选、WebGL 首包候选、CDN 自动关联派生路径与窗口共用规则。开启自动关联时路径字段不可编辑；要改锚点可先显式关闭自动关联，再编辑路径。
- 接口实现候选复用现有类型扫描，不实例化候选。动态包名选项仅执行源码固定的 `AssetBundleBuildArgs.GetPackageNameOptions`；未知 Package 动态提供者不通过 Action 反射执行。
- 首次进入 Android `DevelopmentBuild + BuildAppBundle` 组合时，Plan 明确给出上传签名风险；确认保存后实际构建仍需按构建入口验收。
- `inspect-settings` 的 `valueJson` 表示保存 JSON token，`effectiveValueJson` 另表示当前 Runner 平台同步后的值；显隐、候选按有效实例判断，读取不表示有效值已保存。检查建立无构造器的字段状态；不运行 Step、参数构造器或动态版本默认值。缺失/非法保存参数导致状态不确定时标记 `stateKnown=false`，不伪造默认值。
- 密码特性及凭据、Token、Webhook、Secret 等字段仅显示 `sensitive / hasValue`，不输出原文、完整 ParamsJson 或单值摘要。Plan 与 Receipt 不携带任何字段输入值。

## 保存和失败保护

Plan 在独立 `PipifySettingsSO` 副本预演，冻结 GUID、资产路径、整体文件/序列化摘要、Registry 参数模型、Active BuildTarget 及 Android 工程导出开关。执行前与复制前再次核对，任何漂移要求重新 Plan。

PipifyWindow 注册草稿 guard，包括未提交的 `SerializedProperty` 与等待下一帧模态交互的 Batch 编辑。真实资产脏态或任意匹配窗口草稿会阻止 Action 覆盖。Plan 展示目标 Batch 索引、完整 Step ID 顺序和无值字段地址；执行前再次核对。保存后仅刷新干净窗口的参数缓存、展开态和列表；领域服务不引用窗口类型。

只用 `SaveAssetIfDirty` 保存目标；不保存其它资产。相同 `edit` 请求为 no-op；`create/clone` 的目标已经存在且完整配置相同时也不写。已有同名但不同配置会阻塞，需要显式 `edit`。

写入开始前失败返回 blocked；复制真实资产开始后失败返回 `partial + Receipt`，不自动回滚或重放。Verify 只核对文件和整体状态，不构造参数、保存或恢复执行。跨 domain reload 可做 Verify，不能重放旧 Plan。

## 当前边界

1. Array/List、复杂参数对象、私有字段和属性当前没有窗口编辑能力，本服务也不开放；这些保存 token 在未编辑时保持原样。Steps 列表本身支持结构编辑。
2. 未核实 Package 参数构造器不用于自动创建默认值；可先在窗口创建保存，再检查、保留、复制、排序或编辑其受支持的保存字段。未知动态提供者字段拒绝自动编辑。
3. 旧空参数、缺失非字符串字段或非法枚举需要先在窗口按实际版本整理并保存，Action 不隐式迁移或猜测构造默认。
4. 本入口不创建 Settings 资产、不切换 Workspace 绑定、不删除或重排 Batch 列表、不运行流水线、不修改其它 Config/Scene/Prefab。
5. 固定字段 token 编辑保留未请求值，不自动实施构建校验、网络测试或外部服务验收。接口选项配对、路径存在性等执行条件仍由对应 Step 检查。
6. 自定义 JSON 转换器、opt-in/字段序列化模型、重命名 JSON 字段、`JsonIgnore` 和不可序列化字段不开放通用写入，需要对应领域封装；不会把反射字段名错误当作实际 JSON 字段。

## 代码导航与验证

- [共享配置服务](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.ConfigurationEditing.cs)：预演、完整资产保护、参数 token、inspect、单资产保存。
- [共享参数规则](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.ConfigurationEditing.ParameterPolicy.cs)：基础类型、显隐、只读、候选、固定动态提供者及签名风险。
- [配置 Action](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Ensure/PipifyConfigureBatchAction.cs)：冻结、单次执行、Receipt 与只读 Verify。
- [检查 Action](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Inspect/PipifyInspectSettingsAction.cs)：当前资产和 Schema 只读查询。
- [Pipify 主文档](EditorUtil.Pipify.md)、[窗口](../../Windows/PipifyWindow.md)。

每次执行仍须验证真实资产保存、窗口草稿/刷新、domain reload 与 MCP 调用链；源码和静态编译检查不能替代本次操作的运行证据。

## 保存 token 与窗口候选的区别

Inspect 用保存 token 建立不调用构造器的可信规则实例，平台同步只供 effective/显隐/候选，不自动保存；未知 DTO 可 stateKnown=false。Action 新默认只用固定已审工厂并冻结结果，窗口仍保留 Package 默认构造器和动态 provider 反射。ReadValue 对单基础值严格校型；旧 ParamsJson 的 JObject 读取器仍可接受 Newtonsoft 的部分扩展语法，不能混称所有入口都严格标准 JSON。

可见性依赖缺失/循环/转换异常有保可见回退，只是候选 UI 规则，不是权限门；最终状态下只读与枚举/下拉才校请求字段。动态 placeholder 不落盘，新默认与已有保存值分开；敏感字段隐藏依命名/属性，不是任意秘密内容识别。BuildPreview 只改独立 clone，非目标 Batch 全树保留；SavePreview Copy 开始后无回滚，保存通知不重试。
