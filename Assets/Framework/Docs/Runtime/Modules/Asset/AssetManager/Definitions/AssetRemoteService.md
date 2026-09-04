# AssetRemoteService

**类签名**：`public sealed class AssetRemoteService : IRemoteService`
**命名空间**：`NovaFramework.Runtime`

YooAsset 远端寻址服务。常规主备地址负责 Bundle 与默认元数据；白名单设备可额外注入版本元数据主备根地址。全部模板统一替换 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`；Runtime `{Platform}` 由 Player 编译宏决定，不读取 Editor Active BuildTarget 或 ConfigMaster。

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
| `m_Platform` | `PlatformType` | Player 编译宏对应的运行平台枚举，构造器内通过 `ResolvePlatform()` 缓存 |
| `m_Channel` | `ChannelType` | Config 导出时同步到场景的启动期渠道快照 |
| `m_Package` | `string` | 包名，构造器缓存 |
| `m_Version` | `string` | 应用版本号，构造器内读取 `Application.version` |
| `m_RemoteBaseUrls` | `string[]` | 常规主备 URL 前缀缓存 |
| `m_MetadataBaseUrls` | `string[]` | 白名单版本元数据 URL 前缀缓存 |
| `m_AllBaseUrls` | `string[]` | 两组地址去重合集，供 `BaseUrls` 返回 |
| `m_WebGLBuiltinMetadataRootUrl` | `string` | WebGL 远端计划耗尽后临时使用的首包元数据根地址；不影响 Bundle |

---

## 完整公开 API

```csharp
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package)
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package, ChannelType channel)
public AssetRemoteService(string hostServerUrl, string hostServerUrlFallback, string package, ChannelType channel, string metadataRootUrl, string metadataRootUrlFallback)
public IReadOnlyList<string> BaseUrls { get; }
public IReadOnlyList<string> GetRemoteUrls(string fileName)
```

`BeginWebGLBuiltinMetadataFallback` / `EndWebGLBuiltinMetadataFallback` 是 AssetManager 使用的内部作用域开关。开启期间只有 `.version/.hash/.bytes` 指向首包根地址，结束后立即恢复常规远端候选。

---

## 关键算法

`BuildRemoteUrlCache()`：先解析主/备配置值，解析顺序为：

1. 若配置值本身就是完整 URL，则直接使用
2. 为空或不是 URL，则该地址视为不可用

`GetRemoteUrls(fileName)`：

- `<PackageFilePrefix_?><Package>.version`、`<PackageFilePrefix_?><Package>_<PackageVersion>.hash`、`<PackageFilePrefix_?><Package>_<PackageVersion>.bytes`（`PackageVersion` 非空）：按当前 Player 有效 `YooAssetSettings.PackageFilePrefix` 识别；白名单命中时按“白名单主备 → 常规主备”排序，未命中时使用“常规主备”。AssetManager 会在本次启动内按候选顺序处理传输失败与内容校验失败。
- Bundle、`.json`、`.report` 及其他文件：只使用常规主备地址。
- WebGL 首包回退期间：版本元数据只返回首包同源地址；Bundle 仍只使用常规主备地址。

`ApplyTemplate(template)`：依次替换 `{Platform}`、`{Channel}`、`{Package}`、`{Version}`。

`BaseUrls`：返回常规与白名单元数据基地址的去重合集。资源下载、CDN 与热更新由 Asset 模块通过 YooAsset 的 UnityWebRequest 后端和 AssetDownloadUrlPolicy 独立路由，不进入 HostKey + NetCmd 业务请求链。

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
