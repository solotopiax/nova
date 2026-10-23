---
name: nova-project-integrate-resource
description: 在项目组要接入 Nova 业务资源、对象池或引用池生命周期，配置资源加密与轮换，或按明确范围清理本地热更缓存并核验时使用。
---

# Nova 接入业务资源

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，先确认本次是资源接入、池生命周期、资源加密还是本地缓存清理。资源分支冻结资源归属、目标 Package、地址、消费方和生命周期；池与缓存只冻结本次分支的必要输入。`Resources/BuiltIn/**`、第三方插件资源或已有非 Nova 资源方案不因本 Skill 自动迁移。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或扫描整个 `Assets`。

| 条件 | 读取 |
|---|---|
| 要判定资源归属或是否应走 Bundle | `Docs/Onboarding/RESOURCE_WORKFLOW.md` |
| 要配置当前项目的 YooAssetSettings 或 Collector 路径 | `Docs/Editor/Config/Definitions/YooAssetEditorConfigs.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.md` |
| 要加载、持有或释放非场景资源 | `Docs/Runtime/Modules/Asset/AssetComponent.md` 与 `Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetHandle.md` |
| 资源是需实例化的 Prefab | `Docs/Runtime/Modules/Prefab/PrefabComponent.md` 与 `Docs/Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md` |
| 本次确需构建选定 Package | 路由到 `nova-project-build-bundles`，由该 Skill 按需读取构建文档 |
| 本次配置资源加密、客户端密钥源码或轮换 | [资源加密参考](references/resource-encryption.md)，再按需读取 `Docs/Runtime/Modules/Asset/ResourceEncryption.md` |
| 本次使用对象池/引用池、修资源异步持有，或清编辑器/运行时冗余缓存 | [生命周期与缓存参考](references/resource-lifecycle.md)，只读本次分支 |

## 冻结输入

资源接入时确认资源所有权、目标 Package、唯一地址、目标 Platform/Channel/Mode、消费方、持有期和释放/销毁责任。Collector 或 ConfigMaster 的实际路径不明确时先停止确认；不要用全工程搜索选一个副本，也不要改写现有 Addressables、StreamingAssets 或第三方插件方案。

池分支冻结具体类型、池名、对象名、Target 身份、取用模式、持有方与归还/销毁责任；纯数据引用池不要求虚构 Package 或资源地址。Editor 缓存分支冻结实际沙盒、第一层 `.version` 清单、DeviceID 基线和已退出 Play 的状态。运行时缓存分支冻结实际 Package、已激活清单/目标版本、业务调用入口与 Owner、现有句柄、目标 Player/设备和只清未被当前清单引用的 Bundle 范围；路径、配置和已有入口由 Agent 查明。执行删除仍需当前精确授权，已有同范围授权不重复询问。

资源加密分支另冻结私有 Master/坐标、Package/keyId、Bundle/Manifest 配套方案、源码目录与旧首包/缓存/回退。真实密钥属于项目秘密，配置和生成源码正文不回显；已有精确任务授权覆盖必要本地步骤，按实际 Action 使用本轮绑定计划，不逐步重问。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已确认的业务资源、Package、Collector 和地址 | 当前项目已配置的 `BundleCollectorSetting`；通过 Unity Editor / MCP 修改其资产 | Collector 中的资源归属与地址 | Collector、Package 与地址的检查结果 |
| ConfigMaster 当前坐标指向的 YooAsset 配置 | `nova.project.config.inspect-bundle-collector(masterGuid)` 定位既有 Collector | 已确认的配置链，不创建第二真源 | Master 当前坐标、解析路径和 Collector 资产定位证据；不替代成员与地址检查 |
| 已确认的资源消费方和生命周期 | `Nova.Asset.Load*` 返回 Handle；调用方在生命周期末 `Release()` | 持有并释放 Handle 的业务消费链 | 加载、使用和释放的局部代码/运行证据 |
| Prefab 资源 | `Nova.Prefab.Instantiate*` 与 `Nova.Prefab.Destroy` | 受统一回收链管理的实例 | 实例创建、销毁与诊断证据 |
| 确有本地构建需求的选定 Package | 停止本 Skill 扩张，路由 `nova-project-build-bundles` | 由 Bundle 构建 Skill 独立生成的本地产物 | 该 Skill 返回的 `BuildResult` 与产物证据 |
| 已确认的资源异步消费方或池生命周期 | 授权业务源码的 `workspace-edit`；编译消费既有 Handle、ObjectPool、ReferencePool API | 成对持有、取消、归还与销毁的业务源码 | 编译与实际运行的替换/取消/关闭/回收结果，见生命周期参考 |
| 已授权的本地热更缓存清理 | 已发现的 `Nova/Clean Hotfix Caches` 菜单 | 既有沙盒及第一层 `.version` 删除结果 | 非 Play 的真实前后文件状态与 DeviceID 保留证据 |
| 已授权的运行时冗余 Bundle 缓存清理 | 项目已有或获授权接入的业务入口，编译消费 `Nova.Asset.LoadManifestAsync(package, ct)` / 按需 `RefreshManifestAsync(package, ct)`，再 `ClearUnusedCacheAsync(package, ct)` | 指定 Package 未被当前清单引用的磁盘 Bundle 清理结果 | 接入改动的编译/Play 证据；请求手机清理时需目标设备前后状态、有效版本和资源可加载证据，详见生命周期参考 |

