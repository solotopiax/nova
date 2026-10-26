# 本地化表源、设置与绑定

已有非敏感译文标量复用[共享表源 CLI](../../nova-project-integrate-table/references/source-edit.md)。先 inspect 实际源、Sheet、字段、Name 键及语言列，冻结 sourceHash、完整源/生成根、beforeValue 和真实 string 类型，再 plan/execute/verify。该标量分支保留原限制，不增删 Key、语言列、FontMark、行页、公式或格式。

用户明确追加 Key 或语言时，复用同 `nova_table_source.py` 的 `inspect --layout localization-texts` 和原 plan/execute/verify，读取随包[专用请求与边界](../../../../Docs/Onboarding/LOCALIZATION_SOURCE_EDIT.md)。严格请求含 `source/sourceHash/sourceRoots/generatedRoots/layout`，并有 `addLanguages` 或 `addKeys`。语言来自当前 `NovaFramework.Runtime.Language`，Plan 冻结随包 `Language.cs` SHA；新增语言须覆盖工作簿全部有效 Sheet 和各原 Key 的显式译文，新 Key 必须包含全部最终语言。同计划可同时追加语言与 Key；合计1–256项、最多8192格。只追加 Key 行和最右字段后的相邻空白物理语言列，不移动、删改名或重排已有结构，不修改公式、FontMark、字体或其他 ZIP 成员。

每个源文件成功后，原 Operation 核对全部有效 Localization Unit 的最终语言集合；再调用 `nova.project.localization.export` 的完整文本/类型/支持语言输出并验证 Config、资源与绑定。字体含复杂数据、其他结构变化或表头不兼容时使用项目已确认的专门源编辑器，缺入口明确阻断。`.xls/.xlsm`、宏、删除或改名不扩入此分支；源结构 Verify 不证明运行显示。

正式导出继续复用同 Action；源变更后必须新 Plan，不能沿用变更前的来源快照。`target=text/font`、`scope=all/code` 可使用本 Skill 的 `export-code-data` profile 验收导出，但本页请求含源/绑定/显示更新时，仍按完整更新的原 Play 门交付；不把源或 UI 工作悄悄截成导出任务。代码分支由 Agent 自动读取并冻结 persisted Master 的 GUID/path/原字节和顶层 Namespace、实际来源/模板工具与完整写集；必要现有数据由当前 Plan 核，不要求用户手填配置。

新来源契约 Receipt 严核来源、生成配置、正式产物与删除项，代码另核本次编译与生成类型身份；旧摘要 Receipt 即使 success 也只保留历史范围证据和警告。`text/font` 的所有 scope（含 data）都会写持久配置，因此当前 Action 均严格冻结 Master/Globals/GUID/path/顶层 Namespace；data 不属于低门纯代码 profile。`languages` 仅 `scope=data`，只发布语言数据与临时配置，不要求 Master、不验代码 Namespace，但当前底层仍读取 `RuntimeProvider.GetNamespace()`，不能声称从不读取 Namespace。`sourceContractVersion=1` 的显式 null/版本损坏或字段缺失不能降为旧摘要；完整字段以当前 Receipt 为准。此范围与实际快照按匹配版本 Action/Exporter 核，不套用 Table 独立 conf 的规则。

组件设置与节点保存遵循[共享 Unity MCP](../../nova-project-edit-settings/references/unity-mcp-editing.md)，批量节点与字段细节复用[本地化绑定](../../nova-project-update-ui-view/references/localization-binding.md)。普通自有 Prefab 已有字段优先 `settings.inspect-target/patch-target`；场景、复杂列表、变体/嵌套或增组件使用当前 Provider 的真实 `manage_components/ manage_prefabs` Schema。配置先完整读取 `m_LocalizationSettings` 中的单元与格式/路径，保留未请求的项；不对缺失聚合字段做默认重建。

当前 `TextLocalizing` 的持久字段是 `m_LocalizingKeyName`、`m_LocalizingFontMark`，支持 `TextMeshProUGUI`。对已存在组件省略 `components_to_add`，无组件时先确认 TMP、唯一节点与真实 Key/Mark，显式添加并设置两个字段；不依赖自动挂载钩子一定触发或把默认 Main 当现存值。用完整 `prefab_path/target/component_properties` 精确保存同一 Prefab；场景按当前组件 ID/索引写入后只保存目标活动场景。相同值不写，不全工程 FixMissing，不把运行时 SetKeyName/SetFontMark 当持久绑定。

匹配当前版本时，`Awake` 只处理本地 TMP 渲染；首次 `Start` 等一次下一帧末尾后再取当前 Manager、确保唯一订阅并刷新，以避开 `EventComponent.Start` 重建事件池。等待中禁用不补订阅，销毁取消等待；后续启用立即恢复，注销前核真实池而不信旧订阅标记。新建动态实例也有该一次等待；框架重启后的重新启用会改用当前 Manager。不要复制每节点全局监听或用组件开关作为正常启动的必要步骤；持续启用中主动重建公共事件池仍由重建调用方安排生命周期恢复。

语言来源、自动字体与字段用途从匹配版本 Docs/源码核实：字体需 AutoFontAdapt=true、非空且 Ordinal 匹配的 Mark、当前语言有效字体行及可加载 AssetLocation，不能只看译文。文本、字体、支持语言列表保持同一格式；更改格式/回退/语言集合是额外语义，不随单元格值修改自动执行。

运行时文本按 `Nova.Localization.GetText(name)` 读取文本表的 `Name`；字体表实际键是 `Language + Mark`，不新增 `Name` 字段。授权业务源码可调用 `Nova.Localization.GetFontData(mark)` 查当前语言，或 `GetFontData(language, mark)` 查指定语言；返回首个 Ordinal 精确匹配的原始配置行，空标记或未命中返回 null，无隐式语言/标记回退。该查询不加载字体资源，也不代替 `TextLocalizing` 持久绑定或真实显示验收；既有 `GetFontDatas(language)` 列表 API 保留。

验收通过已保存的业务入口。全新 Play 中不手动切换静态绑定组件，先等待首次启动门完成；既有启动 Owner 完成 Load 与 InitCurrentLanguage 后，复用业务切换方法；需要补观察点时只修改授权的项目 UIView/启动源码，保留刷新与取消生命周期，不执行私有方法或临时任意代码。用户请求的完整冻结语言集合中逐一检查同一实际 TMP 的文字、回退、方向、必要字体与布局，并检查启停页面无重复监听；动态计数/输入节点保留原拥有者。请求多语言时只验部分语言、编译、静态 GetText、截图、先开关组件才通过和初始化返回都不足以证明显示完成。

报告分别给源 CLI、持久配置/绑定、正式导出、编译与逐一覆盖请求语言的 Play 证据。恢复本轮语言偏好和测试态，删除临时探针及文件；正式用户译文与绑定保留，未请求部分不改。

验证集合来自当前任务：明确单语言只验该项；明确三种或更多时逐项覆盖，不以两种通过代替全体。任务要求切换但未给集合时，由现有基准和目标冻结最少两种；不修改或扫描未请求语言。

仅代码生成的额外输入按 Action 读取范围冻结：字体需要现有正式数据；JSON 文本需要源模型首语言的现有正式数据用于 Map。文本代码分支还验支持语言代码清单与三类型身份；真实模板和 Luban 工具成员复用共享快照，持久配置在 Sync 前是输入、生成后是产物，不能用旧配置 Hash 拒绝合法改写。所有写入仍以 live Plan 为准，不从说明拼第二份目录清单。
