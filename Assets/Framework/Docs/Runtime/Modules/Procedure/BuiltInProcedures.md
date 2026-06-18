# BuiltIn Procedures（内置流程）

**命名空间**：`NovaFramework.Runtime`

当前框架内置 Procedure 只有 5 个，构成框架启动链。

## 当前流程链

```text
ProcedureSplash
  -> ProcedureCheckVersion
       -> [ForcedDownload]               ProcedureAppDownload
       -> [RecommendedDownload]          ProcedureAppDownload
       -> [NoDownload && HasAssetPatch]  ProcedureHotfix
       -> [其他情况]                      ProcedureLoadDll

ProcedureCheckVersion
  -> [AppDownloadCheckUrl 为空]      App 检查降级为 NoDownload，继续后续流程
  -> [EnableHotfix = false]          只跳过资源热更检查，不跳过 App 大版本检查
  -> [EnableHotfix = true]           继续资源清单加载与补丁判断

ProcedureAppDownload
  -> [ForcedDownload 取消]          Nova.Self.QuitApplication()
  -> [RecommendedDownload 取消且有补丁] ProcedureHotfix
  -> [RecommendedDownload 取消且无补丁] ProcedureLoadDll
  -> [确认后跳商店/下载]             保持在当前流程并重新显示弹窗

ProcedureHotfix
  -> [下载成功] ProcedureLoadDll
  -> [下载失败且重试] 保持在当前流程重新下载
  -> [下载失败且允许跳过] ProcedureLoadDll
  -> [下载失败且必须退出] Nova.Self.QuitApplication()

ProcedureLoadDll -> 业务入口 Procedure
```

## 当前内置类型

| 文件 | 说明 |
|---|---|
| `Procedures/ProcedureSplash.cs` | 启动链入口，负责闪屏最短保底时长 |
| `Procedures/ProcedureCheckVersion.cs` | 大版本检查 + 资源补丁判断 |
| `Procedures/ProcedureAppDownload.cs` | 强制/推荐更新弹窗、跳商店或下载 APK |
| `Procedures/ProcedureHotfix.cs` | 资源补丁下载与失败重试 |
| `Procedures/ProcedureLoadDll.cs` | 加载 Config、AOT metadata、业务 DLL，并跳转业务入口 |

## 黑板数据

| 键名 | 类型 | 写入者 | 读取者 |
|---|---|---|---|
| `ProcedureDataKeys.AppVersionResult` | `AppVersionResult` | `ProcedureCheckVersion` | `ProcedureAppDownload` |
| `ProcedureDataKeys.HasAssetPatch` | `bool` | `ProcedureCheckVersion` | `ProcedureAppDownload` / `ProcedureHotfix` |

## 关联文档

- [ProcedureComponent.md](ProcedureComponent.md)
- [ProcedureDataKeys.md](ProcedureDataKeys.md)
- [LauncherUI.md](LauncherUI.md)
- [Procedures/ProcedureLoadDll.md](Procedures/ProcedureLoadDll.md)
- [../App/Definitions/AppVersionResult.md](../App/Definitions/AppVersionResult.md)