资源密钥新增/轮换先复用实际 `nova.project.config.inspect-panel` / `nova.project.config.patch-panels` 的 ResourceEncryption 分支，保存私有 Master 并 Verify；精确字段与旧 key 保留约束见加密参考。随后用 `nova.project.config.generate-resource-keys` 指定同一已保存 Master 和明确坐标，Plan 冻结真实 Player 程序集、源码/link 和旧工具产物迁移写集，Execute 单次生成并保存生成路径状态，实际编译重载后 Verify。无需激活 Master 或先创建 Pipify Batch；现有 ConfigWindow 顶部导出和已审 `export.config` Batch 仍可用。`ResourceKeyCodeGenerator.Generate/GenerateForCI/ValidateGenerated` 是底层 C# API。`nova.project.config.export-runtime` 当前不生成密钥源码；固定 `config.patch-coordinate` 和通用 settings 仍不能替代资源面板 Action，不写 YAML 或任意 C# 旁路。

## 实施与验证

1. 先分类资源；归属不明时返回 `blocked`，非 Nova 业务资源可返回 `not_applicable`，而不是强制迁移。
2. 仅通过 Unity Editor / MCP 更新 Collector、资源资产或序列化引用；禁止手写 YAML。业务代码不得在 Asset 模块外直接依赖 YooAsset 细节。
3. 让消费方保存 `Nova.Asset` 返回的 Handle 并在明确生命周期末释放；场景 Handle 只使用 `UnloadAsync()`。Prefab 实例使用 `Nova.Prefab` 的实例化/销毁链。
4. 本 Skill 不直接调用 `BundleBuilder`。需要本地 Bundle 时把已冻结的 Package、平台、版本、缓存与首包拷贝意图交给 `nova-project-build-bundles`；不得为绕开其计划、确认和证据门而直调 C# API。
5. 在获授权的 Unity 环境，以当前项目实际 PlayMode 做最小加载与释放验证；优先 EditorSimulateMode，项目选择其他模式时不替换其配置。

资源和池接入必须有编译与本次实际 `play` 证据才报告 `success`；只读分析或源码完成但未运行时返回 `partial`。Editor 缓存清理不进入 Play，只有精确范围的真实前后文件状态、结果提示和 DeviceID 保留都核验后才可报告 `success`。运行时清理必须从真实业务入口验证清单、Owner/句柄与缓存结果；请求手机结果时以指定设备实际清理和更新后资源加载为准，Editor 菜单或 Play 不能代替。内置实现正常返回只表示底层报告成功，实际磁盘删除仍须独立核验，缺结果证据保留 `partial`；清单未就绪、底层失败异常及取消边界见生命周期参考。它不清当前清单仍引用的 Bundle、用户存档、浏览器或 CDN 缓存。机器契约的 `static` 是各分支共同最低起点，不能代替上述分支验收；资源构建或加密部署仍按专用参考收齐产物与运行证据。包/地址或缓存范围不确定时返回 `blocked`。删除资源、外部发布、凭据和 Git 操作均需要精确授权；本轮已有授权继续有效。

加密分支逐层报告私有配置保存、唯一提供器及实际客户端程序集/编译、正式 Bundle、Player/浏览器加载与旧缓存兼容。只生成源码或编辑器 codec 测试不足以证明完整部署；轮换时保留旧 keyId，直到全部保留首包/缓存/回退不再使用旧密钥。最后清理本轮测试态与临时物，保留项目正式密钥源码和交付产物，明确清场结果。
