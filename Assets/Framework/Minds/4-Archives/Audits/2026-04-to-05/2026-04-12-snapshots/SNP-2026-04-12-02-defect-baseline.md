---
id: SNP-2026-04-12-02
title: 缺陷统计基线（2026-04-12 时点快照）
type: snapshot
date: 2026-04-12
status: archived
keywords: [SNP-2026-04-12-02, 缺陷统计基线（2026-04-12 时点快照）]
tags: [snapshot, audit, 2026-04-12]
sources:
  - .claude/plans/audit-reports/00-architecture-review.md
  - .claude/plans/audit-reports/00-risk-registry.md
  - .claude/plans/audit-reports/00-optimization-plan.md
related:
  - "[[ADR-003-main-session-as-team-leader|ADR-003]]"
  - "[[ADR-004-static-runtime-review-split|ADR-004]]"
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-04-read-what-you-change|PAT-04]]"
---

# 缺陷统计基线（2026-04-12 时点快照）

> ⚠️ 这是 2026-04-12 时点的审计快照，描述当时状态，**禁止修改**。下次审计请新建 `2026-XX-XX/` 目录。

## 概要

2026-04-12 审计发现全框架缺陷共 193 项，按严重度分布：P0 崩溃 12 项 / P1 数据错误 36 项 / P2 资源泄露 41 项 / P3 竞态并发 38 项 / P4 边界条件 66 项。Debug 模块以 17 项缺陷居首（含 3 项 P0）。本快照归档 P0 全部 12 项详细清单（文件:行号 + 修复方向）、P1 高优先级 11 项明细 + 模块级数量摘要、P2/P3 关键模式归类、P4 高频模式。

## 正文

### 1. 缺陷统计总览（T1/T2/T3 × 16 模块 × P0~P4）

| 批次 | 模块 | P0 | P1 | P2 | P3 | P4 | 小计 |
|------|------|----|----|----|----|-----|------|
| **T1** | Network | 2 | 3 | 3 | 3 | 3 | 14 |
| **T1** | Event | 1 | 4 | 3 | 2 | 3 | 13 |
| **T1** | Asset | 2 | 3 | 3 | 3 | 3 | 14 |
| **T1** | UI | 1 | 3 | 3 | 2 | 3 | 12 |
| **T1** | Hotfix | 1 | 3 | 3 | 3 | 4 | 14 |
| **T1** | Localization | 1 | 3 | 2 | 2 | 3 | 11 |
| **T2** | Persist | 0 | 1 | 4 | 2 | 3 | 10 |
| **T2** | Procedure | 0 | 3 | 3 | 1 | 3 | 10 |
| **T2** | ObjectPool | 0 | 2 | 3 | 2 | 4 | 11 |
| **T2** | Table+Config | 0 | 3 | 2 | 2 | 3 | 10 |
| **T3** | Debug | 3 | 5 | 3 | 3 | 3 | 17 |
| **T3** | SDK | 1 | 0 | 0 | 2 | 2 | 5 |
| **T3** | Sound | 0 | 0 | 3 | 2 | 4 | 9 |
| **T3** | Vibrate | 0 | 1 | 3 | 2 | 2 | 8 |
| **T3** | Nova+Bases+Utils | 0 | 2 | 1 | 4 | 16 | 23 |
| **Bonus** | Editor 层 | 0 | 0 | 2 | 3 | 7 | 12 |
| | **总计** | **12** | **36** | **41** | **38** | **66** | **193** |

### 2. P0 — 崩溃级（12 项，必须修复）

| # | 模块 | 文件 | 问题 | 修复方向 |
|---|------|------|------|----------|
| 1 | Network | `NetworkManager.cs` | HttpResponse 未归还 ReferencePool，循环中异常导致跳过 Release | try-finally 包裹 Release 调用 |
| 2 | Network | `WebSocketManager.cs` | 重连状态机在 Connecting 状态下收到 Close 事件时死锁 | 添加 Connecting→Closed 状态转换 |
| 3 | Event | `EventPool.cs` | Fire 后 handler 抛异常导致后续 handler 被跳过 | try-catch 包裹每个 handler 调用 |
| 4 | Asset | `ABLoadManager.cs` | 异步加载回调在 AB 已被卸载后访问 null bundle | 回调前检查 bundle 有效性 |
| 5 | Asset | `AssetLoadManager.cs` | 引用计数 underflow（Release 多于 Acquire）导致负数 | 添加 <=0 防护，Log.Error |
| 6 | UI | `UIManager.cs` | UIGroup 深度排序在 UI 数量 >100 时 StackOverflow（递归排序） | 改为迭代排序 |
| 7 | Hotfix | `HotfixManager.cs` | 并发下载 SemaphoreSlim 在异常路径未 Release | try-finally 包裹 semaphore.Release |
| 8 | Localization | `LocalizationManager.cs` | ResolveLanguage 回退到 null 语言时 NullRef | 添加 null 检查 + 默认语言兜底 |
| 9 | Debug | `DebugComponent.Visitors.cs:176` | `m_TextEditor` 从未初始化，COPY 按钮点击 NullRef | Init() 中初始化 `new TextEditor()` |
| 10 | Debug | `DebugComponent.ConsoleWindow.cs:558` | `m_PersistComponent` 为 null 时 `UpdateLogShowState` NullRef | 添加 null 检查 |
| 11 | Debug | `DebugComponent.RuntimeFlow.cs:63` | `transform.Find("Canvas/Image")` 违规运行时路径查找 | 改为 `[SerializeField]` 引用 |
| 12 | SDK | `SDKComponentInspector.Methods.cs:39` | 运行时面板 `SDKManager` 为 null 时 NullRef | 添加 null 守卫 |

