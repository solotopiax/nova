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
| 装包 / 升级包 / 卸载包 | 最新 registry 版本的单包安装/升级、direct dependency 卸载，还是指定版本、降级、来源切换、批量升级或 Framework 发布 |
| 接 SDK / Kit | 是否为已发布且包内文档可发现的单一 Nova SDK/Kit；包名、安装/升级意图、三维坐标、平台前置和最小本地探针是否已确认 |
| 事件 / 消息通知 | Event 事件还是网络消息；载荷、发布者、订阅拥有者、Fire/FireNow、线程与注销生命周期是否已确认 |
| 存档 / 本地保存 | PlayerPrefs、FileFragment 或 SQLite；classify/item、保存时机、清理范围、平台和加密前置是否已确认 |
| 加载场景 / 切换关卡 | 入口 Scene、Build Settings、Nova 资源化 Content 场景，还是普通资源/Prefab；Package、location、mode、Handle owner 与卸载时机是否已确认 |
| Android 依赖 / EDM4U / GeneratedLocalRepo | UPM Resolve、EDM4U Force Resolve、Gradle 构建诊断还是清理缓存；当前 Android Target 与依赖图是否已确认 |
| 登录 / 领奖 / 业务 API | HostKey / NetCmd 行、协议、认证、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针是否都已确认 |
| BGM / 背景音乐 / 点击音效 / 声音 | 真实 AudioClip、Collector、地址、Sound 表、声音组、加载入口、业务触发、停止生命周期和实际播放成功探针是否都已确认 |
| 振动 / 震动 | Emphasis、Custom 或预设；数据源、Unit、Collector、业务触发、停止责任、目标真机和体感探针是否都已确认 |
| 流程 / Procedure | 业务 Procedure 还是框架启动链；业务程序集、进入/离开、异步取消、下一状态和 Play 探针是否都已确认 |
| 启动不了 | Editor 编译、Play 门禁、黑屏、加载链、网络、真机进程还是服务端结果 |
| 能不能打包 / 打包前检查 | 只读构建前置检查、真正构建 Bundle/Player，还是分析一次既有失败 |
| 真机闪退 / SDK 无回调 | Android/iOS 设备、Bundle ID、时间窗、日志源和脱敏规则是否已冻结；诊断不等于安装、启动或修复 |

