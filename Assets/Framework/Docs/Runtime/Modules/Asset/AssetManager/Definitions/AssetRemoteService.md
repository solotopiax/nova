# AssetRemoteService

**类签名**：`public sealed class AssetRemoteService : IRemoteService`
**命名空间**：`NovaFramework.Runtime`

YooAsset 远端寻址服务。直接使用主/备 URL 配置，并替换 `{Platform}` / `{Package}` / `{Version}` 占位符返回候选 URL 列表。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/AssetRemoteService.cs` | `AssetRemoteService` | YooAsset IRemoteService 实现 |

---

## 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_HostServerUrl` | `string` | 主下载地址配置值 |
| `m_HostServerUrlFallback` | `string` | 备用下载地址配置值 |
| `m_Platform` | `PlatformType` | 运行平台枚举，构造器内通过 `ResolvePlatform()` 缓存 |
| `m_Package` | `string` | 包名，构造器缓存 |
| `m_Version` | `string` | 应用版本号，构造器内读取 `Application.version` |
| `m_Cache` | `string[]` | 已替换占位符的 URL 前缀缓存（1~2 项） |

---

## 完整公开 API

```csharp
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package)
public IReadOnlyList<string> GetRemoteUrls(string fileName)
```

---

## 关键算法

`BuildRemoteUrlCache()`：先解析主/备配置值，解析顺序为：

1. 若配置值本身就是完整 URL，则直接使用
2. 为空或不是 URL，则该地址视为不可用

`GetRemoteUrls(fileName)`：遍历 `m_Cache`，每项拼接 `/{fileName}`，返回候选 URL 列表。

`ApplyTemplate(template)`：`.Replace("{Platform}", ...).Replace("{Package}", ...).Replace("{Version}", ...)`

---

## 使用示例

```csharp
// AssetManager.BuildPlayModeOptions 内部构造（不直接调用）
var remoteService = new AssetRemoteService(
    launch.HostServerUrl, launch.HostServerUrlFallback, packageName);
```

---

## 关联文档

- [IAssetManager.md](../Interfaces/IAssetManager.md)