### 3. P1 — 数据错误（36 项，必须修复）

#### 3.1 高优先级（跨模块影响，11 项明细）

| # | 模块 | 文件 | 问题 |
|---|------|------|------|
| 1 | Bases | `NovaExtensionForFoundation.Float.cs:32` | **FloatToTimeString minutes 计算错误**：`minutes = intTime / 60` 未对 3600 取模，>=3600s 输出 "02:120:00" |
| 2 | Bases | `NovaLinkedListRange.cs` | `Contains()` 中 `current.Value.Equals(value)` 当 Value 为 null 时 NRE |
| 3 | Config | `ConfigManager.cs:34-39` | Initialize 未校验 Namespaces 非空，延迟到 LoadConfigAsync 才暴露 |
| 4 | Config | `ConfigManager.cs:63-68` | LoadConfigAsync 中 ABPath/AssetName 空检查 throw 被 Component 吞掉 |
| 5 | Table+Config | 多处文档 | **Priority 文档严重失真**：Table 文档写 4（实际 14），Config 文档写 3（实际 15） |
| 6 | ObjectPool | 多处文档 | **Priority 文档严重失真**：文档全部写 16（实际 2） |
| 7 | Debug | `ConsoleWindow.cs:55` | `private Texture2D m_LogBGTex` 名称遮蔽 Component 层同名字段 — 死字段 |
| 8 | Debug | `DebugComponent.DiskAndPerformance.cs:120` | `m_IsInit` 死字段无任何读写引用 |
| 9 | Debug | `DebugComponent.RuntimeFlow.cs:98` | 使用 `Application.dataPath` 截取盘符，应使用 `persistentDataPath` |
| 10 | Debug | `ConsoleWindow.cs:1630` | 环形淘汰在 lock 内调 Unity 主线程 API（线程不安全） |
| 11 | Vibrate | `VibrateManager.cs:70-78` | 空路径时抛 InvalidOperationException，与 Sound 模块不一致 |

#### 3.2 模块级 P1 数量摘要

| 模块 | P1 数量 | 关键主题 |
|------|---------|----------|
| Network | 3 | AES 解密异常未处理、DoH 超时无降级、HTTP 302 未跟随 |
| Event | 4 | EventTypeID 注册冲突静默覆盖、EventData 引用池未归还 |
| Asset | 3 | 异步回调链中间节点失败后续节点悬空、弱引用缓存未清理 |
| UI | 3 | UIView 生命周期状态不一致、泛型 Open API 类型推断歧义 |
| Hotfix | 3 | 版本比对溢出、增量策略哈希碰撞、资源矫正路径错误 |
| Localization | 3 | 字体适配加载竞态、TextLocalizing TMP 刷新链断裂 |
| Persist | 1 | SQLite WAL 模式下脏标记不一致 |
| Procedure | 3 | FSM 状态切换时序、内置流程硬编码依赖、Launcher UI 生命周期 |
| ObjectPool | 2 | FullName 属性每次分配字符串、构造器副作用调用 Release() |

### 4. P2 — 资源泄露（41 项，应当修复）— 关键模式

| 模式 | 出现模块 | 影响 |
|------|----------|------|
| **async void / UniTaskVoid 异常吞没** | Network, Hotfix, Sound, Debug | 未观察异常可能导致进程终止 |
| **IReference 池对象未归还** | Event, Network, Hotfix | 对象池计数失衡，内存持续增长 |
| **Component 缺少 OnDestroy 清理** | Config, ObjectPool | 域重载后字段残留 |
| **手动创建 Texture2D 未 Destroy** | Debug | GPU 纹理泄露 |
| **BaseComponentInspector 无条件 Repaint** | Editor 全局 | 编辑态持续 CPU 开销 |
| **SoundDataReceiver 非池化重复 new** | Sound | GC 压力 |
| **NovaOrderedDictionary Keys/Values 每次分配** | Bases | 紧循环 GC 压力 |

### 5. P3 — 竞态与并发（38 项，应当修复）— 关键模式

| 模式 | 出现模块 | 影响 |
|------|----------|------|
| **Dictionary 非线程安全** | Util.Assembly, Fsm, Event | 并发写入导致数据结构损坏 |
| **静态缓存重入** | Bases Extensions | 回调链中覆盖缓存数据 |
| **async void 生命周期风险** | SDK, Sound, Debug | MonoBehaviour Destroy 后异步续体执行 |
| **集合遍历中间接修改** | ObjectPool, Event | 潜在 InvalidOperationException |

### 6. P4 — 边界条件（66 项，建议修复）— 高频模式

- Component 层重复参数校验（与"谁使用谁校验"原则冲突）：ObjectPool(17 处), Table(4 处)
- ArgumentException vs ArgumentNullException 不一致：ObjectPool, Table
- 冗余 using 指令：多模块
- LanguageTable 数组长度与枚举不同步无编译检查
- DataReceiver 重入返回错误资源

## 时点信息
- 审计日期：2026-04-12
- 审计基线：develop 分支 HEAD
- 审计范围：569 个 C# 文件 + 358 个文档（Runtime 438 + Editor 131）

## 关联
- ADR：[[ADR-003-main-session-as-team-leader|ADR-003]] [[ADR-004-static-runtime-review-split|ADR-004]]
- Pattern：[[PAT-03-runtime-verify-three-step|PAT-03]] [[PAT-04-read-what-you-change|PAT-04]]
- 同批次快照：[[SNP-2026-04-12-01-module-scoring]] [[SNP-2026-04-12-03-dependency-graph]] [[SNP-2026-04-12-04-optimization-phases]] [[SNP-2026-04-12-05-doc-sync-deviations]]
