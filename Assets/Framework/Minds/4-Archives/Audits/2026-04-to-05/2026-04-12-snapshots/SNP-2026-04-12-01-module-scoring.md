---
id: SNP-2026-04-12-01
title: 模块评分矩阵与架构亮点痛点（2026-04-12 时点快照）
type: snapshot
date: 2026-04-12
status: archived
keywords: [SNP-2026-04-12-01, 模块评分矩阵与架构亮点痛点（2026-04-12 时点快照）]
tags: [snapshot, audit, 2026-04-12]
sources:
  - .claude/plans/audit-reports/00-architecture-review.md
  - .claude/plans/audit-reports/00-risk-registry.md
  - .claude/plans/audit-reports/00-optimization-plan.md
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[PAT-01-defect-severity|PAT-01]]"
  - "[[PAT-02-static-review-four-dim|PAT-02]]"
---

# 模块评分矩阵与架构亮点痛点（2026-04-12 时点快照）

> ⚠️ 这是 2026-04-12 时点的审计快照，描述当时状态，**禁止修改**。下次审计请新建 `2026-XX-XX/` 目录。

## 概要

2026-04-12 全模块深度审计对 Nova Framework 的 19 个模块（含 Bases / Utils / Editor 三个非业务层）按 4 个维度（继承链 / C+M 分离 / 接口解耦 / 目录结构）打分。15 个业务模块全部 4 维度满分（4.0~5.0 综合分），唯有 Debug 模块综合 2.8 严重失衡。框架整体评分 4.3 / 5。本快照同时归档 6 条架构亮点与 4 条核心架构痛点（Debug 失衡、async void 滥用、Component 校验不一致、Priority 文档失真）。

## 正文

### 1. 评分矩阵（19 个模块 × 4 维度）

| 模块 | 继承链 | C+M 分离 | 接口解耦 | 目录结构 | 综合 |
|------|--------|----------|----------|----------|------|
| Nova 根节点 | 5 | 5 | 5 | 5 | **4.5** |
| Event | 5 | 5 | 5 | 5 | **4.5** |
| Network | 5 | 4 | 5 | 5 | **4.3** |
| Asset | 5 | 5 | 5 | 5 | **4.5** |
| UI | 5 | 5 | 5 | 5 | **4.5** |
| Hotfix | 5 | 5 | 5 | 5 | **4.3** |
| Localization | 5 | 5 | 5 | 5 | **4.5** |
| Persist | 5 | 5 | 5 | 5 | **4.5** |
| Procedure | 5 | 5 | 5 | 5 | **4.5** |
| ObjectPool | 5 | 5 | 5 | 5 | **4.0** |
| Table | 5 | 5 | 5 | 5 | **4.8** |
| Config | 5 | 5 | 5 | 5 | **4.8** |
| SDK | 5 | 5 | 5 | 5 | **4.8** |
| Sound | 5 | 5 | 5 | 5 | **4.8** |
| Vibrate | 5 | 5 | 5 | 5 | **5.0** |
| **Debug** | 4 | **2** | 3 | 3 | **2.8** |
| Bases 基础层 | N/A | N/A | 5 | 5 | **4.0** |
| Utils 工具层 | N/A | N/A | 5 | 5 | **4.0** |
| Editor 层 | N/A | 5 | 5 | 5 | **4.5** |

**框架整体评分：4.3 / 5**

### 2. 架构亮点（6 条）

1. **Component + Manager 分离一致性极高**：16 个业务模块中 15 个严格遵循三层继承链 `FrameworkManager → XxxManagerBase (internal abstract) → XxxManager (internal sealed partial)`。访问修饰符无一例外正确。

2. **接口解耦彻底**：所有 Component 通过 `IXxxManager` 接口持有 Manager，全局访问通过 `Nova.Xxx` 静态属性。平级模块间无直接依赖，跨模块通信走 Event。

3. **Priority 驱动的生命周期管理**：`FrameworkManagersGroup` 以 Priority 排序的 LinkedList 管理所有 Manager，Update 正序 / Shutdown 逆序，设计优雅且可预测。

4. **Editor 三文件联动规范**：全部 16 个 ComponentInspector 严格遵循 `.cs / .Visitors.cs / .Methods.cs` 三文件结构，声明顺序 = 绑定顺序 = 绘制顺序，一致性极高。

5. **EditorUtil.Draw 封装体系**：绝大多数 Inspector 通过统一的 `EditorUtil.Draw` 封装层绘制 UI，仅发现 1 处不合理的直接使用（TextLocalizingInspector）。

6. **Helper 注入模式**：Log / Txt / Reference 三个基础服务通过 Helper 接口 + TypeCreator 反射注入，可替换性好。

### 3. 架构痛点（4 条）

#### 3.1 Debug 模块 — 架构严重失衡

**问题**：DebugComponent 承载了 35+ 个 partial 文件的业务逻辑（日志收集、IMGUI 绘制、FPS/RAM 计数器、磁盘检测、GM 工具、白名单请求、触摸状态机），而 DebugManager 几乎是空壳。

**影响**：严重违反 SRP 和 Component / Manager 分离原则。单个类文件数量是其他模块的 5-10 倍，维护成本极高。

**建议**：将业务逻辑下沉到 DebugManager，Component 仅保留 IMGUI OnGUI() 绘制分发和 Unity 生命周期桥接。

#### 3.2 async void 滥用

**问题**：`SDKComponent.Start()`、`SoundManager.LoadAndPlaySoundAsync`、`DebugComponent.GetDeviceWhiteDevices` 等使用 `async void` 而非 `async UniTask` / `async UniTaskVoid`。

**影响**：异常无法被调用方捕获；MonoBehaviour Destroy 后异步续体仍可执行；调用方无法知晓完成时机。

**建议**：统一改为 `async UniTaskVoid`（fire-and-forget 场景）或 `async UniTask`（需要 await 场景），在 `.Forget()` 处理未观察异常。

#### 3.3 Component 门面层参数校验不一致

**问题**：部分 Component（ObjectPool 17 处、Table 4 处）在门面层做了参数校验（throw ArgumentNullException），而其他 Component（Config、UI 优化后）纯透传不校验。

**影响**：与项目确立的"谁使用谁校验"原则冲突，增加维护认知负担。

**建议**：统一移除 Component 层校验，全部下沉到 Manager 层。

#### 3.4 Priority 文档系统性失真

**问题**：ObjectPool（文档 16 vs 代码 2）、Table（文档 4 vs 代码 14）、Config（文档 3 vs 代码 15）的 L2 文档中 Priority 值全部错误。ARCHITECTURE.md 中的 Shutdown 顺序表也存在排序错误。

**影响**：开发者依据文档理解模块初始化 / 销毁顺序时获得完全错误的信息。

**建议**：建立 Priority 单一真相源（ARCHITECTURE.md），L2 文档引用而非复制。

## 时点信息
- 审计日期：2026-04-12
- 审计基线：develop 分支 HEAD
- 审计范围：569 个 C# 文件 + 358 个文档（Runtime 438 + Editor 131）

## 关联
- ADR：[[ADR-001-component-manager-three-layer|ADR-001]] [[ADR-002-manager-priority-system|ADR-002]]
- Pattern：[[PAT-01-defect-severity|PAT-01]] [[PAT-02-static-review-four-dim|PAT-02]]
- 同批次快照：[[SNP-2026-04-12-02-defect-baseline]] [[SNP-2026-04-12-03-dependency-graph]] [[SNP-2026-04-12-04-optimization-phases]] [[SNP-2026-04-12-05-doc-sync-deviations]]
