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
- 查询“有什么功能 / 参数是什么意思 / 对应工具在哪里”时，先复用随包 `Agents/Tools/nova_capabilities.py` 的只读 `inventory --detail summary` / `describe --detail summary`，锁定精确任务或函数后才用 `describe <id> --detail full` 展开必要契约与正文；不一次加载全部任务契约，不把参数查询扩大成配置修改。
- L3：只把已确认的输入交给被选中的 Action Adapter；Router 自身不写入项目。
- L4：只交付本次路由所需的定位证据，不把路由结论冒充业务功能验证。

## 路由步骤

1. 区分项目组任务与框架组任务。新增 Runtime 模块、Inspector、SDK Plugin、Framework API 或修改 `Packages/com.solotopia.nova.framework` 都标为 `not_applicable`，说明应转交框架组。
2. 冻结目标、消费项目路径、目标平台/渠道/模式、目标场景或产物，以及允许的副作用。缺失且会改变方案时，提出一个最小澄清问题。
3. 消除常见歧义：

| 用户词 | 必须确认 |
|---|---|
| 页面 / 界面 | 只建 View，还是还需 Prefab、UI 注册、入口与运行验证 |
| 功能清单 / 参数解释 / Inspector 或 Window 选项 | 查询源码声明与默认值，还是当前项目有效值与配置修改；查询结果不证明已运行或已自动化 |
| 表 / 配置 | Luban、UI、Localization、Network、Sound、Vibrate、ConfigMaster，还是项目自有SO/Prefab字段；表源修改不等于生成物修改 |
| 日常流程 / 批量自动化 / Pipify | 维护已保存Batch配置，还是实际运行；目标Settings、Step顺序、字段与运行副作用分别冻结 |
| 打包 / 发布 | Bundle、Player、CDN、商店、UPM；外部写入或 Git 不因名称自动获授权 |
| 上传 Google Play / AAB / supply 报错 | 配置、真实上传（含仅验证）还是只读排障；AAB/包名、轨道、用途、凭据账号及完整 Batch 范围是否已确认 |
| 资源更新 / 热更 | 构建 Bundle、部署 CDN、运行时下载或缓存清理 |
| HybridCLR / 热更 DLL | 业务 DLL 本地刷新、full AOT、Bundle、Player、CDN 或运行时诊断；不得把这些目标合并成一次操作 |
| 装包 / 升级包 / 卸载包 | 最新 registry 版本的单包安装/升级、direct dependency 卸载，还是指定版本、降级、来源切换、批量升级或 Framework 发布 |
| 接 SDK / Kit | 是否为已发布且包内文档可发现的单一 Nova SDK/Kit；包名、安装/升级意图、三维坐标、平台前置和最小本地探针是否已确认 |
| 事件 / 消息通知 | Event 事件还是网络消息；载荷、发布者、订阅拥有者、Fire/FireNow、线程与注销生命周期是否已确认 |
| 存档 / 本地保存 | PlayerPrefs、FileFragment 或 SQLite；classify/item、保存时机、清理范围、平台和加密前置是否已确认 |
| 金币 / 体力防改、关键整数内存防护 | 现有数值 owner、可信初值、int/long 类型及失败后的结算/保存边界；仅内存接入不要求存储选型、classify/item 或 AES 密钥 |
| 加载场景 / 切换关卡 | 入口 Scene、Build Settings、Nova 资源化 Content 场景，还是普通资源/Prefab；Package、location、mode、Handle owner 与卸载时机是否已确认 |
| Android 依赖 / EDM4U / GeneratedLocalRepo | UPM Resolve、EDM4U Force Resolve、Gradle 构建诊断还是清理缓存；当前 Android Target 与依赖图是否已确认 |
| 登录 / 领奖 / 业务 API / HTTP取消 | 先区分新协议或路由/认证接入，与既有单URL请求/页面Owner取消维护；前者冻结真实HostKey/NetCmd、认证与账号，后者复用原请求参数、实际可取消Manager、Owner/token与受控探针，不无条件要求表导出或账号 |
| BGM / 背景音乐 / 点击音效 / 声音 | 真实 AudioClip、Collector、地址、Sound 表、声音组、加载入口、业务触发、停止生命周期和实际播放成功探针是否都已确认 |
| 振动 / 震动 | Emphasis、Custom 或预设；数据源、Unit、Collector、业务触发、停止责任、目标真机和体感探针是否都已确认 |
| 流程 / Procedure | 业务 Procedure 还是框架启动链；业务程序集、进入/离开、异步取消、下一状态和 Play 探针是否都已确认 |
| 开始游戏 / 结算 / 重开 / 回大厅 | 当前业务 Service、入口与 Procedure/FSM 的起点/终点、局身份、结果/奖励、重开保留范围和退出清理是否已确认 |
| 暂停 / 恢复 / 倍速 | 框架全局速度与暂停缓存，还是单局/页面业务状态；输入、音频、计时、资源与网络是否跟随，由谁持有和恢复 |
| 启动不了 | Editor 编译、Play 门禁，还是 WebGL 浏览器页面/loader/Wasm/data、Unity/Nova 加载链、网络、真机进程或服务端结果 |
| 能不能打包 / 打包前检查 | 只读构建前置检查、真正构建 Bundle/Player，还是分析一次既有失败 |
| 真机闪退 / SDK 无回调 | Android/iOS 设备、Bundle ID、时间窗、日志源和脱敏规则是否已冻结；诊断不等于安装、启动或修复 |

