# LocalizationComponentInspector

**类签名**：`[CustomEditor(typeof(LocalizationComponent))] internal sealed partial class LocalizationComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.LocalizationComponent`

本地化组件的 Inspector 面板，绘制管理器类型选择器、文本数据导出区（FileFolderTree 树形文件展示 + 语言聚合导出）和字体数据导出区（Luban 暂存事务导出）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `LocalizationComponentInspector.cs` | `sealed partial LocalizationComponentInspector` | 主体：`OnEnable` 绑定序列化属性，`OnInspectorGUI` 调度绘制入口 |
| `LocalizationComponentInspector.Visitors.cs` | `partial LocalizationComponentInspector` | 字段：全部 `SerializedProperty`、样式、折叠状态 |
| `LocalizationComponentInspector.Methods.cs` | `partial LocalizationComponentInspector` | 私有方法：`DrawConfigs`（含修复按钮 + HelpBox）、`DrawTextExportSection`、`DrawFontExportSection`、`DrawSupportedLanguagesFields`、`DoExportAllTextData`、`DoExportAllFontData` |

---

## §3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract, NovaFramework.Editor)
        └── LocalizationComponentInspector (sealed partial)
```

---

## §4 关键字段表

### SerializedProperty

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_CurLocalizationManagerTypeName` | `SerializedProperty` | 本地化管理器实现类全名 |
| `m_EditorLanguage` | `SerializedProperty` | 编辑器语言类型（仅编辑器内生效） |
| `m_RuntimeLanguagePrefer` | `SerializedProperty` | 终端语言类型优先策略（仅终端运行时生效） |
| `m_FallbackLanguage` | `SerializedProperty` | 回退语言枚举 |
| `m_AutoFontAdapt` | `SerializedProperty` | 字体自动适配开关 |
| `m_LocalizationSettings` | `SerializedProperty` | LocalizationSettings 根属性 |
| `m_DataFormat` | `SerializedProperty` | 模块统一 Luban 数据格式，JSON / Binary 二选一，默认 JSON |
| `m_TextSourceDirPath` | `SerializedProperty` | 文本数据源目录路径 |
| `m_FontSourceDirPath` | `SerializedProperty` | 字体数据源目录路径 |
| `m_TextUnitsSettings` | `SerializedProperty` | 文本数据单元设置列表 |
| `m_FontUnitsSettings` | `SerializedProperty` | 字体数据单元设置列表 |
| `m_FontTemplatePath` | `SerializedProperty` | 字体数据模板文件路径 |
| `m_SupportedLanguagesDataExportPath` | `SerializedProperty` | 语言列表数据导出路径；由旧字段通过 `FormerlySerializedAs` 迁移 |
| `m_SupportedLanguagesAssetLocation` | `SerializedProperty` | 语言列表 Asset 地址 |

### 样式与状态

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_LocalizationManagerTypeNames` | `List<string>` | ILocalizationManager 实现类名列表 |
| `m_FolderFoldoutState` | `Dictionary<string, bool>` | 目录树折叠状态 |

---

## §5 完整公开 API

继承自 `BaseComponentInspector`，无额外 public 方法。

```csharp
// 生命周期（override）
protected override void OnEnable()
// 绑定 SerializedProperty，收集 ILocalizationManager 类型名

public override void OnInspectorGUI()
// base.OnInspectorGUI → DrawConfigs → DrawTextExportSection → DrawFontExportSection → FinalRefreshInspectorGUI
```

---

## §9 关键算法

### Inspector 绘制结构

```
OnInspectorGUI()
  ├── base.OnInspectorGUI()
  ├── DrawConfigs()
  │     ├── TypesSelector（ILocalizationManager 实现类）
  │     ├── 分割线
  │     ├── EnumSelector（编辑器语言类型 m_EditorLanguage）
  │     ├── Toggle（终端语言类型优先策略 m_RuntimeLanguagePrefer）
  │     ├── EnumSelector（回退语言类型 m_FallbackLanguage）
  │     ├── Toggle（字体自动适配）
  │     ├── Enum（Luban 数据格式：JSON / Binary）
  │     ├── HelpBox（以 `(1)`～`(5)` 说明两种格式、影响范围、后缀切换和反格式清理）
  │     ├── 分割线
  │     ├── Button（修复预制体缺失 TextLocalizing → TextLocalizingValidator.FixMissingInPrefabs）
  │     ├── HelpBox（扫描说明，MessageType.Info）
  │     └── 分割线
  ├── DrawTextExportSection()
  │     ├── Foldout（文本数据导出）
  │     ├── DrawTemplatePathHintReadOnly（模板文件位置）
  │     ├── 表格目录位置行（TextField + 选择 + 打开文件夹）
  │     ├── DrawSourceFilesListWithFolders（树形文件列表 + 自定义文件行）
  │     │     文件行：文件名 + 数据导出位置（含 {0} 语言占位符）+ 类型导出位置 + Asset 地址
  │     └── HelpBox（说明数据导出位置在导出时替换 {0}；Asset 地址在运行时按当前语言替换 {0}）
  └── DrawFontExportSection()
        ├── Foldout（字体数据导出）
        ├── DrawTemplatePathHintReadOnly（字体模板文件位置）
        ├── 表格目录位置行（TextField + 选择 + 打开文件夹）
        └── DrawSourceFilesListWithFolders（同结构，字体单元设置）
