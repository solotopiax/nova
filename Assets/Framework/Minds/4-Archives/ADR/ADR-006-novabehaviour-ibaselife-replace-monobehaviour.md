---
id: ADR-006
title: NovaBehaviour + IBaseLife 替代业务 MonoBehaviour
status: superseded
date: 2026-05-14
archived-date: 2026-05-23
summary: NovaBehaviour+IBaseLife替代业务MB
category: hotfix
aliases:
  - ADR-006
tags: [adr, nova, hybridclr, novabehaviour, ibaselife]
supersedes: []
superseded-by:
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
related:
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]"
  - "[[GLO-01-novabehaviour]]"
---

# ADR-006：NovaBehaviour + IBaseLife 替代业务 MonoBehaviour

## 背景（Context）

HybridCLR IL2CPP 模式下，Prefab 序列化的 `m_Script` 字段引用必须指向 AOT 层程序集（`NovaFramework.Runtime`）中的类型。如果业务 DLL 层的 `MonoBehaviour` 子类直接挂在 Prefab 上：

- IL2CPP 在 AOT 期不会保留业务 DLL 类型，Prefab 反序列化时 `m_Script` 引用失效。
- 即使通过反射加载业务 DLL 后，Prefab 上挂载的组件仍无法被识别为业务类型。

历史尝试（已废弃）：

- `IBaseLifeAdapter`：在 AOT 层做适配桥接，但增加了业务脚本对适配器接口的额外依赖，且没有解决 Prefab `m_Script` 引用问题。
- `ComponentBindings` + `NovaBehaviourAdapter`：双组件配合，序列化数据与生命周期分离，重复职责。
- 旧 UIManager 在 `CreateUIView` 时 `AddComponent(viewType)` 兜底——见 [[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]。

## 决策（Decision）

**业务 MonoBehaviour 子类全部退出 Prefab 序列化层；引入 AOT 层 `NovaBehaviour` 作为唯一业务承载组件，业务行为类强制实现 `IBaseLife` 接口体系。**

### 1. Prefab 承载约束

- Prefab `m_Script` **禁指**业务 DLL 类型，必须指向 AOT 层 `NovaBehaviour`
- Prefab 根节点挂 `NovaBehaviour` + 配置 `m_ScriptName`（全类型名 `Namespace.Type`）+ 键值绑定
- **禁止**在 Prefab 上预挂业务 DLL 层 MonoBehaviour 子类
- `ComponentBindings` / `NovaBehaviourAdapter` 已合并进 `NovaBehaviour`，禁止新增或修改

### 2. IBaseLife 强制实现

业务行为类**禁止任何 `: MonoBehaviour` 形态**，必须实现 `IBaseLife`：

| 成员 | 说明 |
|---|---|
| `Host` | 反向引用 `NovaBehaviour`（业务通过它访问绑定数据） |
| `OnAwake` | 替代 `Awake` |
| `OnStart` | 替代 `Start` |
| `OnDestroyed` | 替代 `OnDestroy` |

四个成员**无 DIM**（无默认接口实现），全部强制实现。

### 3. 按需附加 trait 接口

业务类按需附加扩展生命周期接口：

`IEnableLife` / `IUpdateLife` / `IFixedUpdateLife` / `ILateUpdateLife` / `ITrigger3DLife` / `ICollision3DLife` / `ITrigger2DLife` / `ICollision2DLife` / `IMouseLife` / `IApplicationLife` / `IVisibilityLife`

未附加的 trait 在 `NovaBehaviour` 内 `m_XxxLife` 缓存为 null，对应 Unity Message 仅付 null check 成本。

### 4. 键值访问唯一入口

业务脚本通过 `Host : NovaBehaviour` 访问 Prefab 绑定数据：

```csharp
Host.Get<T>(key)
Host.GetInt(key)
// ...
```

禁止绕过 `NovaBehaviour` 直接操作绑定节点。

### 5. 已废弃删除

- `IBaseLifeAdapter` —— 不得再引用
- `ComponentBindings` —— 已合并入 `NovaBehaviour`
- `NovaBehaviourAdapter` —— 已合并入 `NovaBehaviour`

## 后果（Consequences）

### 正面

- Prefab `m_Script` 全部指向 AOT 类型，IL2CPP 兼容性零问题。
- `NovaBehaviour` 作为唯一桥接组件，反射创建业务行为类一处搞定。
- trait 接口体系按需付费：未附加的接口零执行成本（仅 null check）。
- 业务作者只写 `IBaseLife` + 可选 trait，不再受 Unity Message 签名约束（如 `void Update()` 拼写错也能编译过的陷阱）。
- 编辑器审查可静态扫描 `: MonoBehaviour` 关键词作为违规检测点。

### 负面

- 业务作者需理解全新生命周期模型（`OnAwake/OnStart/OnDestroyed` 而非 `Awake/Start/OnDestroy`），过渡期成本。
- `IBaseLife` 强制实现 4 个成员（无 DIM），样板代码量增加。
- 不能直接使用 Unity 的 `[RequireComponent]` 等 attribute（Prefab 上没有业务类型可标）。
- 调试器堆栈中多一层 `NovaBehaviour` 桥接，跳转到业务方法路径变长。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 业务 MB 子类直接挂 Prefab | IL2CPP 模式下 Prefab `m_Script` 引用失效，无解 |
| `IBaseLifeAdapter` 适配器模式 | 业务类需依赖适配器接口，未解决 Prefab 序列化根因 |
| `ComponentBindings` + `NovaBehaviourAdapter` 双组件 | 职责分散，序列化与生命周期分别维护，复杂度高 |
| 抛弃 Prefab 全用代码生成 UI/对象 | 失去 Unity 编辑器可视化能力，DCC 工作流断裂 |
| Mirror 反射创建 + 无 NovaBehaviour 桥接 | 失去 trait 接口的零成本附加机制；Unity Message 路由难统一 |

## 验证依据（Verification）

- 规则文件：`.claude/rules/framework-hotupdate-constraints.md` §二、Prefab 与组件约束
- Grep 关键词（应零命中业务 DLL 层）：`: MonoBehaviour`、`IBaseLifeAdapter`、`ComponentBindings`、`NovaBehaviourAdapter`
- 业务类合规检查：业务 DLL 中所有行为类应 `: IBaseLife`
- Prefab 巡检：通过 UnityMCP 查询 Prefab `m_Script` 指向应在 AOT 层程序集

## 关联

- 规则文件：`.claude/rules/framework-hotupdate-constraints.md` §2
- 相关 ADR：[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]（程序集与命名空间）、[[ADR-007-procedure-tier-split|ADR-007]]（Procedure 分档）、[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]（UIManager 取消兜底）
- 相关 Glossary：[[GLO-01-novabehaviour]]
