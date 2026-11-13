# 资源工作流

Nova 推荐业务资源采用 YooAsset/Bundle，但资源检查必须先识别所有权和使用场景，不能把所有 `Resources` 目录当成错误。

## 先分类，再决定加载方式

| 资源类型 | 常见加载方式 | 默认处理 |
|---|---|---|
| Nova 业务资源，需要版本、热更或可控卸载 | `Nova.Asset` / YooAsset Handle | 推荐路径；持有并正确释放 Handle |
| `Resources/BuiltIn/**` 内置资源 | `Resources.Load` 等 Resources 接口 | 合法的特殊用途 |
| UPM 包内第三方插件 Resources | 插件自己的 `Resources.Load` 或封装 API | 合法，不要求 Contract 白名单 |
| 导入到 `Assets/**` 的第三方插件 Resources | 插件自己的 `Resources.Load` 或封装 API | 合法，不要求 Contract 白名单 |
| 项目业务 Resources | Unity Resources 接口 | 可以运行；在 Nova 托管业务范围内通常给 Warning 并建议评估 Bundle |
| 视频、数据库、证书、离线表等原始文件 | 项目明确选择的 StreamingAssets/文件方案 | 按平台和发布需求评估，不作一刀切禁止 |

第三方插件包括其自带的 Demo、Editor 支撑资源和运行时资源。只要内容属于插件自身，位于 UPM 或 `Assets/**` 都不因使用 Resources 而非法，也不需要逐项加入 Nova 白名单。

## Nova 业务资源

需要下载、版本管理、增量更新、依赖追踪或确定卸载时，优先使用 YooAsset/Bundle：

1. 通过 Collector 把业务资源纳入对应 Package。
2. 构建 Bundle，并验证运行目标使用的 Package 与地址。
3. 通过 `Nova.Asset` 加载，保留返回的 Handle。
4. 在所有者生命周期结束时释放 Handle；场景 Handle 使用 `UnloadAsync()`。

API 细节见 [AssetComponent](../Runtime/Modules/Asset/AssetComponent.md)，Bundle 构建见 [EditorUtil.BundleBuilder](../Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)。

## 配表模板与样式

Nova 配表的业务内容可以扩展，原有视觉风格和工作簿结构应保留。优先沿用当前项目已确认的同类表；新项目从当前已安装 Framework 的 [Templates/](../../Templates/) 或正式 MainDemo Sample 选择字段一致的原表。UI、Sound 等模板各有自己的样式，不能把所有模块统一成另一套配色，也不能让通用表格 Skill 的默认美化覆盖现有格式。复制 Sample 后须移除示例业务行，不能把示例资源或地址当产品数据。

此约束覆盖全部配表和未来新增表型；工具支持范围不能缩小约束范围。下表区分物料复制与后续业务编辑，复制成功不代表示例数据已变成产品数据，也不代表已完成导出或接入。

