# AssetRemoteService

**类签名**：`public sealed class AssetRemoteService : IRemoteService`
**命名空间**：`NovaFramework.Runtime`

YooAsset 远端寻址服务。常规主备地址负责 Bundle 与默认元数据；白名单设备可额外注入版本元数据主备根地址。全部模板统一替换 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`。

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
| `m_MetadataRootUrl` / `m_MetadataRootUrlFallback` | `string` | 白名单设备的版本元数据主备根地址 |
| `m_Platform` | `PlatformType` | 运行平台枚举，构造器内通过 `ResolvePlatform()` 缓存 |
| `m_Channel` | `ChannelType` | Config 导出时同步到场景的启动期渠道快照 |
| `m_Package` | `string` | 包名，构造器缓存 |
| `m_Version` | `string` | 应用版本号，构造器内读取 `Application.version` |
| `m_RemoteBaseUrls` | `string[]` | 常规主备 URL 前缀缓存 |
| `m_MetadataBaseUrls` | `string[]` | 白名单版本元数据 URL 前缀缓存 |
| `m_AllBaseUrls` | `string[]` | 两组地址去重合集，供 DoH detect-only 预检 |

---

## 完整公开 API

```csharp
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package)
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package, ChannelType channel)
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package, ChannelType channel, string metadataRootUrl, string metadataRootUrlFallback)
public IReadOnlyList<string> BaseUrls { get; }
public IReadOnlyList<string> GetRemoteUrls(string fileName)
```

---

## 关键算法

`BuildRemoteUrlCache()`：先解析主/备配置值，解析顺序为：

1. 若配置值本身就是完整 URL，则直接使用
2. 为空或不是 URL，则该地址视为不可用

`GetRemoteUrls(fileName)`：

- `<Package>.version`、`<Package>_<PackageVersion>.hash`、`<Package>_<PackageVersion>.bytes`（`PackageVersion` 非空）：白名单元数据主备优先，再回退常规主备；白名单命中时 AssetManager 会在本次启动内按该顺序处理传输失败与内容校验失败。
- Bundle、`.json`、`.report` 及其他文件：只使用常规主备地址。

`ApplyTemplate(template)`：依次替换 `{Platform}`、`{Channel}`、`{Package}`、`{Version}`。

`BaseUrls`：返回常规与白名单元数据基地址的去重合集。`AssetManager` 在 Host/Web 包初始化前对全部实际启用地址执行 DoH detect-only 预检；YooAsset HTTPS 请求仍使用原域名，以保持 Host/SNI 语义。

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
