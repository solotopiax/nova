# 用自然语言驱动 Nova 开发

用户只需描述希望游戏发生什么变化，例如“给大厅加设置页”“把这批关卡数据接入”“解释并调整请求超时”。Codex 先读取当前项目事实，复用 Nova 已有 Skill 与执行接口，完成源数据、配置、资源、业务调用和验证链。只有确实需要用户决定的规则、素材、协议或外部账号才补问；不要求用户记忆类型名、菜单路径或导出顺序。

当前能力由 [任务与功能导航](../../Agents/coverage.json)、所属源码/XML、Docs 与 Skill/Action 契约共同给出。自动生成的符号索引只定位身份、位置与文档，不保存第二份人工语义。导航与结构通过不能代表人工已审或运行完成；任务须在本次执行验收。实际计数使用下面的只读命令查询，不另存进度或历史验收摘要。

## 查询、解释和固定操作

安装包中的 `Agents/Tools/nova_capabilities.py` 提供确定性查询，Python 标准库即可执行，不加载 Unity、不修改业务文件、不执行任意源码；查询缓存仅写项目 Library。开发仓例子：

```sh
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . inventory --kind tasks --query '配置' --detail summary --limit 5 --json
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . inventory --kind features --detail summary --json
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . inventory --kind functions --query 'AppComponent.CheckAsync' --detail summary --json
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . describe runtime.App --detail summary --json
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . describe '<精确任务或函数 id>' --detail full --json
```

消费项目把脚本路径替换成当前 `PackageInfo.resolvedPath/Agents/Tools/nova_capabilities.py`，`--root` 为消费项目根；需要显式包位置时使用 `--agents-root <resolvedPath>/Agents`。Windows 使用 `py -3 -B`。`inventory` 支持 `functions / fields / files / inputs / entries / features / tasks`，默认25条、最多200条，按 `nextOffset` 分页。默认 `summary`；锁定精确函数或任务 ID 后用 `describe <id> --detail full` 展开。同名和重载存在歧义时返回候选，不猜选。

日常函数查询只取命中的索引条目、所属导航和实际来源文件，不加载全库 Docs/知识/源文件。首次使用便携快照时在项目 `Library/Nova/AgentIndex/` 建立 SQLite 派生缓存；后续按键读取。快照变化则重建缓存，缓存可删除，不入 Git。安装包已携带便携索引，查询只需 Python 标准库，无需 .NET。

输出的 `sourceStatus` 区分 `current / stale / unavailable / not_applicable`，只说明当前位置是否可用；`semanticReview=not-performed` 明确工具没有进行人工语义审查。`full` 只在文件和 span 指纹均匹配时返回正文及附近 XML。参数含义与来源在 XML/Docs 查阅；声明默认值不能当作当前资产值。读取源码不授予执行任意 C# 的权限。

固定的导出、构建、包管理等操作复用已声明的 Action 或已有 CLI/Pipify。Project Action 使用 `nova_project_action` 的 live `describe → plan → execute → verify`；确认与目标绑定，重复调用、domain reload、取消和失败按当前契约处理。开放式页面设计与业务代码仍由 Operation Skill 编排，通过 Unity Editor 修改资产并验证，不能用万能反射入口代替。

## 每个业务任务的执行闭环

实参从当前项目的源、配置、对象和已加载运行状态取得，并分清声明默认值和当前有效值；技术名称及路径由 Agent 查阅，业务规则和真实素材由本次目标确定。先冻结唯一目标、源码/资产写集和实际入口，再读取命中 Skill 的必要引用，不为一个目标加载所有任务契约。

已有业务 Owner/Service 优先复用；缺少现成实现时，若创建它属于已授权业务开发目标，可以在项目自有程序集内补明确输入/结果/错误的最小业务胶水。调用当前 Skill 声明的公开领域 API，保留加载时序、身份、重复/并发、取消和退出清理；资产绑定、表源/正式导出、配置和 DLL 链仍由已有 Operation 完成。没有真实素材、协议或规则时推进独立部分并明确缺项，不创建框架 Manager、注册器、万能 C# 执行入口或假成功探针。

