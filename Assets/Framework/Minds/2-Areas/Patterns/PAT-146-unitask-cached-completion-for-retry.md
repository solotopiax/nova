---
id: PAT-146
title: 缓存 UniTask 完成结果保障启动重试
summary: 缓存异步结果前先明确重复等待语义
category: runtime
type: pattern
status: active
date: 2026-07-16
source: cur-session
aliases:
  - PAT-146-unitask-cached-completion-for-retry
keywords:
  - PAT-146
  - UniTask缓存
  - Preserve
  - 启动重试
tags: [pattern, methodology, unitask, retry, lifecycle, sdk]
related:
  - ADR-022
---

# PAT-146：缓存 UniTask 完成结果保障启动重试

## 适用场景（When）

- Component 或 Manager 暴露惰性初始化任务，并把 `UniTask` 缓存在字段中。
- Procedure、启动器或失败弹窗允许用户重新执行同一段初始化编排。
- 初始化只应执行一次，但完成结果需要被后续流程再次等待。
- 线上出现 `Token version is not matched, can not await twice or get Status after await`。

## 核心做法（What & How）

### 1. 不直接缓存原始 UniTask 后重复返回

UniTask 的异步状态机 source 通常会在 `GetResult` 后回收到池中。缓存原始任务并在完成后再次
`await`，会以旧 token 访问已复用的 source，抛出 token/version 不匹配异常。

需要复用完成结果时，在第一次创建任务时就明确保存语义：

```csharp
private UniTask? m_InitializeTaskCache;

private UniTask GetOrCreateInitializeTask()
{
    if (m_InitializeTaskCache.HasValue)
    {
        return m_InitializeTaskCache.Value;
    }

    m_InitializeTaskCache = InitializeAsync().Preserve();
    return m_InitializeTaskCache.Value;
}
```

`Preserve()` 必须在原始任务第一次被等待之前调用。它记忆成功、异常或取消结果，使初始化完成后的
后续等待复用同一结果而不重新执行初始化。

### 2. 不用业务层状态判断掩盖缓存契约错误

下面的调用侧绕过不能替代框架修复：

```csharp
if (!component.IsInitialized)
{
    await component.InitializeTask;
}
```

它只能绕过已经成功完成的情况，不能修正 `InitializeTask` 属性本身返回已消费任务的问题，也容易让
不同调用方形成不一致的使用约定。重复等待语义应由任务拥有者统一保证。

### 3. 区分“完成后重复等待”与“未完成时并发等待”

当前 UniTask `Preserve()` 的 `MemoizeSource.OnCompleted` 在底层任务未完成时仍把 continuation 直接转发给
原 source。因此本 Pattern 只保证任务完成后的重复等待，不把它描述成并发等待方案。

如果契约明确要求多个调用方在任务未完成时同时等待，应改用具备多 continuation 能力的共享完成源，
并单独添加并发等待测试，不能仅凭 `Preserve()` 名称推断。

### 4. 用红绿测试证明修复命中根因

回归测试至少验证：

1. fake 初始化方法异步挂起一次，避免退化成同步 CompletedTask。
2. 第一次 await 完成后再次获取并 await 同一公开属性。
3. 修复前精确失败为 token/version mismatch。
4. 修复后两次等待都完成，底层初始化调用次数仍为 1。

## 为什么这么做（Why）

`ProcedurePreload` 首次执行时已经完成 SDK 初始化，后续登录因网络超时进入失败弹窗。用户点击重试后，
Preload 从头执行并再次 await SDK 缓存任务。SDKComponent 原先缓存并返回原始 `UniTask`，第二次等待即触发
token/version 异常，导致真正的网络失败被新的异步生命周期错误覆盖。

将完成结果保存语义收口在 SDKComponent 后，业务 Procedure 可以安全重试，同时 SDK 初始化仍只执行一次。

## 反模式（Anti-patterns）

- 把异步方法返回的原始 `UniTask` 长期保存在字段里并重复返回。
- 只在 Procedure 中增加 `IsInitialized` 判断，保留公开任务属性的错误语义。
- 看到 `Preserve()` 就宣称支持任务未完成时的并发等待，却没有源码证据和并发测试。
- 修复后只跑一次成功路径，没有先证明测试能复现原异常。
- 把网络首次失败与重试阶段的 UniTask 异常混成同一个根因。

## 跨项目复用提示

适用于所有使用 UniTask 的 Unity 项目，尤其是 SDK 初始化、配置加载、登录前置、资源清单加载等
“只执行一次但允许流程重试”的启动任务。是否采用 `Preserve`、共享完成源或其他抽象，应由任务的
顺序重复等待与并发等待契约决定。

## 来源（Origin）

- 会话日期：2026-07-16
- 关键对话节选：
  > 用户：这个问题如何修复
  > 用户：你帮我修改吧，然后obs
  > AI：修复前测试准确复现 Token version mismatch；缓存任务增加 Preserve 后定向测试通过，初始化仍只执行一次。

## 关联

- 相关 ADR：[[ADR-022-sdk-plugin-architecture|ADR-022]]
- 当前实现：`Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Methods.cs`
- 回归测试：`Assets/Tests/Editor/SDKComponentInitializeTaskTests.cs`
- 当前文档：`Assets/Framework/Docs/Runtime/Modules/SDK/SDKComponent.md`
