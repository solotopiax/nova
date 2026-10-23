# IAppManager

`IAppManager` 定义的是启动期 App 大版本检查契约。

调用方真正应该依赖的是这些语义：

- 能发起一次版本检查并得到 `AppVersionResult`
- 能读取这次检查暴露出的命中规则和目标地址
- 能触发商店跳转或 APK 下载入口

内置 `AppManager` 还实现可选扩展接口 `IRecommendedDownloadDismissalRecorder`，用于记录用户主动放弃推荐更新。该能力不直接加入 `IAppManager`，避免破坏已有自定义实现。

## 契约定位

直接依赖它的通常是：

- `AppComponent`
- `ProcedureCheckVersion`
- `ProcedureAppDownload`

## 调用方可依赖的语义

### 1. `Initialize(config)` 是正式接管配置入口

组件层在 `Start()` 里会用它接管一整份 `AppManagerConfig`。

### 2. `CheckAsync(ct)` 返回启动路由判定结果

返回值只有三类：

- `NoDownload`
- `RecommendedDownload`
- `ForcedDownload`

调用方可以依赖：

- 这是启动链里的 App 大版本分流信号
- 不是资源补丁检查结果

### 3. `OpenStoreAsync(ct)` 和 `DownloadAsync(ct)` 是两条执行入口

- `OpenStoreAsync()`：商店跳转
- `DownloadAsync()`：APK 下载

内置 APK 下载仅支持 Android Player，完成后返回 persistentDataPath 下已提交的本地绝对路径。取消或失败保留旧正式文件；路径不表示签名、目标版本或安装成功，安装由独立 Native 能力或业务显式发起。

### 4. 检查状态通过属性暴露

- `MatchedRule`
- `TargetStoreUrl`
- `TargetDownloadUrl`

这些状态是给流程层继续决策用的。

### 5. 推荐更新放弃记录是可选扩展

- `IRecommendedDownloadDismissalRecorder.RecordRecommendedDownloadDismissed()` 只记录用户主动取消推荐更新的时间。
- `AppComponent.RecordRecommendedDownloadDismissed()` 会在当前 Manager 支持该接口时转发；不支持时保持兼容并输出 warning。
- 推荐提示间隔的读取与判断仍由内置 `AppManager.CheckAsync()` 完成。

## 变更影响面

如果这里的契约变化，会直接影响：

- [AppComponent.md](../AppComponent.md)
- [../../Procedure/ProcedureCheckVersion.md](../../Procedure/ProcedureCheckVersion.md)
- [../../Procedure/Procedures/ProcedureAppDownload.md](../../Procedure/Procedures/ProcedureAppDownload.md)

尤其高风险的是：

- `CheckAsync()` 返回枚举语义变化
- `MatchedRule` 的规则定义变化
- `DownloadAsync()` 的平台、取消、文件提交与独立安装边界

## 相关实现

关键源码：

- [IAppManager.cs](../../../../../Scripts/Runtime/Modules/App/Managers/AppManager/Interfaces/IAppManager.cs)

相关文档：

- [AppManager.md](AppManager.md)
- [AppManagerBase.md](AppManagerBase.md)
- [../AppComponent.md](../AppComponent.md)
- [../Definitions/AppVersionResult.md](../Definitions/AppVersionResult.md)
