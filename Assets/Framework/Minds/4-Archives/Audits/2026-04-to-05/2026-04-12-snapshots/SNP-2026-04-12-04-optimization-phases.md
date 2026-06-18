---
id: SNP-2026-04-12-04
title: 终极优化方案 8 个 Phase（2026-04-12 时点快照）
type: snapshot
date: 2026-04-12
status: archived
keywords: [SNP-2026-04-12-04, 终极优化方案 8 个 Phase（2026-04-12 时点快照）]
tags: [snapshot, audit, 2026-04-12]
sources:
  - .claude/plans/audit-reports/00-architecture-review.md
  - .claude/plans/audit-reports/00-risk-registry.md
  - .claude/plans/audit-reports/00-optimization-plan.md
related:
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[PAT-07-tradeoff-phased-delivery|PAT-07]]"
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
---

# 终极优化方案 8 个 Phase（2026-04-12 时点快照）

> ⚠️ 这是 2026-04-12 时点的审计快照，描述当时状态，**禁止修改**。下次审计请新建 `2026-XX-XX/` 目录。

## 概要

基于 2026-04-12 审计的 193 项缺陷（P0×12 / P1×36 / P2×41 / P3×38 / P4×66），制定 8 阶段终极优化方案：Phase 1 紧急修复 P0、Phase 2 数据正确性 P1、Phase 3 资源/竞态 P2+P3、Phase 4 Component 校验统一化、Phase 5 Partial 拆分规范化、Phase 6 代码风格治理、Phase 7 文档全量同步、Phase 8 Debug 模块架构重构（长期）。本快照归档 4 条执行原则、8 个 Phase 概览、执行优先级总览图与修复后验收标准。

## 正文

### 1. 执行原则（4 条）

1. **先修崩溃，再修数据，最后优化** — P0/P1 阻断交付，P2/P3 应当修复，P4 按排期
2. **每修一个 class 当场同步文档** — 原子操作，不攒到末尾
3. **不做架构大重构** — 除 Debug 模块外，所有修复控制在模块内部，不改变框架骨架
4. **测试验证** — 每个 P0/P1 修复后必须通过 UnityMCP 编译验证

### 2. Phase 1：紧急修复（P0 × 12 项）

**目标**：消除所有崩溃级缺陷
**预计工作量**：2-3 个 agent 并行，一轮迭代

#### 2.1 Debug 模块 P0（3 项）

| # | 文件 | 修复内容 |
|---|------|----------|
| 1 | `DebugComponent.Visitors.cs:176` | 在 `Init()` 中添加 `m_TextEditor = new TextEditor();` |
| 2 | `DebugComponent.ConsoleWindow.cs:558` | `UpdateLogShowState`/`SaveLogShowSetting`/`OnDrawLockScroll` 添加 `if (m_PersistComponent == null) return;` |
| 3 | `DebugComponent.RuntimeFlow.cs:63` | 将 `transform.Find("Canvas/Image")` 改为 `[SerializeField] private RectTransform m_BlockRectTransform`，在 Inspector 中绑定 |

#### 2.2 SDK 模块 P0（1 项）

| # | 文件 | 修复内容 |
|---|------|----------|
| 4 | `SDKComponentInspector.Methods.cs:39` | 在访问 `sdkComponent.SDKManager` 前添加 null 守卫 |

#### 2.3 T1 模块 P0（8 项）

| # | 模块 | 修复方向 |
|---|------|----------|
| 5 | Network | HttpResponse try-finally 包裹 ReferencePool.Release |
| 6 | Network | WebSocket 重连状态机添加 Connecting→Closed 转换 |
| 7 | Event | EventPool.Fire 中 try-catch 包裹每个 handler |
| 8 | Asset | AB 异步回调前检查 bundle 有效性 |
| 9 | Asset | 引用计数添加 <=0 防护 |
| 10 | UI | UIGroup 深度排序改为迭代实现 |
| 11 | Hotfix | SemaphoreSlim try-finally 包裹 Release |
| 12 | Localization | ResolveLanguage null 检查 + 默认语言兜底 |

### 3. Phase 2：数据正确性（P1 × 36 项）

**目标**：修复所有数据错误
**预计工作量**：3-4 个 agent 并行，一轮迭代

#### 3.1 跨模块影响的 P1（5 项关键）

