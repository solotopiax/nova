# EditorUtil.Draw.SourceFileTree

**类签名**：`public static class SourceFileTree`（`EditorUtil.Draw` 嵌套静态类）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Draw.SourceFileTree`

数据源文件树绘制与命名空间列表编辑的静态工具方法集。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.Draw.SourceFileTree.cs` | `EditorUtil.Draw.SourceFileTree` | 全部方法：文件树绘制、单文件行绘制、命名空间列表编辑、孤儿条目清理 |

---

## §3 继承关系

```
EditorUtil (static partial)
  └── EditorUtil.Draw (static partial)
        └── EditorUtil.Draw.SourceFileTree (static)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `SourceFileNameColor` | `static readonly Color` | `(0xB8/255, 0xF2/255, 0xF2/255)` | 文件列表中文件名的显示颜色（浅青） |
| `SettingsColor` | `static readonly Color` | `(200/255, 145/255, 120/255)` | 设置标签与路径文字使用的颜色（橙褐色） |
| `c_DirLabelWidth` | `const float` | `106f` | 表格目录位置标签宽度（像素） |
| `c_ExportLabelWidth` | `const float` | `82f` | 导出区域标签宽度（数据/类型导出位置） |
| `c_FieldLabelWidth` | `const float` | `82f` | Asset 地址标签宽度（像素） |
| `c_ButtonWidthSmall` | `const float` | `60f` | 小按钮宽度（选择、打开、导出） |
| `c_ButtonWidthMedium` | `const float` | `100f` | 中按钮宽度（打开文件夹） |
| `c_ButtonWidthLarge` | `const float` | `163f` | 大按钮宽度（打开文件夹） |
| `c_IndentPixelsPerLevel` | `const float` | `15f` | Unity 每层缩进的像素宽度 |
| `ContentStyle` | `static GUIStyle` | `null` | 导出区域内容行标签样式（延迟初始化） |
| `ContentFieldStyle` | `static GUIStyle` | `null` | 导出区域输入框样式（延迟初始化） |
| `SourceFileNameStyle` | `static GUIStyle` | `null` | 数据源文件名样式（延迟初始化） |

---

## §5 完整公开 API

### 委托定义

```csharp
// 自定义单个数据源文件行绘制委托
public delegate void DrawSourceFileRowDelegate(
    string filePath,
    string capturedRelativePath,
    int seq,
    float indentSpace,
    int savedIndent,
    SerializedProperty detailProp,
    SerializedProperty sourceUnitsSettingsProperty);
```

### 主入口

```csharp
// 按目录层级绘制可折叠的数据源文件列表：构建目录树，清除孤儿条目，递归绘制根节点及子文件夹
public static void DrawSourceFilesListWithFolders(
    string directoryPath,
    SerializedProperty sourceUnitsSettingsProperty,
    Dictionary<string, bool> foldoutState,
    int minHeaderRowCount = 5,
    Func<string[], string[]> fileFilter = null,
    DrawSourceFileRowDelegate customDrawSourceFileRow = null)

// 递归绘制目录树节点：根节点 Foldout、子文件夹 Foldout、文件行
public static void DrawFolderNode(
    FileFolderTree.TreeNode node,
    string rootPathNorm,
    SerializedProperty sourceUnitsSettingsProperty,
    Dictionary<string, bool> foldoutState,
    DrawSourceFileRowDelegate customDrawSourceFileRow = null)
```

### 文件行绘制

```csharp
// 默认文件行绘制（文件名行 + 数据导出行 + 类型导出行 + Asset 地址行）
public static void DrawDefaultSourceFileRow(
    string filePath,
    string capturedRelativePath,
    int seq,
    float indentSpace,
    int savedIndent,
    SerializedProperty detailProp,
    SerializedProperty sourceUnitsSettingsProperty)

// 绘制文件名行：序号 + 文件名 + 打开 + 打开文件夹按钮
public static void DrawDefaultFileNameRow(string filePath, int seq, float indentSpace, int savedIndent)

// 绘制数据导出位置行：标签 + TextField + 选择/导出/打开文件夹按钮
public static void DrawDataExportRow(
    string filePath,
    string capturedRelativePath,
    float indentSpace,
    int savedIndent,
    SerializedProperty detailProp,
    SerializedProperty sourceUnitsSettingsProperty,
    Action<string, string, SerializedProperty> onExportData = null,
    Action<string, SerializedProperty> doRefreshDataTypeNames = null)

// 绘制类型导出位置行：标签 + TextField + 选择/导出/打开文件夹按钮
public static void DrawClassExportRow(
    string filePath,
    string capturedRelativePath,
    float indentSpace,
    int savedIndent,
    SerializedProperty detailProp,
    SerializedProperty sourceUnitsSettingsProperty,
    Action<string, string, SerializedProperty> onExportClass = null,
    Action<string, SerializedProperty> doRefreshDataTypeNames = null)

// 绘制 Asset 地址行（FindPropertyRelative("AssetLocation")）
public static void DrawAssetLocationRow(SerializedProperty detailProp, float indentSpace, int savedIndent)

