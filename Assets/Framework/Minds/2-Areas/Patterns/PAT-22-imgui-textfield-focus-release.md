---
id: PAT-22
title: IMGUI TextField 切数据源前必须先释放焦点
type: pattern
status: active
date: 2026-05-16
summary: IMGUI TextField编辑后需主动释放焦点
category: inspector
aliases:
  - PAT-22-imgui-textfield-focus-release
keywords:
  - IMGUI TextField 切数据源前必须先释放焦点
  - PAT-22
  - PAT-22-imgui-textfield-focus-release
tags:
  - pattern
  - methodology
  - editor
  - imgui
related:
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-10-imgui-popup-horizontal-wrap|PAT-10]]"
---

# PAT-22：IMGUI TextField 切数据源前必须先释放焦点

## 适用场景（When）

任何 EditorWindow / Inspector 自绘界面，**左侧列表 + 右侧详情**的双栏结构，或任意"选中索引切换 → 同一批 IMGUI 控件绑定到新数据源"的场景：
- 左侧列表单击 / 双击 / 右键选中
- 右侧 Tab / 分页切换
- 上下键切换选中行
- 任何把 `m_SelectedIndex` 改写后用同一段 `EditorGUILayout.TextField(...)` 渲染新数据的逻辑

只要**控件位置 ID 不变 + 数据源被换掉**，就在适用范围内。

## 核心做法（What & How）

1. **选中切换前一律 `GUI.FocusControl(null)`**
   - 调用顺序：先释放焦点，再写新选中索引，再让下一次 OnGUI 重绘
   ```csharp
   private void SelectIndex(int next)
   {
       if (next == m_SelectedIndex) return;          // 同项无需释放
       GUI.FocusControl(null);                       // 关键：释放 editing buffer
       m_SelectedIndex = next;
       Repaint();
   }
   ```

2. **封装成统一入口**
   - 所有"切换选中"动作必须经 `SelectXxx(int index)` 一道闸口；散落在按钮事件 / 键盘事件中各自赋值是反模式
   - 同理适用：`SelectBatch(int)` / `SelectItem(int)` / `SelectTab(int)`

3. **覆盖范围不止 `TextField`**
   - 凡是有 editing buffer 的 IMGUI 控件均会泄漏：`TextArea` / `DelayedTextField` / `IntField` / `FloatField` / `Vector*Field` / `ObjectField`（路径输入态）
   - 一律用同一释放策略

4. **同项切换不释放**
   - `next == m_SelectedIndex` 时不调用 `FocusControl(null)`，避免用户正在键入时被打断

## 为什么这么做（Why）

IMGUI 用"控件位置 ID（基于 GUILayout 调用顺序生成）"分配 editing buffer。当列表切换、右侧用同一批控件位置渲染新数据时，**位置 ID 没变 → editing buffer 被复用 → 旧数据未提交的本地编辑文本被继续渲染到新控件上**，视觉上就是"新选中项的某字段被污染成上一项的文本"。

这个坑在 Nova 工程内至少踩过两次，每次都被当成"显示 bug"花时间排查（实际是 IMGUI 焦点机制问题）。一道闸口比每个事件处理点都加一行 `FocusControl(null)` 更难漏。

## 反模式（Anti-patterns）

| 反模式 | 现象 |
|---|---|
| 切换选中只改索引不释放焦点 | 新选中项的描述/名称字段显示上一项的内容 |
| 在按钮事件 / 键盘事件里各自赋值 `m_SelectedIndex` | 漏覆盖一处即埋雷，以后排查难定位 |
| 只对 `TextField` 释放、忽略 `TextArea/DelayedTextField` | 多行描述、参数行编辑同样会污染 |
| 用 `Repaint()` 想"刷掉"buffer | `Repaint` 不清焦点，buffer 仍会复用 |

## 跨项目复用提示

- 适用于**任何 Unity Editor 工具**，不限于 Nova；只要项目用 IMGUI 自绘 EditorWindow / Inspector 就值得抄
- 不适用 UI Toolkit（UIElements）：UI Toolkit 的 `TextField` 是 VisualElement，绑定关系明确，无该问题
- 跨项目搬运时唯一需要适配的是"统一 Select 入口"的命名，规则本身无项目耦合

