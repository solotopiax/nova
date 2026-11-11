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

## Sample 的 UI 表源与导出

各 Demo 的 `Excels/UIs/UIs.xlsx` 是 UI 注册数据的权威来源。单主题 Demo 应只保留自身入口视图，`Name` 对应 UIView 类型名，`AssetLocation` 对应本 Demo Collector 收集的 Prefab 地址，`UIGroupName` 对应场景中的 UI 分组。复制 MainDemo 后仅替换路径并不足够，还必须替换 Excel 中的导航与模块演示条目。

修复 UI 注册缺失时，先修改 Excel，再通过 [UI Exporter](../Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md) 导出 JSON/Binary 和 C#；核对场景 UISettings、生成表属性、Prefab 与业务泛型入口一致。只修改 JSON 会在下次导出时被旧表源覆盖。AppsflyerDemo 的入口为 `DemoAppsflyerView`，数据地址为 `UIs`，UI 分组为 `Demo`。

## 新项目默认固定启动资源

新建 Nova 项目默认采用 MainDemo 的固定启动资源作为可运行基线。开发态来源为 `Assets/Samples/MainDemo/Resources/BuiltIn/**`；消费项目从实际安装版本的 MainDemo Sample 经正式导入取得，再通过 Unity 资产工具引入项目自有目录。不要依赖开发机路径、修改包缓存，或要求产品长期保留整套 Demo。canonical `Nova.prefab` 虽已填入路径，Framework 自身的 Resources 当前主要是 Debug 资源，并不包含这套启动 UI 实物。

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
