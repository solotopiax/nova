# Nova Agent 快速入口

这是一份给 Agent 的 Level 0 路由。首次接触 Nova、接手已有项目、调整场景或资源流程时，先用本页建立最低限度的正确上下文，再按任务进入 Level 1 页面和现有模块文档。

## AI Project Skills 一次性初始化

Nova Project Skills 随 Framework UPM 包发布，但包不能隐式写入消费项目的 `.agents/skills`。首次让 Agent 通过自然语言使用 Nova 项目组 Skill 时，从本 Framework 包根目录先预览所需 Profile：

```bash
python3 Agents/Tools/nova_skills.py sync --project-root <消费项目根目录> --profile core --dry-run
```

确认输出的 Profile 与副作用后，再去掉 `--dry-run` 创建受管投影。随后 Agent 可发现 `nova-project-router`，由它路由到相应 Operation 或 Workflow。需要 UI 能力时选择 `ui` 或 `p0` Profile；Profile 收窄不会自动删除已投影 Skill。

这是明确、可审计的一次性初始化，不是编码工作，也不应由 UPM 包在未确认时偷偷写入项目。若产品要求“安装包后无需任何初始化即可触发”，需要由项目模板或 Agent Host 预置 bootstrap；该宿主集成不属于 Framework UPM 包本身。

## 先检查项目，不要套模板

不要假定当前项目来自 Starter，也不要假定它必须采用固定目录、固定场景数或固定资源拓扑。先检查：

1. 当前启动场景和 Build Settings 中实际参与构建的场景。
2. 场景中 Nova 根节点来自哪个 Prefab，是否存在重复或手工拼装的残缺根节点。
3. 项目当前使用的资源系统、配置入口、构建入口和第三方插件。
4. 哪些目录与场景由项目明确交给 Nova 管理；不要接管未声明范围。
5. 用户本次要解决的具体任务，再决定需要阅读哪些模块文档。

若一个场景负责承载 Nova，必须使用框架提供的完整 canonical Prefab：`Assets/Framework/Prefabs/Nova.prefab`。不要新建空 GameObject 后只挂 `Nova`、`AssetComponent` 或少数组件来仿造它。

## 三层约束

### Hard Error：最小、确定的结构错误

Hard Error 只用于能确定破坏 Nova 运行完整性的情况，例如 Nova 托管入口使用残缺或错误的根 Prefab、同一运行拓扑出现多个 Nova、已明确声明为 Content 的场景又包含 Nova，或必需的序列化引用已经损坏。Hard Error 可以阻止 Play；发布流程也可显式选择把它作为门禁，但 ProjectGuard 不通过全局预处理器阻断 Unity Build。

### Warning：建议与可疑偏离

Warning 用于尚不能判定非法的情况，例如业务代码直接使用 `Resources.Load`、场景未分类、没有自动化流水线，或项目采用了与 Sample 不同的目录和加载方式。Warning 用来提示 Agent 向用户核实，不阻止日常 Play 与 Unity Build。

### Release Strict：项目显式启用的发布验收

Release Strict 只在项目或发布流程显式启用时检查 Bundle、Smoke、证据或项目自定义发布条件。它不是所有 Nova 项目的默认开发门禁，也不应自动施加到普通 Play、Unity Build 或开发 CI。

Contract、Pipify、representative asset 和分目录 `AGENTS.md` 都可以帮助特定项目形成更强的自动化，但不是 Nova 普遍必需条件。存在 Contract 时，只验证项目主动声明的 Nova 托管范围。

## 场景与资源底线

- 单场景、自定义启动链和多场景都可以；拓扑按项目实际需求选择。
- 多场景项目可以 Additive 加载 Content；Nova 不会自动加载 Content，加载时机和卸载生命周期由项目明确实现。
- `Resources/BuiltIn/**` 是框架的特殊内置资源区，允许通过 Resources 接口加载。
- UPM 包以及导入到 `Assets/**` 的第三方插件可以保留并使用自身的 Resources，不要求 Nova Contract 白名单。
- Nova 业务资源推荐使用 YooAsset/Bundle，以获得版本、卸载和发布能力；这个建议不能据此否定插件或未声明范围内的合法资源方案。
- Unity Build、`BuildPipeline.BuildPlayer` 和 `EditorUtil.Build` 都是合法构建入口；Pipify 是可选自动化入口，不是唯一入口。

## 按任务继续阅读

- [项目与场景结构](Onboarding/PROJECT_STRUCTURE.md)：识别 Nova 根、单/多场景与 Content 边界。
- [资源工作流](Onboarding/RESOURCE_WORKFLOW.md)：选择 YooAsset、BuiltIn Resources 或插件自有 Resources。
- [验证与构建](Onboarding/VALIDATION.md)：理解 Hard Error、Warning、Release Strict 以及直接 Unity Build。
- [框架文档索引](INDEX.md)：进入 Runtime、Editor 与具体模块事实。
- [架构总览](ARCHITECTURE.md)：理解 Component + Manager 分层。

## Agent 开工检查

- 已检查现有工程事实，没有把 Sample 或 Starter 当成强制模板。
- 承载 Nova 的场景引用完整的 `Assets/Framework/Prefabs/Nova.prefab`。
- 已确认运行拓扑中 Nova 的唯一性，以及 Content 的显式加载和卸载责任。
- 已区分业务资源、`Resources/BuiltIn` 与第三方插件 Resources。
- 已选择与任务相称的验证层级，没有把 Release Strict 强加给日常开发。
