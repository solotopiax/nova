---
id: GLO-17
title: Unity Editor 的 Inspector 与 EditorWindow
summary: Inspector 编辑对象契约，Window 承载独立工具流程
category: editor
status: active
date: 2026-08-06
aliases:
  - GLO-17-unity-editor-inspector-window
  - Inspector
  - EditorWindow
keywords:
  - GLO-17
  - Inspector
  - EditorWindow
  - SerializedProperty
  - EditorUtil.Draw
tags: [glossary, nova, unity, editor, inspector]
related:
  - "[[ADR-047-editor-active-master-anchor|ADR-047]]"
---

# GLO-17：Unity Editor 的 Inspector 与 EditorWindow

## 定义

- **Inspector**：围绕当前选中对象或组件绘制、校验和持久化序列化字段的编辑界面。在 Nova 中属于配置与序列化契约面。
- **EditorWindow**：拥有独立生命周期和业务入口的编辑器窗口，用于跨对象工具流程、批处理、配置工作区或诊断界面。

## Nova 边界

- Inspector 新增字段必须保持 `[SerializeField]` 声明、`SerializedProperty` 绑定和绘制顺序一致，并检查旧 Prefab、场景和配置资产兼容性。
- Inspector 业务绘制优先复用 `EditorUtil.Draw`，不在单个 Inspector 内重复封装 IMGUI 控件。
- EditorWindow 保持 UI 壳层与业务能力分离；部署、导出、校验等能力应由独立工具类执行。
- 多维配置窗口以当前激活坐标和 WorkingCopy 为编辑上下文，不能让窗口切换产生隐式落盘或跨维度污染。
- Runtime 不得依赖 Editor 类型或程序集。

## 易混淆项

- Inspector 是对象编辑面，不适合承载跨项目批处理引擎。
- EditorWindow 可以编辑多个对象，但不能因此绕过 SerializedObject、Undo、延迟保存或现有配置快照协议。
- HelpBox、Foldout 和按钮的视觉调整不改变序列化契约；新增或改名字段才会改变契约面。

## 示例

`AssetComponentInspector` 负责热更配置的字段绑定和显示；“白名单部署”属于跨文件上传流程，因此放在 ConfigWindow，并把真正上传能力下沉到 `EditorUtil.CDN`。

## 来源

- `Assets/Framework/Scripts/Editor/AGENTS.md` 与 `Inspectors/AGENTS.md`。
- [[ADR-047-editor-active-master-anchor|ADR-047]]：编辑器激活配置锚点。

---