| 优先级 | 文件 | 修复内容 |
|--------|------|----------|
| **最高** | `NovaExtensionForFoundation.Float.cs:32` | `minutes = intTime / 60` → `minutes = (intTime % 3600) / 60` |
| **最高** | `NovaLinkedListRange.cs:Contains()` | `current.Value.Equals(value)` → `EqualityComparer<T>.Default.Equals(current.Value, value)` |
| 高 | `ConfigManager.cs:34-39` | Initialize 中添加 Namespaces 非空校验 |
| 高 | `ConfigManager.cs:63-68` | ABPath/AssetName 校验前移到 Initialize，LoadConfigAsync 改为 return false + Log.Error |
| 高 | `VibrateManager.cs:70-78` | 空路径抛异常改为 Log.Debug + return，与 Sound 对齐 |

#### 3.2 文档 Priority 全量修正

| 文档文件 | 修正 |
|----------|------|
| `ObjectPoolManager.md` (6 处) | 16 → 2 |
| `ObjectPoolManagerBase.md` | 16 → 2 |
| `ObjectPoolComponent.md` | 16 → 2 |
| `TableManagerBase.md` | 4 → 14 |
| `TableManager.md` | 4 → 14 |
| `ConfigManagerBase.md` | 3 → 15 |
| `ARCHITECTURE.md` Shutdown 表 | 修正排序顺序 |

#### 3.3 Debug 模块 P1（5 项）

- 删除 ConsoleWindow 内死字段 `m_LogBGTex`
- 删除 `m_IsInit` 死字段
- 修正 `Application.dataPath` → `Application.persistentDataPath`
- 将环形淘汰中 Unity API 调用延迟到主线程
- 删除 `m_winMiniStyle` 死字段及所有赋值

### 4. Phase 3：资源泄露 + 竞态（P2 × 41 + P3 × 38）

**目标**：消除资源泄露和并发风险
**预计工作量**：4 个 agent 并行，两轮迭代

关键改动：
- **3.1 async void → async UniTaskVoid 统一化**：`SDKComponent.cs:38`、`SoundManager.Methods.cs:43`、`DebugComponent.cs:548`
- **3.2 ConfigComponent 补全 OnDestroy**：清理 `m_LoadTcs / IsLoadOver / m_ConfigManager`
- **3.3 Debug 模块 Texture2D 泄露修复**：`m_LogBGTex / m_LabelPopWinBGTex` 在 OnDestroy 中 Destroy
- **3.4 线程安全加固**：`Util.Assembly.cs` Dictionary → ConcurrentDictionary，`s_Assemblies` volatile；`NovaExtensionForUnity.GameObject.cs` 静态缓存改 `[ThreadStatic]` 或局部变量
- **3.5 BaseComponentInspector 条件 Repaint**：仅在 Playing 或脏属性时触发
- **3.6 ObjectPool FullName 缓存**：构造时一次性算 `m_FullName`，属性直返
- **3.7 ObjectPool 构造器副作用消除**：直接赋值 `m_Capacity / m_ExpireTime`，绕过 setter Release 副作用

### 5. Phase 4：Component 校验统一化

**目标**：贯彻"谁使用谁校验"原则
**预计工作量**：1 个 agent，一轮迭代

#### 5.1 移除 Component 层冗余校验

| Component | 校验处数 | 操作 |
|-----------|----------|------|
| ObjectPoolComponent | 17 处 throw | 全部删除 |
| TableComponent | 4 处 throw | 全部删除 |
| 其他已优化 Component | 0 | 无需操作 |

#### 5.2 确认 Manager 层校验完整

对上述 Component 删除的每个校验点，确认对应 Manager 方法已有等价校验。

### 6. Phase 5：Partial 拆分规范化

**目标**：所有 partial class 严格遵循四文件拆分
**预计工作量**：2 个 agent 并行

#### 6.1 ObjectPool 模块（5 个类需拆分）

| 类 | 当前 | 目标 |
|---|------|------|
| ObjectPoolManager | 1 文件 660 行 | 3 文件 (.cs/.Visitors.cs/.Methods.cs) |
| ObjectPool\<T\> | 1 文件 700 行 | 3 文件 |
| Object\<T\> | 1 文件 170 行 | 2-3 文件 |
| ObjectBase | 1 文件 | 2 文件 |
| ObjectPoolBase | 1 文件 | 2 文件 |

#### 6.2 Table+Config 文件重命名

| 当前 | 目标 |
|------|------|
| `TableManager.Method.cs` | `TableManager.Methods.cs` |
| `ConfigManager.Method.cs` | `ConfigManager.Methods.cs` |
| `TableManager.UnitDataReceiver.cs` | `TableManager.Definitions.cs` |
| `ConfigManager.UnitDataReceiver.cs` | `ConfigManager.Definitions.cs` |

### 7. Phase 6：代码风格治理

**目标**：消除高密度风格违规
**预计工作量**：2 个 agent 并行

