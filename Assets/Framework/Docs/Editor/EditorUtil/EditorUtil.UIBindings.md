# EditorUtil.UIBindings

为**已有业务 UIView 的普通 Prefab** 批量绑定明确字段，或按需生成独立的序列化字段 partial。工具默认不运行，不扫描、改写或接管旧 Prefab 和业务代码；原手工字段、Inspector 绑定、PSD2UGUI 和 UI 注册导出流程保持原用法。

## 先选择任务

已有 PSD/PSB 制作和编辑树问题先使用 [PSD2UGUI 任务入口](../../../Agents/Skills/nova-project-psd2ugui/SKILL.md)。本工具处理已经存在的 UIView、控件与直接对象引用，不生成布局、不替代 [UIExporter](../DataPipeline/Implements/UIs/UIExporter.md) 的注册表导出，也不生成按钮事件业务。

普通业务 Prefab 的绑定复用 [更新界面 Skill](../../../Agents/Skills/nova-project-update-ui-view/SKILL.md) 的 [控件绑定分支](../../../Agents/Skills/nova-project-update-ui-view/references/control-bindings.md)；新页面只有先经现有创建路径落成 Prefab、挂载业务 View 后，才能进入本工具。

## 怎样要求生成字段

当前使用入口是 **AI/MCP 调用下方两个 Action**，Unity 中尚无本工具专用的“生成字段”按钮或勾选项。它也没有常驻开关：只有明确执行生成或绑定才修改目标。不使用时继续手写字段、在 Inspector 拖引用即可。

可以直接向 Agent 提出下面的请求，再把示例路径、类名和控件换成自己页面中的实际对象：

```text
请为 Assets/Game/UI/Shop.prefab 生成 UI 控件字段并完成绑定：
页面脚本 ShopView；
Content/BuyButton 的 Button 对应 m_BuyButton；
Content/CoinText 的 TextMeshProUGUI 对应 m_CoinText。
如果业务类不是 partial，只给类声明补上 partial。
新字段放独立的 ShopView.UIBindings.g.cs；编译后再绑定。
保留现有布局、原字段、事件监听和业务代码。
```

Agent 先读取真实页面，核对控件和当前工具限制，再展示准确写入范围。对象 ID、完整类型名由工具读取，使用者无需填写。生成器要求业务类已是顶层、非泛型 `partial class`；若需要补 `partial`，这是本次选用增强的局部源码修改，随后须编译并重新 Inspect/计划，生成器不会自行改原类。原有非 partial 页面仍可手写字段或使用已有字段绑定。

“生成字段”和“绑定引用”是两步：生成成功只得到字段源码，编译后的新字段尚未绑定控件；必须重新读取、另建绑定计划。只想绑定已经声明好的字段时，请明确说“绑定已有字段，不生成代码”，选择 `mode=bind`。

## 可执行入口

| Action | 输入 | 当前结果 |
| --- | --- | --- |
| `nova.project.ui.inspect-bindings` | `{prefabPath}` | 只读列出实际对象、UIView、直接字段、当前引用和受阻原因 |
| `nova.project.ui.configure-bindings` | `{mode,prefabPath,viewObjectId,bindings}` | `mode=bind` 绑定；`mode=generate` 单独生成字段源码 |

每项 `bindings` 含 `fieldName`、`targetObjectId`、`typeName`，全部必填；一次一至一百二十八项，字段名不得重复。对象 ID 使用当前 Inspect 返回的 `GlobalObjectId` 字符串，`typeName` 为字段声明类型的完整 `AssemblyQualifiedName`，不是仅类名、命名空间或 DLL 文件名。

Inspect 返回领域 `Inspection` 的 PascalCase 字段：`PrefabPath`、`PrefabGuid`、`Objects[{ObjectId,HierarchyNames,TypeName}]`、`Views[{ObjectId,TypeName,ScriptPath,Fields,BlockedReason}]`。`HierarchyNames` 是逐段名称数组，重名对象仍有各自 ID；不能拼路径或按整数顺序猜绑定。`Fields` 包含 `FieldName`、`TypeName`、`CurrentObjectId`、`MissingReference`，只报告对象引用身份，不复制业务字段内容。

两个 Action 都先 live `describe`；写入按 `plan → execute → verify`，用本次 PlanId 绑定精确目标和确认。未开放时不改用任意 C#、私有反射或手写 YAML。底层 `EditorUtil.UIBindings` 提供 `Inspect`、`Preview` / `Apply` / `Verify`、`PreviewGeneration` / `Generate` / `VerifyGeneration`，用于框架工具复用，不等于另一套可任意调度的 Action。

## 已有字段绑定

绑定请求选择同一 Prefab 内同一个已挂载的具体业务 UIView，以及本 Prefab 内的现存 GameObject/Component。字段必须是业务继承链中可编辑的直接 Unity 对象序列化字段，不包括框架状态、数组、深层字段、只读/隐藏字段或 UnityEvent。

