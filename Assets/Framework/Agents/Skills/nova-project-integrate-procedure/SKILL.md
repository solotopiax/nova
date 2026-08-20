---
name: nova-project-integrate-procedure
description: Use when 项目组要在现有 Nova 消费项目中新增或调整业务 Procedure，并需验证进入、离开、异步取消与下一状态跳转时使用。
---

# Nova 接入业务 Procedure

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支按需读取文档和项目事实：确认生命周期与流程级取消时读 `Docs/Runtime/Modules/Procedure/ProcedureBase.md`；确认框架启动扫描与业务流程边界时读 `Docs/Runtime/Modules/Procedure/ProcedureComponent.md`；确认现有延迟注册链时读 `Docs/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md`；确认状态查询、追加注册或跳转语义时读 `Docs/Runtime/Modules/Procedure/ProcedureManager.md` 与 `IProcedureManager.md`。只有项目使用 HybridCLR 且需要核对业务 DLL 交付事实时，才读取当前项目的 asmdef、ConfigRuntime/ConfigMaster 只读事实和 DLL 映射；不要递归加载全部 Procedure、Config、Asset 或 HybridCLR 文档。

## 冻结输入与阻断门

先冻结唯一项目根、目标业务 Procedure 类型与源码位置、业务程序集名、命名空间、是否由当前业务热更 DLL 交付、进入职责、离开职责、下一状态及跳转条件、既有调用点、异步任务与取消预期、允许写入集，以及可观察进入、离开、跳转和取消的 Play 探针。

- 业务 Procedure 必须位于项目业务 DLL 层并继承 `ProcedureBase`；不移动、不修改 `NovaFramework.Runtime` 中的框架内置 Procedure。目标若是框架启动链或 Framework API 改造，返回 `not_applicable`。
- 复用现有 `ProcedureComponent` 的框架扫描与 `ProcedureLoadDll → RegisterAdditionalProcedures` 业务延迟注册链。不得创建第二个注册器、手工维护业务 Procedure 列表、在入口场景提前扫描业务程序集，或复制 `UniTask.Yield()` 延迟注册逻辑到业务层。
- 程序集、命名空间、热更 DLL 归属、进入/离开职责、下一状态、调用点或 Play 探针任一不唯一时返回 `blocked`，不得根据文件名或相邻流程猜测。
- 重写生命周期方法必须保留对应 `base` 调用。流程内异步工作必须绑定继承自 `ProcedureBase` 的 `CancellationToken`，在关键 `await` 后响应取消；不得另建脱离流程生命周期的长期任务来绕过离开取消。
- 本 Skill 不自动修改入口 Scene、ConfigMaster/ConfigRuntime、`Nova.prefab`、`ProcedureComponent`、程序集装载配置或 DLL 生成物。若目标 Procedure 尚不在既有业务 DLL/命名空间/加载链中，先返回 `blocked` 并报告所需的独立配置或 DLL 刷新 Operation。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的业务程序集、命名空间和目标 Procedure | 工作区源码只读检查 | Procedure 确实位于业务 DLL 层 | asmdef/编译单元归属、完整类型名与现有 DLL 映射一致；框架内置源码未改 |
| 已冻结的进入、离开、下一状态、调用点与异步取消契约 | `workspace-edit` | 目标业务 Procedure 与必要的既有业务调用点 | 写入只覆盖冻结源码；生命周期保留 `base`，异步使用流程 `CancellationToken`，跳转后不继续执行旧状态逻辑 |
| 现有注册与查询链 | `ProcedureBase`、`ProcedureComponent`、`ProcedureLoadDll`、`IProcedureManager` 的只读 API/源码证据 | 复用现有 FSM 的业务 Procedure | 无第二注册器；运行时可从同一 Procedure Manager 查询到目标类型 |
| 已冻结的 Play 探针 | Unity 编译与 Unity Editor 自动化通道 Play Mode | 可运行的业务流程链 | 编译无新增错误，Play 中目标 Procedure 完成预期进入、离开、取消与下一状态跳转 |

## 实施与验证边界

1. 先用只读证据确认目标类型落在已加载的业务程序集、完整类型名唯一，且既有 `ProcedureLoadDll` 会扫描该程序集；不要为“让它被发现”新增注册代码。
2. 只编辑冻结的业务 Procedure 源码、必要的既有业务调用点和本地 Play 探针。进入时启动的异步工作使用当前流程 `CancellationToken`；离开职责不得与其他状态重复，状态切换放在已确认的触发点，切换后不再执行依赖旧状态的逻辑。
3. 请求 Unity 刷新并等待编译完成。新增编译错误时停止，不改入口 Scene、Config、`Nova.prefab` 或 Framework 来绕过错误。
4. 在 Unity Editor 自动化通道 Play Mode 沿冻结调用点触发目标流程，记录当前流程/Procedure History 或等价探针：目标已注册并进入；离开时异步任务观察到取消且不继续产生副作用；随后进入精确的下一状态。探针不得依赖发布包不存在的 Editor-only 历史作为产品逻辑。
5. 只有业务层归属、编译、现有延迟注册、进入、离开、异步取消和下一状态 Play 证据全部成立才报告 `success`。已完成允许的源码步骤但无法取得 Play 证据时最高为 `partial`；关键输入不唯一或既有 DLL/注册链不覆盖目标类型时为 `blocked`；要求修改框架内置 Procedure 或建立第二注册机制时为 `not_applicable`。

不默认新建或修改入口 Scene、Config、`Nova.prefab`、Framework 源码、程序集/DLL 装载配置、Bundle/Player、设备安装、外部发布或 Git。删除、移动类型、改变程序集或命名空间、修改公共调用契约、构建、安装、外部写入、凭据使用、Git commit / push 都需要对精确目标重新确认。
