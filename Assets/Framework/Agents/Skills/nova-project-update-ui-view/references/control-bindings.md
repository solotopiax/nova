# 既有 Prefab 控件绑定与可选字段生成

只在用户要为一个已有 UIView 的多个控件补字段引用，或明确选择生成机械字段时读取。事实与限制见 [UIBindings 工具文档](../../../../Docs/Editor/EditorUtil/EditorUtil.UIBindings.md)。原手工绑定继续可用，旧页面不自动转换；PSD/PSB 编辑树与布局先匹配 `nova-project-psd2ugui`，不要用本工具重造其输入链。

## 先讲清当前使用入口

这是 AI/MCP 调用分支，没有专用 Unity 生成按钮或常驻开关。向用户说明实际入口，并按 [可复制请求示例](../../../../Docs/Editor/EditorUtil/EditorUtil.UIBindings.md#怎样要求生成字段) 具体确认页面、控件、字段和生成/绑定意图，不能只说“明确要求生成”。可读的技术 ID/类型由 Agent 获取；主动选用时可能需要局部补 partial，不要求旧页面统一迁移，也不把生成成功当作引用已绑定。

## 从页面确定精确输入

先定位用户授权的项目普通 Prefab，确认业务 View 已挂载、脚本已编译且资产无草稿。通过 live `nova.project.ui.inspect-bindings` 的 `describe` 和只读请求 `{prefabPath}` 获取当前对象、Views、字段与 BlockedReason。只读结果不授予修复授权；变体/嵌套/脏态或回调受阻时，保留原工作，继续已有专用编辑路径，不 Unpack/Apply/Revert 或改成另一种结构凑通过。

回调边界覆盖本 Prefab 全部 MonoBehaviour，不能只看 UIView：业务/未知来源组件的序列化回调、OnValidate、ExecuteAlways/ExecuteInEditMode 均阻断。正常 uGUI/TMP 原组件须实际程序集、Unity 注册包、MonoScript 类型与物理脚本来源一致才放行，不能按名称前缀信任或移除回调绕过。完整检查见领域文档；本工具不承诺隔离装载期生命周期或任意全局 Import/AssetPostprocessor，不把预检通过当成全局回调验收。

根据实际控件职责选定同一 View 的完整绑定集，使用返回的对象 ID 与字段声明类型，展示字段、当前引用、目标对象和差异。可读取的 ID/类型由 Agent 获取，不让用户逐个抄技术字段；有多种合理业务对应关系时只澄清该决策。字段缺失、组件缺失或已有不同引用不得静默补猜。

## 稳定执行请求

`nova.project.ui.configure-bindings` 的当前请求字段为：

```json
{
  "mode": "bind",
  "prefabPath": "实际项目普通 Prefab 路径",
  "viewObjectId": "本次 Inspect 返回的 UIView ObjectId",
  "bindings": [
    {
      "fieldName": "实际直接序列化字段名",
      "targetObjectId": "本次 Inspect 返回的目标 ObjectId",
      "typeName": "实际字段声明类型的 AssemblyQualifiedName"
    }
  ]
}
```

示意值不是可执行输入，不从层级名称、类名或整数位置编造 ID。整批一至一百二十八项，空字段可填、同引用 no-op，不覆盖已有不同/失效引用，不改 UnityEvent 或层级。先 describe，再 Plan；已有精确任务授权继续有效，执行仍绑定本次 PlanId/锁/写集。

`mode=bind` 只保存原 Prefab，Execute 返回 partial 后用原回执只读 Verify，核所有请求字段与范围外保护。无需修改注册行时不触发 UI 导出。工具 Verify 的绑定成功只覆盖持久化；本页面任务还需当前编译状态及明确要求的 Play 交互。

## 仅明确选择生成时

把 `mode` 改为 `generate`，字段、目标和完整类型仍来自同一 Inspect 范围。它只在原业务类已经是可核验 partial 时生成同目录 `<Type.Name>.UIBindings.g.cs`，不自动改原业务类，不生成业务监听。非 partial 项目可保留现有字段并直接绑定；只有本次明确选择自动声明新字段时才在授权业务源码范围局部调整，不能为工具启用要求所有旧页迁移。

生成 Plan 核准确输出、归属/正文Hash、原始业务程序集来源与首次 meta；Execute 只写字段文件。等待本次导入、编译、重载并 Verify 后，重新 Inspect 已编译字段，再独立 Plan `mode=bind`。不一趟生成并绑定，不手改受管 partial，不删除已有生成字段或重放旧计划。

## 失败和完成边界

计划漂移/不支持结构保持 blocked；提交已开始却未通过保存、来源、编译或恢复验证保持 partial。保留 Receipt、recovery_token 与恢复目录，只读 Verify 不恢复或再次生成。恢复失败当前没有独立 Action，报告所需人工核查，不能换任意 C# 或手写 YAML 覆盖。

验收精确字段与完整原文件、事件/布局保留、重复 no-op；生成另验编译及重新绑定。用户要页面交互时从真实入口打开验证，多控件/重名/缺组件/变体/嵌套分别观察成功或明确拒绝；未执行的 Play、设备与提效测量保留实际状态。最后只清本轮测试材料，未完成事务的恢复目录保留。