// 绘制带缩进偏移的一行内容（SetIndentLevel(0) + Space + 内容 + RestoreIndentLevel）
public static void DrawIndentedRow(float indentSpace, int savedIndent, Action drawContent)
```

### 命名空间列表编辑

```csharp
// 绘制命名空间列表编辑区域：标题行（带 + 按钮）+ 各条目输入框（带 - 按钮）
public static void DrawNamespacesList(
    SerializedProperty namespacesProp,
    float labelWidth = 90f,
    float spaceWidth = 93f,
    Action onNamespacesChanged = null)
```

### 工具方法

```csharp
// 确保 GUIStyle 已初始化（延迟创建，避免 OnEnable 时 EditorStyles 未就绪）
public static void EnsureStylesInitialized()

// 根据相对路径在 UnitSettings 列表中查找/新建对应条目
public static SerializedProperty GetOrCreateDetailSettingsForFile(
    SerializedProperty sourceUnitsSettingsProperty,
    string sourceRelativePath)

// 递归收集目录树中所有文件节点的相对路径
public static void CollectRelativePaths(
    FileFolderTree.TreeNode node,
    string rootPathNorm,
    HashSet<string> result)

// 删除 UnitSettings 中磁盘上已不存在的孤儿条目（倒序遍历）
public static void RemoveOrphanUnits(
    SerializedProperty unitsProperty,
    HashSet<string> diskRelativePaths)
```

---

## §9 关键算法

### DrawSourceFilesListWithFolders 流程

```
DrawSourceFilesListWithFolders(directoryPath, unitsProp, foldoutState, ...)
  ├── EditorUtil.FileSystem.GetFiles() → allFiles
  ├── fileFilter?.Invoke(allFiles) → 过滤（可选）
  ├── FileFolderTree.BuildTree(rootPathNorm, allFiles) → root
  ├── CollectRelativePaths(root, rootPathNorm) → diskRelativePaths
  ├── RemoveOrphanUnits(unitsProp, diskRelativePaths) → 清除孤儿条目
  ├── EnsureStylesInitialized()
  └── DrawFolderNode(root, rootPathNorm, unitsProp, foldoutState, customDraw)
```

### DrawFolderNode 递归结构

```
DrawFolderNode(node, rootPathNorm, unitsProp, foldoutState, customDraw)
  ├── isRoot == true：
  │   ├── Foldout("rootDirName (N)")，初始展开
  │   └── IncreaseIndentLevel
  ├── foreach 子文件夹 child（按名称升序排列）：
  │   └── Foldout("childName (N)") → 展开时递归 DrawFolderNode + 维护缩进
  ├── foreach 当前节点文件（按路径升序排列）：
  │   ├── GetOrCreateDetailSettingsForFile → detailProp
  │   └── customDraw != null ? customDraw(…) : DrawDefaultSourceFileRow(…)
  └── isRoot == true：DecreaseIndentLevel
```

### GetOrCreateDetailSettingsForFile

线性扫描 `SourcePath` 匹配；未找到时追加新元素并写入 `SourcePath`，时间复杂度 O(n)，仅在 Inspector 绘制时调用。

---

## §10 常见误区

| 误区 | 正确做法 |
|------|----------|
| 在 `OnEnable` 中调用 `EnsureStylesInitialized()` | 不要，`OnEnable` 时 `EditorStyles` 未就绪；在 `DrawSourceFilesListWithFolders` 内部调用 |
| `DrawDataExportRow` / `DrawClassExportRow` 的 `onExportData`/`onExportClass` 传 `null` 时导出按钮无效果 | 正常行为；需传入回调才能触发导出 |
| `foldoutState` 传入 `null` | `DrawSourceFilesListWithFolders` 会直接返回；必须传入已初始化字典 |

---

## §11 使用示例

```csharp
// Inspector.Methods.cs 中绘制数据源文件树
private void DrawExportSourceFiles()
{
    if (string.IsNullOrEmpty(m_SourceDirPath.stringValue)) return;
    if (!Directory.Exists(m_SourceDirPath.stringValue)) return;

    EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(
        m_SourceDirPath.stringValue,
        m_UnitsSettings,
        m_FolderFoldoutState,
        customDrawSourceFileRow: (filePath, relPath, seq, indent, savedIndent, detailProp, unitsProp) =>
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indent, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(
                filePath, relPath, indent, savedIndent, detailProp, unitsProp,
                onExportData: (fp, exportPath, dp) => DoExportDataForFile(fp, exportPath, dp),
                doRefreshDataTypeNames: (fp, namesProp) => DoRefreshDataTypeNames(fp, namesProp));
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(
                filePath, relPath, indent, savedIndent, detailProp, unitsProp,
                onExportClass: (fp, exportPath, dp) => DoExportClassForFile(fp, exportPath, dp));
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indent, savedIndent);
        });
}
```

---

## §13 关联文档

- [EditorUtil.Draw.md](EditorUtil.Draw.md)
- [FileFolderTree.md](../../Definitions/FileFolderTree.md)
- [EditorUtil.Luban.ExportHelper.md](../EditorUtil.Luban/EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.DataTypeNameHelper.md](../EditorUtil.Luban/EditorUtil.Luban.DataTypeNameHelper.md)