4. 选择最窄的路径：
   - 范围或项目事实不清：`nova-project-check-readiness`。
   - 编译失败、Play 门禁、黑屏或启动链异常：`nova-project-diagnose-startup`。
   - 入口场景、Build Settings、Nova 根或 Content 职责：`nova-project-setup-entry-scene`。
   - 一个已确认三维坐标的 ConfigMaster / ConfigRuntimeSO：`nova-project-configure-runtime`。
   - 一个已配置 registry 包的最新版本安装、同源最新版本升级或 direct dependency 安全卸载：`nova-project-manage-upm-package`；指定版本、降级、来源切换、批量升级、Framework 自身或发布不进入该 Operation。
   - 当前消费项目的 direct Nova Framework 需要从同一 registry 升级到最新或明确更高版本：`nova-project-upgrade-framework`；必须由跨 reload 独立的包外 UPM 宿主执行，Framework 内部 Action 不得自升级。
   - 一个已发布且包内文档可发现的 Nova SDK/Kit，需要组合最新包安装/升级、单一三维配置、平台前置与最小本地探针：`nova-project-onboard-sdk-kit`；开发新 Plugin、厂商后台、凭据和真实设备成功不进入该 Workflow。
   - 已确认 Luban 表源、导出和运行时读取链：`nova-project-integrate-table`。
   - 已有文本、字体或 `TextLocalizing` 绑定：`nova-project-update-localization`。
   - 新建或注册业务 `UIView`：`nova-project-ui-create-view`。
   - 已注册 View、Prefab 或 UI 注册内容的定向调整：`nova-project-update-ui-view`。
   - 已有数据表驱动业务页面并从现有入口打开：`nova-project-data-driven-ui`。
   - 已确认业务资源的 Collector、地址、加载和释放链：`nova-project-integrate-resource`。
   - 已配置 HybridCLR，且只需在当前 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标下完成业务 DLL 本地 compile -> copy 整批刷新：`nova-project-refresh-hotfix-dlls`；full AOT、Bundle、Player 及 CDN/运行时诊断分别路由，不扩入该 Operation。
   - 已配置 HybridCLR，且需要按当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标执行 `Generate All` 与 link.xml 验证：`nova-project-generate-hybridclr-artifacts`；它不 Copy DLL、不构建最终 Player，也不替代最终 Player 后的 `CopyAotDlls`。
   - 已确认协议、认证、路由、请求/响应、重放语义、业务调用入口、测试端点、测试账号和成功探针的业务 HTTP API：`nova-project-integrate-network-api`；缺任一项时先提出最小澄清问题，不猜协议或主备切换策略。
   - 已确认声音表、真实 AudioClip、Collector、地址、声音组、加载入口、业务触发、停止生命周期和实际播放探针：`nova-project-integrate-sound`；缺真实资源或成功探针时先澄清，不以 serialID 代替成功。
   - 已确认 Emphasis / Custom 振动数据、Unit、地址、Collector、业务触发、唯一停止责任、目标真机和体感探针：`nova-project-integrate-vibration`；没有真机反馈时最高为 `partial`。
   - 已确认业务程序集、业务 Procedure、进入/离开职责、异步取消、下一状态和 Play 探针：`nova-project-integrate-procedure`；复用 `ProcedureLoadDll` 延迟注册，不建立第二注册器。
   - 已确认事件类型、载荷、发布者、订阅拥有者、分发模型、线程和注销生命周期：`nova-project-integrate-event`；池化事件实例不得跨 handler 或异步链持有。
   - 已确认存储实现、classify/item、Config 后 Persist 加载顺序、保存/清理语义和加密前置：`nova-project-integrate-persistence`。
   - 已纳入当前 Nova 资源配置的 Content 场景，且 Package/default-Package、location、LoadSceneMode、唯一 ISceneHandle owner 与卸载时机明确：`nova-project-integrate-content-scene`；入口场景仍走 `nova-project-setup-entry-scene`。
   - 实际构建前只读检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置：`nova-project-preflight-build`；它不执行构建或修复。
   - 已有 Player、Gradle、Xcode 或 WebGL 构建失败证据，需要定位最早失败阶段：`nova-project-diagnose-build`；不得借诊断重跑构建、Resolve 或清理。
   - Android SDK/Kit 变更后需要 EDM4U Force Resolve：`nova-project-resolve-android-dependencies`；当前 Action 含 Destructive 且 MCP 未开放时必须报告可信审批阻断，不能退化为任意 C# 或删目录。
   - 已冻结 Android/iOS 设备、Bundle ID、时间窗、日志源与脱敏规则的真机日志诊断：`nova-project-diagnose-device-runtime`；不安装、不清数据、不启动应用、不修复工程。
   - 本地 YooAsset Package 产物：`nova-project-build-bundles`；本地安装包或平台工程：`nova-project-build-player`。
   - 单一确定的 Operation 不强制经过 Workflow；多个写入操作仅在读写集无冲突时并行。
5. 读取所选 Skill 的 `references/contract.json` 后，按真实 Adapter 状态标记执行方式：引用 `nova_project_action` 的分支只通过 live `describe(action_id)` 判断为“当前 MCP 可执行”；Tool 缺失或 `describe` 未返回目标 ID 时标记“传输未就绪 / 当前未开放”，不得根据静态文档猜测可用，也不得退化为任意 C#。不依赖 Project Action 的分支标记其实际 Adapter（只读检查、受控 Unity 编辑、CLI 或人工判断）。Router 只报告可用性，不调用 `plan` 或制造写入。
6. 明确不可做的事：不扫描主仓 `Minds/` 或 `.nova/`，不套用 Sample，不猜 UIGroup / 表字段 / 资源地址，不手写 Prefab 或 Scene YAML，不把 Pipify 顺序 Batch 当作可并行 DAG。

## 输出

输出 `success`（仅指路由完成）、`blocked` 或 `not_applicable`，并包含：冻结的输入、推荐 Skill、原因、真实 Adapter 可用性、计划写入集、确认门、最低验证证据和未验证项。路由 Skill 本身不应声称业务功能已完成。
