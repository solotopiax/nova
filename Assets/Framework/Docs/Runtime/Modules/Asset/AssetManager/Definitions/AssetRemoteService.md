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
| `m_Platform` | `PlatformType` | Player 编译宏对应的运行平台枚举，构造器内通过 `Util.UrlTemplate.ResolveRuntimePlatform()` 缓存 |
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

1. 若配置值以 http/https 开头，则替换模板后去掉尾部斜杠
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

## 地址输入与快照

构造时冻结平台、渠道、包名、Application.version 和地址数组；之后修改源配置不会刷新该实例。配置只按 http/https 前缀识别，再替换模板、去掉尾部 `/`，不 Trim 前导空白，也不完整校验 host、query 或 fragment；没有常规地址时记 Error 而不抛异常。`GetRemoteUrls` 因而可能返回空列表，不保证至少一项，也不编码 fileName。

主备根地址与 `BaseUrls` 合集按 OrdinalIgnoreCase 去重，可能合并仅路径大小写不同的根；拼接后的完整 URL 列表则按字符串精确去重。`BaseUrls` 暴露内部数组为 IReadOnlyList，并非深拷贝；调用方不应强转修改。临时首包根不进入该合集，Begin/End 仅替换一个字段，没有嵌套作用域或请求取消功能。

元数据识别使用当前 YooAsset 配置得到的精确 package version 文件名；hash/bytes 还要求匹配包前缀且中间版本非空，大小写敏感。不将任意 `.bytes` 当作白名单元数据；它与下载策略仅按扩展名分类的用途不同。
