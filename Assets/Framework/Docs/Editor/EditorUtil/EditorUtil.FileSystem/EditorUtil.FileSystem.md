# EditorUtil.FileSystem

**类签名**：`public static class EditorUtil.FileSystem`
**命名空间**：`NovaFramework.Editor`

Editor 层文件系统工具，封装 `AssetDatabase`、`System.IO`、操作系统 shell 操作，供 Inspector 按钮回调和数据导出流程调用。

---

## 文件

| 文件 | 说明 |
|------|------|
| `EditorUtil.FileSystem.cs` | 所有方法实现 |

---

## 完整 API 签名

```csharp
// 在文件管理器（Explorer / Finder）中打开指定文件夹
void EditorUtil.FileSystem.OpenFolder(string path)

// 用系统默认程序打开文件
void EditorUtil.FileSystem.OpenFile(string path)

// SerializedProperty（存储绝对路径）→ 转为相对于 Assets/ 的路径
// 例：返回 "Assets/Tables/HeroData.xlsx"
string EditorUtil.FileSystem.GetProjectRelativePath(SerializedProperty property)

// 绝对路径 → 相对于 Assets/ 的路径
string EditorUtil.FileSystem.GetProjectRelativePath(string path)

// 相对于 Assets/ 的路径 → 绝对路径
string EditorUtil.FileSystem.GetProjectFullPath(string path)

// 获取目录下所有匹配文件路径（绝对路径数组）
// searchPattern：如 "*.xlsx"，默认 "*.*"
// excludePrefix：排除以指定前缀开头的文件名（如 "~$" 排除 Excel 临时文件）
string[] EditorUtil.FileSystem.GetFiles(string directoryPath, string searchPattern = "*.*", string excludePrefix = null)

// 判断 filePath 是否位于 parentDirPath 目录下（路径统一替换反斜杠后比较）
bool EditorUtil.FileSystem.IsSubPathOf(string filePath, string parentDirPath)

// 解析 Nova 模板文件路径：优先 Packages/com.solotopia.nova.framework/Templates/，回退 Assets/Framework/Templates/
string EditorUtil.FileSystem.ResolveTemplatePath(string templateFileName)

// 删除指定路径内容：目录则清空目录内所有文件（保留目录结构），文件则删除该文件，路径不存在时静默跳过
void EditorUtil.FileSystem.DeletePath(string path)

// 在工程根目录下查找第一个 .sln 文件，返回其绝对路径；未找到返回空字符串
string EditorUtil.FileSystem.GetScriptsProjectFilePath()

// 延迟刷新 AssetDatabase（下一帧 delayCall 中执行 AssetDatabase.Refresh）
// 适合在同步操作（如文件写入）完成后调用，避免在 GUI 绘制中直接刷新
void EditorUtil.FileSystem.RefreshDelayed()

// 使用 SQLiteStudio 可视化工具打开指定 SQLite 数据库文件（仅 Windows Editor）
// 查找路径：{ProjectRoot}/Tools/SQLiteStudio/SQLiteStudio.exe
// 若未找到，弹出引导弹窗提示用户下载并放置到 Tools/SQLiteStudio/ 目录
void EditorUtil.FileSystem.OpenSQLiteStudio(string databasePath)
```

---

## 注意事项

| 场景 | 说明 |
|------|------|
| `GetFiles` 排除 Excel 临时文件 | 传入 `excludePrefix: "~$"` 过滤 Excel 打开时生成的临时文件 |
| `RefreshDelayed` vs 直接 `Refresh` | Inspector 绘制中不可调用 `AssetDatabase.Refresh()`（会触发重编译），用 `RefreshDelayed()` 推迟到下帧 |
| 路径格式 | `GetProjectRelativePath` 返回值以 `"Assets/"` 开头，可直接传给 `AssetDatabase.*` 方法 |
| `DeletePath` 删目录 | 仅删除目录内的**文件**，不递归删除子目录本身，目录结构保留 |
| `OpenSQLiteStudio` 平台限制 | 仅 `UNITY_EDITOR_WIN` 下有效，其他平台输出 Warning 并静默跳过 |

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)