4. 选择最窄的路径：
   - 只查功能、参数声明、源码/文档及技能执行入口：直接使用下面的共享查询 CLI；Router 交付查询和路由结果，不强制进入写入 Operation。
   - 范围或项目事实不清：`nova-project-check-readiness`。
   - 编译失败、Play 门禁、黑屏或启动链异常：`nova-project-diagnose-startup`；已有 WebGL 页面启动失败先按其浏览器参考只读 loader → Wasm/data → Unity/Nova，不自动启动服务、刷新页或清缓存。
   - 入口场景、Build Settings、Nova 根或 Content 职责：`nova-project-setup-entry-scene`。
   - 一个已确认三维坐标的 ConfigMaster / ConfigRuntimeSO，或其已有面板字段/明确维度轴：`nova-project-configure-runtime`；非规范Master复用其中的独立 `normalize-master` 路线，明确合并保留锚点和整Master差异，整理后重新查阅及计划字段修改。
   - 项目自有 SO、Scene、Prefab 的 Inspector 字段、内置组件、变体/嵌套或已核实 UnityEvent：`nova-project-edit-settings`；普通资产优先受控 Action，场景及复杂 Prefab 按共享 Unity MCP 参考执行。ConfigMaster/ConfigRuntime/PipifySettings 仍走各自专用入口。
   - 创建、克隆、改名、排序或配置已保存Pipify Batch：`nova-project-configure-pipeline`；默认只保存，实际运行另冻结完整Batch副作用。
   - 一个已配置 registry 包的最新版本安装、同源最新版本升级或 direct dependency 安全卸载：`nova-project-manage-upm-package`；指定版本、降级、来源切换、批量升级、Framework 自身或发布不进入该 Operation。
   - 当前消费项目的 direct Nova Framework 需要从同一 registry 升级到最新或明确更高版本：`nova-project-upgrade-framework`；必须由跨 reload 独立的包外 UPM 宿主执行，Framework 内部 Action 不得自升级。
   - 已发布 Nova SDK/Kit 的安装、配置、登录、支付、广告、云存档、推送、统计或平台业务接入：`nova-project-onboard-sdk-kit`；按目标包和任务只读相应 reference，复用已有公开 API 与项目自有业务源。已有 Excel 支付商品先只读转换，再复用 Config 面板保存及正式导出；厂商后台、凭据和设备结果分别验收。
   - 只需导出当前TableSettings、修改已有非敏感xlsx/csv标量，或给已有自描述xlsx追加普通字段后正式导出：`nova-project-export-tables`；源修改与普通字段追加复用同一 `nova_table_source` CLI 的明确分支。字段类型、行键与默认值从真实表确认，复杂Schema不扩入该分支。
   - 已确认 Luban 表源、导出和运行时读取链，含已有自描述xlsx普通字段追加并在游戏读取：`nova-project-integrate-table`；复用源CLI和正式导出，不手改生成代码，不把Sample表当未知业务目标。
   - 已有文本/字体修改、明确追加 Key/语言或语言切换：`nova-project-update-localization`；源结构追加复用其共享 CLI 严格分支，删除/改名及复杂字体不扩入该分支。给明确页面集合补齐 `TextLocalizing` 与引用则复用 `nova-project-update-ui-view` 的本地化绑定参考，逐对象保存并覆盖本次请求语言集合验证。
   - 新建或注册业务 `UIView`：`nova-project-ui-create-view`。
   - 已注册 View、Prefab 或 UI 注册内容的定向调整：`nova-project-update-ui-view`。
   - 已有数据表驱动业务页面并从现有入口打开：`nova-project-data-driven-ui`。
   - 已确认业务资源的 Collector、地址、加载和释放链，对象池接入、缓存维护、加密或密钥轮换：`nova-project-integrate-resource`；按需读取生命周期或加密参考，配置、生成、构建和运行分别验证。
   - 业务存储、关键整数内存防护、存档诊断、值 AES、SQLite Cipher 或 Privacy 默认 AES 轮换：`nova-project-integrate-persistence`；金币/体力防改按其内存防护参考复用 Core 公开 API 与业务 owner。密码与 Privacy 分支仍复用同三 Action，限定当前macOS Editor与精确数据/配置范围，设备与外部消费者独立验收；资源密钥、App AES与业务schema分别路由。
   - 已配置 HybridCLR，且只需在当前 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标下完成业务 DLL 本地 compile -> copy 整批刷新：`nova-project-refresh-hotfix-dlls`；full AOT、Bundle、Player 及 CDN/运行时诊断分别路由，不扩入该 Operation。
   - 已配置 HybridCLR，且需要按当前 Target、DevelopmentBuild 与激活 ConfigMaster 当前坐标执行 `Generate All` 与 link.xml 验证：`nova-project-generate-hybridclr-artifacts`；它不 Copy DLL、不构建最终 Player，也不替代最终 Player 后的 `CopyAotDlls`。
   - HTTP API、WebSocket 或已有普通请求的Owner取消/响应回收维护：`nova-project-integrate-network-api`。新协议或路由/认证变化才冻结相关HostKey/NetCmd、导出、账号与重放语义；既有单URL维护复用原参数、当前Manager与生命周期，不强制导表、Network加载或账号。WebSocket按连接、认证、心跳、重连与响应生命周期参考执行；可读项目事实先核，缺真实业务选择再澄清。
   - 加入声音表、真实 AudioClip、Collector、地址、声音组与播放/停止行为：`nova-project-integrate-sound`；从项目发现已有加载 Owner，缺现成触发代码时在授权业务源范围补齐，缺真实资源则保留依赖缺项，不以 serialID 代替实际播放与停止。
   - 已确认 Emphasis / Custom 振动数据、Unit、地址、Collector、业务触发、唯一停止责任、目标真机和体感探针：`nova-project-integrate-vibration`；没有真机反馈时最高为 `partial`。
   - 已确认业务程序集、业务 Procedure、进入/离开职责、异步取消、下一状态和 Play 探针：`nova-project-integrate-procedure`；复用 `ProcedureLoadDll` 延迟注册，不建立第二注册器。
   - 开始游戏、结算、重开或回大厅：`nova-project-integrate-procedure` 的 `references/business-flow.md`；从实际入口复用项目现有业务 Service 与 Procedure/FSM。不存在 `Nova.Game` 门面；缺业务 Service 且创建正式项目实现属于现有授权时，补齐最小业务胶水，缺玩法决定或外部协议则报告对应缺项。
   - 暂停、恢复或倍速先确定拥有者：框架全局 `Nova.Self` 速度/暂停缓存走 `nova-project-edit-settings` 的 `references/app-and-debug-maintenance.md`；单局或页面的业务状态、输入/计时/资源等走 `nova-project-integrate-procedure` 的业务流程参考。`Time.timeScale=0` 不证明所有调度、网络或异步操作停止，不把直接设速度与缓存暂停混为同一操作。
   - 已确认事件类型、载荷、发布者、订阅拥有者、分发模型、线程和注销生命周期：`nova-project-integrate-event`；池化事件实例不得跨 handler 或异步链持有。
   - 已确认存储实现、classify/item、Config 后 Persist 加载顺序、保存/清理语义和加密前置：`nova-project-integrate-persistence`。
   - 已有存档查询、迁移或清理：`nova-project-integrate-persistence` 的维护参考；备份、转换和恢复以实际实现为准，字段开关保存不代表旧数据迁移成功。
   - App 大版本更新、RuntimeDebugger、编译日志级别或相关宏：`nova-project-edit-settings` 的 App 与调试维护参考；参数、宏、业务分支和平台结果分别验证。
   - 现有埋点说明与源 Tracks 文字维护：`nova-project-onboard-sdk-kit` 的 Tracks 参考；复用表源 CLI，不手改汇总物，不把叶子编辑称为事件新增或后台验收。
   - 已纳入当前 Nova 资源配置的 Content 场景，且 Package/default-Package、location、LoadSceneMode、唯一 ISceneHandle owner 与卸载时机明确：`nova-project-integrate-content-scene`；入口场景仍走 `nova-project-setup-entry-scene`。
   - 实际构建前只读检查 Target、场景、Config、YooAsset Package 与 HybridCLR 前置：`nova-project-preflight-build`；它不执行构建或修复。
   - 已有 Player、Gradle、Xcode 或 WebGL 构建失败证据，需要定位最早失败阶段：`nova-project-diagnose-build`；不得借诊断重跑构建、Resolve 或清理。
   - Android SDK/Kit 变更后需要 EDM4U Force Resolve：`nova-project-resolve-android-dependencies`；该 Action 含 Destructive，必须绑定精确计划确认，不能退化为任意 C# 或删目录。
   - 已冻结 Android/iOS 设备、Bundle ID、时间窗、日志源与脱敏规则的真机日志诊断：`nova-project-diagnose-device-runtime`；不安装、不清数据、不启动应用、不修复工程。
   - 本地 YooAsset Package 产物：`nova-project-build-bundles`；WebGL、Android、iOS 的本地安装包/平台工程及已核验产物的本机预览或设备安装：`nova-project-build-player`，分别按构建、CLI 与平台验证参考冻结范围。
   - 配置 Google Play AAB 上传、执行已授权的上传 Batch，或排查 fastlane supply 权限/版本号/提交失败：`nova-project-upload-google-play`；诊断不重试，仅验证仍有远端上传副作用，不能当作本地构建或 UPM 发布。
   - 单一确定的 Operation 不强制经过 Workflow；多个写入操作仅在读写集无冲突时并行。
