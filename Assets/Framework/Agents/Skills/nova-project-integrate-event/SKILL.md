---
name: nova-project-integrate-event
description: Use when 项目组要在现有 Nova 项目中新增或调整业务事件类型、订阅与发布链，并需验证分发时序、回池边界和订阅注销生命周期时使用。
---

# Nova 接入业务事件

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支按需读取：定义事件载荷、`Clear()` 或异步安全持有策略时读 `Docs/Runtime/Modules/Event/Definitions/EventData.md`；选择排队或同步分发时读 `Docs/Runtime/Modules/Event/EventManager.md` 与 `Docs/Runtime/Modules/Event/Interfaces/IEventManager.md`；确认现有 `Nova.Event` 门面、事件池配置或诊断探针时读 `Docs/Runtime/Modules/Event/EventComponent.md`。不要递归加载全部 Event、ReferencePool 或业务模块文档。

## 冻结输入与阻断门

先冻结唯一项目根、目标业务程序集和命名空间、事件完整类型名与载荷字段、发布者与触发点、订阅者及其拥有者生命周期、`Fire` 或 `FireNow` 的选择和调用线程、需要异步持有的数据策略、允许写入集，以及可观察发布、处理、注销和回池边界的 Play 探针。任一项不唯一时返回 `blocked`，不要按相邻事件、文件名或 Sample 猜测。

- 业务事件类型继承 `EventData`，并在 `Clear()` 重置所有自有字段。若 handler 结束后还要异步使用事件数据，必须在 handler 内复制所需值，或由该事件类型显式实现深拷贝 `Clone()`；默认 `Clone()` 不可用。
- `Fire(sender, eventData)` 是线程安全的入队路径，处理器在下一次 Event 更新时于主线程分发；跨线程发布只能走它。`FireNow(sender, eventData)` 是同步立即分发，非线程安全，只能在已确认的主线程同步路径使用。
- 事件由 `ReferencePool.Get<T>()` 取得后，分发链会在处理结束时回池。发布者和 handler 都不得再次手动回池，也不得跨 handler、跨帧或跨异步任务持有该实例。
- 订阅和注销使用同一个 handler 实例，并绑定到已冻结的拥有者生命周期；关闭、销毁或离开职责时必须恰好注销一次。不要用 `SetDefaultHandler` 取代业务订阅，也不在本 Skill 中调整全局事件池配置、`Nova.prefab` 或框架 Event 实现。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的程序集、事件类型、载荷和调用链 | 工作区源码只读检查 | 业务代码归属与既有 Event 门面事实 | 完整类型名、asmdef 和现有 `Nova.Event` 可用性一致 |
| 已冻结的发布、订阅、注销与异步持有契约 | `workspace-edit` | 目标 `EventData`、发布者、订阅者和本地 Play 探针 | `Clear()` 完整；同一 handler 成对订阅/注销；没有手动回池或长期持有 |
| 已冻结的分发模型与调用线程 | `ReferencePool.Get<T>()`、`Nova.Event.Subscribe<T>()`、`Unsubscribe<T>()`、`Fire()` / `FireNow()`、`EventData.Clone()` | 可观察的业务事件链 | 选择与线程、时序及持有策略一致 |
| 已冻结的运行验证路径 | Unity Editor 自动化通道 的脚本刷新、编译与 Play Mode 观察 | 已编译且可触发的事件链 | 发布、处理、注销与所选分发时序均可观察 |

## 实施与验证边界

1. 先确认目标业务程序集、事件类型、发布点、订阅拥有者、注销点和分发模型；目标是修改 Framework Event、全局池策略或默认处理器时返回 `not_applicable`。
2. 只编辑冻结的业务 `EventData`、发布/订阅源和本地 Play 探针。订阅在拥有者进入时建立，在对应关闭、销毁或离开点注销；不要新增全局单例订阅器来规避生命周期。
3. 对 `Fire` 的探针必须确认处理不在调用栈内、而在后续 Event 更新发生；对 `FireNow` 的探针必须确认处理在调用栈内完成且调用点为主线程。两种模型都在本次范围时分别验证，不能用一种证据替代另一种。
4. 请求 Unity 刷新并等待编译完成，再从冻结触发点执行 Play 验证。handler 若启动异步工作，只传递复制值或明确的深拷贝，不把池化事件对象带入异步链。
5. 只有事件归属、编译、所选分发时序、处理器效果、订阅注销和回池边界均有 Play 证据时报告 `success`。已完成允许的源码步骤但缺少 Play 证据时最高为 `partial`；输入不唯一、没有事件门面或无法冻结生命周期时为 `blocked`；框架或全局事件策略改造为 `not_applicable`。

不默认修改 Framework 包、`Nova.prefab`、EventComponent/事件池配置、Scene、Prefab、程序集配置、Bundle/Player、设备、外部服务或 Git。删除业务事件、改变公共载荷契约、改变分发模型、使用 `FireNow`、修改既有订阅者、外部写入、凭据使用、Git commit / push 都需要本 Skill 之外的精确确认。
