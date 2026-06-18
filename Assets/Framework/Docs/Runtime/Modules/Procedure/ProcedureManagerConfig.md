# ProcedureManagerConfig

`ProcedureManagerConfig` 是流程系统启动配置。

它只描述两件事实：

- 当前 FSM 要接管哪些 `ProcedureBase` 实例
- 启动时先进入哪个入口流程类型

## 什么时候先看这页

优先看这页的场景：

- 你要确认 `ProcedureManager.Initialize(...)` 实际吃到的输入是什么。
- 你要排查入口流程类型为什么找不到。
- 你要判断业务流程追加注册是否属于这个配置对象的职责。

## 配置语义

### 1. `Procedures`

- 类型：`ProcedureBase[]`
- 语义：初始化时要放进 FSM 的全部流程实例

`ProcedureManager.Initialize(...)` 会对它做防御性拷贝，然后创建 FSM。

### 2. `EntranceProcedureType`

- 类型：`Type`
- 语义：FSM 创建后首先进入的入口流程类型

如果它无效，初始化会直接失败。

## 调用方可依赖的边界

- 这是“初始化配置”，不是运行期增量注册容器
- HybridCLR 业务流程的后续接入，不通过这里完成，而是走 `RegisterAdditionalProcedures(...)`

## 风险点 / 易错点

- `Procedures` 为空时，`ProcedureManager.Initialize(...)` 会抛异常。
- `EntranceProcedureType` 为空时，入口流程无法启动。
- 不要把运行时追加业务流程的职责塞进这个配置对象。

## 继续阅读

关键源码：

- [ProcedureManagerConfig.cs](../../../../Scripts/Runtime/Modules/Procedure/Managers/Definitions/ProcedureManagerConfig.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [IProcedureManager.md](IProcedureManager.md)
- [ProcedureComponent.md](ProcedureComponent.md)
