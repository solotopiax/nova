---
name: nova-project-integrate-content-scene
description: Use when 项目组要把已资源化的业务 Content 场景接入现有 Nova 加载链，并需冻结 Package、地址、加载模式、调用点与卸载时机，验证 ISceneHandle 生命周期时使用。
---

# Nova 接入 Content 场景

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支按需读取下列随 Framework 发布的文档，不递归加载全部 Asset、Scene 或 Prefab 文档。

| 当前要确认的事实 | 读取 |
|---|---|
| Content 的角色、Nova 拓扑与场景所有者 | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| 该场景是否已是 Nova 托管业务资源、其 Package 与地址 | `Docs/Onboarding/RESOURCE_WORKFLOW.md` |
| Asset 启动前提、默认 Package 与 Handle 责任 | `Docs/Runtime/Modules/Asset/AssetComponent.md` |
| 场景 Handle 的唯一卸载语义 | `Docs/Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md` |
| 请求实际是普通资源或 Prefab，而非场景 | `Docs/Runtime/Modules/Prefab/PrefabComponent.md` |

本 Skill 只接入已经纳入当前 Nova 资源配置的 Content 场景及其业务调用链。普通资源走 `nova-project-integrate-resource`；Prefab 走既有 `Nova.Prefab.Instantiate* / Nova.Prefab.Destroy` 链；入口拓扑或 Build Settings 变更走 `nova-project-setup-entry-scene`。若项目明确采用 Unity 原生 `SceneManager` 流程而非 Nova 资源化场景，返回 `not_applicable`，不静默迁移。

## 冻结输入与阻断门

先冻结唯一项目根、目标 Content 场景及其业务归属、Collector 中已存在的 Package、该 Package 与当前运行时默认 Package 的映射、唯一 location、`LoadSceneMode`、加载调用点和唯一 Handle 所有者、卸载触发条件与精确时机、取消/失败处理、Platform/Channel/DevelopMode、Play 探针以及最小源码写入集。

- `Nova.Asset.LoadSceneAsync` 当前按运行时默认 Package 解析 location；目标 Package 若不是当前默认映射，不能通过改 Config、Collector 或资源设置来硬凑，返回 `blocked` 并交给独立资源/配置任务。
- `LoadSceneMode.Single` 与 `Additive` 必须由项目明确选择；不得因示例或相邻场景猜测。Single 的场景切换也不能代替所返回 `ISceneHandle` 的一次明确卸载责任。
- 场景加载成功返回后，业务所有者必须在整个使用期持有同一个 `ISceneHandle`，并在冻结的结束时机仅调用一次 `await UnloadAsync()`；完成后该 Handle 已失效，不得再访问。
- 不得用 `Object.Destroy`、`SceneManager.UnloadSceneAsync`、普通 `Release()`、直接丢弃 Handle 或重复 `UnloadAsync()` 替代该卸载链。取消或异常发生在 `LoadSceneAsync` 尚未返回 Handle 前时，由当前 Framework 的加载实现收束；一旦 Handle 已返回，责任在业务所有者。
- 场景未在现有 Collector / Package / 地址链中、加载者或卸载者不唯一、运行坐标不明确、或请求同时改入口 Scene、Build Settings、ConfigMaster/ConfigRuntime 时，返回 `blocked`，不扩大写入范围。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 Content 场景、Package 默认映射与 location | 工作区只读检查；当前 Framework 的 Asset 文档与项目既有 Collector / Config 事实 | 已证实的资源化场景解析前提 | 场景归属、Package、默认映射和 location 一致，未把普通资源或 Prefab 误作场景 |
| 已冻结的 mode、调用点、所有者与卸载时机 | `workspace-edit` | 业务加载者持有 `ISceneHandle` 的源码 | 只改冻结的业务调用点；Handle 不丢失，`UnloadAsync()` 恰好一次 |
| 已冻结的取消、失败和 Play 探针 | `Nova.Asset.LoadSceneAsync / ISceneHandle.UnloadAsync` | 可观察的 Content 加载与卸载链 | 加载完成、使用期、卸载完成与 Handle 失效均有证据 |
| 当前 Unity 状态 | Unity Editor 自动化通道 的脚本刷新、编译与 Play Mode 只读观察 | 最小运行验证记录 | 编译无本次新增错误；当前项目实际模式下完成一次精确 load / unload |

## 实施与验证边界

1. 先验证目标是 Content 场景而不是普通 Asset、Prefab、入口 Scene 或未资源化的 Unity 原生场景；确认它不承载第二个 Nova 根节点。不要为了本 Skill 新建 Collector、Package、地址、Config 或 Build Settings 条目。
2. 只编辑冻结的业务加载者、必要的既有业务调用点和局部 Play 探针。使用 `Nova.Asset.LoadSceneAsync(location, mode, ct)` 后将返回的 `ISceneHandle` 交给已确认所有者；不要在业务层直接依赖 YooAsset 场景 API。
3. 到冻结的结束条件时由同一所有者 `await handle.UnloadAsync()` 一次，并停止访问该引用。调用点更替、重复进入、取消、失败或 owner 销毁的竞态无法明确时先停止，不用多个兜底卸载点赌不会重复。
4. 请求 Unity 刷新并等待脚本编译完成；不以修改 Scene、Prefab、Collector、Config、缓存或 Bundle 来绕过错误。只有本次另外明确要求构建 Package 时，才由独立资源构建 Skill 处理。
5. 在获授权的 Unity Play Mode 从冻结调用点验证：目标 location 在冻结运行坐标下加载、预期 mode 生效、业务所有者持续持有有效 Handle、结束时一次 `UnloadAsync()` 完成且之后不再访问。静态源码或仅编译通过最高为 `partial`。

只有上述 Package / location 前提、唯一 owner、一次卸载、编译和 Play 证据都成立才返回 `success`；已写入但未取得 Play 证据为 `partial`；关键事实不唯一为 `blocked`；非 Nova 资源化场景、普通资源或 Prefab 为 `not_applicable`。

不默认修改 Collector、ConfigMaster、ConfigRuntime、入口 Scene、Build Settings、`Nova.prefab`、Bundle、Player、设备或外部发布。删除、迁移场景、改变 Package 默认映射、任何生成物构建、外部写入、凭据使用、Git commit / push 都需要独立且针对精确目标的确认。
