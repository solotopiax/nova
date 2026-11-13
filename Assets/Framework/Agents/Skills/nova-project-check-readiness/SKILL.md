---
name: nova-project-check-readiness
description: Use when 项目组需要评估陌生 Unity 项目能否接入、继续使用或安全开展 Nova 开发，或希望在修改前得到阻塞项、风险与证据分级时使用。
---

# Nova 项目就绪检查

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

再读取 [references/contract.json](references/contract.json)，只检查用户声明的项目范围。该 Skill 只报告，不安装包、不修复配置、不删除缓存。

## 渐进式披露

- L0：先用 frontmatter、Catalog 与共同底线判断是否是项目就绪评估。
- L1：只读取本 Skill 与 [references/contract.json](references/contract.json)，锁定用户声明的项目范围。
- L2：仅在目标已明确时读取对应的项目文件、包解析结果和所需模块 Docs；若已安装或本次范围点名 Nova SDK/Kit，先定向读取其解析元数据与包内 [Nova/{Doc,Docs,DOCS}/INDEX.md（包内定位）](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现)，再按该 INDEX 路由所需资料；需要 Unity 证据时再确认并连接 Unity。
- L3：只调用只读检查 Adapter，不把诊断变成修复。
- L4：按实际取得的静态、Unity 或 Play 证据分级报告，缺口保持 `partial`。

## 检查步骤

新项目或开工准备评估先按 [Docs/Onboarding/PROJECT_STRUCTURE.md](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) 的“新项目开工准备”逐项只读建立前置清单，报告真实状态、依据和后续 Operation；一般工程层级可以说明不适用，标准预加载节点按下段核对。默认 MainDemo 启动 Resources 与预加载基线不能仅凭 canonical Prefab 或路径字段判齐备；HybridCLR 按环境文档分别核对包、本地安装、启用及所需产物。只读评估不负责安装、复制资源或配置；清单交给获授权的准备步骤，必需项未齐不称整套环境 ready。

清单同时记录各准备项的所选 Skill/contract、实际 Adapter、参数/物料来源与依赖关系；参数类型/默认声明、项目当前值和业务认可的取值分别判断。按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 对专有物料暂缺项先只读查找 Nova 各 Demo 的临时配套来源，标记临时用途及后续替换点；仍缺必需输入时才标为待准备或阻塞，交给对应 Operation，不能给出猜测值供后续直接写入。ConfigMaster/App 参数转配置 Skill 读取所属文档；本检查不生成密钥、分配后台编号或代填配置。

新游戏或产品基础能力评估必须按同页“标准产品预加载基线”逐项核 MainDemo 八节点，记录真实来源、代码/配置、输入物料、结果判定、最小可用证据及已就绪/待准备/阻塞。小游戏、单机或离线不能默认省略模块；平台不支持某项具体能力时仍核保留节点及其支持分支，不能把模块整体标成不适用。SDK 全局任务完成、空表加载成功或只有 UI/Sound 启动，均不证明完整基线已具备。缺项只报告，不借评估修改现有项目。

