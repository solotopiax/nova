# ProcedureComponent

`ProcedureComponent` 是流程系统的 Unity 场景入口。  
它解决的是“框架启动时，如何把一组 `ProcedureBase` 实例交给 `ProcedureManager` 并启动 FSM”。

它负责框架内置流程的注册起步，不负责业务 DLL 中 Procedure 的后续接入。

## 什么时候先看这页

优先看这页的场景：

- 你要排查启动链为什么没有进入预期入口流程。
- 你要确认业务 Procedure 为什么在 `Start()` 时还不可见。
- 你要看 `LauncherSettings` 是谁持有、谁消费。
- 你要判断 `RunHistory` 是不是会进入发布包。

如果问题已经落到 HybridCLR 业务流程接入，继续看 [ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)。

## 依赖与边界

### 它依赖什么

- `IProcedureManager`
- `ProcedureManagerConfig`
- `ProcedureBase`
- `Util.TypeCreator`
- `Util.Assembly`
- `LauncherSettings`

### 它对外负责什么

- 反射创建 `IProcedureManager`
- 扫描当前框架程序集里的 `ProcedureBase` 非抽象子类
- 定位入口流程类型
- 初始化 `ProcedureManager`
- 暴露 `CurrentProcedure / HasProcedure / GetProcedure`
- 在 Editor 下记录 `RunHistory`

### 它不负责什么

- 不负责业务 DLL 的加载
- 不负责业务 Procedure 的扫描注册
- 不负责具体流程切换逻辑实现
- 不负责 Launcher UI 的渲染实现

业务 Procedure 的延迟注册由 `ProcedureLoadDll` 负责。

## 核心流程

### Awake：先创建 Manager

`Awake()` 中：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IProcedureManager>(m_CurManagerTypeName)`

如果类型名无效，会立即抛错。

### Start：只扫描框架程序集中的内置 Procedure

`Start()` 的逻辑很明确：

1. 用 `Util.Assembly.GetTypeNames(typeof(ProcedureBase), typeof(ProcedureBase).Assembly)` 扫描类型
2. 逐个 `Activator.CreateInstance(type)`
3. 用 `m_EntranceProcedureTypeName` 匹配入口类型
4. 调 `m_ProcedureManager.Initialize(...)`

这里的扫描范围只限 `typeof(ProcedureBase).Assembly`，也就是当前框架程序集。  
业务 DLL 里的 Procedure 此时还不在这里面。

### 业务 Procedure 不是在这里注册

业务 Procedure 的接入时机在 `ProcedureLoadDll`：

1. 业务 DLL 加载完成
2. `Util.Assembly.RefreshAssemblies()`
3. 扫描业务程序集中的 `ProcedureBase` 子类
4. 调 `RegisterAdditionalProcedures(...)`

所以：

- `ProcedureComponent.Start()` 只负责框架启动骨架
- 业务入口接力发生在后面的加载流程里

### RunHistory 只存在于 Editor

`RunHistory`、`m_LastProcedureTypeFullName` 和 `RecordProcedureTransition(...)` 都包在 `#if UNITY_EDITOR` 下。

发布构建里：

- 没有这部分字段
- 没有 `Update()` 里的采样逻辑
- 没有这部分运行时开销

## 高价值 API 面

### 1. 入口与状态

- `CurManagerTypeName`
- `EntranceProcedureTypeName`
- `LauncherSettings`
- `CurrentProcedure`

### 2. 查询

- `HasProcedure<T>()`
- `GetProcedure<T>()`
- `GetProcedure(Type procedureType)`

### 3. 调试

- `RunHistory`（仅 Editor）

## 关键状态

- `m_CurManagerTypeName`：决定创建哪种 `IProcedureManager`
- `m_EntranceProcedureTypeName`：框架启动入口流程全名
- `m_LauncherSettings`：启动期 UI 与闪屏相关配置
- `m_ProcedureManager`：真实流程管理器
- `m_RunHistory`：Editor 下的流程跳转历史

## 风险点 / 易错点

- `m_EntranceProcedureTypeName` 必须是完整类型名；值失效时 `Start()` 会直接抛异常。
- 旧场景如果还保留 `NovaFramework.Runtime.ProcedureLaunch` 之类历史值，启动会失败。
- `Activator.CreateInstance(type)` 要求流程类可无参构造；自定义内置 Procedure 改坏这一点会在启动时暴露。
- `ProcedureComponent` 不会扫描业务程序集；如果你期待业务 Procedure 一开场就可见，那是错误时序假设。
- `RunHistory` 只在 Editor 可用，不能把它当成发布包里的监控能力。

## 继续阅读

关键源码：

- [ProcedureComponent.cs](../../../../Scripts/Runtime/Modules/Procedure/ProcedureComponent.cs)
- [ProcedureComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Procedure/ProcedureComponent.Visitors.cs)
- [ProcedureComponent.Methods.cs](../../../../Scripts/Runtime/Modules/Procedure/ProcedureComponent.Methods.cs)

相关文档：

- [ProcedureManager.md](ProcedureManager.md)
- [IProcedureManager.md](IProcedureManager.md)
- [ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- [LauncherSettings.md](LauncherSettings.md)
- [BuiltInProcedures.md](BuiltInProcedures.md)