最终从已保存的真实入口验证目标效果与失败/退出分支，按各层实际证据报告状态。所有任务共用 [Agent 快速入口的执行与清场规则](../START_HERE.md#执行任务与清场的共同约束)：恢复本轮测试态，清理临时 runner、脚本/meta、日志、marker 和产物，必要 Refresh 后核对目标与工作树，保留正式交付和原有工作。证据缺失或清场残留不能改成完整成功。

## 从需求到可运行功能

| 用户目标 | 复用任务入口 | 必须完成的链路 |
|---|---|---|
| 新项目首次运行 | readiness、entry scene、runtime configuration | 环境/包 → 当前场景与 Nova 根 → 配置 → 编译 → Play |
| 大厅或设置页 | UI create/update、event、persistence | View/Prefab → 表注册 → 导出 → 资源地址与绑定 → 入口/交互 → 保存恢复 → Play |
| 关卡或列表数据 | integrate table、data-driven UI | 权威表源 → 正式导出 → 生成访问代码 → Config/组件引用 → 页面/关卡消费 |
| 声音、多语言、振动 | 各领域已有 Operation | 源表与真实资源 → 导出 → Collector/绑定 → 触发/停止或切换 → 实际效果 |
| 登录、云存档、支付、广告 | onboard SDK/Kit、network、runtime configuration | 包/依赖 → 配置/协议 → 初始化 → 业务调用 → 响应/回调 → Play/真机/后台证据 |
| 构建试玩版本 | preflight、hotfix、bundle、player | 配置/导出 → 所需 DLL/AOT → Bundle → Player → BuildReport/产物；部署另按目标明确执行 |
| 排查失败 | startup/build/device diagnosis | 当前日志/时间窗 → 最早失败函数与阶段 → 直接证据 → 最小修复 → 对应验证 |

这些是复用方向，具体动作和平台限制以当前 Skill 契约和 live MCP 为准，不把尚未封装的任务当成现成可执行功能。

## 新功能如何保持生态闭环

框架维护要求由开发仓工作流门禁定义；消费项目无需访问主仓 `.nova` 或 `Minds`。参数与局部行为保存在源码/XML，流程、配置来源与生命周期保存在已有 Docs，Skill 任务与执行契约真源保存在 Agents，固定行为留在现有领域实现与 Action/CLI/Pipify。新增函数、重载、参数和绑定改变后，必须更新对应文档、功能映射、相邻 XML 和必要执行契约；仅内部等价变化按风险最小复核，不机械制造 Skill。

复用顺序为：现有领域 API → 现有确定性 Action/CLI/Pipify → 现有 Operation 的参数或按需引用 → 有真实跨操作依赖时才新增 Workflow。只有独立用户任务、现有边界不能承载且维护收益明确时，才新增顶层 Skill。函数级审查与任务级封装分别管理，避免“一函数/一参数/一包一个 Skill”。

每个可执行任务说明输入、默认与实际值、写入范围、失败/恢复语义以及最低成功证据。任务 `validation` 统一为 `verify-at-execution`，保留 `required` 要求并使用 `retention=ephemeral`；结果在当次对话报告，不读取旧验收文件。导出成功不证明运行消费成功，请求或 serialID 创建不证明行为完成，编译不证明 Play/真机/后台成功。必要绑定或产物缺失时必须保留未完成状态。

## 维护检查与完整验收

```sh
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . index --json
python3 -B Assets/Framework/Agents/Tools/nova_capabilities.py --root . check --json
```

`index` 使用 .NET SDK/Roslyn 只读解析当前 C#/Python，临时编译器自动清场，开发快照只写 `Library/Nova/AgentIndex/api-index.jsonl`。完整 AST/XML/调用片段不写第二份长期索引。发布器在独立副本脱敏后生成 `Agents/api-index.jsonl`，以实际分发字节计算指纹和位置；不以私有原文的摘要续签公开副本。

开发/发布入口可显式使用 `check --rebuild --json`，临时解析当前源并在内存检查，退出清理编译器和定位临时文件；首次 Git 检出不必先生成永久 Library 快照。消费项目使用随包快照，不使用 rebuild。

`check` 是显式全量入口，核当前源与输入的新增/删除/字节指纹、文档引用、任务映射、Skill 与 Action 契约。它不标记任何函数 `reviewed`，不维护历史审查完成率，也不代表 Unity、设备或外部验收。任务的确认、写集、锁、幂等、恢复与最低证据要求仍由既有执行契约控制。

### 变更只复核实际影响

修改说明、导航或路径时，修正链接并重建定位索引即可，不需要重新审核全部历史函数。整文件指纹只防止旧偏移读取新源码，不是语义失效判断。

改变行为、参数、权限、失败或恢复方式时，审查实际受影响的函数、共享常量/基类/依赖及跨文件调用方；把新事实更新到相邻 XML、已有模块 Docs 和必要执行契约。不再创建函数九维 JSON、变更桥链或历史审查摘要。自动生成、链接存在及指纹匹配都不能替代这次代码审查。

当次验证在对话中报告，临时材料和未完成事务的恢复状态按 [执行与清场约束](../START_HERE.md#执行任务与清场的共同约束)分别处理。
