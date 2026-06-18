---
id: PAT-112
title: ManagerConfig 宿主 Inspector → 子结构透传链路
summary: Inspector 字段经 Config 透传到子结构
category: arch
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-112-managerconfig-host-to-leaf-passthrough
keywords:
  - PAT-112
  - ManagerConfig 宿主 Inspector → 子结构透传链路
  - PAT-112-managerconfig-host-to-leaf-passthrough
tags: [pattern, arch, runtime, config, inspector]
related:
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-32-runtime-module-sop|PAT-32]]"
  - "[[PAT-40-field-insertion-by-semantic-grouping|PAT-40]]"
  - "[[ADR-041-ui-depth-factor-to-inspector|ADR-041]]"
---

# PAT-112：ManagerConfig 宿主 Inspector → 子结构透传链路

## 适用场景（When）

需要把"项目自决"的运行时参数从 `XxxComponent` Inspector 透传到 Manager 内部嵌套结构（UIGroup / Channel / Pool 等）或 Helper 时使用。常见触发点：

- 数值原本是静态常量 / 硬编码，需要外部化（如 `UIDepthConfig.c_GroupDepthFactor` 改字段）
- Manager 内部嵌套类（`UIManager.UIGroup`）需要拿到这些值，但又不该自己持有 SerializedField
- Helper（如 `UIGroupHelper`）需要这些值参与计算

## 核心做法（What & How）

**五段透传链**（以 UIComponent 深度因子为例）：

```
[1] UIComponent.Visitors.cs
    [SerializeField] private int m_GroupDepthFactor = 1000;
    [SerializeField] private int m_ViewDepthFactor  = 10;

[2] UIComponentInspector 三件套
    cs / .Visitors.cs / .Methods.cs 联动绘制；
    与上一组字段间用 EditorUtil.Draw.Line() 分组分隔

[3] UIComponent.Start()
    var config = new UIManagerConfig
    {
        InstanceRoot     = m_InstanceRoot,
        GroupDepthFactor = m_GroupDepthFactor,
        ViewDepthFactor  = m_ViewDepthFactor,
        ...
    };
    m_UIManager.Initialize(config);

[4] UIManager.Initialize(config)
    m_GroupDepthFactor = config.GroupDepthFactor;
    m_ViewDepthFactor  = config.ViewDepthFactor;

[5] UIManager 内部使用
    - 创建 UIGroup 时把两值传入嵌套类构造 / 字段
    - IUIGroup 暴露只读属性 GroupDepthFactor / ViewDepthFactor
    - UIView.OnDepthChanged 通过 m_UIGroup.GroupDepthFactor 现取
    - 创建 UIGroupHelper 时调 helper.SetDepthFactor(GroupDepthFactor) 注入
```

**关键约束：**

| 层 | 职责 | 禁忌 |
|---|---|---|
| Component | 持 `[SerializeField]` 字段；构造 Config DTO | 不直接把字段塞进 Manager 内部静态变量 |
| ManagerConfig | 纯 DTO，仅用于一次性传参 | **禁加 `[Serializable]`**（[[PAT-27-config-no-serialize|PAT-27]]）；不被 Inspector 序列化 |
| Manager | Initialize 一次性吸收，存入私有字段 | 不直接 `FindObjectOfType<UIComponent>()` 反查 |
| Manager 嵌套类 / Helper | 通过构造参数 / Setter 注入 | 不持有对 UIComponent 的引用 |
| 外部访问 | 走接口（IUIGroup / IUIGroupHelper）暴露的只读属性 | 不暴露具体类，不暴露 Manager 实例字段 |

## 为什么这么做（Why）

- **数值外部化原则**：`framework-component-manager-architecture.md` 明文要求时长 / 路径 / 容量等参数走 Inspector 字段
- **ManagerConfig 不能 `[Serializable]`**：Inspector 上不能展开 Config 整体绘制（[[PAT-27-config-no-serialize|PAT-27]]）；字段必须散在 Component 上独立绘制
- **嵌套类 / Helper 不持 SerializedField**：Manager 内部结构是纯 C# 类，不挂 Mono；序列化字段不能嵌入；只能由 Component 注入
- **接口边界**：跨 asmdef 或跨模块调用方应通过 `IUIGroup` / `IUIGroupHelper` 接口拿值，不该耦合到具体实现类

## 反模式（Anti-patterns）

| 反模式 | 正确做法 |
|---|---|
| `[Serializable] class UIManagerConfig` 整体作为 Inspector 字段 | 散为多个独立 `[SerializeField]` 字段，构造 Config 仅在 Start 一次性发生 |
| 静态常量类（`UIDepthConfig.c_xxx`）当配置接口 | 字段散到 Component Inspector；保留默认值即可（[[ADR-041-ui-depth-factor-to-inspector|ADR-041]]） |
| Manager 嵌套类构造时反查 `FrameworkComponentsGroup.Get<UIComponent>()` | 由 Manager 在创建嵌套类时显式注入参数 |
| Helper 直接读静态常量计算 sortingOrder | 由 Manager 调 `helper.SetDepthFactor(value)` 注入；Helper 只持自己的 `m_DepthFactor` |
| 外部模块（UIView）`m_UIManager.Field` 直读 Manager 私有字段 | 走 `m_UIGroup.GroupDepthFactor` 接口属性 |

## 跨项目复用提示

可直接搬到任意 Component + Manager 工程；只需对齐：

1. Component / Manager 三层继承链规范（`framework-component-manager-architecture.md`）
2. ManagerConfig 是纯 DTO 的约定（[[PAT-27-config-no-serialize|PAT-27]]）
3. Inspector 三件套联动写法（[[PAT-09-inspector-config-i18n|PAT-09]] / [[PAT-40-field-insertion-by-semantic-grouping|PAT-40]]）

