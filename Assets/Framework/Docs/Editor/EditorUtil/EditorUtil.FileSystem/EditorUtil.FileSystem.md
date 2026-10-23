# EditorUtil.FileSystem

**类签名**：`public static class EditorUtil.FileSystem`
**命名空间**：`NovaFramework.Editor`

Editor 层文件系统工具，封装 `AssetDatabase`、`System.IO`、操作系统 shell 操作，供 Inspector 按钮回调和数据导出流程调用。

---

## 文件

| 文件 | 说明 |
|------|------|
| `EditorUtil.FileSystem.cs` | 路径、文件打开、扫描和刷新等公共方法 |
| `EditorUtil.FileSystem.OutputApplier.cs` | Editor 内部批量文件应用、备份与失败回滚基础设施 |

---

## 完整 API 签名

```csharp
// 在文件管理器（Explorer / Finder）中打开指定文件夹
void EditorUtil.FileSystem.OpenFolder(string path)

// 用系统默认程序打开文件
void EditorUtil.FileSystem.OpenFile(string path)

// SerializedProperty 中的路径 → 转为相对于工程根的路径
// 例：返回 "Assets/Tables/HeroData.xlsx"
string EditorUtil.FileSystem.GetProjectRelativePath(SerializedProperty property)

// 工程内绝对路径 → 相对于工程根的路径；工程根、工程外及普通相对输入保持原有语义
string EditorUtil.FileSystem.GetProjectRelativePath(string path)

// Editor 内部严格尝试转换为 Assets/...；工程外、Packages 或异平台绝对路径返回 false，不修改调用方数据
bool EditorUtil.FileSystem.TryGetProjectAssetPath(string path, out string assetPath)

// 相对于工程根的路径 → 绝对路径
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

// Editor 内部：递归删除临时目录及其 Unity .meta
void EditorUtil.FileSystem.DeleteUnityTempRoot(string tempRoot)

// Editor 内部：取得标准化目录租约；同一路径在释放前禁止再次进入
IDisposable EditorUtil.FileSystem.AcquireWorkspace(string rootPath)
```

---

## 注意事项

| 场景 | 说明 |
|------|------|
| `GetFiles` 排除 Excel 临时文件 | 传入 `excludePrefix: "~$"` 过滤 Excel 打开时生成的临时文件 |
| `RefreshDelayed` vs 直接 `Refresh` | Inspector 绘制中不可调用 `AssetDatabase.Refresh()`（会触发重编译），用 `RefreshDelayed()` 推迟到下帧 |
| 路径格式 | `GetProjectRelativePath` 保持兼容行为：工程内绝对路径转为工程根相对路径，工程根、工程外及普通相对输入保留原有路径语义并统一为正斜杠。必须得到 Unity `AssetPath` 时使用 `TryGetProjectAssetPath`；失败由调用方显式处理 |
| `DeletePath` 删目录 | 仅删除目录内的**文件**，不递归删除子目录本身，目录结构保留 |
| `OpenSQLiteStudio` 平台限制 | 仅 `UNITY_EDITOR_WIN` 下有效，其他平台输出 Warning 并静默跳过 |

## 内部输出事务

`EditorUtil.FileSystem.OutputApplier` 供模块专用导出器复用文件替换、精确删除、备份和失败回滚能力。它不解释 Excel、Luban 或模块产物范围，不属于公开 API。

`AcquireWorkspace` 与 `DeleteUnityTempRoot` 同样只处理目录生命周期，不保存模块业务状态。模块负责选择工作区路径，并在 `finally` 中清理。

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.FileSystem.OutputApplier.md](EditorUtil.FileSystem.OutputApplier.md)

## 路径、扫描与工作区边界

GetProjectFullPath 的相对输入依当前进程工作目录解析，不强制拼 Unity 项目根；绝对输入仅统一斜杠。兼容 GetProjectRelativePath(string) 保留普通相对输入中的 `..` 和工程外绝对路径。SerializedProperty 重载取有效目录（文件则取父目录），原字段为空时可能写默认目录并 Apply，不是纯查询。严格 Try 方法从 Application.dataPath 父目录判工程内，拒绝异平台绝对路径；TryGetProjectAssetPath 还要求 Assets 的后代，不接受 Assets 根本身、Packages，不核存在或符号链接实际归属。返回 false 时必须忽略 out 值。

IsSubPathOf 只是补尾斜杠后的字符串 StartsWith，不解析 `..`、大小写规则或符号链接，不能做删除授权。GetFiles 递归、不排序，匹配为空返回 null，前缀仅按文件名过滤；输出路径形态依输入，异常向上传播。ResolveTemplatePath 优先固定 Packages 再 Assets，两个都不存在仍返回 Packages 候选，后缀未限制路径段。首个 .sln 也不排序。

AcquireWorkspace 是规范化路径的域内 OrdinalIgnoreCase 互斥，不跨进程、不解析符号链接；Dispose 只释放租约，不清文件或等待生成进程。DeleteUnityTempRoot 没有“临时根”白名单，先尝试 DeleteAsset，再原生递归删除和旁 .meta；调用者必须确认本轮归属。DeletePath 也无 Undo 或回滚，可能留下/删除 meta。OpenFile/OpenFolder 失败只日志；SQLiteStudio 仅 Windows 启动且不核 DB 存在或进程完成。RefreshDelayed 每次登记刷新，不合并、无完成收据。
