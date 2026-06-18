---
id: PAT-30
title: Nova 框架使用红线（陷阱清单）
type: pattern
status: active
date: 2026-05-18
summary: 框架使用红线汇总命名访问绕封装清单
category: arch
aliases:
  - framework-usage-redlines
keywords: [Nova 框架使用红线（陷阱清单）, PAT-30, framework-usage-redlines]
tags:
  - nova
  - framework
  - redline
  - api-discipline
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-002-manager-priority-system]]"
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006（已归档）]]"
  - "[[ADR-008-managerbase-internal-abstract]]"
  - "[[ADR-011-load-unload-and-ireference-pairing]]"
  - "[[ADR-018-json-via-util-json]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
---

# PAT-30：Nova 框架使用红线（陷阱清单）

## 适用场景

- 业务/框架代码调用 Nova 的 Component / Manager / Pool / Util 时，作为高频核对清单。
- code-reviewer 静态审查时，先按此页排除已知陷阱。

## 核心做法

下表按调用面汇总常见红线，来源仍以 `Docs` 和代码为准。

### Component / Manager 调度

| 场景 | 红线 |
|---|---|
| Manager 构造时机 | 必须在 `Component.Start()` 中创建，禁止在 `Awake()` 中创建 |
| Manager 获取 | 只能 `FrameworkManagersGroup.GetManager<IXxx>()`，传**接口类型**；传具体类抛异常 |
| `Util.TypeCreator` 入参 | 必须传完整限定类名（含 namespace），如 `"NovaFramework.Runtime.XxxManager"` |
| `base.Awake()` | 任何 Component 子类重写 `Awake` 必须先调 `base.Awake()`，否则不会注册到 `FrameworkComponentsGroup` |
| 数据加载时序 | `Nova.Config.IsLoadOver` / `Nova.Table.IsLoadOver` 为 `true` 才能读；禁止在 `Awake` 内读取 |

### 资源 / 引用池

| 场景 | 红线 |
|---|---|
| 销毁 Prefab 实例 | 走 `PrefabComponent.Destroy(go)` 或 `Object.Destroy(go)` / 父节点销毁；`PrefabInstanceTag.OnDestroy` 单路释放 `IAssetHandle`。**`AssetComponent` 无 `Destroy`**，禁用 `Nova.Asset` 销毁 Prefab 实例 |
| YooAsset Load | Load 后必须显式释放对应句柄或资源入口，禁止空 `releaseFunc`（详 [[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]） |
| `EventData` | 分发后自动归还引用池；handler 外**禁止**持有 `EventData` 引用 |

### UI / UIView

| 场景 | 红线 |
|---|---|
| `OnClose` | 必须重置实例状态，否则池化复用时旧状态泄漏到新实例 |
| `OnInit(isNewInstance=false)` | 不触发用户钩子（仅复用初始化）；新逻辑别挂在这里 |
| `OnDepthChanged` 重写 | 必须 `base.OnDepthChanged(...)`，否则 `m_DepthInUIGroup` 不同步 |
| `OnRecycle` 重写 | 必须 `base.OnRecycle()`，否则框架侧字段不复位 |

### 表格 / 配置

| 场景 | 红线 |
|---|---|
| 表格容器（`TbXxx`） | 必须实现 `ITable`（提供 `Mode`），否则 `BuildTablesFromCache` 无法存入 `m_Tables` |

### Editor / Inspector

| 场景 | 红线 |
|---|---|
| GUI 绘制 | 禁直接调 `EditorGUILayout.*` / `GUILayout.*`，统一 `EditorUtil.Draw.*` |
| 读 Component 私有字段 | 走 `EditorUtil.Serializer.GetProperty<TTarget,TValue>(target, "m_FieldName")`，禁自行反射 |
| 文件系统 | 走 `EditorUtil.FileSystem.*`，禁直接 `System.IO.*` 绕过 AssetDatabase |
| 运行时数据绘制 | 仅在 `IEditorRuntimeDrawer.Draw` 内 `Application.isPlaying` 判定后读取 |
| `serializedObject.Update()` | 由 `BaseComponentInspector.OnInspectorGUI()` 统一调用，子类不得重复 |
| `SerializedProperty` 绑定 | 必须在 `OnEnable` 中，禁止 `OnInspectorGUI` 内每帧查找 |
| Excel 导出前清理 | 先调 `EditorUtil.FileDataConverter.DeleteExportFiles(dataPath, classPath)` 再转换 |
| 类型名查询 | 走 `EditorUtil.TypeCache.GetTypeNames(typeof(IXxx))`，禁自行反射扫程序集 |
| `AssetDatabase.Refresh` | 走 `EditorUtil.FileSystem.RefreshDelayed()`（delay 版），禁在 GUI 绘制中直接 Refresh |

## Why（为什么这些是铁律）

- `Start` 阶段构造保证 Component 就绪，`Awake` 内构造会破坏注册顺序。
- 外部只能见到接口，`GetManager<具体类>` 直接抛异常是契约保护。
- `AssetComponent` 的销毁与 `PrefabInstanceTag.OnDestroy` 单路收口绑定，Editor 工具统一入口则是为了减少全局改 callsite。

## 反模式

- ❌ `Awake()` 中创建 Manager 或读取 `Nova.Config.*`——其他 Component / Config 数据未就绪
- ❌ `FrameworkManagersGroup.GetManager<XxxManager>()`——传具体类必抛异常，必须传 `IXxxManager`
- ❌ `Object.Destroy(prefabInstance)` 之外用 `Nova.Asset.Destroy(go)`——`AssetComponent` 上根本没有这个方法，编译期就该挡住
- ❌ Inspector 子类里 `EditorGUILayout.PropertyField(...)` / `GUILayout.Label(...)`——绕开 `EditorUtil.Draw` 表面看着没事，实际丢失了对齐 / 多语言 / Width 等统一规则
- ❌ Inspector `OnInspectorGUI` 内 `serializedObject.FindProperty("m_X")`——基类已 Update，子类再绑定一次还每帧重查，性能直接掉
- ❌ Editor 里直接 `File.WriteAllText`/`Directory.CreateDirectory`——绕过 AssetDatabase，刷新时机不可控