```

### 导出逻辑

| 数据类型 | 导出方式 | 说明 |
|----------|----------|------|
| 文本数据 | `EditorUtil.Localization.TextExporter.ExportTextAll` | 保留 PreFilter、多语言投影和事务发布；最终 Luban 阶段按所选格式生成 JSON 或 Binary |
| 字体数据 | `EditorUtil.Localization.FontExporter.ExportFontAll` | 在暂存区按所选格式生成数据/C#，验证后统一发布正式产物 |
| 支持语言 | 独立 Luban target | 从文本源语言列生成临时 CSV，导出 `LocalizationSupportedLanguagesTables`，与文本/字体共用格式选项 |

切换格式时仅自动替换标准 `.json` / `.bytes` 后缀；自定义后缀会保持原值并被标记为无效。文本、字体和支持语言均经过 `FileSystem.OutputApplier`，不直接写入正式目录。
Inspector 在调用前只执行 `serializedObject.ApplyModifiedProperties()`，不刷新或写回 Excel Sheet 名。每个 Pipeline 阶段在 Luban 前扫描当前输入并生成 schema manifest。

---

## §11 使用示例

Inspector 自动挂载，无需手动调用。

```csharp
// Inspector 绘制入口
public override void OnInspectorGUI()
{
    base.OnInspectorGUI();
    DrawConfigs();
    DrawTextExportSection();
    DrawFontExportSection();
    FinalRefreshInspectorGUI();
}
```

---

## §13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [LocalizationComponent.md](../../../Runtime/Modules/Localization/LocalizationComponent.md)
- [LocalizationSettings.md](../../../Runtime/Modules/Localization/LocalizationSettings.md)
- [EditorUtil.Localization.TextExporter.md](../../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.TextExporter.md)
- [EditorUtil.Localization.FontExporter.md](../../EditorUtil/EditorUtil.Localization/EditorUtil.Localization.FontExporter.md)
- [EditorUtil.Luban.Pipeline.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Draw.SourceFileTree.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md)
- [FileFolderTree.md](../../Definitions/FileFolderTree.md)

## 输入与执行边界

格式切换本身只改文本/字体 Unit 和支持语言路径的标准后缀，不转换现有文件、不修改 AssetLocation，也不立即执行导出；自定义后缀保留，未知枚举沿 JSON 显示分支处理。导出按钮的可用条件主要是目录文本非空与 Unit 数量，不等于实际目录、占位符、schema 或 Collector 就绪。全量导出先 Apply，再从组件的真实 LocalizationSettings 直接读取，并丢弃下层返回值；按钮结束不能替代正式产物验证。

OnEnable 可填空模板路径并 Apply，因此打开面板不是绝对只读。预览/路径校验在某些行先于当前文本编辑计算，可能下一帧才反映新值；空白 Asset 地址也不由 `IsNullOrEmpty` 拒绝。Prefab 修复按钮作用于扫描到的工程 Prefab，不能把它误认为仅修当前组件。

文本模板的 Name 是 Key，`#Desc` 是说明列，语言列必须对应 Language 枚举；样例业务键不构成正式词表。字体模板以 Language/Mark 选择字体，AssetLocation 是资源地址，MaterialName 为空使用默认材质，FontSizeScaleRatio 相对初始字号；模板的 Main、中文字体地址和 1.0 只是样例，不保证消费项目存在这些资源。
