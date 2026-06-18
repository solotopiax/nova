---
id: MOC-Table
title: 数据表系统 MOC（Table System）
summary: TableComponent、ITable、Luban 导出与运行时查表速查
category: runtime
status: active
date: 2026-06-05
aliases:
  - MOC-Table
  - 数据表系统图谱
  - 配置表系统图谱
  - Luban图谱
tags: [moc, nova, table, luban, runtime]
keywords: [TableManager, TableComponent, TableSettings, TableUnitSetting, TableManagerConfig, ITable, DataTableMode, LoadAsync, LoadSync, GetTable, HasTable, Luban, Excel, 数据表, 配置表, 表格, 数值导出]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-018-json-via-util-json|ADR-018]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-28-luban-load-release-symmetric|PAT-28]]"
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
---

# MOC-Table：数据表系统图谱

## 一句话

Table 模块负责“Luban 导出结果的运行时加载与查表”；业务公开入口是 `Nova.Table.LoadAsync / LoadSync / HasTable / GetTable`，不是把 `ITableManager` 内部方法名直接当外部 API。

## 业务语言入口

| 用户说 | 等同 |
|---|---|
| 加个数据表 / 表格 / 配置表 / Excel 表 | TableManager / TableSettings / Luban 代码生成 |
| 加载表格 / 读数据表 | `Nova.Table.LoadAsync()` / `LoadSync()` |
| 查数据 / 取一行 | `Nova.Table.GetTable<TbXxx>()` → 访问生成的 Luban 容器类 |
| 表格存在吗 | `Nova.Table.HasTable<TbXxx>()` |
| 表格数量 | `Nova.Table.Count` |
| Excel 文件 / 数据源路径 | `TableSettings.SourceDirPath`（仅编辑器）/ `TableUnitSetting.SourcePath` |
| 导出路径 / 数据导出位置 | `TableUnitSetting.DatasExportPath` / `ClassesExportPath` |
| Asset 地址 / 资源路径 | `TableUnitSetting.AssetLocation`（Asset 地址，非"资源地址"） |
| 数值导出 / 代码生成 / Excel→JSON | `EditorUtil.Excel` / `EditorUtil.Table` / `EditorUtil.Luban` |
| 列表模式 / 映射模式 / 单例模式 | `DataTableMode.List / Map / Singleton` |

## 三层继承链（铁律）

```
FrameworkManager
  └── TableManagerBase : ITableManager    (internal abstract)
        └── TableManager                  (internal sealed partial)
```

- `TableComponent` — Unity MonoBehaviour 入口，`Nova.Table` 全局访问
- `TableSettings` — Inspector 序列化的表格设置（`[Serializable]`，Component 字段）
- `TableUnitSetting` — 单个数据源的单元设置（Asset 地址 + 导出路径 + 类型名列表）
- `TableManagerConfig` — 初始化配置 DTO（纯 DTO，无序列化）
- `ITable` — 定义在 `Runtime/Core/Table/ITable.cs` 的框架接口，业务 `TbXxx` 容器类实现它

## 文件入口结构

```text
Runtime/Core/Table/
├── ITable.cs / ILubanTables.cs
├── DataReceiver.cs / LubanDataReceiver.cs
└── LubanTablesLoader.cs

Modules/Table/
├── TableComponent.cs / .Visitors.cs
└── Managers/
    ├── Definitions/
    │   ├── TableManagerConfig.cs         ← 初始化配置 DTO（含 List<TableUnitSetting>）
    │   └── TableSettings.cs              ← TableSettings + TableUnitSetting（[Serializable]）
    ├── Implements/
    │   ├── TableManager.cs / .Methods.cs / .Visitors.cs
    │   └── TableManagerBase.cs
    └── Interfaces/
        └── ITableManager.cs
```

编辑器导出入口：

```text
Editor/EditorUtil/
├── EditorUtil.Excel/
├── EditorUtil.Table/
└── EditorUtil.Luban/
```

## 关联 ADR

| ADR | 标题 | 一句要点 | status |
|---|---|---|---|
| [[ADR-001-component-manager-three-layer\|ADR-001]] | Component + Manager 三层继承链 | Table 模块遵守三层结构 | accepted |
| [[ADR-008-managerbase-internal-abstract\|ADR-008]] | ManagerBase internal abstract | TableManagerBase 修饰符约束 | accepted |
| [[ADR-010-validation-on-consumer-side\|ADR-010]] | 谁使用谁校验 | Component 不校验，Manager 内校验 | accepted |
| [[ADR-016-framework-vs-business-access\|ADR-016]] | 框架 vs 业务访问 | 业务侧通过 `Nova.Table` 访问 | accepted |
| [[ADR-018-json-via-util-json\|ADR-018]] | JSON 统一走 Util.Json | 表格 JSON 加载必须走 Util.Json，禁直调 JsonConvert / JsonUtility | accepted |
| [[ADR-042-assetmanager-load-api-all-return-handle\|ADR-042]] | AssetManager Load API 统一返回 handle | 表格数据加载与释放应沿当前 handle 语义理解，不回退到旧裸资源心智 | accepted |

## 关联 PAT

| PAT | 一句要点 |
|---|---|
| [[PAT-27-config-no-serialize\|PAT-27]] | `TableManagerConfig` 纯 DTO 禁 [Serializable]；`TableSettings` 是 Component 侧序列化类（允许 [Serializable]），两者职责不同 |
| [[PAT-28-luban-load-release-symmetric\|PAT-28]] | 表格数据读取要保持“加载后可追踪释放”的对称心智，但具体释放实现以当前 `DataReceiver` 调用链为准 |
| [[PAT-30-framework-usage-redlines\|PAT-30]] | `TbXxx` 容器必须实现 `ITable`；Excel 导出前先调 `DeleteExportFiles` 清理；JSON 禁直调 JsonConvert |

## 导航提醒

- 业务公开入口是 `TableComponent` / `Nova.Table`，不是把 `ITableManager` 内部方法名直接外露成 API。
- `ITable` 由框架层定义，业务层负责实现，不要反写成“接口定义在业务 DLL”。
- 具体加载链和释放链以 `Docs` 与源码为准；这页只保留长期入口与边界。

## 常见误区

- 直接把 `LoadTablesAsync / LoadTablesSync` 写成业务公开 API
- 把 `ITable` 的定义位置写成业务层
- 把 `TableSettings` 和 `TableManagerConfig` 当成同一种配置对象
- 忽略 `Runtime/Core/Table` 这层公共抽象，只盯 `Modules/Table`
