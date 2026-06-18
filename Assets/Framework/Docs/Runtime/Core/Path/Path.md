# Path

**类签名**：`public static partial class Path`  
**命名空间**：`NovaFramework.Runtime`

Nova 运行时路径工具的根入口。当前实现是一个 `partial` 静态类，根类只保留平台名与分隔符规范化能力，具体路径能力拆到 `Streaming`、`Persistent`、`Cache`、`Hotfix`、`Persist` 五个子分区。

## 文件组成

| 文件 | 作用 |
|---|---|
| `Path.cs` | 根定义，提供 `PlatformName` 与 `NormalizeSeparator` |
| `Path.Streaming.cs` | `StreamingAssets` 只读路径 |
| `Path.Persistent.cs` | `persistentDataPath` 可写路径 |
| `Path.Cache.cs` | Unity `Caching.currentCacheForWriting.path` |
| `Path.Hotfix.cs` | 热更新相关本地路径，当前只保留 APK 下载落点 |
| `Path.Persist.cs` | Nova 持久化子目录（`FileFragment` / `SQLite`） |

## 关键成员

| 成员 | 类型 | 说明 |
|---|---|---|
| `PlatformName` | `string` | 编译期平台名称，当前分支为 `iOS` / `WebGL` / `Android` |
| `NormalizeSeparator(string path)` | `string` | 内部工具方法，把 `\` 统一替换成 `/` |

## 当前结构说明

- `Path` 本身只是静态路径工具，不承担运行时管理职责。
- `Path.Hotfix` 当前只定义 `ApkDownloadedFullPath`，语义是“安装包下载后的本地保存路径”。
- `Path.Persist` 是 Nova 持久化目录约定，不等同于 `Path.Persistent`。前者是业务子目录规划，后者是 Unity 提供的根可写目录。

## 使用示例

```csharp
string streamingFolder = Path.Streaming.FolderFullPath;
string configPath = Path.Streaming.GetFileFullPath("config/launch.json");
string persistentSavePath = Path.Persistent.GetFileFullPath("save/user.json");
string cacheBundlePath = Path.Cache.GetFileFullPath("bundles/main.bundle");
string apkPath = Path.Hotfix.ApkDownloadedFullPath;
string sqliteDbPath = Path.Persist.SQLite.FileFullPath;
```

## 关联文档

- [Path.Streaming.md](Path.Streaming.md)
- [Path.Persistent.md](Path.Persistent.md)
- [Path.Cache.md](Path.Cache.md)
- [Path.Hotfix.md](Path.Hotfix.md)
- [Path.Persist.md](Path.Persist.md)
