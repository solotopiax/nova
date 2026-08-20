---
name: nova-project-update-localization
description: Use when 项目组要定向更新已有 Nova 本地化文本、字体或 TextLocalizing 绑定，并在切换指定语言后验证显示与字体适配时使用。
---

# Nova 更新本地化

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在确认数据源、格式或导出位置时读 `Docs/Runtime/Modules/Localization/LocalizationSettings.md` 和 `Docs/Editor/Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md`；文本变更才读 `Docs/Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md`；字体变更才读 `Docs/Editor/EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md`；运行验证才读 `Docs/Runtime/Modules/Localization/LocalizationComponent.md`、`Docs/Runtime/Modules/Localization/LocalizationManager.md` 与 `Docs/Runtime/Modules/Localization/TextLocalizing.md`。不要递归加载全部 Localization、Luban 或 UI 文档。

## 冻结输入与决策门

冻结已有 `LocalizationComponent`、目标文本 Key / 字体 Mark、语言集合、源文件与行范围、既有 `TextLocalizing` 绑定、数据格式、输出位置，以及可观察的目标 UI 和两种验证语言。文本、字体和支持语言列表是不同数据链，不能因名称相近而混用。

- 仅通过已确认、可复现的项目数据源编辑入口修改指定的文本或字体源；没有这类入口时返回 `blocked`，不得自行重建 `.xlsx`、更换表结构或新造导出管线。
- 增删 Key、语言列、字体 Mark、支持语言、数据格式、回退规则或输出路径都需要新的确认。值的定向改动不默认升级为全量语言或全项目扫描。
- 只更新已确认的 `TextLocalizing` / TMP 绑定。所有 Prefab、Scene、组件和序列化引用写入必须经 Unity Editor 自动化通道；不得手写 YAML，也不得调用全工程的“修复缺失 TextLocalizing”作为默认动作。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的文本、字体源与单元设置 | 项目现有的已确认数据源编辑入口；LocalizationComponent Inspector 用于设置与路径 | 指定源数据与 LocalizationSettings 配置 | 变更仅覆盖确认的 Key、语言、Font Mark 与路径 |
| 文本、语言列表或字体的导出范围 | 文本使用 `EditorUtil.Localization.TextExporter.ExportTextData` / `ExportTextCode` / `ExportTextAll`；字体使用 `FontExporter.ExportFontData` / `ExportFontCode` / `ExportFontAll`；复用既有 Batch 时用对应 `export.localization.*` Step | 选定的文本、字体、类型和支持语言正式产物 | Exporter 的暂存发布成功；不手改生成物 |
| 已确认的既有 UI 绑定与验证语言 | Unity Editor 自动化通道 更新 `TextLocalizing`；运行时经 `Nova.Localization.SetLanguageSync()` 或 `SetLanguageAsync()` 切换 | 正确 Key / FontMark 绑定的 TMP 显示 | Unity 编译通过，Play Mode 下两种语言都显示目标文本；启用字体适配时字体也匹配 |

文本仅改值且语言集合、类型和 Key 结构未变时，只导出所需数据；新增 Key、语言或类型输出变化时才调用对应完整导出。完整文本导出会同时处理支持语言列表；不要把单独的语言列表 Step 当成文本更新的默认替代。

## 执行与验证边界

按“冻结定向数据 → 更新既有绑定 → 选定导出 → Unity 编译 → Play 切换语言”执行。Play 前确保既有启动链已经完成 `Load*()` 与 `InitCurrentLanguage*()`；随后切换指定语言并检查同一 UI 的 Key、回退行为和需要时的 `AutoFontAdapt + FontMark`。不要将 `GetText()` 的静态预览、单语言截图或导出日志单独称为成功。

最低成功证据是 Unity 编译通过，并在 Play Mode 中实际切换两种冻结语言后验证文本显示；涉及字体时还需验证目标字体适配。无法进入 Play、语言不在支持列表、Key / FontMark 无法唯一定位或源编辑入口不可用时返回 `partial` 或 `blocked`，不修改无关语言、Prefab 或 Framework。
