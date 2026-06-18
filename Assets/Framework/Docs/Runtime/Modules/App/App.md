# App 模块

**命名空间**：`NovaFramework.Runtime`  
**全局访问**：`Nova.App`

App 模块负责大版本检查、更新提示路由、跳商店与 APK 下载。当前事实以 `Assets/Framework/Scripts/Runtime/Modules/App/` 为准，旧的 `Launch` 命名不再代表独立模块。

## 当前目录结构

```text
Runtime/Modules/App/
├── AppComponent.cs / AppComponent.Visitors.cs
├── Definitions/
│   ├── AppDownloadRoute.cs
│   ├── AppDownloadRule.cs
│   ├── AppManagerConfig.cs
│   ├── AppVersionResponse.cs
│   └── AppVersionResult.cs
└── Managers/AppManager/
    ├── Interfaces/IAppManager.cs
    └── Implements/
        ├── AppManager.cs
        ├── AppManager.Visitors.cs
        ├── AppManager.Methods.cs
        ├── AppManager.Download.cs
        └── AppManagerBase.cs
```

## 模块边界

- `AppComponent` 是 Unity 侧入口，只做序列化持有与公开 API 转发。
- `IAppManager` / `AppManager` 承担版本检查、规则判断、下载与跳商店逻辑。
- `Definitions/*` 只承载 DTO、配置与枚举，不放流程逻辑。

## 当前公开入口

- `Nova.App.CheckAsync()`：执行大版本检查。
- `Nova.App.DownloadAsync()`：触发 APK 下载。
- `Nova.App.OpenStoreAsync()`：跳转商店。

## 子文档

| 文档 | 说明 |
|---|---|
| [AppComponent.md](AppComponent.md) | Component 入口与公开 API |
| [AppManager/IAppManager.md](AppManager/IAppManager.md) | Manager 接口 |
| [AppManager/AppManagerBase.md](AppManager/AppManagerBase.md) | 抽象基类 |
| [AppManager/AppManager.md](AppManager/AppManager.md) | 主实现 |
| [Definitions/AppManagerConfig.md](Definitions/AppManagerConfig.md) | 初始化配置 |
| [Definitions/AppVersionResult.md](Definitions/AppVersionResult.md) | 版本检查结果 |

## 关联文档

- [Runtime/Runtime.md](../../Runtime.md)
- [Procedure/ProcedureCheckVersion.md](../Procedure/ProcedureCheckVersion.md)
- [Procedure/Procedures/ProcedureAppDownload.md](../Procedure/Procedures/ProcedureAppDownload.md)
- [Editor/Inspectors/AppComponentInspector/AppComponentInspector.md](../../../Editor/Inspectors/AppComponentInspector/AppComponentInspector.md)
