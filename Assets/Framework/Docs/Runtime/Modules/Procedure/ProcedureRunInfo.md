# ProcedureRunInfo

`ProcedureRunInfo` 是 Editor 下的流程运行记录条目。

它只用于：

- 让 `ProcedureComponent` 在编辑器里记录流程切换历史

## 当前语义边界

- 整个类型包在 `#if UNITY_EDITOR` 下
- 发布构建里这个类型根本不存在

字段语义是：

- `TypeFullName`：流程类型全名
- `EnterRealtime`：进入时间
- `LeaveRealtime`：离开时间
- `Finished`：是否已经结束
- `Elapsed`：运行时长；未结束时会实时增长

## 风险点 / 易错点

- 任何依赖这个类型或 `RunHistory` 的代码，都必须做好 `#if UNITY_EDITOR` 保护。
- 它是调试记录，不是运行时流程监控或埋点体系。

## 继续阅读

关键源码：

- [ProcedureRunInfo.cs](../../../../Scripts/Runtime/Modules/Procedure/Definitions/ProcedureRunInfo.cs)

相关文档：

- [ProcedureComponent.md](ProcedureComponent.md)
