---
id: GLO-01
title: NovaBehaviour / IBaseLife / Host.Get<T>
type: glossary
status: archived
date: 2026-05-14
archived-date: 2026-05-23
superseded-by:
  - "[[ADR-032-drop-novabehaviour-bridge|ADR-032]]"
summary: NovaBehaviour+IBaseLife业务脚本契约
category: hotfix
tags: [glossary, nova, hybridclr, monobehaviour]
aliases:
  - NovaBehaviour
  - IBaseLife
  - Host
  - Host.Get
  - IEnableLife
  - IUpdateLife
  - NovaBehaviourAdapter
  - IBaseLifeAdapter
related:
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path]]"
  - "[[ADR-009-uimanager-no-addcomponent-fallback]]"
  - "[[GLO-03-component-procedure-manager]]"
---

# NovaBehaviour / IBaseLife / Host.Get<T>

## 一句话定义
HybridCLR 模式下业务脚本的唯一 AOT 承载组件与生命周期契约。

## 详细说明

HybridCLR + IL2CPP 模式下，Prefab 序列化的 `m_Script` 引用必须落在 AOT 程序集（`NovaFramework.Runtime`）中，业务 DLL（`Game.Runtime`）里的 MonoBehaviour 子类无法直接挂在 Prefab 上。Nova 用 `NovaBehaviour` 这一个 AOT 组件替代所有业务 MonoBehaviour：Prefab 只挂 `NovaBehaviour` + 配置 `m_ScriptName`（`Namespace.Type` 全类型名）+ 键值绑定，运行期由 NovaBehaviour 反射创建业务行为对象。

业务行为类不再继承 MonoBehaviour，而是强制实现 `IBaseLife`，包含 4 个无 DIM 的身份成员：`Host`（指回 NovaBehaviour）/ `OnAwake` / `OnStart` / `OnDestroyed`。需要其他 Unity Message 时按需附加 trait 接口扩展，由 NovaBehaviour 内的 `m_XxxLife` 字段缓存；未实现的 trait 缓存为 null，对应 Unity Message 仅付一次 null check 成本。

旧的 `IBaseLifeAdapter` / `ComponentBindings` / `NovaBehaviourAdapter` 已合并删除，禁止再引用。

## 关键 API / 文件

- 类型：`NovaFramework.Runtime.NovaBehaviour`（AOT 唯一业务承载 MB）
- 接口：`IBaseLife`（身份成员：`Host` / `OnAwake` / `OnStart` / `OnDestroyed`）
- Trait 接口：`IEnableLife` / `IUpdateLife` / `IFixedUpdateLife` / `ILateUpdateLife` / `ITrigger3DLife` / `ICollision3DLife` / `ITrigger2DLife` / `ICollision2DLife` / `IMouseLife` / `IApplicationLife` / `IVisibilityLife`
- 键值访问：`Host.Get<T>(key)` / `Host.GetInt(key)` / `Host.GetString(key)` 等
- 规则源：`.claude/rules/framework-hotupdate-constraints.md` §二

## 使用规则

- ✅ 业务脚本声明：`public sealed class HeroView : IBaseLife, IUpdateLife { ... }`
- ✅ Prefab 根节点：挂 `NovaBehaviour`，`m_ScriptName = "Game.Runtime.HeroView"`
- ✅ 访问绑定数据：`var icon = Host.Get<Image>("Icon");`
- ❌ 严禁 `public class HeroView : MonoBehaviour`（业务脚本不得继承 MB）
- ❌ 严禁在 Prefab 上预挂业务 DLL 层 MonoBehaviour 子类
- ❌ 严禁绕过 NovaBehaviour 直接 `transform.Find("Icon").GetComponent<Image>()` 访问绑定节点
- ❌ 严禁引用已删除的 `IBaseLifeAdapter` / `ComponentBindings` / `NovaBehaviourAdapter`

## 常见误解

- 误以为只要不挂 Prefab，业务类就能继承 MonoBehaviour：实际上**任何** `: MonoBehaviour` 形态在业务 DLL 都被零容忍禁掉，统一 `IBaseLife`。
- 误以为没附加 trait 的 Unity Message 完全不调用：实际上 NovaBehaviour 会调用，但通过 `m_XxxLife == null` 短路，开销仅一次 null check。
- 误以为 `Host.Get<T>` 是从 GetComponent 取：它访问的是 NovaBehaviour 序列化在 Inspector 里的键值绑定表，不是运行期 GetComponent。
- 误以为还有 `NovaBehaviourAdapter` / `IBaseLifeAdapter` 可用：已废弃删除，文档/搜索结果若提到要立刻替换。

## 关联

- ADR：[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour]]、[[ADR-005-hybridclr-namespace-single-write-path]]、[[ADR-009-uimanager-no-addcomponent-fallback]]
- Pattern：[[PAT-08-architecture-antipatterns]]
- 相关术语：[[GLO-03-component-procedure-manager]]、[[GLO-02-framework-manager-tiers]]
