---
id: SNP-2026-04-12-03
title: 依赖图与执行流程（2026-04-12 时点快照）
type: snapshot
date: 2026-04-12
status: archived
keywords: [SNP-2026-04-12-03, 依赖图与执行流程（2026-04-12 时点快照）]
tags: [snapshot, audit, 2026-04-12]
sources:
  - .claude/plans/audit-reports/00-architecture-review.md
  - .claude/plans/audit-reports/00-risk-registry.md
  - .claude/plans/audit-reports/00-optimization-plan.md
related:
  - "[[ADR-002-manager-priority-system|ADR-002]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[PAT-05-l0-l1-l2-docs|PAT-05]]"
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# 依赖图与执行流程（2026-04-12 时点快照）

> ⚠️ 这是 2026-04-12 时点的审计快照，描述当时状态，**禁止修改**。下次审计请新建 `2026-XX-XX/` 目录。

## 概要

本快照归档 2026-04-12 审计实测的 Runtime Manager 完整继承链（含 Priority 数值，与代码一致）、Update 升序执行顺序、Shutdown 降序执行顺序、模块间依赖方向 ASCII 图、基础设施三层栈（业务模块 / Component+Manager / Bases / Utils）、启动 → 帧循环 → 销毁三阶段数据流。Priority 数值以代码实测为准（ObjectPool=2 / Table=14 / Config=15），与 ARCHITECTURE.md 一致，但与多个 L2 文档不一致（参见 SNP-05）。

## 正文

### 1. Runtime Manager 完整继承链（含 Priority 实测值）

```
FrameworkManager (public abstract)
├── AssetManagerBase (internal abstract) : IAssetManager          Priority = 1
│   └── AssetManager (internal sealed partial)
├── ObjectPoolManagerBase (internal abstract) : IObjectPoolManager Priority = 2
│   └── ObjectPoolManager (internal sealed partial)
├── PersistManagerBase (internal abstract) : IPersistManager       Priority = 3
│   └── PersistManager (internal sealed partial)
├── EventManagerBase (internal abstract) : IEventManager           Priority = 4
│   └── EventManager (internal sealed partial)
├── ConfigManagerBase (internal abstract) : IConfigManager         Priority = 15
│   └── ConfigManager (internal sealed partial)
├── TableManagerBase (internal abstract) : ITableManager           Priority = 14
│   └── TableManager (internal sealed partial)
├── UIManagerBase (internal abstract) : IUIManager                 Priority = 5
│   └── UIManager (internal sealed partial)
├── NetworkManagerBase (internal abstract) : INetworkManager       Priority = 6
│   └── NetworkManager (internal sealed partial)
├── HotfixManagerBase (internal abstract) : IHotfixManager         Priority = 7
│   └── HotfixManager (internal sealed partial)
├── LocalizationManagerBase (internal abstract) : ILocalizationManager Priority = 8
│   └── LocalizationManager (internal sealed partial)
├── ProcedureManagerBase (internal abstract) : IProcedureManager   Priority = 9
│   └── ProcedureManager (internal sealed partial)
├── SDKManagerBase (internal abstract) : ISDKManager               Priority = 16
│   └── SDKManager (internal sealed partial)
├── DebugManagerBase (internal abstract) : IDebugManager           Priority = 17
│   └── DebugManager (internal sealed partial)
├── VibrateManagerBase (internal abstract) : IVibrateManager       Priority = 18
│   └── VibrateManager (internal sealed partial)
└── SoundManagerBase (internal abstract) : ISoundManager           Priority = 19
    └── SoundManager (internal sealed partial)
```

### 2. Update 执行顺序（Priority 升序）

```
Asset(1) → ObjectPool(2) → Persist(3) → Event(4) → UI(5) → Network(6) →
Hotfix(7) → Localization(8) → Procedure(9) → Table(14) → Config(15) →
SDK(16) → Debug(17) → Vibrate(18) → Sound(19)
```

### 3. Shutdown 执行顺序（Priority 降序）

```
Sound(19) → Vibrate(18) → Debug(17) → SDK(16) → Config(15) → Table(14) →
Procedure(9) → Localization(8) → Hotfix(7) → Network(6) → UI(5) →
Event(4) → Persist(3) → ObjectPool(2) → Asset(1)
```

### 4. 模块间依赖方向

