# EditorUtil.SpriteAtlasGeneration

按一份明确配置批量维护多份 Unity SpriteAtlas，只更新有变化的输出，并保留已有图集 GUID。默认不运行、不注册自动导入扫描或构建回调，不修改旧 Batch、源图片、Collector 或全局 Sprite Packer 模式；旧项目无需添加配置或改代码。

## 先判断是否需要这个工具

Unity 原生图集的 `Objects for Packing` 已可直接关联文件夹，收集其中有效图片；原生 `Include in Build` 可控制随 Player 构建包含图集及运行时加载。因此，普通的“一个文件夹对应一张图集”优先使用原生配置，不必增加本工具。[Unity 文件夹收集说明](https://docs.unity.cn/Manual/sprite/atlas/workflow/select-items-objects-packing-list.html)、[Include in Build 说明](https://docs.unity.cn/6000.0/Documentation/Manual/sprite/atlas/workflow/include-build.html)。

本工具仍生成原生 SpriteAtlas，打包、图集纹理和渲染合批沿 Unity 原有机制；额外提供的是**同一配置管理多份图集、明确排除路径、写入前差异预演、Pipify 串联及输出归属/恢复保护**。它没有新的图集算法，也不因使用生成器就获得额外合批或性能收益。

当前实现把目录展开为具体 Sprite 列表写入图集，不保留原生文件夹引用。新增、移除或移动图片后，须再次运行生成步骤更新成员；普通原生文件夹引用无需这一步。只有项目确实需要上述批量规则时再选择它，并考虑规则、生成步骤和归属清单的维护成本，不能把原生目录能力描述为本工具独有优势。

## 先明确配置来源

项目主动选择使用时，通过 `Assets/Create/Nova/Sprite Atlas Generation Settings` 创建项目自有 `SpriteAtlasGenerationSettingsSO`，在原生 Inspector 保存。它是权威配置，不手写 Unity YAML，不修改包内或第三方资产。

| 字段 | 用途与声明默认 |
| --- | --- |
| `Enabled` | 默认 `false`；未启用时预演只报告关闭，实际生成拒绝执行 |
| `OutputFolder` | 默认空；必须是已存在且已导入的项目 Assets 子目录，不能为 Assets 根、来源目录或与来源相互包含 |
| `Format` | 默认 `SpriteAtlasGenerationFormat.V2`；必须与项目已有 Sprite Packer 模式一致 |
| `Rules` | 默认空；不自动按目录创建规则 |

每条 `SpriteAtlasGenerationRule` 明确：

- `Id`：稳定且配置内唯一的规则身份；生成后不复用给另一组图片。
- `AtlasName`：不含目录和扩展名的输出名，直接写入声明的输出根；忽略大小写后不重名。
- `SourceFolders`：递归读取的明确 Assets 子目录；`ExcludedPaths` 只接收范围内完整文件/目录路径，不接受通配符。
- `IncludeInBuild=false`、`Padding=4`、`AllowRotation=false`、`TightPacking=false`、`GenerateMipMaps=false`：只影响输出图集。Padding 只允许 `2/4/8/16/32` 像素，实际值以保存配置为准。

`V1` 输出 `.spriteatlas`，接受已有 `BuildTimeOnlyAtlas` / `AlwaysOnAtlas`；`V2` 输出 `.spriteatlasv2`，接受已有 `SpriteAtlasV2` / `SpriteAtlasV2Build`。Disabled 或格式不匹配时停止，不自动开关或迁移项目设置。源 Sprite 名称在同图集中忽略大小写重名时也拒绝，不自动改名或删除源图。

## 预演、应用和增量边界

公开入口为 `NovaFramework.Editor.EditorUtil.SpriteAtlasGeneration`：

| API | 行为 |
| --- | --- |
| `Preview(string settingsAssetPath)` | 只读返回 `SpriteAtlasGenerationPlan`，不创建目录、图集或清单 |
| `Apply(SpriteAtlasGenerationPlan)` | 重建预演并比较指纹，无漂移后应用差异 |
| `Generate(string settingsAssetPath)` | 新鲜预演后应用，供已选择的生产链复用 |
| `Recover(string settingsAssetPath)` | 仅恢复该配置 GUID 的遗留事务，无事务不做事 |

计划包含配置路径/GUID、输出根、Enabled、Fingerprint 和逐输出 Entries。每项记录 `RuleId`、`OutputPath`、`Change` 及实际 Sprite 的路径/GUID/LocalId/Name。差异含义：

| `Change` | 写入行为 |
| --- | --- |
| `Create` | 创建尚不存在且未被其他资产占用的输出 |
| `Update` | 图块集合或打包设置变化，原地保存，已有 GUID 保留 |
| `Reimport` | 源内容、导入或路径变化但作者内容不变，仅重导入相关图集 |
| `Unchanged` | 不保存、不重导入 |
| `Retained` | 规则删除或输出改名后的旧输出，仅报告并保留 |

输出根的 `nova-sprite-atlas-manifest.json` 记录配置 GUID、规则、图集 GUID 与输入/输出摘要。只有本配置已登记且字节未被外部修改的输出才能更新；已有手工图集、孤立 meta、无归属占用、未知清单版本、GUID 漂移或外部编辑均拒绝接管。重复生成完全无变化时，图集、meta、归属清单都不重写。

删除规则或改名不会删除旧图集。它们作为 `Retained` 保留，实际清理需要另行明确归属和引用，不由本工具猜测删除。工具只负责作者图集生产，Collector/地址与 Bundle 发布仍用原资源流程。

## 从自然语言接到 Pipify

通过已有 [配置流水线 Skill](../../../Agents/Skills/nova-project-configure-pipeline/SKILL.md) 的 `inspect-settings` / `configure-batch` 读取 live Schema、创建或修改明确 Batch，再按原授权 `run-batch`。首次先使用仅含预演步骤的 Batch，不因“预演”而顺带运行后面的 Bundle 或外部步骤。

| StepId | 参数 | 实际调用 |
| --- | --- | --- |
| `spriteatlas.generate` | `SettingsAssetPath`；`PreviewOnly=true` 为默认 | `Preview`；明确设 `false` 才 `Apply` |
| `spriteatlas.recover` | `SettingsAssetPath` | `Recover`，不重新生成 |

预演核对配置与完整差异后，用户本次目标包含生成时，才明确选择 `PreviewOnly=false`。需要随资源构建更新图集的项目把生成 Step 排在自己的 `bundlebuilder.build` 之前并保存；旧 Batch 不自动插入该步骤，直接 Bundle 构建也不暗中生成图集。保存 Batch 与实际执行分别验收。

已有 Pipify 窗口与 CLI 复用同一 Registry/Runner，CLI 入口仍是 `NovaFramework.Editor.EditorUtil+Pipify+Cli.Run`，参数和工作区规则见 [Pipify 文档](EditorUtil.Pipify/EditorUtil.Pipify.md)。不新增图集专用 CLI、任意 C# 或另一套 Runner。`PreviewOnly` 是该 Step 的模式，不使整个 Batch 变成只读。

## 失败、取消与恢复

预演发现漂移、输出归属冲突或配置不匹配即停止。应用建立精确图集/meta/归属清单备份，并在生成过程中复核源与配置；同步导入回调改变输入时只处理本工具输出，不覆盖源。异常先尝试恢复本次已知写入；导入回调改动其他目标时保留未知改动与备份，后续恢复也不能将其登记为可覆盖的新状态。恢复失败保留 `Library/Nova/SpriteAtlasGeneration/<settingsGuid>`，拒绝后续生成。该目录是正在使用的恢复状态，不能作为临时日志清理。

回滚本事务首次新建、且原文件与 meta 均不存在的成对产物时，先通过 `AssetDatabase.DeleteAsset` 注销，再确认文件、meta 和 `LoadMainAssetAtPath` 结果均已清除；仅删磁盘文件不能证明 Unity 导入缓存已恢复。每步继续检查其他输出的已知摘要，不吸收删除回调造成的未知改动；原有资产仍沿其原字节恢复路径处理。

通过显式恢复 Step 使用同一配置路径，按保存配置 GUID 定位事务；不接受任意备份目录。未完成事务恢复核对归属、当时输出根、备份及已知输出 Hash，发现外部修改拒绝覆盖并保留现场。提交或恢复成功先持久化终态，再删除备份；仅备份清理失败不会回滚已经提交的输出。`Recover` 遇到已提交/已恢复终态只核已知终态输出并继续清理，不依赖已经删除的旧备份；未知文件或输出漂移继续保留。任何遗留事务都会阻断新生成，恢复不是重放生成。

同步生成不提供逐文件取消入口，Runner 的取消检查仍在 Step 边界，不能承诺按取消立即中断或回滚已完成的整条 Batch。

## 验收路径与来源

在项目声明范围验证首次创建、重复生成无变化、源内容修改/移动/删除只影响对应图集、GUID 与引用稳定、规则改名/删除保留旧输出、漂移阻断及失败恢复。随后按需要实际构建 Bundle，并通过 `SpriteAtlas.GetSprite` 或 [SpriteBinding](../../Runtime/Modules/Asset/SpriteBinding.md) 加载真实子图；Editor 生成成功不证明 Bundle、消费工程或设备通过，也不证明性能提升。

源码：[配置与规则](../../../Scripts/Editor/EditorUtil/EditorUtil.SpriteAtlas/SpriteAtlasGenerationSettingsSO.cs)、[计划与结果](../../../Scripts/Editor/EditorUtil/EditorUtil.SpriteAtlas/SpriteAtlasGenerationDefinitions.cs)、[预演与应用](../../../Scripts/Editor/EditorUtil/EditorUtil.SpriteAtlas/EditorUtil.SpriteAtlasGeneration.cs)、[恢复事务](../../../Scripts/Editor/EditorUtil/EditorUtil.SpriteAtlas/EditorUtil.SpriteAtlasGeneration.Transaction.cs)、[Pipify Steps](../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.SpriteAtlas.cs)。
