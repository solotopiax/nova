# Path.Persist

**类签名**：`public static class Path.Persist`
**命名空间**：`NovaFramework.Runtime`

`Path` 静态类的持久化存储路径分区，集中管理应用运行时需要持久化存储的文件和目录路径。内部按数据类型拆分为 `FileFragment`（文件片段）和 `SQLite`（数据库）两个嵌套静态类。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Path.Persist.cs` | 持久化存储路径相关静态类 |

## 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `FileFragment.FolderFullPath` | `string` | 文件片段根目录绝对路径（`{persistentDataPath}/Persist/FileFragment`） |
| `SQLite.FolderFullPath` | `string` | SQLite 数据库目录绝对路径（`{persistentDataPath}/Persist/SQLite`） |
| `SQLite.FileFullPath` | `string` | SQLite 数据库文件绝对路径（`{persistentDataPath}/Persist/SQLite/game.db`） |

## 关联文档

- [Path](Path.md)