```
                    ┌──────────────────────────────┐
                    │        Nova (Root)            │
                    │   DontDestroyOnLoad 根节点    │
                    └──────────┬───────────────────┘
                               │ 获取所有 Component
                    ┌──────────▼───────────────────┐
                    │  FrameworkComponentsGroup     │
                    │  (静态注册表 Dictionary<Type>) │
                    └──────────┬───────────────────┘
                               │
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │  Asset   │◄───────│  Hotfix  │        │  Table   │
    │Component │        │Component │        │Component │
    └──────────┘        └──────────┘        └──────────┘
          ▲                                       ▲
          │                                       │
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │   UI     │        │  Event   │◄───ALL │  Config  │
    │Component │        │Component │        │Component │
    └──────────┘        └──────────┘        └──────────┘
          ▲                    ▲
          │                    │
    ┌──────────┐        ┌──────────┐
    │Procedure │───────►│   SDK    │
    │Component │        │Component │
    └──────────┘        └──────────┘
```

**关键原则**：
- 所有模块通过 `Nova.Xxx` 访问其他模块（间接依赖）
- 跨模块通信优先走 Event 系统
- Runtime → Editor 严格单向（零违规）
- Editor 只通过 Component 公开 API 读写数据

### 5. 基础设施三层栈

```
┌─────────────────────────────────────────────┐
│              业务模块层 (16 Components)       │
├─────────────────────────────────────────────┤
│         FrameworkComponent / Manager         │
│      FrameworkComponentsGroup / ManagersGroup│
├─────────────────────────────────────────────┤
│    Bases: ReferencePool / FSM / Log / Txt    │
│    Bases: NovaLinkedList / MultiDictionary    │
│    Bases: DataReceiver / Extensions          │
├─────────────────────────────────────────────┤
│    Utils: TypeCreator / Assembly / Encrypt    │
│    Utils: Json / SQLite / SysIO / MD5        │
└─────────────────────────────────────────────┘
```

### 6. 启动流程（Nova.Awake → Component.Awake → Nova.Start → Component.Start）

```
Nova.Awake() [DefaultExecutionOrder(-1000)]
  ├── base.Awake() → FrameworkComponentsGroup.RegisterComponent
  ├── TxtHelper = TypeCreator.Create<ITxtHelper>()
  ├── LogHelper = TypeCreator.Create<ILogHelper>()
  ├── RefHelper = TypeCreator.Create<IReferenceHelper>()
  ├── Application.targetFrameRate = m_TargetFrameRate
  └── Application.runInBackground = true

各 Component.Awake() [自然执行顺序]
  ├── base.Awake() → FrameworkComponentsGroup.RegisterComponent
  └── m_XxxManager = TypeCreator.Create<IXxxManager>(typeName)
       └── FrameworkManager 构造器 → FrameworkManagersGroup.RegisterManager(this)
           └── 按 Priority 排序插入 LinkedList

Nova.Start()
  ├── Self = this
  ├── Asset = GetComponent<AssetComponent>()  // 获取全部 16 个 Component
  └── ... (其他 Component)

各 Component.Start()
  └── m_XxxManager.Initialize(config)
       └── 配置注入、跨模块引用获取
```

### 7. 帧循环

```
Nova.Update()
  └── FrameworkManagersGroup.Update()
       └── foreach manager in linkedList (Priority 升序)
            └── manager.Update()
```

### 8. 销毁流程

```
Nova.OnDestroy()
  └── FrameworkManagersGroup.Shutdown()
       └── foreach manager in linkedList.Reverse (Priority 降序)
            └── manager.Shutdown()
```

## 时点信息
- 审计日期：2026-04-12
- 审计基线：develop 分支 HEAD
- 审计范围：569 个 C# 文件 + 358 个文档（Runtime 438 + Editor 131）

## 关联
- ADR：[[ADR-002-manager-priority-system|ADR-002]] [[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]
- Pattern：[[PAT-05-l0-l1-l2-docs|PAT-05]] [[PAT-06-main-session-dispatch|PAT-06]]
- 同批次快照：[[SNP-2026-04-12-01-module-scoring]] [[SNP-2026-04-12-02-defect-baseline]] [[SNP-2026-04-12-04-optimization-phases]] [[SNP-2026-04-12-05-doc-sync-deviations]]
