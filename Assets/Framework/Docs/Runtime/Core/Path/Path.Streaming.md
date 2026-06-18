# Path.Streaming

**类签名**：`public static class Streaming`（`Path` 的嵌套静态类）
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Path.Streaming`

StreamingAssets 只读路径容器，根路径取自 `Application.streamingAssetsPath`（各平台差异由 Unity 内部统一）。

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Core/Path/Path.Streaming.cs` | `Path.Streaming` | StreamingAssets 只读路径 |

## § 5 公开 API

```csharp
public static readonly string FolderFullPath;   // = NormalizeSeparator(Application.streamingAssetsPath)
public static string GetFileFullPath(string relativePath);
```

**注意**：Android / WebGL 真机的 `streamingAssetsPath` 是 jar 协议或相对 URL，不能直接用 `File` API 读取，需走 `UnityWebRequest`。

## § 11 使用示例

```csharp
string folder = Path.Streaming.FolderFullPath;
string filePath = Path.Streaming.GetFileFullPath("config/launch.json");
```

## § 13 关联文档

- [Path.md](Path.md)
