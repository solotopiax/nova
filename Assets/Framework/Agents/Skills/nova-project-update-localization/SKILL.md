---
name: nova-project-update-localization
description: Use when 项目组要正式导出已有本地化文本或字体的 all/code 产物，或定向更新文本、追加 Key 或语言、调整字体或 TextLocalizing 绑定并验证显示时使用。
---

# Nova 更新本地化

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。确定性导出先通过 `nova_project_action` 对 `nova.project.localization.export` 执行 `describe`；只有 Action 报告配置问题时才读对应 Exporter 文档。仅在确认数据源、格式或导出位置时读 `Docs/Runtime/Modules/Localization/LocalizationSettings.md` 和 `Docs/Editor/Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md`；运行验证才读 `Docs/Runtime/Modules/Localization/LocalizationComponent.md`、`Docs/Runtime/Modules/Localization/LocalizationManager.md` 与 `Docs/Runtime/Modules/Localization/TextLocalizing.md`。不要递归加载全部 Localization、Luban 或 UI 文档。

修改已有译文标量、追加 Key 或语言、组件设置或绑定时，读取[表源、设置与绑定](references/source-and-binding.md)。表源复用现有 `nova_table_source.py`；仅追加 Key/语言时按该页读取 `Docs/Onboarding/LOCALIZATION_SOURCE_EDIT.md` 的 `layout=localization-texts` 分支。配置和 TMP 节点复用现有受控 settings 或共享 Unity MCP；需要更详细的 Prefab/场景保存规则时只读共享 reference。

授权业务源码读取单条字体配置时，复用公开 `Nova.Localization.GetFontData(mark)` 或 `GetFontData(language, mark)`，按真实 `Language + Mark` 查询，不要求字体表补 `Name`；匹配、null、资源与显示边界见上述 reference 和匹配版本 Docs，不把查询当作字体已加载。

## 仅导出已有文本或字体的数据和代码

用户仅要求正式导出已有文本/字体的 `all` / `code` 产物时，选 `taskProfiles[id=export-code-data]`，对应 `localization.export`；只允许 `target=text/font` 与 `scope=all/code`。冻结项目根、已保存活动场景、唯一 `Nova` / `LocalizationComponent`、现有 Settings、实际 Unit/源、数据格式、正式路径及当前 Action 完整写集；自动取得 `localizationExportSettingsSourcesTargetScopeAndWriteSet`，不要求 UI 绑定、业务 Owner 或 Play 语言集合。单独 `data` 和 `languages` 不属于此 profile，不能借它降低原验收要求。

自动读取 `activeConfigMasterAndGeneratedNamespace`：核 `ProjectSettings/Nova/Globals.json` 的 `configMasterGuid` / `configMasterPathHint`、实际可加载 `ConfigMasterSO` 身份与原文件摘要，冻结顶层 `Namespace` 及预期生成类型全名。该来源不是 `ConfigRuntimeSO.Namespace` 或 `DimensionalResolver.ResolveNamespace`；未绑定、身份或值漂移时 `blocked`，不按场景或第一个 Master 补猜。已绑定且明确空值可生成 global namespace。按需读 [TextExporter](../../../Docs/Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md) 或 [FontExporter](../../../Docs/Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md)；来源、实际模板/工具、代码所需现有数据及可重建配置的细目以 live Plan 为准，不建立第二目录清单。

复用 `nova.project.localization.export` 的 live `describe → plan → execute → verify`。Plan 冻结来源、Namespace 与完整 `writeSet`；Execute 在写入前复核，写入开始后或重载导致 `partial` 不重放。当前宿主若未提供对应冻结或复核门，代码导出 `blocked`，不假称待发布防护已可用。完整更新全链输入留在下方分支，本分支只冻结 `profile.requiredInputs` 与 Action `PlanId/writeSet`，危险副作用原确认门、禁止范围、锁和幂等均继承。

新 `sourceContractVersion=1` Receipt 必须严格核输入、配置、完整输出/删除与 SHA；显式 null、错误版本或缺项不能降为旧收据。`scope=code` 的字体会读取现有正式数据，JSON 文本会读取源模型首语言的现有正式数据生成 Map，来源随 Plan 冻结；Sync 后允许重建的配置按新产物验收，不用旧配置 Hash 阻断合法生成。等待本次编译和脚本重载后，以 Core `recovery_token` 只读 Verify，核本次编译证据、生成命名空间、类型全名及既有消费者一致。旧 Receipt 保持原摘要验证结果与警告，旧摘要 `success` 不能当作新来源/Namespace/本次编译与类型的完整验收。完整产物、compile/type、范围外状态与清场均通过才报告“所选文本/字体数据和代码导出成功”；缺证据仍 `partial`，不承诺实际文字显示、语言切换或字体适配。本分支验收后结束，下面用于完整本地化更新。

