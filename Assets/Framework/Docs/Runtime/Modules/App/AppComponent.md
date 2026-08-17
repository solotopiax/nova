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
- 当前节点 `DevelopMode` 对应的主/备检查地址与超时
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

### 运行期 API 都是薄透传

- `CheckAsync(ct)`
- `DownloadAsync(ct)`
- `OpenStoreAsync(ct)`

真正的行为语义都在 `AppManager`。

## 高价值状态面

- `MatchedRule`
- `TargetStoreUrl`
- `TargetDownloadUrl`
- `EnableAppUpdate`

这些都不是组件自己算出来的，而是 `AppManager` 在检查后暴露出来的状态。

## 风险点 / 易错点

- `Start()` 只注入配置，不会自动做检查。
- `DownloadAsync()` 虽然有门面，但当前底层实现仍是占位骨架，不是可用下载链。
- 大版本检查现在固定走主备双地址：主地址为空、失败、超时、返回空内容、JSON 无法解析或版本规则无效时自动切到备用；备用也不可用时返回 `NoDownload`。
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
