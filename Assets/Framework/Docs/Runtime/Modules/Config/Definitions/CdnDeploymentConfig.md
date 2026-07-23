# CdnDeploymentConfig

**类签名**：`#if UNITY_EDITOR [Serializable] public sealed class CdnDeploymentConfig`
**命名空间**：`NovaFramework.Runtime`

CDN 内容部署与缓存清理的编辑态配置 DTO，共 10 个 string 字段。仅随 `ConfigMasterSO` 保存，不参与 `ConfigRuntimeSO` 导出；Runtime 侧无感知。

> 本类仅在 `#if UNITY_EDITOR` 代码块内定义，运行时程序集中不存在此类型。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/CdnDeploymentConfig.cs` | `CdnDeploymentConfig` | CDN 内容部署与缓存清理的编辑态配置（Editor-only） |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Endpoint` | `string` | `null` | 阿里云 OSS 标准地域 Endpoint |
| `AccessKeyID` | `string` | `null` | 阿里云访问密钥 ID |
| `AccessKeySecret` | `string` | `null` | 阿里云访问密钥 Secret；在 `ConfigMasterSO` 中以明文序列化 |
| `PresetOSSPath` | `string` | `null` | OSS 固定远端前缀，格式 `oss://bucket-name/fixed/prefix` |
| `LocalDirectory` | `string` | `null` | 项目根相对的本地部署目录；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `RemotePathSuffix` | `string` | `null` | 拼接在固定 OSS 前缀后的可编辑远端目录后缀；支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}` |
| `ZoneID` | `string` | `null` | Cloudflare Zone ID；当前页面可编辑字段 |
| `PurgeURL` | `string` | `null` | 旧版 Cloudflare Zone purge API 完整 URL；隐藏保留，仅用于已有资产兼容迁移 |
| `Token` | `string` | `null` | Cloudflare API Token；在 `ConfigMasterSO` 中以明文序列化 |
| `CachePaths` | `string` | `null` | 英文逗号、分号或换行分隔的待清理缓存 URL（`[TextArea(3, 8)]`） |

---

## §12 注意事项

- 整个类包裹在 `#if UNITY_EDITOR` 内，运行时程序集中不存在此类型
- `AccessKeySecret` / `Token` 在 `ConfigWindow` 界面上做遮罩显示，日志与 Console 输出做脱敏；**遮罩与脱敏 ≠ 存储加密**，`ConfigMasterSO` 资产文件仍以明文序列化保存，请避免将资产直接外发
- `LocalDirectory` 为**项目根相对路径**（`PAT-36`），禁止写入绝对路径
- `LocalDirectory` / `RemotePathSuffix` 保存占位符原文，部署时按当前 ConfigWindow 平台、Nova.prefab 的 AssetComponent 默认资源包名、`Application.version` 解析；规则与 Asset 主机服务器 URL 一致
- `CachePaths` 为多行文本（`[TextArea(3, 8)]`），支持英文逗号 `,`、分号 `;` 或换行分隔多个待清理 URL
- `ZoneID` 优先用于构造 Cloudflare purge 地址；`PurgeURL` 仅为旧资产兼容字段，不在页面显示
- `PresetOSSPath` 为固定前缀，`RemotePathSuffix` 为可编辑后缀，两者拼接后再叠加 `LocalDirectory` 下各文件的相对路径得到最终 OSS Object Key（见 `EditorUtil.CDN.CombineObjectKey`）

---

## §11 使用示例

```csharp
// EditorUtil.CDN 内部消费（internal，程序集外经 ConfigWindow CDN 面板触发）
CdnDeploymentConfig config = DimensionalResolver.ResolveCdn(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
int uploaded = await EditorUtil.CDN.DeployAsync(config, projectRoot);
int purged = await EditorUtil.CDN.PurgeAsync(config);
```

---

## §13 关联文档

- [ConfigMasterSO.md](../ConfigMasterSO.md)（`CdnDeployment` 顶层字段、`CdnMask` / `CdnOverrides` 维度字段）
- [CdnDeploymentOverride.md](CdnDeploymentOverride.md)（CDN 面板维度 Override 单项）
- [EditorUtil.CDN.md](../../../../Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.md)（`DeployAsync` / `PurgeAsync` 消费入口）
