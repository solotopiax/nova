---
id: ADR-021
title: Inspector + RuntimeDrawer 双层架构
status: accepted
date: 2026-05-18
summary: Inspector与RuntimeDrawer双层解耦绘制
category: inspector
aliases:
  - inspector-runtime-drawer-two-layer
keywords: [ADR-021, Inspector + RuntimeDrawer 双层架构, inspector-runtime-drawer-two-layer]
tags:
  - adr
  - nova
  - framework
  - editor
  - inspector
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[PAT-31-inspector-sop]]"
  - "[[PAT-18-editor-window-vs-util-split]]"
---

# ADR-021：Inspector + RuntimeDrawer 双层架构

## 背景（Context）

Nova 每个 `XxxComponent` 都有一份 `XxxComponentInspector`，里面同时承担两类绘制：

1. **静态配置态**：`[SerializeField]` 字段、运行模式开关、引用资产配置
2. **动态运行时态**：`Application.isPlaying` 后展示的 Manager 内部状态、池化情况、加载进度等

如果把两者放同一个 `OnInspectorGUI` 里，会出现编辑态空引用、主文件膨胀、顺序无契约和复用困难。

## 决策（Decision）

**Inspector 与 RuntimeDrawer 分两层抽象**：

1. `BaseComponentInspector : UnityEditor.Editor`
   - 持有 `protected List<IEditorRuntimeDrawer> m_RuntimeDrawers`
   - `OnInspectorGUI` 模板方法：编译态拦截 → `serializedObject.Update` → 子类绘制配置字段 → 逐一调 `drawer.Draw(target)` → `Apply` → `Repaint`
2. `IEditorRuntimeDrawer`
   - 单方法 `void Draw(Object target)`
   - 实现内部必须 `if (!Application.isPlaying) return;` 守卫
   - 通过 `EditorUtil.Serializer.GetProperty<TTarget,TValue>` 反射读取 Component 私有字段，禁直接公开字段
3. 业务侧：`XxxComponentInspector` 只写配置态绘制；运行时态拆成多个 `YyyRuntimeDrawer`，在 `OnEnable` 中注册。

## 后果（Consequences）

### 正面
- 编辑态与运行时绘制解耦。
- RuntimeDrawer 顺序由注册顺序控制。
- Component 不需要为 Inspector 暴露 public 字段。
- partial 拆分后 diff 更干净。

### 负面
- 运行时面板要跨多文件查看。
- RuntimeDrawer 共享数据需要重新读 target。
- 私有字段重命名要同步更新字符串字面量。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| Inspector 单层，运行时绘制写在主 `OnInspectorGUI` | 文件膨胀，编辑态/运行时分支缠绕，新增一类面板必动主文件 |
| Inspector 持有 `Action<Object>` 列表代替接口 | 失去类型 + 命名空间约束，反射调试困难，无法用 Roslyn 静态分析批量检查 |
| 把运行时面板抽成独立 EditorWindow | 用户需主动打开窗口，操作链路变长；与 Component Inspector 上下文割裂 |
| Component 直接公开 public 字段供 Inspector 读 | 破坏 [[ADR-017-component-manager-isolation]]：业务代码会误用，封装失效 |

## 验证依据（Verification）

- 类继承关系：`BaseComponentInspector : UnityEditor.Editor` 与 `IEditorRuntimeDrawer` 两个类型存在且仅在 `NovaFramework.Editor` 程序集
- 静态审查：所有 `XxxComponentInspector : BaseComponentInspector`（`grep -rn "class.*ComponentInspector.*:" Assets/Framework/Scripts/Editor/`），没有直接 `: UnityEditor.Editor` 的业务 Inspector
- 运行时面板代码守卫：`grep -rn "Application\.isPlaying" Assets/Framework/Scripts/Editor/Inspectors/` 应在每个 RuntimeDrawer 实现内出现至少一次
- 字段读取：`grep -rn "EditorUtil\.Serializer\.GetProperty" Assets/Framework/Scripts/Editor/Inspectors/` 是 RuntimeDrawer 主流读法