`target=text/font` 的 `scope=data` 同样写持久 Luban 配置，因此当前 Action 也要求真实 Master/Namespace；它不属于此 profile。`target=languages` 仅支持 `scope=data`，使用临时配置，不要求 Master、不验代码类型，也不证明运行时加载；当前底层仍可读取 `RuntimeProvider.GetNamespace()`，不能表述成从不读取命名空间。

## 完整更新的冻结输入与决策门

冻结已有 `LocalizationComponent`、目标文本 Key / 字体 Mark、语言集合、源文件与行范围、既有 `TextLocalizing` 绑定、数据格式、输出位置，以及可观察的目标 UI 和用户请求的完整验证语言集合。文本、字体和支持语言列表是不同数据链，不能因名称相近而混用。

- 已有标量使用共享表源 CLI；已授权的 Key/语言追加使用同 CLI 的专用分支，冻结实际源 SHA、随包真实 `NovaFramework.Runtime.Language` 声明及全部有效 Sheet 的语言集合。字体和其他结构只走项目已确认的源入口；缺入口返回 `blocked`。
- 追加 Key/语言需冻结完整最终译文与写集，保留原行列、公式与非目标 ZIP 内容；删除/改名、FontMark、数据格式、回退规则或输出路径使用各自已授权入口。已有同范围授权不重复询问；值的定向改动不默认扩大语言或全项目范围。
- 只更新已确认的 `TextLocalizing` / TMP 绑定。所有 Prefab、Scene、组件和序列化引用写入必须经 Unity Editor 自动化通道；不得手写 YAML，也不得调用全工程的“修复缺失 TextLocalizing”作为默认动作。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的文本、字体源与单元设置 | 同表源 CLI 的已有标量或 `localization-texts` Key/语言追加分支；其他结构用项目专门源编辑器；组件设置复用 settings/共享 Unity MCP | 指定源数据与 LocalizationSettings 配置 | 源 SHA 与真实语言枚举冻结，新增语言全有效 Sheet/原 Key 覆盖、新 Key 含全部最终语言；源成功后仍需正式导出与运行验证 |
| 文本、语言列表或字体的导出范围 | `nova.project.localization.export` | 选定的文本、字体、类型和支持语言正式产物 | Action Verify 成功；不手改生成物 |
| 已确认的既有 UI 绑定与验证语言 | 已声明 `manage_prefabs/ manage_components` 更新并精确保存绑定；业务现有语言入口或授权业务源码复用公开切换 API | 正确 Key / FontMark 绑定的 TMP 显示 | 独立持久绑定读回，Unity 编译通过，Play Mode 逐一验证冻结语言集合的目标文本；字体另验 |

文本仅改值且语言集合、类型和 Key 结构未变时，只导出所需数据；新增 Key、语言或类型输出变化时才通过 `nova.project.localization.export` 选择对应完整导出。完整文本导出会同时处理支持语言列表；不要把单独的语言列表范围当成文本更新的默认替代，也不要退化为任意 C#、反射或临时 Pipify。

## 完整更新的执行与验证边界

按“冻结定向数据 → 更新既有绑定 → 选定导出 → Unity 编译 → Play 切换语言”执行。Play 前确保既有启动链已经完成 `Load*()` 与 `InitCurrentLanguage*()`；随后切换指定语言并检查同一 UI 的 Key、回退行为和需要时的 `AutoFontAdapt + FontMark`。不要将 `GetText()` 的静态预览、截图或导出日志单独称为成功。

最低成功证据是 Unity 编译通过，并在 Play Mode 中验证用户请求的每种冻结语言的文本显示；请求三种必须三种全部通过，不截取两种；单语言任务只验证该语言，不扩无关语言。需要证明语言切换且用户未指定集合时，冻结基准与目标两种语言后验证；涉及字体时还需验证目标字体适配。无法进入 Play、语言不在支持列表、Key / FontMark 无法唯一定位或源编辑入口不可用时返回 `partial` 或 `blocked`，不修改无关语言、Prefab 或 Framework。
