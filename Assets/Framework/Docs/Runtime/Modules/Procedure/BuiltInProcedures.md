# BuiltIn Procedures（内置流程）

**命名空间**：`NovaFramework.Runtime`

当前框架内置 Procedure 只有 5 个，构成框架启动链。

## 当前流程链

```text
ProcedureSplash
  -> ProcedureCheckVersion
       -> [ForcedDownload]               ProcedureAppDownload
       -> [RecommendedDownload]          ProcedureAppDownload
       -> [NoDownload && RequiresStartupAssetWork] ProcedureHotfix
       -> [其他情况]                                ProcedureLoadDll

ProcedureCheckVersion
  -> [AppDownloadCheckUrl 为空]      App 检查降级为 NoDownload，继续后续流程
  -> [EnableHotfix = false]          只跳过常规资源热更检查，不跳过 App 大版本检查；WebGL Tags/All Warmup 仍会加载 Manifest
  -> [EnableHotfix = true]           继续资源清单加载与补丁判断

ProcedureAppDownload
  -> [ForcedDownload 取消]          Nova.Self.QuitApplication()
  -> [RecommendedDownload 取消且需启动资源工作] ProcedureHotfix
  -> [RecommendedDownload 取消且无需启动资源工作] ProcedureLoadDll
  -> [确认跳商店 / 下载或安装失败]    保持当前流程，重新显示更新弹窗；已下载 APK 只重试安装
  -> [APK 交付 / Installed 且旧进程仍存活] AppDownloaded 仅显示确认
  -> [AppDownloaded 推荐更新确认]    按资源工作需求进入 ProcedureHotfix 或 ProcedureLoadDll
  -> [AppDownloaded 强制更新确认]    Nova.Self.QuitApplication()

ProcedureHotfix
  -> [非 WebGL] Downloader 下载
  -> [WebGL TagsOnLaunch / AllOnLaunch] Bundle Warmup
  -> [成功] ProcedureLoadDll
  -> [失败且重试] 保持在当前流程重新执行
  -> [失败且允许跳过] ProcedureLoadDll
  -> [失败且必须退出] Nova.Self.QuitApplication()

ProcedureLoadDll -> 业务入口 Procedure
```

## 当前内置类型

| 文件 | 说明 |
|---|---|
| `Procedures/ProcedureSplash.cs` | 启动链入口，负责闪屏最短保底时长 |
| `Procedures/ProcedureCheckVersion.cs` | 大版本检查 + 资源补丁判断 + WebGL 启动资源路由 |
| `Procedures/ProcedureAppDownload.cs` | 强制/推荐更新弹窗、跳商店或下载 APK，并保存路径供可选独立安装重试 |
| `Procedures/ProcedureHotfix.cs` | 资源补丁下载或 WebGL Warmup，与失败重试 |
| `Procedures/ProcedureLoadDll.cs` | 加载 Config、AOT metadata、业务 DLL，并跳转业务入口 |

## 黑板数据

| 键名 | 类型 | 写入者 | 读取者 |
|---|---|---|---|
| `ProcedureDataKeys.AppVersionResult` | `AppVersionResult` | `ProcedureCheckVersion` | `ProcedureAppDownload` |
| `ProcedureDataKeys.HasAssetPatch` | `bool` | `ProcedureCheckVersion` | 当前只记录补丁判断结果；不再作为启动路由依据 |
| `ProcedureDataKeys.RequiresStartupAssetWork` | `bool` | `ProcedureCheckVersion` | `ProcedureAppDownload` |

## 关联文档

- [ProcedureComponent.md](ProcedureComponent.md)
- [ProcedureDataKeys.md](ProcedureDataKeys.md)
- [LauncherUI.md](LauncherUI.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- [../App/Definitions/AppVersionResult.md](../App/Definitions/AppVersionResult.md)