关键改动：
- **6.1 NetworkComponentInspector 专项（~60 处）**：所有 `<summary>` 改三行格式、移除对齐空格、移除 `// ----` 分隔注释
- **6.2 Debug 模块风格（~18 处）**：const 前缀 → `c_`、static 前缀 → `s_`、公有字段 `m_` 修正、删注释死代码、移除 `#region`
- **6.3 PersistComponentInspector（~10 处）**：移除 `// ----` 分隔注释
- **6.4 DebugComponentInspector GUIStyle 缓存**：每帧 new GUIStyle 改静态缓存（`s_RightAlignStyle`）

### 8. Phase 7：文档全量同步

**目标**：文档同步率 100%
**预计工作量**：doc-writer 专项

关键改动：
- **7.1 缺失文档补全**：SoundComponentInspector.md / ObjectPoolComponentInspector.md / TableComponentInspector.md / ConfigComponentInspector.md
- **7.2 方法名/描述修正**：TableComponent.md 移除虚假 `LoadAsync().Forget()`；ConfigComponent.md `LoadConfigAsync()` → `LoadAsync()`；ConfigManagerConfig.md / ConfigSettings.md 移除虚假 Namespaces fallback；ObjectPoolComponent.md OnDestroy 移除虚假 Shutdown；VibrateType.md None "默认振动" → "不振动(直接返回)"；ReleaseObjectsFilter.md "值大优先淘汰" → "值小先淘汰"
- **7.3 ARCHITECTURE.md Shutdown 顺序表修正**：按 Priority 降序 `Sound(19) → Vibrate(18) → Debug(17) → SDK(16) → Config(15) → Table(14) → ...`

### 9. Phase 8：Debug 模块架构重构（长期）

**目标**：将业务逻辑从 DebugComponent 下沉到 DebugManager
**预计工作量**：architect 出方案 → 2 个 coder 并行执行

#### 9.1 职责迁移计划

| 当前位置（Component） | 目标位置（Manager） |
|----------------------|---------------------|
| 日志收集/过滤/环形淘汰 | DebugManager |
| FPS/RAM 计数器 | DebugManager |
| 磁盘检测定时协程 | DebugManager |
| GM 工具注册与调用 | DebugManager |
| 窗口模式状态机 | DebugManager |
| 设备白名单 HTTP 请求 | DebugManager |

#### 9.2 Component 保留职责

- IMGUI OnGUI() 绘制分发（Unity 要求 MonoBehaviour）
- [SerializeField] 配置字段
- Unity 生命周期桥接（Awake/Start/OnDestroy）

#### 9.3 IDebugWindow 接口解耦

将 `IDebugWindow.Initialize(DebugComponent, ...)` 参数类型改为 `IDebugContext` 接口，消除子窗口对 DebugComponent 具体类的硬耦合。

#### 9.4 Release 构建剥离

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private void OnGUI() { ... }
#endif
```

### 10. 执行优先级总览

```
Phase 1: P0 紧急修复 ──────── 阻断交付，必须立即执行
    ↓
Phase 2: P1 数据正确性 ────── 必须修复，一轮迭代
    ↓
Phase 3: P2+P3 资源/竞态 ──── 应当修复，两轮迭代
    ↓
Phase 4: 校验统一化 ────────── 低风险重构
    ↓
Phase 5: Partial 拆分 ──────── 纯结构优化
    ↓
Phase 6: 风格治理 ──────────── 不影响功能
    ↓
Phase 7: 文档同步 ──────────── 全程穿插执行
    ↓
Phase 8: Debug 重构 ─────────── 长期项目
```

### 11. 修复后验收标准

| 维度 | 标准 |
|------|------|
| 编译 | UnityMCP read_console 零错误零警告 |
| P0 | 0 项 |
| P1 | 0 项 |
| P2 | ≤ 5 项（均为已评估可接受的设计选择） |
| P3 | ≤ 10 项（均有文档化的线程安全假设） |
| 文档同步率 | 100%（每个 public class 有对应 .md） |
| Partial 拆分 | 100% 合规 |
| Priority 文档 | 与代码 100% 一致 |

## 时点信息
- 审计日期：2026-04-12
- 审计基线：develop 分支 HEAD
- 审计范围：569 个 C# 文件 + 358 个文档（Runtime 438 + Editor 131）

## 关联
- ADR：[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]] [[ADR-007-procedure-tier-split|ADR-007]]
- Pattern：[[PAT-07-tradeoff-phased-delivery|PAT-07]] [[PAT-08-architecture-antipatterns|PAT-08]]
- 同批次快照：[[SNP-2026-04-12-01-module-scoring]] [[SNP-2026-04-12-02-defect-baseline]] [[SNP-2026-04-12-03-dependency-graph]] [[SNP-2026-04-12-05-doc-sync-deviations]]
