# FileFolderTree

**类签名**：`public static class FileFolderTree`
**命名空间**：`NovaFramework.Editor`

扫描目录下指定扩展名的文件，构建层级目录树结构，供 Inspector 中展示文件/目录树使用（通常配合 `EditorUtil.Draw.*` 绘制）。

---

## 文件

| 文件 | 说明 |
|------|------|
| `Structers/FileFolderTree.cs` | 树构建逻辑 + `TreeNode` 内嵌类 |

---

## 完整 API 签名

```csharp
public static class FileFolderTree
{
    // 根据根路径和文件完整路径数组构建树结构
    // rootPathNorm：根目录路径（标准化后，以 '/' 结尾）
    // fileFullPaths：文件完整路径数组（由 EditorUtil.FileSystem.GetFiles 获取）
    // 返回：根节点（SegmentName 为空）
    static TreeNode BuildTree(string rootPathNorm, string[] fileFullPaths)
}
```

---

## TreeNode 结构

```csharp
public sealed class TreeNode
{
    public string SegmentName;              // 当前层级名称（文件夹名或文件名片段）
    public string FullPath;                 // 完整路径（用于读取文件或跳转）
    public readonly List<TreeNode> Children; // 子目录节点列表
    internal readonly Dictionary<string, TreeNode> ChildIndex; // 子节点名称索引，O(1) 查找
    public readonly List<string> FileFullPaths; // 该层级下直属文件的完整路径列表

    // 递归统计此节点及所有子节点的文件总数
    public int TotalFileCount()
}
```

---

## 使用示例

```csharp
// 构建 Excel 目录树用于 Inspector 展示
string[] xlsxFiles = EditorUtil.FileSystem.GetFiles(excelDir, "*.xlsx", excludePrefix: "~$");
string rootNorm = excelDir.Replace("\\", "/").TrimEnd('/') + "/";
FileFolderTree.TreeNode root = FileFolderTree.BuildTree(rootNorm, xlsxFiles);

// 递归绘制目录树（伪代码）
void DrawNode(FileFolderTree.TreeNode node, int indent)
{
    foreach (var child in node.Children)
    {
        EditorUtil.Draw.Foldout(child.SegmentName, child.FullPath);
        DrawNode(child, indent + 1);
    }
    foreach (var file in node.FileFullPaths)
    {
        EditorUtil.Draw.Label("  " + Path.GetFileName(file));
    }
}
DrawNode(root, 0);
```

---

## 关联文档

- [EditorUtil.FileSystem.md](../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
- [EditorUtil.Draw.md](../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md)