同时按[三平台构建与 HybridCLR 基线](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#三平台构建与-hybridclr-基线)核对 HybridCLR 安装、启用、真实业务程序集、Config DLL/link 映射与资源收集，以及目标 Batch 的 Config/Network 导出、生成、DLL 部署、Bundle/Player 顺序。缺项列待准备或阻塞，不能将单机项目自行归为非热更、不适用；普通 IL2CPP 包或仅包存在不证明基线就绪。三平台配置存在与逐平台生成、构建、运行证据分别报告，本 Operation 仍保持只读。

1. 用已解析宿主的 Python 3.9+ 运行 `nova_skills.py resolve` 确认消费项目实际解析到的 Framework 包：macOS/Linux 使用 `python3`，Windows 使用 `py -3`；记录 Unity 版本、Framework 版本、目标平台、当前场景和 Build Settings 的可见事实。
   Unity 已打开时，按共同入口只读确认窗口归属、阻塞提示和忙碌状态，MCP 无响应不直接判进程卡死。新项目或后续包含 UI 布局时，按 [Docs/Runtime/Modules/UI/UIComponent.md](../../../Docs/Runtime/Modules/UI/UIComponent.md) 记录已有屏幕适配方案、有效配置及缺项，交给写入 Operation 在制作布局前补齐；完整产品检查包含独立启动画布的闪屏/Loading/启动弹窗和玩法/暂停/结算；只覆盖产品声明的方向，并区分内容避让与整页收缩。本检查不点击窗口或更改设置。
2. 从项目事实而非 Sample 推断拓扑：检查 Nova 托管场景是否使用完整 canonical [Nova.prefab（定位）](../../../Docs/Onboarding/PROJECT_STRUCTURE.md)、运行拓扑是否唯一、Content 场景是否声明加载/卸载责任、资源和配置入口由谁管理。
3. 按[三工具基线](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#configpipifyplugpals-基线) 只读核对 Config（ConfigMaster）、Pipify、PlugPals 的配置及实际使用证据，三者适用于所有 Nova 项目；ConfigMaster/PipifySettings 工作区配对、正式导出及消费、适用 Batch 的保存/运行、仓库查询与依赖解析分别判断。缺项列为待准备/阻塞，证据不足列为待验证，转相应 Skill，不将未使用 Pipify 或 PlugPals 视为无关项；本检查不运行 Batch 或安装包。已有有效证据可复用，局部评估只报告相关缺项，不扩展整项目整改；普通 Warning 依实际影响分级。继续核对任务所需的业务 asmdef、UIGroup、Table/资源及构建入口，直接 Build 不替代三工具基线。
4. 对已安装或被点名的 `com.solotopia.nova.framework.sdk.*` / `com.solotopia.nova.framework.kit.*` 进入只读扩展包分支：从 [Packages/manifest.json（定位）](../../../Docs/START_HERE.md#先确定框架位置与文档入口)、[Packages/packages-lock.json（定位）](../../../Docs/START_HERE.md#先确定框架位置与文档入口) 与实际解析包根记录 package id、声明与解析版本、source 和根路径；先读取包内首个存在的 [Nova/Doc/INDEX.md（包内定位）](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现)、[Nova/Docs/INDEX.md（包内定位）](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现) 或 [Nova/DOCS/INDEX.md（包内定位）](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现)，不要因没有 INDEX 而宽扫全包。
5. 只沿该包 INDEX 的必要入口确认精确 Config 类型及其完整名称、配置路由/协议、资源归属与地址、目标平台或厂商前置。需要 Unity 类型证据时，通过 `nova_project_action` 调用只读 Action `nova.project.config.inspect-plugin-types`，按 `kind=sdk|kit|all` 取得稳定元数据；不要直接调用 Scanner 或构造消费项目插件类型。扫描只证明 Editor 已发现类型，不证明已启用、路由可达、资源存在或运行时成功。Tool 尚不可用时保留为 `partial`，不得退化为任意 C#。
6. 缺少包内 INDEX、Config 类型、路由、资源、平台前置或 Unity 扫描证据时，逐项列为 `unknown` 或 `partial`，不猜测、不安装包、不改版本、不补配置或尝试修复。包未安装也不是默认 Hard Error，除非它是用户已冻结目标的必需前置。
7. 仅在 Unity 已打开且用户允许时收集编译、场景层级、受控类型扫描或 Play 证据；区分确定错误、建议与证据缺口，不把静态检查写成运行验证。
8. 对“接入 Nova”额外确认目标 Nova 版本、替换还是并存既有框架、私有依赖可用性、目标平台和允许的迁移范围；这些信息不足时保持 `blocked`。

## 报告格式

按 `ready`、`blocked`、`risk`、`unknown` 四类列出每项结论的文件、版本、规则 ID 或日志证据；扩展包项还应给出 package id、版本、source、INDEX 路径以及未取得的 Config/路由/资源/平台证据。最终结果只能是：

- `success`：本次只读评估已完整交付，不等于项目已通过运行验证；
- `partial`：部分证据不可获取，明确缺口；
- `blocked`：关键目标或前置条件缺失；
- `not_applicable`：目标不是 Nova 项目组消费端范围。

不要基于引用数量判断是否接入成功，不要修改 [Packages/manifest.json（定位）](../../../Docs/START_HERE.md#先确定框架位置与文档入口)、[Packages/packages-lock.json（定位）](../../../Docs/START_HERE.md#先确定框架位置与文档入口)、PackageCache、已解析 SDK/Kit 或 Framework 包、场景或 Prefab，也不要建议删除 [Library/（项目本地位置）](../../../Docs/START_HERE.md#执行任务与清场的共同约束) 作为默认诊断动作。
