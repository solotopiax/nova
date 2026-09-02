# AppComponent

`AppComponent` 是 `Nova.App` 对应的场景入口。

它本身不做版本判断、商店跳转或 APK 下载实现，职责只有两件事：

- 反射创建 `IAppManager`
- 把 Inspector 上的启动期 App 配置下发给 Manager

## 核心流程

### Awake：只创建 Manager

`Awake()` 会：

1. `base.Awake()`
2. `Util.TypeCreator.Create<IAppManager>(m_CurManagerTypeName)`

### Start：只注入配置

`Start()` 会把这些配置打包进 `AppManagerConfig`：

- App 更新总开关
- 当前节点 `DevelopMode` 对应的主/备检查地址与每次物理请求超时
- 版本检查主备完整轮数（默认 `1`）与额外完整重试次数（默认 `1`）
- 最近成功域名优先和 App 版本检查 UWR 埋点开关（均默认开启）
- 商店 / APK 路由与地址配置
- 推荐更新规则开关
- 强制更新规则开关

其中版本检查地址不再直接读取单一 Inspector 字段，而是：

- `DevelopMode = Debug` → 读取 Debug 主/备地址
- `DevelopMode = Release` → 读取 Release 主/备地址

选出当前模式对应的 URL 后，会按启动期快照替换以下可选占位符：

- `{Platform}`：编译宏对应的 `PlatformType` 枚举名
- `{Channel}`：Config 导出时同步到 `AppComponent` 的渠道快照
- `{Package}`：同场景 `AssetComponent` 的默认资源包名；为空时取包列表首项
- `{Version}`：`Application.version`

不含占位符的完整 URL 保持原样。占位符解析不依赖尚未加载的 `ConfigRuntimeSO`。

它不会在 `Start()` 阶段主动做一次版本检查。

`EnableAppUpdate` 默认关闭。关闭后 App 大版本检查稳定返回 `NoDownload`，启动链仍会继续执行 Asset 模块自己的热更新判断；需要 App 大版本检查时，由项目在 Inspector 中主动开启。

### 版本检查主备执行口径

`AppComponent` 只注入配置；实际候选编排由 `AppManager` 使用共享 `HttpFallback` 规划器完成。去重后的候选数为 `C`、完整轮数为 `R`、额外完整重试次数为 `K` 时，最多物理请求数为：

```text
C × R × (K + 1)
```

执行顺序固定为“首次执行/重试 → 完整轮次 → 当前候选”。例如主、备都有效、`R=1`、`K=1` 时，默认顺序为 `主 → 备 → 主 → 备`；这保证主备在每次完整执行中都有机会，`K` 不表示只重试最后一个失败域名。

最近成功域名优先只存在于当前 `AppManager` 进程内存中：下一条检查链会把最近取得**有效版本规则**的候选移到每轮首位。候选全部失败不会清除该偏好；配置不再包含该域名或 Manager 重置时才失效。

### 运行期 API 都是薄透传

- `CheckAsync(ct)`
- `RecordRecommendedDownloadDismissed()`
- `DownloadAsync(ct)`
- `OpenStoreAsync(ct)`

真正的行为语义都在 `AppManager`。

其中 `RecordRecommendedDownloadDismissed()` 只由推荐更新弹窗的取消分支调用。内置 Manager 会保存当前 UTC Unix 秒；自定义 Manager 未实现 `IRecommendedDownloadDismissalRecorder` 时不会中断启动，只会保持原有每次提示行为。

## 高价值状态面

- `MatchedRule`
- `TargetStoreUrl`
- `TargetDownloadUrl`
- `EnableAppUpdate`

这些都不是组件自己算出来的，而是 `AppManager` 在检查后暴露出来的状态。

## 风险点 / 易错点

- `Start()` 只注入配置，不会自动做检查。
- `DownloadAsync()` 虽然有门面，但当前底层实现仍是占位骨架，不是可用下载链。
- 大版本检查不是“主一次、备一次”的固定两步：它按完整轮数和重试次数执行去重后的候选，每次重试都会重新执行全部轮次。传输/客户端数据处理失败、`404`、`408`、`429`、`5xx`、空正文或无效 JSON/版本规则会推进；其他正式 HTTP 状态（包括 `401`）停止整链并返回 `NoDownload`。合法 JSON（包括不触发更新的 `NoDownload`）立即终止。
- 调用方取消时，内置 UWR 物理请求会被中止，且不会推进到下一个候选；每次物理请求独享配置的超时值。
- `EnableUWRTracks` 只控制 App 版本检查这条逻辑链的统一 UWR 埋点；底层物理入口不会另建链，避免同一次发送重复上报。
- `PrimaryDownloadUrl` / `FallbackDownloadUrl` 的 APK 下载语义完全未改；`DownloadAsync()` 仍是既有占位骨架。
- 地址选路依据是当前节点上的 `DevelopMode` 场景快照，而不是尚未加载的 `ConfigRuntimeSO`。
- URL 占位符大小写敏感；未知或拼写错误的占位符会保持原样，通常最终表现为请求失败并降级。

## 继续阅读

关键源码：

- [AppComponent.cs](../../../../Scripts/Runtime/Modules/App/AppComponent.cs)
- [AppComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/App/AppComponent.Visitors.cs)

相关文档：

- [AppManager/AppManager.md](AppManager/AppManager.md)
- [AppManager/IAppManager.md](AppManager/IAppManager.md)
- [Definitions/AppManagerConfig.md](Definitions/AppManagerConfig.md)
- [Definitions/AppVersionResult.md](Definitions/AppVersionResult.md)
- [../Procedure/ProcedureCheckVersion.md](../Procedure/ProcedureCheckVersion.md)
- [../Procedure/Procedures/ProcedureAppDownload.md](../Procedure/Procedures/ProcedureAppDownload.md)
