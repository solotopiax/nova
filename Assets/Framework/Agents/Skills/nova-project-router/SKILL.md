---
name: nova-project-router
description: Use when 项目组以自然语言提出 Nova 项目接入、业务开发、构建或排障需求，且任务范围、风险或应触发的 Nova 消费端 Skill 尚不明确时使用。
---

# Nova 项目任务路由

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，然后只读当前消费项目和 Framework 包内 `Docs/INDEX.md`。目标是产出可执行的边界与下一步，而不是直接修改项目。

## 渐进式披露

- L0：只用 frontmatter、Catalog 与本页共同底线判断是否命中。
- L1：命中后只读取本 Skill 与 `references/contract.json`，冻结任务范围和副作用。
- L2：仅在候选路径确定后，读取该路径所列的模块 Docs 或目标项目事实；不要先展开所有 Skill。
- L3：只把已确认的输入交给被选中的 Action Adapter；Router 自身不写入项目。
- L4：只交付本次路由所需的定位证据，不把路由结论冒充业务功能验证。

## 路由步骤

1. 区分项目组任务与框架组任务。新增 Runtime 模块、Inspector、SDK Plugin、Framework API 或修改 `Packages/com.solotopia.nova.framework` 都标为 `not_applicable`，说明应转交框架组。
2. 冻结目标、消费项目路径、目标平台/渠道/模式、目标场景或产物，以及允许的副作用。缺失且会改变方案时，提出一个最小澄清问题。
3. 消除常见歧义：

| 用户词 | 必须确认 |
|---|---|
| 页面 / 界面 | 只建 View，还是还需 Prefab、UI 注册、入口与运行验证 |
| 表 / 配置 | Luban、UI、Localization、Network、Sound、Vibrate 或 ConfigMaster |
| 打包 / 发布 | Bundle、Player、CDN、商店、UPM；外部写入或 Git 不因名称自动获授权 |
| 资源更新 / 热更 | 构建 Bundle、部署 CDN、运行时下载或缓存清理 |
| HybridCLR / 热更 DLL | 业务 DLL 本地刷新、full AOT、Bundle、Player、CDN 或运行时诊断；不得把这些目标合并成一次操作 |
| 接 SDK | 启用已有消费包，还是开发新的 SDK Plugin |
| 登录 / 领奖 / 业务 API | HostKey / NetCmd 行、协议、认证、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针是否都已确认 |
| BGM / 背景音乐 / 点击音效 / 声音 | 真实 AudioClip、Collector、地址、Sound 表、声音组、加载入口、业务触发、停止生命周期和实际播放成功探针是否都已确认 |
| 启动不了 | 编译、Play 门禁、黑屏、加载链、网络或服务端结果 |

4. 选择最窄的路径：
   - 范围或项目事实不清：`nova-project-check-readiness`。
   - 编译失败、Play 门禁、黑屏或启动链异常：`nova-project-diagnose-startup`。
   - 入口场景、Build Settings、Nova 根或 Content 职责：`nova-project-setup-entry-scene`。
   - 一个已确认三维坐标的 ConfigMaster / ConfigRuntimeSO：`nova-project-configure-runtime`。
   - 已确认 Luban 表源、导出和运行时读取链：`nova-project-integrate-table`。
   - 已有文本、字体或 `TextLocalizing` 绑定：`nova-project-update-localization`。
   - 新建或注册业务 `UIView`：`nova-project-ui-create-view`。
   - 已注册 View、Prefab 或 UI 注册内容的定向调整：`nova-project-update-ui-view`。
   - 已有数据表驱动业务页面并从现有入口打开：`nova-project-data-driven-ui`。
   - 已确认业务资源的 Collector、地址、加载和释放链：`nova-project-integrate-resource`。
   - 已配置 HybridCLR，且只需在当前 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标下完成业务 DLL 本地 compile -> copy 整批刷新：`nova-project-refresh-hotfix-dlls`；full AOT、Bundle、Player 及 CDN/运行时诊断分别路由，不扩入该 Operation。
   - 已确认协议、认证、路由、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针的业务 HTTP API：`nova-project-integrate-network-api`；缺任一项时先提出最小澄清问题，不猜协议或主备切换策略。
   - 已确认声音表、真实 AudioClip、Collector、地址、声音组、加载入口、业务触发、停止生命周期和实际播放探针：`nova-project-integrate-sound`；缺真实资源或成功探针时先澄清，不以 serialID 代替成功。
   - 本地 YooAsset Package 产物：`nova-project-build-bundles`；本地安装包或平台工程：`nova-project-build-player`。
   - 单一确定的 Operation 不强制经过 Workflow；多个写入操作仅在读写集无冲突时并行。
5. 明确不可做的事：不扫描主仓 `Minds/` 或 `.nova/`，不套用 Sample，不猜 UIGroup / 表字段 / 资源地址，不手写 Prefab 或 Scene YAML，不把 Pipify 顺序 Batch 当作可并行 DAG。

## 输出

输出 `success`（仅指路由完成）、`blocked` 或 `not_applicable`，并包含：冻结的输入、推荐 Skill、原因、计划写入集、确认门、最低验证证据和未验证项。路由 Skill 本身不应声称业务功能已完成。
