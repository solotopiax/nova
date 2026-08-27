# CDNEditorConfigs

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class CDNEditorConfigs`
**命名空间**：`NovaFramework.Editor`

CDN 内容部署与缓存清理的编辑态配置 DTO。仅随 `ConfigMasterSO` 保存，不参与 `ConfigRuntimeSO` 导出；Runtime 侧无感知。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义，运行时程序集中不存在此类型。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Config/Definitions/CDNEditorConfigs.cs` | `CDNEditorConfigs` | CDN 内容部署与缓存清理的编辑态配置（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Endpoint` | `string` | `null` | 阿里云 OSS 标准地域 Endpoint |
| `AccessKeyID` | `string` | `null` | 阿里云访问密钥 ID |
| `AccessKeySecret` | `string` | `null` | 阿里云访问密钥 Secret；在 `ConfigMasterSO` 中以明文序列化 |
| `PresetOSSPath` | `string` | `null` | OSS 固定远端前缀，格式 `oss://bucket-name/fixed/prefix` |
| `VersionCheckLocalFilePath` | `string` | `null` | 项目根相对的版本检查本地文件位置；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `VersionCheckRemoteFilePath` | `string` | `null` | 拼接在固定 OSS 前缀后的版本检查云端文件位置；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `LocalDirectory` | `string` | `null` | 项目根相对的本地部署目录；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `AutoLinkLatestVersion` | `bool` | `true` | 自动从 `LocalDirectory` 指向的包根或版本目录关联最后生成的有效 YooAsset 版本目录；随 CDN 维度配置保存 |
| `RemotePathSuffix` | `string` | `null` | 拼接在固定 OSS 前缀后的可编辑远端目录后缀；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `AutoLinkLatestAssetCheckVersionFiles` | `bool` | `true` | 白名单部署是否以 `.bytes` 配置路径的父目录为锚点，自动关联最新完整版本的 `.bytes/.hash/.version` 三文件；独立于热更资源开关并随 CDN 维度保存 |
| `ZoneID` | `string` | `null` | Cloudflare Zone ID；当前页面可编辑字段 |
| `PurgeURL` | `string` | `null` | 旧版 Cloudflare Zone purge API 完整 URL；隐藏保留，仅用于已有资产兼容迁移 |
| `Token` | `string` | `null` | Cloudflare API Token；在 `ConfigMasterSO` 中以明文序列化 |
| `CachePaths` | `string` | `null` | 英文逗号、分号或换行分隔的待清理缓存 URL（`[TextArea(3, 8)]`） |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- `AccessKeySecret` / `Token` 在 `ConfigWindow` 界面上做遮罩显示，日志与 Console 输出做脱敏；**遮罩与脱敏 ≠ 存储加密**，`ConfigMasterSO` 资产文件仍以明文序列化保存，请避免将资产直接外发
- `VersionCheckLocalFilePath` 与 `LocalDirectory` 为**项目根相对路径**（`PAT-36`），禁止写入绝对路径
- `VersionCheckLocalFilePath` / `VersionCheckRemoteFilePath` / `LocalDirectory` / `RemotePathSuffix` 保存占位符原文；此 Editor 部署链中 `{Platform}` 取 Unity 当前 Active BuildTarget 映射的 `PlatformType`，`{Channel}` 取 ConfigMaster 当前 Channel，`{Package}` / `{Version}` 分别取默认资源包名 / `Application.version`。这不改变 Runtime Asset 主机服务器 URL 由编译宏解析 `{Platform}` 的契约
- `AutoLinkLatestVersion` 开启时，界面展示与实际部署都会重新解析最新有效版本目录；关闭后 `LocalDirectory` 必须手动指向待部署目录
- `AutoLinkLatestAssetCheckVersionFiles` 开启时，界面和部署以已配置 `.bytes` 文件的父目录为锚点重新解析最新版本，并通过 YooAsset 当前 `PackageFilePrefix + PackageName + PackageVersion` 命名规则生成匹配的三个文件位置；关闭后恢复三项手工配置
- 自动关联不比较目录名：YooAsset 的 `PackageVersion` 是任意字符串。候选目录必须同时包含匹配的 `.version`、`.bytes`、`.hash` 和 `.report`，且 report 引用的全部 bundle 文件存在；按 `.version` 的 `LastWriteTimeUtc` 选择最新，多个候选时间完全相同时明确报歧义
- 版本检查文件位置与热更资源目录会合并进入“批量部署到 CDN”的上传计划；本地文件或目录错误会在对话框和日志中包含解析后的具体路径
- `CachePaths` 为多行文本（`[TextArea(3, 8)]`），支持英文逗号 `,`、分号 `;` 或换行分隔多个待清理 URL
- `ZoneID` 优先用于构造 Cloudflare purge 地址；`PurgeURL` 仅为旧资产兼容字段，不在页面显示
- `PresetOSSPath` 为固定前缀，`RemotePathSuffix` 为可编辑后缀，两者拼接后再叠加 `LocalDirectory` 下各文件的相对路径得到最终 OSS Object Key（见 `EditorUtil.CDN.CombineObjectKey`）

---

## §11 使用示例

```csharp
// EditorUtil.CDN 内部通用解析示例（internal，程序集外经 ConfigWindow CDN 面板触发）。
// 这里显式传 Android 是底层 Resolver API 的用法；ConfigWindow / Pipify 的平台值实时来自 Unity Active BuildTarget。
CDNEditorConfigs config = DimensionalResolver.ResolveCDNEditorConfigs(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
int uploaded = await EditorUtil.CDN.DeployAsync(config, projectRoot);
int purged = await EditorUtil.CDN.PurgeAsync(config);
```

---

## §13 关联文档

- [ConfigMasterSO.md](../ConfigMasterSO.md)（`CDNEditorConfigs` 顶层字段、`CDNEditorConfigsMask` / `CDNEditorConfigsOverrides` 维度字段）
- [CDNEditorConfigsOverride.md](CDNEditorConfigsOverride.md)（CDN 面板维度 Override 单项）
- [EditorUtil.CDN.md](../../EditorUtil/EditorUtil.CDN/EditorUtil.CDN.md)（`DeployAsync` / `PurgeAsync` 消费入口）
