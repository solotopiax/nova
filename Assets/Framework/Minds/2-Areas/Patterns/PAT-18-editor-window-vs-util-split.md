---
id: PAT-18
title: EditorWindow 与 EditorUtil 职责分离
type: pattern
status: active
date: 2026-05-16
summary: EditorWindow与EditorUtil分层职责拆分
category: editor
aliases:
  - PAT-18-editor-window-vs-util-split
keywords:
  - PAT-18
  - PAT-18-editor-window-vs-util-split
  - EditorWindow 与 EditorUtil 职责分离
tags:
  - pattern
  - editor
  - unity
  - architecture
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
---

# PAT-18：EditorWindow 与 EditorUtil 职责分离

## 适用场景（When）

- Unity 项目内编写多个 EditorWindow（配置面板 / 数据导入工具 / 资源管理工具）
- EditorWindow 需要执行非 UI 操作：文件读写 / AssetDatabase 增删 / 反射扫描 / 数据校验
- 多个 Window 之间存在相同的核心逻辑（如类型扫描、JSON 序列化）有复用价值
- 团队希望 Window 代码可独立替换为 UI Toolkit / IMGUI 等不同呈现方案而不动核心逻辑

## 核心做法（What & How）

### 一刀切的边界

| 归属 | 职责 |
|---|---|
| **EditorWindow**（`Assets/Framework/Scripts/Editor/Windows/`） | 仅 UI 交互：绘制、按钮、状态切换、焦点管理、弹框调用 |
| **EditorUtil**（`Assets/Framework/Scripts/Editor/EditorUtil/`） | 文件读写、反射扫描、数据校验、导入导出、数据转换、ScriptableObject 创建、Asset 查询、类型发现 |

### 命名规约

EditorUtil 子模块：`EditorUtil.{FeatureName}`

- `EditorUtil.Config` — Config 数据导入导出
- `EditorUtil.Luban` — Luban 表格生成
- `EditorUtil.ABMarkTool` — AssetBundle 打标
- `EditorUtil.Draw` — IMGUI 绘制原语（按钮 / 标题 / 缩进 / HelpBox）

### 拆分流程

1. **写 Window 前先列出非 UI 操作清单**，每条对应一个 EditorUtil API
2. Window 文件**只**出现：`EditorWindow` 子类、`OnGUI` / `OnEnable` / `OnDisable` / 字段、状态机
3. 看到 Window 内出现 `Assembly.GetTypes` / `AssetDatabase.CreateAsset` / `Directory.CreateDirectory` / `JsonUtility.*` / `File.WriteAllText` 等 → 立即拆出
4. 写 Spec 时在「架构分层」章节显式标注哪些逻辑归 EditorUtil、哪些归 Window

### 复用原则

- 跨窗口可复用的逻辑永远不能写在 Window 里（否则第二个 Window 来时只能复制粘贴）
- EditorUtil API 设计成无状态静态方法或可注入的小服务
- Window 持有的状态（当前选中、临时数据、UI flag）不下沉，仍归 Window

## 为什么这么做（Why）

- **可测试性**：EditorUtil 可独立单测，Window UI 难以单测；分离后核心逻辑零 UI 耦合
- **可替换性**：未来从 IMGUI 迁到 UI Toolkit 时只换 Window 层，EditorUtil 零改动
- **跨窗口复用**：第二个用相同核心逻辑的 Window 来时直接调 EditorUtil，无需复制粘贴
- **诊断友好**：Window 出 bug 容易定位是「UI 层」还是「核心逻辑层」
- **历史踩坑**：未做此拆分时，Window 一改就炸三个，原因是核心逻辑藏在 OnGUI 内部分支里

## 反模式（Anti-patterns）

- **Window 直接 AssetDatabase 操作**：`AssetDatabase.CreateAsset(so, path)` 写在 OnGUI 按钮回调里
- **反射扫描埋在 Window 字段初始化**：`m_Types = Assembly...GetTypes().Where(...)` 直接在 Window 类成员上
- **同一段 IO 逻辑复制三遍**：三个 Window 都写一份 `File.WriteAllText(path, json)`，同一逻辑变成三个真理源
- **EditorUtil 反过来引用 Window**：核心层依赖 UI 层，分层倒挂
- **小工具 Window「太简单了不分」**：第一版确实简单；第二个需求来时核心逻辑已和 UI 缠绕无法剥离
- **EditorUtil 内塞 IMGUI 调用**：除 `EditorUtil.Draw` 这种 UI 原语模块外，EditorUtil 不应出现 `EditorGUILayout.*`

## 跨项目复用提示

- **思想完全可迁移**：任何"工具 + UI"双轨项目都适用，桌面/Web/CLI 工具都能套用 MVC、MVP、MVVM 同源理念
- 关键是定边界：UI 层 / 核心层 / 数据层；Nova 用的是「UI 层 vs 核心层」二分
- 项目体量大时核心层可再分：原子操作（无状态工具函数） vs 高级编排（有状态服务）
- 不适合的项目：一次性脚本工具、原型阶段未稳定的 prototype（边界尚未浮现）

## 关联

- 相关 Pattern：[[PAT-09-inspector-config-i18n|PAT-09]]（Inspector 配置分组与对齐规范）
- 历史源头：2026-04-28 ConfigWindow 设计会话用户原话："window 永远只是 ui 交互相关，具体核心功能都是要走 EditorUtil 的"