`Preview(BindingRequest)` 整批校验，返回只在当前域单次可用的 `BindingPlan`；`Apply` 再核完整 Prefab、对象状态、脚本与元数据快照后一次保存。空字段才赋值，相同引用为 no-op；已有不同引用、失效引用、缺组件、类型不符、同名遮蔽均拒绝，不清空原业务引用。未列字段、层级、组件和全部事件监听保持原样。

变体、嵌套 Prefab、Missing Script、脏资产/PrefabStage 草稿、无法隔离的自定义 Inspector 会受阻。回调预检覆盖**所选 Prefab 内全部 MonoBehaviour**：任一业务或未知来源组件实现 `ISerializationCallbackReceiver`、声明/继承 `OnValidate`，或带 `ExecuteAlways` / `ExecuteInEditMode` 时，在完整状态快照和保存前拒绝，不能只检查所选 UIView。保持原编辑状态，沿目标已有专用 Prefab 编辑方式处理，不自动 Unpack、Apply/Revert、删组件或关闭回调凑通过；受阻不要求旧项目迁移。

正常 uGUI/TMP 原类型的上述回调仅在真实程序集对象、Unity 注册包、`MonoScript.GetClass()`、脚本所属包与实际物理文件一致时放行；接受当前 Registry/BuiltIn 来源的 `com.unity.ugui`，以及 TMP 对应的旧包 `com.unity.textmeshpro`。名字或程序集前缀不构成信任，自定义子类和本地改装包不因此放行。该预检不是任意回调隔离沙箱，不承诺隔离 Prefab 装载期生命周期、全局 Import 或 AssetPostprocessor；这些行为不能凭“字段预检通过”视作已经受控。

成功保存回执由只读 `Verify` 检查实际字节、脚本和全部目标引用。Action Execute 先返回 `partial`，Verify 成功只证明绑定持久化与范围保护，不证明页面运行、交互或事件效果。

## 可选 partial 字段生成

只有明确选择自动声明新字段时使用 `mode=generate`。业务类须预先是可核验的顶层、非泛型 `partial class`；非 partial、复杂条件声明或来源不明时停止生成，已有字段绑定仍可继续。工具不自动给旧业务类加 partial，不改原业务源码。

生成路径固定为实际 MonoScript 同目录的 `<Type.Name>.UIBindings.g.cs`，不接受输出路径或任意源码输入；仅生成 `private [SerializeField]` 直接字段。归属标记绑定业务脚本 GUID、真实类型、字段 schema 和正文 SHA；无归属文件、手改、同名成员冲突均拒绝覆盖。已有本工具字段只保留或追加，不能用新请求删除/改类型。完整内容相同不重写。

生成与绑定分两次：

1. Inspect 后选择新字段/真实目标/类型，Plan `mode=generate` 并核唯一生成文件与首次 meta 写集。
2. Execute 只生成源码，保留原 Prefab；等待**本次** Unity 导入、编译和脚本重载，再用原 Receipt / `recovery_token` Verify。
3. 生成 Verify 成功后重新 Inspect，确认真实编译字段，另建 `mode=bind` 的新 Plan；生成回执不能直接绑定，不跨重载重放旧 Execute。

业务源码、程序集定义、Prefab、输出或元数据漂移须重新计划。`VerifyGeneration` 只核源码归属与来源，Action 额外要求本次编译重载；两者不能混为“Prefab 已绑定”。

## 失败、取消与验收

写入前取消或校验失败不开始提交。同步提交开始后不承诺中途取消回滚；绑定/生成异常尝试恢复本次精确文件与对象状态。恢复不完整时保留 `Library/Nova/UIBindings/**` 和 `recoveryPath`，结果为 `partial`，禁止重放；只读 Verify 不自动恢复文件。当前未提供独立恢复 Action，必须保留现场并报告受阻，不能通过任意代码强行覆盖。

验收使用实际已有页面：多控件整批绑定、同名节点、缺组件、类型/引用冲突、重复执行 no-op、业务代码与监听保留；不支持的变体/嵌套应明确拒绝。生成另核业务原文件不变、归属保护、真实编译后新计划绑定及失败恢复。绑定持久化、编译、实际页面 Play 和设备分别报告；操作数或耗时收益须实测，不据生成器存在宣称提效。

来源：[领域模型](../../../Scripts/Editor/EditorUtil/EditorUtil.UIBindings/EditorUtil.UIBindings.Models.cs)、[绑定实现](../../../Scripts/Editor/EditorUtil/EditorUtil.UIBindings/EditorUtil.UIBindings.cs)、[字段生成](../../../Scripts/Editor/EditorUtil/EditorUtil.UIBindings/EditorUtil.UIBindings.Generation.cs)、[只读 Action](../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Inspect/UIBindingsInspectAction.cs)、[配置 Action](../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Ensure/UIBindingsConfigureAction.cs)。
