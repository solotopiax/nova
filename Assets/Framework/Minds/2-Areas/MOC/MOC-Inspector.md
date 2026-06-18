---
id: MOC-Inspector
title: Inspector / Editor 侧图谱
summary: Inspectors、RuntimeDrawer、EditorUtil.Draw 与配置持久化速查
category: inspector
status: active
date: 2026-06-05
aliases:
  - MOC-Inspector
  - Inspector 图谱
  - Editor 图谱
tags: [moc, nova, editor, inspector]
keywords: [Inspector, EditorUtil, Draw, EditorWindow, MenuItem, RuntimeDrawer, BaseComponentInspector, IEditorRuntimeDrawer, Library/Nova, 自定义检视, 编辑器, 工具窗口]
related:
  - "[[ADR-021-inspector-runtime-drawer-two-layer|ADR-021]]"
  - "[[ADR-014-playmode-split-editor-runtime|ADR-014]]"
  - "[[ADR-023-no-editor-prefs-in-framework|ADR-023]]"
  - "[[ADR-027-rule-ban-editor-refcount-batch-apis|ADR-027]]"
---

# MOC-Inspector：Editor 侧图谱

## 一句话

Nova 的 Inspector 体系是“Editor 依赖 Runtime 组件类型进行绘制，但 Runtime 不反向依赖 Editor”；常用积木是 `BaseComponentInspector + EditorUtil.Draw + RuntimeDrawer`。

## 业务语言入口

| 用户说 | 等同 |
|---|---|
| Inspector / 自定义检视 / 面板字段 | `EditorUtil.Draw` + Inspector 三文件 |
| EditorWindow / 工具窗口 / 菜单 | `MenuItem` + `EditorUtil` 封装 |
| Excel / 表格转代码 | `EditorUtil.Excel` + `EditorUtil.Table` + `EditorUtil.Luban` |
| Editor 配置持久化 | `Library/Nova/<模块>Config.json`（禁 `EditorPrefs`） |

## 当前结构

```text
Editor/Inspectors/
├── BaseComponentInspector.cs
├── XxxComponentInspector/
│   ├── XxxComponentInspector.cs
│   ├── XxxComponentInspector.Methods.cs
│   └── XxxComponentInspector.Visitors.cs
└── CustomInspectors/

Editor/Definitions/
├── IEditorRuntimeDrawer.cs
└── RuntimeDrawerBase.cs

Editor/EditorUtil/
├── EditorUtil.Draw/
├── EditorUtil.Excel/
├── EditorUtil.Table/
└── EditorUtil.Luban/
```

## 当前事实边界

- `BaseComponentInspector` 直接依赖 `NovaFramework.Runtime`，各 Inspector 通过 `[CustomEditor(typeof(XxxComponent))]` 绑定运行时组件
- `ADR-014` 的核心是运行时代码不依赖 Editor，不是“Editor 不依赖 Runtime”
- `RuntimeDrawer` 用来补充运行时信息区或复杂只读区，不等于整个 Inspector
- Editor 侧配置持久化走 `Library/Nova/*.json`，不要回退到 `EditorPrefs`
- 表格、配置、UI 等导出工具的现实入口已经是 `EditorUtil.*` 系列，不是旧的 `EditorExcelFileDataParser`

## 关联 ADR

| ADR | 标题 | 一句要点 | status |
|---|---|---|---|
| [[ADR-021-inspector-runtime-drawer-two-layer\|ADR-021]] | Inspector / RuntimeDrawer 两层 | 三文件结构 | accepted |
| [[ADR-014-playmode-split-editor-runtime\|ADR-014]] | Editor / Runtime 分离 | Runtime 不依赖 Editor，Editor 可面向 Runtime 绘制 | accepted |
| [[ADR-023-no-editor-prefs-in-framework\|ADR-023]] | NovaFramework.* 禁 EditorPrefs | 走 Library JSON | accepted |
| [[ADR-027-rule-ban-editor-refcount-batch-apis\|ADR-027]] | Editor 禁批量 RefCount API | 防止编辑器误操作 | accepted |
| [[ADR-026-pipify-runner-no-batch-locking\|ADR-026]] | Pipify Runner 禁批量锁 | 流水线约束 | accepted |
| [[ADR-018-json-via-util-json\|ADR-018]] | Editor 配置 JSON 走 Util.Json | 与 Runtime 一致 | accepted |

## 关联 PAT

| PAT | 一句要点 |
|---|---|
| [[PAT-09-inspector-config-i18n\|PAT-09]] | UI 文案 + 本地化规范 |
| [[PAT-10-imgui-popup-horizontal-wrap\|PAT-10]] | IMGUI 弹窗换行规则 |
| [[PAT-18-editor-window-vs-util-split\|PAT-18]] | EditorWindow vs EditorUtil 职责分离 |
| [[PAT-20-editor-panel-title-indent\|PAT-20]] | 面板标题缩进 |

## 常见误区

- 把 `ADR-014` 理解成“Editor 不依赖 Runtime”
- 把整套 Inspector 逻辑都塞进单个 `.cs`
- 把运行时只读区、诊断区和普通 Inspector 绘制混成一层
- 在框架 Editor 代码里写 `EditorPrefs`

## 关联

- 图谱：[[MOC-Config]]、[[MOC-Table]]、[[MOC-UI]]
