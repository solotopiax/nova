# IProcedureManager

`IProcedureManager` 定义的是流程系统的运行时契约。

调用方真正应该依赖的是这些语义：

- 初始化后会形成一个可查询、可更新的流程 FSM
- 当前流程与查询接口都依赖同一个 FSM 状态
- 业务流程可以在运行期追加进入现有 FSM

## 契约定位

直接依赖它的通常是：

- `ProcedureComponent`
- 启动链中的内置 `Procedure`
- HybridCLR 业务流程注册逻辑

它描述的是“流程系统对外保证什么”，不是“FSM 内部如何存状态”。

## 调用方可依赖的语义

### 1. Initialize 是正式建 FSM 的入口

- `Initialize(ProcedureManagerConfig config)` 不是轻量配置注入
- 调用成功后，入口流程已经被真正启动

这和很多“先 Initialize，再 Start”的管理器语义不同。

### 2. CurrentProcedure 代表当前活跃状态

- `CurrentProcedure` 直接映射当前 FSM 状态
- 在未初始化前它可能是 `null`

调用方不能假设一拿到接口就已经处于某个可用流程。

### 3. 查询接口依赖已初始化状态

- `HasProcedure<T>()`
- `GetProcedure<T>()`
- `GetProcedure(Type procedureType)`

这些查询的对象都是当前 FSM 已注册的状态集合，而不是某个全局类型仓库。

### 4. RegisterAdditionalProcedures 是运行期扩展能力

- 它允许把新的 `ProcedureBase` 实例追加进已有 FSM
- 典型场景是 HybridCLR 业务 DLL 加载完成后补注册业务流程

这条契约变化会直接影响 Nova 现有“框架内置流程 + 业务流程延迟接入”的启动架构。

## 最小 API 面

- 启动：`Initialize(ProcedureManagerConfig config)`
- 当前流程：`CurrentProcedure`
- 查询：`HasProcedure<T>()` / `GetProcedure<T>()` / `GetProcedure(Type)`
- 扩展：`RegisterAdditionalProcedures(ProcedureBase[] procedures)`

## 变更影响面

如果这里的契约变化，会直接影响：

- [ProcedureComponent.md](ProcedureComponent.md)
- [ProcedureManager.md](ProcedureManager.md)
- [ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- 所有依赖当前流程查询或业务流程延迟注册的运行时代码

尤其高风险的是：

- `Initialize()` 是否仍负责真正启动入口流程
- 查询接口是否仍只看当前 FSM
- `RegisterAdditionalProcedures(...)` 是否仍允许运行期接入业务流程

## 相关实现

关键源码：

- [IProcedureManager.cs](../../../../Scripts/Runtime/Modules/Procedure/Managers/Interfaces/IProcedureManager.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [ProcedureComponent.md](ProcedureComponent.md)
- [ProcedureManagerConfig.md](ProcedureManagerConfig.md)
- [ProcedureBase.md](ProcedureBase.md)
- [ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