5. 读取所选 Skill 的 `references/contract.json` 后，按真实 Adapter 状态标记执行方式：引用 `nova_project_action` 的分支只通过 live `describe(action_id)` 判断为“当前 MCP 可执行”；Tool 缺失或 `describe` 未返回目标 ID 时标记“传输未就绪 / 当前未开放”，不得根据静态文档猜测可用，也不得退化为任意 C#。不依赖 Project Action 的分支标记其实际 Adapter（只读检查、受控 Unity 编辑、CLI 或人工判断）。Router 只报告可用性，不调用 `plan` 或制造写入。
6. 明确不可做的事：不扫描主仓 `Minds/` 或 `.nova/`，不套用 Sample，不猜 UIGroup / 表字段 / 资源地址，不手写 Prefab 或 Scene YAML，不把 Pipify 顺序 Batch 当作可并行 DAG。

## 共享查询入口

通过 `nova_skills.py resolve` 定位当前 Framework 后，macOS/Linux 使用：

```text
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind tasks --query <goal> --detail summary --limit 5 --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> inventory --kind features --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <task-or-function-id> --detail summary --json
python3 -B "<resolved Framework package>/Agents/Tools/nova_capabilities.py" --root <projectRoot> describe <task-or-function-id> --detail full --json
```

Windows 将 `python3` 换成 `py -3`；必要时显式传 `--agents-root "<resolved Framework package>/Agents"`。`--detail summary` 保留准确身份、导航与状态，省略重复的全文契约/正文；过滤和分页仍根据完整记录，不把摘要当作执行许可。省略 `--detail` 仍为 `full` 以兼容旧调用，日常路由显式选摘要。若当前已安装版本尚无该参数，先按其实际帮助分页定位，再只展开目标，不声明工具已升级。这些查询复用 `coverage.json` 的功能族、源码、Docs、既有 Skill/Adapter 与限制；源码参数声明和默认值不等于项目当前有效值，后者还需读取已确认的配置资产和运行状态。查询不会安装未使用 SDK，也不读取主仓 `.nova/` / `Minds/`。工具缺失、条目未覆盖或资料不足时明确说明缺口，不据此创建新顶层 Skill。`check` 是框架维护完整性检查，不作为消费项目每次查询的前置。

## 输出

输出 `success`（仅指路由完成）、`blocked` 或 `not_applicable`，并包含：冻结的输入、推荐 Skill、原因、真实 Adapter 可用性、计划写入集、确认门、最低验证证据和未验证项。路由 Skill 本身不应声称业务功能已完成。
