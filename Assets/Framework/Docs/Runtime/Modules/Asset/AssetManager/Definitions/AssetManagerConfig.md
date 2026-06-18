# AssetManagerConfig

**类签名**：`public sealed class AssetManagerConfig`
**命名空间**：`NovaFramework.Runtime`

AssetManager 启动配置，由 `AssetComponent.Start()` 现场 `new AssetManagerConfig{...}` 构造后传入 Manager。

> 纯 DTO，不标注 `[Serializable]`。Inspector 字段散在 `AssetComponent` 上，`Start()` 现场构造透传。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/AssetManager/Definitions/AssetManagerConfig.cs` | `AssetManagerConfig` | 配置数据容器 |

---

## 完整公开 API

字段按源码定义顺序排列（与 AssetComponent.Visitors.cs 六段语义一致）。

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `EditorPlayMode` | `AssetPlayMode` | `EditorSimulateMode` | 编辑器下资源加载模式（`Application.isEditor == true` 时生效）；4 个枚举值均允许选择 |
| `RuntimePlayMode` | `AssetPlayMode` | `HostPlayMode` | 终端（Player）下资源加载模式；不允许 EditorSimulateMode，仅限 OfflinePlayMode / HostPlayMode / WebPlayMode；与 EnableHotfix 双向联动（详见 AssetComponentInspector） |
| `Packages` | `List<string>` | `null` | 需要 CreatePackage 的包名列表，至少包含一个默认包（由 AssetComponent.m_Packages 传入） |
| `DefaultPackageName` | `string` | `null` | 默认包名；为空时取 Packages[0] |
| `AutoCleanupOnSceneUnload` | `bool` | `false` | 场景卸载后是否自动 CleanupAsync 默认包 |
| `EnableHotfix` | `bool` | `true` | **热更新功能总开关**；关闭时启动直跳 ProcedureLoadDll，跳过 CheckVersion / Hotfix / AppDownload 三个 Procedure |
| `AutoHotfix` | `bool` | `true` | 启动期资源补丁就绪后是否自动开始下载 |
| `QuitOnFailedOrCancel` | `bool` | `false` | 下载失败或取消时是否强制退出应用 |
| `MaxDownloadConcurrency` | `int` | `5` | 资源补丁下载最大并发数（推荐 3-8） |
| `RetryDownloadCount` | `int` | `3` | 单文件下载失败自动重试次数，0 表示不重试 |
| `CheckTimeout` | `int` | `5` | 版本检查空闲超时秒数（连续无新字节流入时中止请求） |
| `IdleTimeout` | `int` | `20` | 文件下载空闲超时秒数（连续无新字节流入时中止下载） |
| `HostServerUrl` | `string` | `null` | 当前节点 `DevelopMode` 已选定的主下载地址 URL；默认直接填写完整 URL 模板 |
| `HostServerUrlFallback` | `string` | `null` | 当前节点 `DevelopMode` 已选定的备用下载地址 URL |
| `DecryptorType` | `AssetDecryptorType` | `None` | AssetBundle 解密器类型 |
| `LaunchHotfixTags` | `List<string>` | `null` | 启动期热更按 tag 过滤的 tag 列表；非空时 ProcedureHotfix 用 CreateDownloaderByTags 替代整包下载；null/空表示下载整包 |
| `AutoClearUnusedCacheOnHotfix` | `bool` | `false` | 热更完成后是否自动执行 ClearUnusedCacheAsync 清理冗余磁盘缓存 |

---

## 使用示例

```csharp
// AssetComponent.Start() 内部（仅供理解，不需手动调用）
m_AssetManager.Initialize(new AssetManagerConfig
{
    EditorPlayMode = m_EditorPlayMode,
    RuntimePlayMode = m_RuntimePlayMode,
    Packages = m_Packages,
    DefaultPackageName = m_DefaultPackageName,
    AutoCleanupOnSceneUnload = m_AutoCleanupOnSceneUnload,
    EnableHotfix = m_EnableHotfix,
    AutoHotfix = m_AutoHotfix,
    QuitOnFailedOrCancel = m_QuitOnFailedOrCancel,
    MaxDownloadConcurrency = m_MaxDownloadConcurrency,
    RetryDownloadCount = m_RetryDownloadCount,
    CheckTimeout = m_CheckTimeout,
    IdleTimeout = m_IdleTimeout,
    HostServerUrl = ResolveHostServerUrl(),
    HostServerUrlFallback = ResolveHostServerUrlFallback(),
    DecryptorType = m_DecryptorType,
    LaunchHotfixTags = m_LaunchHotfixTags,
    AutoClearUnusedCacheOnHotfix = m_AutoClearUnusedCacheOnHotfix,
});
```

---

## 关联文档

- [AssetComponent.md](../../AssetComponent.md)
- [AssetPlayMode.md](../../Definitions/AssetPlayMode.md)
- [AssetComponentInspector.md](../../../../../Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.md)
