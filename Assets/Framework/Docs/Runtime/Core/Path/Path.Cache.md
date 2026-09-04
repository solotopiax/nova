# Path.Cache

**类签名**：`public static class Cache`（`Path` 的嵌套静态类）
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Path.Cache`

Unity 缓存目录路径容器。WebGL 不提供 `UnityEngine.Caching` API，因此使用 `Application.temporaryCachePath`。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Path/Path.Cache.cs` | `Path.Cache` | Unity Caching 缓存路径 |

## § 5 公开 API

```csharp
public static string FolderFullPath { get; }
public static string GetFileFullPath(string relativePath);
```

`FolderFullPath` 为属性（非字段）。WebGL 返回 `Application.temporaryCachePath`；其他平台每次访问均从 `UnityEngine.Caching.currentCacheForWriting.path` 实时获取。

## § 11 使用示例

```csharp
string folder = Path.Cache.FolderFullPath;
string filePath = Path.Cache.GetFileFullPath("bundles/main.bundle");
```

## § 13 关联文档

- [Path.md](Path.md)
