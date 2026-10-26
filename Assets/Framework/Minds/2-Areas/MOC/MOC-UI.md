---
id: MOC-UI
title: UI 模块图谱
summary: UI分组管理与公开入口速查
category: runtime
status: active
date: 2026-06-05
aliases:
  - MOC-UI
  - UI 模块图谱
  - UIView 图谱
tags: [moc, nova, ui, view, runtime]
keywords: [UI, UIView, UIManager, UIGroup, UIComponent, UISettings, IUIView, IUIManager, OpenUIViewSync, OpenUIViewAsync, CloseUIView, CloseUIViews, 视图, 界面, 弹窗, 面板]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[ADR-021-inspector-runtime-drawer-two-layer|ADR-021]]"
---

# MOC-UI：UI 模块图谱

## 一句话

UI 模块负责“视图注册表加载 + UIView 生命周期 + UIGroup 分层 + 视图实例池”，公开入口是 `Nova.UI`，不是旧的 `OpenView / CloseView`。

## 何时查这页

- 要改 `UIView` 生命周期
- 要改打开、关闭、批量关闭 UI 的公开入口
- 要分清 `UIComponent / UIManager / UIView / UIGroup`
- 要确认 UI 注册表、对象池和分组深度在哪一层

## 当前结构

```text
Nova.UI
  -> UIComponent
  -> IUIManager
  -> UIManagerBase
  -> UIManager
  -> UIView
  -> IUIGroup / IUIGroupHelper / UIGroupHelper
```

关键类型：

- `UIComponent`：Unity 入口，负责初始化、注册默认分组、桥接公开 API
- `UIManager`：注册表加载、开关视图、分组管理、实例池管理
- `UIView`：业务视图基类，封装 `OnInit / OnOpen / OnClose / OnPause / OnResume`
- `UISettings / UIUnitSetting`：UI 注册表导出设置，走 `IDataTableSettings` 统一导出链
- `IUIGroup / UIGroupHelper`：分组层级与挂载辅助

## 业务语言入口

| 常见说法 | 在 Nova 中对应什么 |
|---|---|
| 加载 UI 注册表 | `Nova.UI.LoadAsync()` / `LoadSync()` |
| 打开一个视图 | `OpenUIViewSync*` / `OpenUIViewAsync*` |
| 关闭一个视图 | `CloseUIView(...)` |
| 批量关闭视图 | `CloseUIViews(...)` |
| 新增或查询层级组 | `AddUIGroup` / `HasUIGroup` / `GetUIGroup` |
| 业务 UI 基类 | `UIView` |
| 按表内名称打开 | `OpenUIViewSync / OpenUIViewAsync` 的字符串重载，名称取 `Name` 列 |
| 按资源地址打开 | `OpenUIViewByAssetSync / OpenUIViewByAssetAsync`，地址取 `AssetLocation` 列 |
| 视图对象池开关 | `inObjectPools` |
| 被遮挡时是否暂停 | `pauseCoveredUIView` |

## 当前事实边界

- 业务公开面是 `OpenUIViewSync* / OpenUIViewAsync* / CloseUIView* / CloseUIViews*`
- `LoadAsync / LoadSync` 先加载并解析 UI 视图注册表，再允许按泛型或资源地址打开视图
- 普通打开方法的字符串首参表示表内 Name，ByAsset 后缀表示首参为 AssetLocation；入口不自动猜测另一种身份，第二参数始终是业务数据。
- 打开策略的唯一来源是已加载的 UI 表：类型、名称与地址只负责选中配置行，分组、覆盖暂停与池化全部使用该行配置；调用参数与硬编码默认值不能覆盖它。
- 资源地址可能由多个配置共用，地址入口必须明确匹配唯一行；缺配置或有歧义时拒绝打开，业务数据 `userData` 仍由调用方传递。
- `UIComponent` 在 `Start()` 中初始化 `UIManagerConfig`、补默认 `UIGroup`、创建实例池
- `UISettings` 不是普通业务配置页，而是 UI 注册表导出链的一部分
- `UIView.Close()` 内部直接转到 `Nova.UI.CloseUIView(this)`
- `UIView` 是统一后缀；不要再把模块公开口径写成 `Panel / Window / Dialog`

## 文件入口

```text
Modules/UI/
├── UIComponent.cs / .Visitors.cs / .UIGroup.cs
├── Definitions/
│   ├── IUIView.cs
│   └── UIView.cs / .Methods.cs / .Visitors.cs
└── Managers/
    ├── UIManager/
    │   ├── Definitions/ UIManagerConfig.cs / UISettings.cs / IUIViewRow.cs
    │   ├── Implements/  UIManager.cs / UIManagerBase.cs / UIManager.UIGroup.cs
    │   └── Interfaces/  IUIManager.cs
    └── UIGroupHelper/
        ├── Definitions/ IUIGroup.cs
        ├── Implements/  UIGroupHelper.cs / UIGroupHelperBase.cs
        └── Interfaces/  IUIGroupHelper.cs
```

## 常见误区

- 把公开入口写成 `OpenView / CloseView`
- 把 `UIComponent` 当成业务逻辑主容器
- 把 UI 注册表导出设置和普通业务配置混成一类
- 将 UI 表的 `Name` 配置行名称误当作 Excel 文件名，或用资源地址猜测配置名称
- 通过旧版行为开关参数或写死默认值绕过表格配置
- 在没加载注册表时就默认泛型打开一定可用
- 把 `UIView` 内部生命周期和 Unity 原生生命周期混写成一套

## 先往哪看

配置来源与打开契约以当前 [UIComponent 文档](../../../Docs/Runtime/Modules/UI/UIComponent.md)、[IUIManager 文档](../../../Docs/Runtime/Modules/UI/UIManager/Interfaces/IUIManager.md) 和 `UIManager.Methods.cs` 中的配置行解析与打开链为准。

- 改模块边界：[[ADR-001-component-manager-three-layer]]
- 改访问方式：[[ADR-016-framework-vs-business-access]]
- 改 Component / Manager 职责：[[ADR-017-component-manager-isolation]]
- 改 Inspector 画法：[[MOC-Inspector]]

## 关联

- 图谱：[[MOC-Manager]]、[[MOC-Localization]]