| 表型 | 模板来源与保真入口 | 后续业务内容 |
| --- | --- | --- |
| Table List / Map / One | `mode=copy`，`table-list` / `table-map` / `table-one` | Table Skill 的真实 Schema、主键及源编辑/字段追加，不强加 Name |
| 本地化文本 / 字体 | `mode=copy`，`localization-text` / `localization-font`；原多 Sheet、合并格及字体字段完整保留 | Localization Skill；Key/语言追加沿专门分支，字体按 Language + Mark，不套 UI 行模型 |
| 网络 HostKeys / NetCmds | `mode=copy`，`network-hostkeys` / `network-cmds`；Debug/Release 页原样保留 | Network Skill；不改写环境页名，不因复制表源发起网络请求 |
| Emphasis / Custom 振动 | `mode=copy`，`vibration-emphasis` / `vibration-custom` | Vibration Skill；同 Name 多行组合及 Order 按领域规则保留，不套 UI 唯一 Name 校验 |
| UI / Sound | `mode=copy` 可原样复制；或使用下文已支持的完整行填入模式 | UI/Sound 各自登记与正式导出 |
| Tracks | 所属源表与 [Network 视觉模板](../TRACKS.md#network-视觉模板)，复用 Tracks 源编辑及汇总入口 | 合并、分组、事件和属性按 Tracks 专用规则；不套 UI/Sound 模型 |
| SDK / Kit 专用表、项目自定义表 | 当前已安装包自己的模板或项目已确认同类源，沿已有领域入口复制、定向编辑并验收保真 | 保留各模块 Schema/导入语义；不在 Framework 工具伪造未支持的模板 ID，缺入口时停止相关写入 |

### 官方模板原样复制

[nova_excel_template.py](../../Agents/Tools/nova_excel_template.py) 的 `mode=copy` 支持 Framework [Templates/](../../Templates/) 现有全部11个模板和两份 MainDemo UI/Sound 原表。通过 `inspect --mode copy --template <kind>` 读取实际来源 SHA 与完整 Sheet 名；Plan、Execute、Verify 与下文使用同一流程。请求精确为：

```json
{
  "mode": "copy",
  "template": "localization-text",
  "target": "Assets/Game/Excels/Localizations/Texts.xlsx",
  "targetHash": null,
  "sourceRoots": ["Assets/Game/Excels"],
  "generatedRoots": ["Assets/Game/Data", "Assets/Game/Runtime/Generated"]
}
```

此模式逐字节复制完整工作簿，Verify 核对输出与冻结模板完全一致；不接收 rows、sheetName 或 title，不重命名页签、不改示例值，也不解释唯一键、合并和多页业务语义。Table、Localization、Network、Vibration 的既有 Skill 通过 `source-template-copy` 分支完成这一步，不要求 Unity 或完整业务接入的前置。随后必须用该领域支持的入口替换示例内容并沿用原格式；缺少安全编辑能力时保留准确缺口，不能回到空白建表。模板目录新增 `.xlsx` 时，同轮补充明确白名单、既有 Skill 路由和逐字节复制测试；未知表型不默认放行。

填内容前冻结实际来源、Sheet、字段和类型、目标源目录、生成目录、数据行及目标现存摘要。保留颜色、字体/字号、边框、对齐、数字格式、行高/列宽、隐藏与冻结状态，以及备注页、批注、校验和关联资源；新增行列从对应数据行或同类字段继承样式。不重新创建空白工作簿，不以整书 import/save 代替原位修改；确需换样式时须有明确重新设计要求。模板字段与业务不一致时换用匹配来源或按领域契约扩展，不能默加字段、填示例值、删除列或改变 Sheet 名导致生成类型漂移。

### UI / Sound 官方表克隆

随包 [Agents/Tools/nova_excel_template.py](../../Agents/Tools/nova_excel_template.py) 使用 Python 标准库，复用已有表源写锁、路径边界和 ZIP 原记录替换能力。UI、Sound Skill 的 `source-from-template` 分支可单独完成表源创建，不要求启动 Unity。业务行填入模式（默认 `mode=rows`，旧请求可省略）仍限定四个固定来源：`ui` / `sound` 对应 [Templates/UITemplate.xlsx](../../Templates/UITemplate.xlsx) / [SoundTemplate.xlsx](../../Templates/SoundTemplate.xlsx)；`main-demo-ui` / `main-demo-sound` 对应 [MainDemo Sample 声明](../../package.json) 下的 `Excels/UIs/UIs.xlsx` / `Excels/Sounds/Sounds.xlsx`（开发态读取同仓正式 MainDemo）。其他模板只支持上述原样复制，不直接填行。工具不会接收任意模板路径，也不修改来源或包缓存。

1. `python3 -B <frameworkRoot>/Agents/Tools/nova_excel_template.py --project-root <projectRoot> inspect --template <kind>` 只读取得真实字段、类型及模板 SHA。不能假定两套官方 Sound 表字段相同。
2. `plan --request - --plan Library/Nova/AgentActions/TableSource/<task>-plan.json` 从 stdin 接收下述请求，冻结实际模板 SHA、目标存在性/原 SHA、完整输入和预期输出 SHA；核对计划后，已有精确授权即可执行。
3. `execute --plan <planRelative> --confirmation-token <planId> --receipt Library/Nova/AgentActions/TableSource/<task>-receipt.json` 与既有源编辑共用单写者锁，一次性消费计划并原子保存。替换已有文件必须明确提供当前整文件 SHA；这属于整表替换，不能用来执行普通叶子修改或覆盖未知改动。
4. `verify --receipt <receiptRelative>` 只读重建预期输出并核对整文件。模板或目标漂移时停止；写入开始后失败为 `partial`，保留状态先 Verify，不重放 Execute。结束且无需恢复后清理本轮计划/回执与预览；不留下历史验收档案。Windows 使用 `py -3 -B` 执行同一工具。

以下仅为请求形状；路径、字段和值必须取自当前项目与 inspect，不能照抄到其他工程：

```json
{
  "template": "ui",
  "target": "Assets/Game/Excels/UIs/UIs.xlsx",
  "targetHash": null,
  "sourceRoots": ["Assets/Game/Excels"],
  "generatedRoots": ["Assets/Game/Data", "Assets/Game/Runtime/Generated"],
  "sheetName": "UIs",
  "title": "游戏界面注册",
  "rows": [{"Name":"MainView","Desc":"主界面","AssetLocation":"MainView","UIGroupName":"Game","PauseCoveredUIView":false,"InObjectPools":false}]
}
```

`targetHash=null` 仅用于不存在的新文件；已有源必须传完整 SHA256。在行填入模式中，`title` 可省略，仅改变 B1 内容。rows 非空、Name 唯一且每行必须显式提供该模板的全部字段和准确 JSON 类型，总计不超过8192业务格；不猜默认值、不改变 Schema，不支持公式或复杂字段扩展。工具改写业务 Sheet 的值、范围及必要名称，本次业务行统一继承第5行的行/格格式，原示例业务值清空；其他 ZIP 原压缩记录（包括样式、主题、备注页及批注）保留。普通已有格编辑或 Table 字段追加继续使用 [nova_table_source.py](../../Agents/Tools/nova_table_source.py)；原样复制模式不使用本段的唯一 Name/行填入规则。

### 保存后的验收

验收同时核对业务数据和工作簿外观：来源与输出的样式、主题和非目标 ZIP 部件保持一致，元数据行、列属性、范围外空白行和备注保留，本次业务行使用模板第5行格式；渲染实际改动区域与来源对照。整书能打开、字段可读或正式导出成功均不能替代此检查。通用表格引擎可只读渲染，不能将预览重新保存覆盖权威表。已有表只有格式修复且字段、Sheet、类型和值完全一致时，核对生成物未变即可；有数据或结构变化则继续对应模块正式导出、编译与实际消费验证，不手改 JSON/C#。

## Sample 的 UI 表源与导出

Sample 声明、实际导入根及清单按 [Sample 导入定位规则](PROJECT_STRUCTURE.md#sample-的导入辅助工具与页面边界) 核对；本节的表源路径相对于该根，不代表消费项目的固定 Assets 路径。

各 Demo 的 `Excels/UIs/UIs.xlsx` 是 UI 注册数据的权威来源。单主题 Demo 应只保留自身入口视图，`Name` 对应 UIView 类型名，`AssetLocation` 对应本 Demo Collector 收集的 Prefab 地址，`UIGroupName` 对应场景中的 UI 分组。复制 MainDemo 后仅替换路径并不足够，还必须替换 Excel 中的导航与模块演示条目。

修复 UI 注册缺失时，先修改 Excel，再通过 [UI Exporter](../Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md) 导出 JSON/Binary 和 C#；核对场景 UISettings、生成表属性、Prefab 与业务泛型入口一致。只修改 JSON 会在下次导出时被旧表源覆盖。AppsflyerDemo 的入口为 `DemoAppsflyerView`，数据地址为 `UIs`，UI 分组为 `Demo`。

## 新项目默认固定启动资源

新建 Nova 项目默认采用 [当前 Framework 声明的 MainDemo Sample](../../package.json) 固定启动资源作为可运行基线，实际导入根按 [Sample 导入定位规则](PROJECT_STRUCTURE.md#sample-的导入辅助工具与页面边界) 确定。开发态来源为 `Assets/Samples/MainDemo/Resources/BuiltIn/**`；消费项目从实际安装版本的 MainDemo Sample 经正式导入取得，再通过 Unity 资产工具引入项目自有目录。不要依赖开发机路径、修改包缓存，或要求产品长期保留整套 Demo。canonical [Nova.prefab](../../Prefabs/Nova.prefab) 虽已填入路径，Framework 自身的 Resources 当前主要是 Debug 资源，并不包含这套启动 UI 实物。

来源尚未导入时，先单独冻结官方 Sample 导入的精确目录、文件、编译和自动导入辅助工具的写入/弹窗副作用，按现有建项授权执行并核验，再进入资源复制。三面板复制的写集不能暗含整套 Sample 导入；无法冻结导入范围时明确保留来源缺口，不能运行 Sample 专用自动配置去覆盖现有工程。导入辅助工具边界见 [项目与场景结构](PROJECT_STRUCTURE.md#sample-的导入辅助工具与页面边界)。

| ProcedureComponent 中的 LauncherSettings 字段 | 默认 Resources 键 |
| --- | --- |
| `SplashPanelPrefab` | `BuiltIn/Prefabs/LauncherSplashPanel` |
| `ProgressPanelPrefab` | `BuiltIn/Prefabs/LauncherProgressPanel` |
| `DialogPanelPrefab` | `BuiltIn/Prefabs/LauncherDialogPanel` |
| `LocalizationJsonPathTemplate` | `BuiltIn/Jsons/LocalizationTexts_{0}` |

准备时核对三面板 Prefab、必要的 Launcher 组件与序列化绑定、字体/材质、闪屏贴图和本次所需启动语言 JSON。MainDemo 当前含 `LocalizationTexts_ChineseSimplified.json` 和 `LocalizationTexts_English.json`；它们服务 `LauncherLocalization`，不能代替正式业务 Localization 表及其初始化。素材可以替换为项目占位物料，但必须实际导入、保存并验证，不只填写空路径。

通过 Unity 检查资源依赖并复制/引用，读回依赖仍指向哪里；复制 Prefab 不自动证明字体、材质、贴图与脚本归属已独立。默认键可以保留；多个 Sample 已导入时可能存在相同 `BuiltIn/...` 键，必须核对有效 Resources 闭包的全局键唯一性。必要时采用项目唯一的 Resources 子路径并同步修改项目自有 `ProcedureComponent.LauncherSettings`。仅把文件移到另一个外层目录，不会改变 Resources 相对键。

启动面板及其脚本、文本、字体、贴图、解密所需代码必须在业务 DLL 和业务 Bundle 加载前就可用，不反向依赖后续预加载。固定启动资源继续走 Resources；业务内容仍按实际资源方案管理，不能因此把所有业务物料搬进 Resources。组件字段和加载语义见 [LauncherSettings](../Runtime/Modules/Procedure/LauncherSettings.md) 与 [LauncherUIController](../Runtime/Modules/Procedure/LauncherUIController.md)。

验收须沿真实启动入口检查闪屏、进度与必要提示面板、目标语言文本以及引用依赖；缺面板可能只产生日志并返回 null，流程继续并不表示资源完整。新增项目的最小启动闭环和准备顺序见 [新项目开工准备](PROJECT_STRUCTURE.md#新项目开工准备)。

## Resources 规则的边界

- `Resources/BuiltIn/**` 可以正常通过 `Resources.Load`、`Resources.LoadAsync` 等接口加载。
- UPM 与 `Assets/**` 下第三方插件自己的 Resources 和加载代码合法。
- 不扫描未声明的整个 `Assets` 并据此阻断 Play 或 Build。
- 对无法确定归属的目录先报告信息或 Warning，由 Agent 向用户核实。
- 业务代码直接使用 `Resources.Load` 不等于项目损坏；它可能失去版本、依赖和释放能力，因此更适合作为迁移建议。
- 已有自定义 Addressables、StreamingAssets 或文件系统方案不应被静默改写。

Agent 检查资源时只覆盖任务声明的业务范围；插件和 BuiltIn 的合法性不依赖 Contract 存在。

## 构建前检查

普通开发构建确认当前资源能在目标平台加载即可。发布任务再按项目声明的目标补足 Bundle、代表资源或真实 Player 运行证据。Pipify 可以编排这些步骤，但不是资源构建或 Player 构建的唯一合法入口。
