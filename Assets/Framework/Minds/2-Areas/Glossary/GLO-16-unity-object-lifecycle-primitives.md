---
id: GLO-16
title: Unity 对象与生命周期基元
summary: 区分场景对象、组件、层级节点与数据资产
category: runtime
status: active
date: 2026-08-06
aliases:
  - GLO-16-unity-object-lifecycle-primitives
  - MonoBehaviour
  - ScriptableObject
  - GameObject
  - Transform
keywords:
  - GLO-16
  - MonoBehaviour
  - ScriptableObject
  - GameObject
  - Transform
  - Unity生命周期
tags: [glossary, nova, unity, runtime, lifecycle]
related:
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
---

# GLO-16：Unity 对象与生命周期基元

## 定义

| 术语 | Nova 中采用的口径 |
|---|---|
| `GameObject` | 场景或 Prefab 中的对象容器；行为与数据由挂载的 Component 提供 |
| `Transform` | 每个 GameObject 必有的层级与空间节点，负责父子关系、位置、旋转和缩放 |
| `MonoBehaviour` | 挂载到 GameObject 的 Unity 生命周期组件，由引擎创建并驱动 `Awake/Start/Update/OnDestroy` 等回调 |
| `ScriptableObject` | 独立于场景对象的数据资产或编辑器配置载体，不具备 GameObject 挂载语义和 MonoBehaviour 生命周期 |

## Nova 边界

- Nova 的 `XxxComponent` 负责 Unity 生命周期接入，底层业务能力下沉到 Manager；不要把跨模块业务逻辑堆进 MonoBehaviour。
- 不直接 `new MonoBehaviour`，组件由 Unity 场景、Prefab 或 `AddComponent` 创建。
- 不使用 `GameObject.Find` / `FindObjectOfType` 解决框架依赖；使用显式组件引用、接口或既有静态门面。
- `ScriptableObject` 适合配置与编辑器真相源，但 Runtime 是否能读取取决于实际导出和加载链，不能假设编辑器资产在 Player 中天然可用。
- 修改 GameObject、Transform 或序列化组件结构时，应通过 Unity Editor 保存资产，不手写 Prefab / Scene YAML。

## 易混淆项

- GameObject 是容器，Transform 是其必有组件；两者不是可互换的对象类型。
- ScriptableObject 是可序列化 Unity 对象，但不是场景 Component，不会获得 `Start/Update`。
- Nova Component 的“Component”是框架职责命名，仍需遵守其具体继承类型和 Manager 分层，不等同于“所有逻辑都写 MonoBehaviour”。

## 示例

`AssetComponent` 在 Unity 生命周期中接入资源模块并持有 `IAssetManager`；资源下载、Manifest 和缓存策略由 AssetManager 实现，而不是由场景 GameObject 自行承担。

## 来源

- `.nova/RULES.md`：Manager / Component 分层、生命周期与显式依赖红线。
- [[PAT-30-framework-usage-redlines|PAT-30]]：框架使用红线汇总。

---
