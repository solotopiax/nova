# Path.Persistent

**类签名**：`public static class Persistent`（`Path` 的嵌套静态类）
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Path.Persistent`

persistentDataPath 可写路径容器。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Path/Path.Persistent.cs` | `Path.Persistent` | persistentDataPath 可写路径 |

## § 5 公开 API

```csharp
public static readonly string FolderFullPath;
public static string GetFileFullPath(string relativePath);
```

`FolderFullPath` 初始化为 `NormalizeSeparator(Application.persistentDataPath)`。

## § 11 使用示例

```csharp
string folder = Path.Persistent.FolderFullPath;
string filePath = Path.Persistent.GetFileFullPath("save/user.json");
```

## § 13 关联文档

- [Path.md](Path.md)
