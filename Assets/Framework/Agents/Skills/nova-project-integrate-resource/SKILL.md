---
name: nova-project-integrate-resource
description: Use when 项目组要把已确认归属的业务资源接入现有 Nova 包、Collector、地址、加载与释放链，并需要验证资源可被当前项目运行时消费时使用。
---

# Nova 接入业务资源

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，先确认资源归属、目标 Package、地址、消费方和生命周期。这个 Skill 只接入已确认的 Nova 业务资源；`Resources/BuiltIn/**`、第三方插件资源或已有非 Nova 资源方案不因本 Skill 自动迁移。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或扫描整个 `Assets`。

| 条件 | 读取 |
|---|---|
| 要判定资源归属或是否应走 Bundle | `Docs/Onboarding/RESOURCE_WORKFLOW.md` |
| 要配置当前项目的 YooAssetSettings 或 Collector 路径 | `Docs/Editor/Config/Definitions/YooAssetEditorConfigs.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md` |
| 要加载、持有或释放非场景资源 | `Docs/Runtime/Modules/Asset/AssetComponent.md` 与 `Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetHandle.md` |
| 资源是需实例化的 Prefab | `Docs/Runtime/Modules/Prefab/PrefabComponent.md` 与 `Docs/Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md` |
| 本次确需构建选定 Package | `Docs/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md` |

## 冻结输入

确认资源所有权、目标 Package、唯一地址、目标 Platform/Channel/Mode、消费方、持有期和释放/销毁责任。Collector 或 ConfigMaster 的实际路径不明确时先停止确认；不要用全工程搜索选一个副本，也不要改写现有 Addressables、StreamingAssets 或第三方插件方案。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已确认的业务资源、Package、Collector 和地址 | 当前项目已配置的 `BundleCollectorSetting`；通过 Unity Editor / MCP 修改其资产 | Collector 中的资源归属与地址 | Collector、Package 与地址的检查结果 |
| ConfigMaster 指向的 YooAsset 配置 | `EditorUtil.Config.YooAssetInjector.LoadBundleCollector` 定位既有 Collector | 已确认的配置链，不创建第二真源 | 当前 ConfigMaster 与 Collector 路径证据 |
| 已确认的资源消费方和生命周期 | `Nova.Asset.Load*` 返回 Handle；调用方在生命周期末 `Release()` | 持有并释放 Handle 的业务消费链 | 加载、使用和释放的局部代码/运行证据 |
| Prefab 资源 | `Nova.Prefab.Instantiate*` 与 `Nova.Prefab.Destroy` | 受统一回收链管理的实例 | 实例创建、销毁与诊断证据 |
| 确有本地构建需求的选定 Package | `EditorUtil.BundleBuilder.BuildAssetBundle(AssetBundleBuildArgs)` 或项目已有等价入口 | 选定 Package 的本地构建产物 | `BuildResult` 与目标资源可解析证据 |

## 实施与验证

1. 先分类资源；归属不明时返回 `blocked`，非 Nova 业务资源可返回 `not_applicable`，而不是强制迁移。
2. 仅通过 Unity Editor / MCP 更新 Collector、资源资产或序列化引用；禁止手写 YAML。业务代码不得在 Asset 模块外直接依赖 YooAsset 细节。
3. 让消费方保存 `Nova.Asset` 返回的 Handle 并在明确生命周期末释放；场景 Handle 只使用 `UnloadAsync()`。Prefab 实例使用 `Nova.Prefab` 的实例化/销毁链。
4. 只有本次验证确实需要时，才构建已确认的一个 Package；不默认清缓存、发布 CDN、跑全量 Bundle 或 Player Build。
5. 在获授权的 Unity 环境，以当前项目实际 PlayMode 做最小加载与释放验证；优先 EditorSimulateMode，项目选择其他模式时不替换其配置。

达到 `play` 证据才报告 `success`；未获得运行证据、构建未执行或包/地址仍不确定时返回 `partial` 或 `blocked`。删除资源、外部发布、凭据和 Git 操作均需要本 Skill 之外的精确确认。
