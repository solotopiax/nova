---
id: PAT-152
title: RuntimeInitialize 跨阶段启动 UniTask
summary: 同阶段回调无序时跨阶段启动 UniTask
category: runtime
type: pattern
status: active
date: 2026-08-06
source: source-device-and-test-verification
aliases:
  - PAT-152-runtime-initialize-unitask-cross-stage
keywords:
  - PAT-152
  - RuntimeInitializeOnLoadMethod
  - AfterAssembliesLoaded
  - BeforeSceneLoad
  - PlayerLoopHelper
  - UniTask PlayerLoop
  - iOS IL2CPP 启动竞态
tags: [pattern, runtime, unitask, lifecycle, initialization, playerloop, il2cpp]
related:
  - "[[GLO-12-unitask-async-await|GLO-12]]"
---

# PAT-152：RuntimeInitialize 跨阶段启动 UniTask

## 适用场景

- Nova Runtime 或 UPM 适配包通过 `[RuntimeInitializeOnLoadMethod]` 自动注册能力。
- 自动初始化方法会立即调用 `UniTask.Yield`、`UniTask.Delay` 或其他依赖 UniTask PlayerLoop runner 的 API。
- 依赖库也在同一个 `RuntimeInitializeLoadType` 阶段注入 PlayerLoop。
- Editor 正常，但 iOS IL2CPP 等 Player 构建在启动期出现 `PlayerLoopHelper.AddAction` 空引用。

## 核心做法

把“尽早建立同步入口”和“启动依赖 PlayerLoop 的异步任务”拆到有先后关系的两个 Unity 初始化阶段：

1. 较早阶段只做同步注册，例如安装 sink、建立有界缓存和创建 `CancellationTokenSource`。
2. 较晚阶段再调用依赖 UniTask PlayerLoop 的异步 API。
3. 较晚阶段先检查同步入口、取消源和当前就绪状态；依赖已经就绪时直接执行快速路径，否则才启动按帧等待。
4. 两个阶段之间产生的数据进入同步阶段建立的缓存，待依赖就绪后按既有顺序派发。

需要在启动期同步接收数据、异步等待依赖就绪时，采用以下阶段拆分：

```text
AfterAssembliesLoaded
  -> 同步注册入口并建立启动缓存

BeforeSceneLoad
  -> 依赖已就绪：直接 flush
  -> 依赖未就绪：启动 UniTask watcher
```

## 为什么这么做

Unity 不保证同一 `RuntimeInitializeLoadType` 内不同 `[RuntimeInitializeOnLoadMethod]` 的调用顺序。当前 UniTask 在 Unity 2020.1 及以上也于 `AfterAssembliesLoaded` 执行 `PlayerLoopHelper.Init`，而 `runners` 数组直到 `Initialize` 才创建。

如果业务注册方法同样在 `AfterAssembliesLoaded` 立即执行 `UniTask.Yield`，它可能先进入 `PlayerLoopHelper.AddAction` 并索引尚未创建的 `runners`。Editor 的 `[InitializeOnLoadMethod]` 会提前调用 UniTask 初始化，因此 Editor 正常不能证明 Player 启动顺序安全。

把 watcher 延后到 `BeforeSceneLoad` 后，前一初始化阶段已完成，既保留启动期同步捕获，又消除对同阶段回调顺序的依赖。

## 反模式

- 在与 UniTask `PlayerLoopHelper.Init` 相同的初始化阶段直接调用 `UniTask.Yield` 或 `UniTask.Delay`。
- 因为 Editor 不复现，就认定 Player 或 IL2CPP 的启动顺序安全。
- 从业务适配层主动调用 `PlayerLoopHelper.Initialize`，越权接管依赖库的 PlayerLoop 生命周期。
- 为规避初始化竞态改用裸 `Task`、协程或临时 MonoBehaviour，破坏 Nova 的统一异步模型。
- 延后整个 sink 注册，导致两个初始化阶段之间的启动事件无法进入缓存。

## 验证方式

回归测试至少应反射确认：

- 同步注册方法声明为较早的初始化阶段。
- 启动 UniTask watcher 的方法声明为严格更晚的初始化阶段。

同时运行受影响包的定向测试并检查 Unity 编译与 Console。设备侧仍应重新执行原始启动路径，确认 `PlayerLoopHelper.AddAction` 空引用不再出现。

## 来源与验证依据

- UniTask 初始化：`UPMPackages/com.solotopia.unitask/Core/Runtime/PlayerLoopHelper.cs`，`Init` 使用 `AfterAssembliesLoaded`，`Initialize` 创建 `runners`，`AddAction` 直接索引该数组。
- 问题类型：iOS IL2CPP 启动期在 `UniTask.Yield -> PlayerLoopHelper.AddAction` 进入尚未建立的 runner，可表现为空引用；Editor 提前初始化不能作为 Player 安全证据。
- 实施验证：反射确认同步注册与 watcher 分属严格递增的初始化阶段，并在受影响的 IL2CPP 真机启动路径复测；仅有 Editor 编译或测试结果不足以关闭设备侧风险。

## 关联

- UniTask 术语与异步边界：[[GLO-12-unitask-async-await|GLO-12]]
