# ProcedureManager

`ProcedureManager` 是流程系统的真实运行核心。

它负责三件事：

- 用一组 `ProcedureBase` 实例创建 FSM
- 启动入口流程并驱动每帧更新
- 在关闭时清理 Launcher UI 并回收 FSM

框架启动骨架由 `ProcedureComponent` 负责准备，真正的流程切换和查询能力由这里承担。

## 什么时候先看这页

优先看这页的场景：

- 你要排查为什么入口流程没有启动。
- 你要确认 HybridCLR 业务流程是怎么追加进现有 FSM 的。
- 你要看 shutdown 时为什么会先销毁启动阶段 UI。

## 依赖与边界

### 它依赖什么

- `ProcedureManagerConfig`
- `Fsm<IProcedureManager>`
- `ProcedureBase`
- `LauncherUIController`

### 它对外负责什么

- 创建并启动流程 FSM
- 查询当前流程与已注册流程
- 在业务 DLL 加载后追加业务 `Procedure`
- 统一关闭流程系统

### 它不负责什么

- 不负责扫描场景中的流程类型
- 不负责加载业务 DLL
- 不负责具体流程内部逻辑

## 核心流程

### 1. Priority 固定为 6

`ProcedureManagerBase.Priority => 6`。

这不是旧文档里写的 `0`。

### 2. Initialize：防御性拷贝后创建 FSM

`Initialize(config)` 会：

1. 校验 `config.Procedures` 非空
2. 校验 `config.EntranceProcedureType` 非空
3. 防御性拷贝 `config.Procedures`
4. `Fsm<IProcedureManager>.Create(this, procedures)`
5. `m_ProcedureFsm.Start(config.EntranceProcedureType)`

入口流程是在这里真正启动的。

### 3. Update：把帧驱动完全交给 FSM

`Update()` 只做：

- `m_ProcedureFsm?.Update()`

也就是说，当前流程的 `OnUpdate()` 调度不在 Manager 里展开，而在 FSM 内部。

### 4. RegisterAdditionalProcedures：把业务流程接进同一 FSM

`RegisterAdditionalProcedures(procedures)` 的语义是：

- 只能在 `Initialize()` 之后调用
- 常用于 HybridCLR 业务 DLL 加载完成之后
- 追加的流程仍进入同一个 `m_ProcedureFsm`

重复类型校验、切换中保护和 `OnInit` 调用由 `Fsm.AddStates(...)` 负责。

### 5. Shutdown：先清 Launcher UI，再关 FSM

`Shutdown()` 的顺序是：

1. `LauncherUIController.DestroyAll()`
2. `m_ProcedureFsm.Shutdown()`
3. `m_ProcedureFsm = null`

这保证了启动阶段残留面板不会挂到流程系统之外。

## 高价值 API 面

- 启动：`Initialize(ProcedureManagerConfig config)`
- 当前流程：`CurrentProcedure`
- 查询：`HasProcedure<T>()` / `GetProcedure<T>()` / `GetProcedure(Type)`
- 业务扩展：`RegisterAdditionalProcedures(...)`
- 关闭：`Shutdown()`

## 风险点 / 易错点

- 在 `Initialize()` 前调用查询接口会直接抛 `InvalidOperationException`。
- `RegisterAdditionalProcedures(...)` 不是给框架内置流程用的常规入口，它主要服务于业务 DLL 延迟接入。
- `Shutdown()` 会主动销毁 Launcher UI；如果你把它当成“纯逻辑关闭”，会漏看 UI 副作用。
- `CurrentProcedure` 在未初始化时是 `null`，不要把它当成入口流程一定已就绪。

## 继续阅读

关键源码：

- [ProcedureManager.cs](../../../../Scripts/Runtime/Modules/Procedure/Managers/Implements/ProcedureManager.cs)
- [ProcedureManagerBase.cs](../../../../Scripts/Runtime/Modules/Procedure/Managers/Implements/ProcedureManagerBase.cs)

相关文档：

- [ProcedureComponent.md](ProcedureComponent.md)
- [IProcedureManager.md](IProcedureManager.md)
- [ProcedureManagerConfig.md](ProcedureManagerConfig.md)
- [ProcedureBase.md](ProcedureBase.md)
- [ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
