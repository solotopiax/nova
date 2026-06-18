---
id: PAT-31
title: 新增 Inspector SOP（继承结构 + 文件命名 + 4 步骤）
type: pattern
status: active
date: 2026-05-18
summary: Inspector三文件SOP声明绑定绘制分离
category: inspector
aliases:
  - inspector-sop
keywords: [PAT-31, inspector-sop, 新增 Inspector SOP（继承结构 + 文件命名 + 4 步骤）]
tags:
  - nova
  - framework
  - sop
  - editor
  - inspector
related:
  - "[[PAT-09-inspector-config-i18n]]"
  - "[[PAT-18-editor-window-vs-util-split]]"
  - "[[PAT-20-editor-panel-title-indent]]"
  - "[[PAT-21-inspector-helpbox-multiline]]"
  - "[[PAT-24-inspector-row-vertical-alignment]]"
---

# PAT-31：新增 Inspector SOP（继承结构 + 文件命名 + 4 步骤）

## 适用场景

- 在 `Assets/Framework/Scripts/Editor/Inspectors/` 下新增或改造 Component Inspector。
- 评审 Inspector PR 是否符合 Nova Editor 规范。

## 核心做法

### 一、继承结构

```text
UnityEditor.Editor
  └── BaseComponentInspector (abstract, NovaFramework.Editor)
        └── XxxComponentInspector (sealed partial)
              └── XxxComponentInspector.XxxRuntimeDrawer : IEditorRuntimeDrawer
```

- `BaseComponentInspector` 是所有 Inspector 的统一基类，负责编译态拦截、`serializedObject.Update/Apply`、调用 RuntimeDrawer 列表
- `IEditorRuntimeDrawer` 是运行时面板单一抽象，仅在 `Application.isPlaying` 时绘制运行时数据
- 业务 Inspector 通过 `m_RuntimeDrawers.Add(new XxxRuntimeDrawer())` 注册，**不**重写 `OnInspectorGUI` 之外的生命周期

### 二、文件命名惯例

| 后缀 | 用途 |
|---|---|
| `XxxComponentInspector.cs` | 主文件（类声明 + `OnEnable` + `OnInspectorGUI`） |
| `XxxComponentInspector.Visitors.cs` | `SerializedProperty` 字段 |
| `XxxComponentInspector.Methods.cs` | 私有辅助方法 |
| `XxxComponentInspector.XxxRuntimeDrawer.cs` | 单个 RuntimeDrawer 内嵌类一文件 |
| `EditorUtil.Yyy.cs` | EditorUtil 子工具分部类 |
| `IEditorXxx.cs` | Editor 层接口（放 `Definitions/`） |

### 三、新增 Inspector 4 步骤

```text
1. 在 Editor/Inspectors/XxxComponentInspector/ 下建目录

2. 创建 XxxComponentInspector.cs：
   [CustomEditor(typeof(XxxComponent))]
   internal sealed partial class XxxComponentInspector : BaseComponentInspector
   {
       protected override void OnEnable()
       {
           base.OnEnable();
           // 绑定 SerializedProperty
           // m_RuntimeDrawers.Add(new XxxRuntimeDrawer());
       }
       public override void OnInspectorGUI()
       {
           base.OnInspectorGUI();
           // EditorUtil.Draw.* 绘制配置字段
       }
   }

3. 若需运行时面板：创建 XxxComponentInspector.XxxRuntimeDrawer.cs，按 `Application.isPlaying` 再读运行时字段

4. 创建对应的类映射文档，跟代码版本走，不入长期知识层
```

### 四、绘制铁律（与已有 PAT 协同）

- **GUI 绘制全走 `EditorUtil.Draw.*`**，禁直接 `EditorGUILayout.*` / `GUILayout.*`
- **配置分组 + Foldout/Space/HelpBox 左缘对齐**（详 [[PAT-09-inspector-config-i18n]]）
- **HelpBox 多条说明必须分行 (1)(2)(3)**（详 [[PAT-21-inspector-helpbox-multiline]]）
- **一级条目编辑区 GUILayout.Width(180f) 上下对齐**（详 [[PAT-24-inspector-row-vertical-alignment]]）
- **配置详情页统一标题 + 16f 缩进**（详 [[PAT-20-editor-panel-title-indent]]）
- **EditorWindow 与 EditorUtil 职责分离**（详 [[PAT-18-editor-window-vs-util-split]]）

## 反模式

- ❌ Inspector 子类重写 `OnEnable` 不调 `base.OnEnable()`——基类的编译态字段初始化丢失
- ❌ `OnInspectorGUI` 内 `serializedObject.FindProperty` 动态查——基类已 `Update`，每帧重查浪费且违背 partial `Visitors.cs` 拆分初衷
- ❌ RuntimeDrawer 不写 `Application.isPlaying` 判定，在编辑态访问运行时字段——崩 / 报 null
- ❌ `EditorGUILayout.PropertyField(prop)` 直接绘制——绕过 `EditorUtil.Draw`，全局对齐/多语言/Width 规则全废
- ❌ Inspector 直接 `System.IO.File.WriteAllText`——绕过 `EditorUtil.FileSystem` 与 AssetDatabase 同步
