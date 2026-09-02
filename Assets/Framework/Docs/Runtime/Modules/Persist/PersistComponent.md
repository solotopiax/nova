# PersistComponent

`PersistComponent` 是 `Persist` 模块的场景入口。

它不直接做读写，而是在 `Awake` 时反射创建三种存储实现，在 `LoadAsync()` 里并行初始化，然后把访问入口统一暴露为：

- `Nova.Persist.PlayerPrefs`
- `Nova.Persist.FileFragment`
- `Nova.Persist.SQLite`

## 什么时候先看这页

- 你要确认三种持久化存储实现是怎么被创建和初始化的。
- 你在排查 `Cur...ManagerTypeName` 配错导致的启动异常。
- 你要决定某类数据该落到哪一种存储实现。

## 依赖与边界

### 它依赖什么

- `IPlayerPrefsManager`
- `IFileFragmentManager`
- `ISQLiteManager`
- `TypeCreator`
- `ProcedurePreload` 之类会显式等待 `LoadAsync()` 的启动流程

### 它对外负责什么

- 从 Inspector 指定的类型名创建三个存储实现实例
- 把 Inspector 配置翻译成三个具体 `Config`
- 统一提供三种持久化入口

### 它不负责什么

- 不负责存储实现内部读写逻辑
- 不负责调度每一帧保存，真正的自动保存由各 Manager 自己处理
- 不负责在 `Awake` 里完成初始化落盘，它只负责创建实例

## 核心流程

### 1. Awake 只做实例创建，不做初始化

`Awake()` 会按三个类型名分别创建：

- `IPlayerPrefsManager`
- `IFileFragmentManager`
- `ISQLiteManager`

任一创建失败都会直接抛 `InvalidOperationException`，不会降级运行。

### 2. LoadAsync 才是真正的存储实现就绪点

`LoadAsync()` 不是重复执行型方法，而是带缓存的惰性任务：

- 第一次调用时启动 `RunLoadAsync()`
- 后续调用返回同一份 `UniTask`

这保证了外部可以多处 await，而不会重复初始化三种存储实现。

### 3. 三种存储实现会并行初始化

`RunLoadAsync()` 用 `UniTask.WhenAll(...)` 同时初始化三种存储实现：

- `PlayerPrefsManagerConfig`
- `FileFragmentManagerConfig`
- `SQLiteManagerConfig`

其中 SQLite 额外接收 `CipherPassword`。

任一存储实现启用 AES 时，组件会在进入这段并行初始化前确认 `Util.Encrypt.AES` 默认 Key/IV 已由 `Nova.Config.LoadAsync()` 的隐私配置注入。缺配会记录包含 `Nova/Open Config → 通用配置 → 隐私配置` 的 Error 并抛出，避免 PlayerPrefs/SQLite 的惰性读取让 `LoadAsync()` 表面成功、实际尚未解密存档。标准启动链由 `ProcedureLoadDll` 先等待 Config；业务 `ProcedurePreload` 只需随后等待 Persist。自定义启动链若绕过 `ProcedureLoadDll`，仍必须先等待 Config，再等待 Persist。

### 4. Inspector 字段决定存储实现行为

组件层真正持有的是三类信息：

- 当前实现类类型名
- 是否启用 AES
- 自动保存间隔

SQLite 还多一个数据库级 `CipherPassword`。

## 选型边界

- `PlayerPrefs`：适合小体量键值数据，底层仍是统一键值写入。
- `FileFragment`：按 `classify` 切成多个 `.dat` 文件，适合按业务分片存档。
- `SQLite`：按 `classify` 映射成表，适合更强查询和更大体量数据，但有平台与插件前提。

## 框架内建的跨启动状态

`Nova.Persist` 是业务通用存储容器，但并非所有框架启动状态都经由它。必须在 `Persist.LoadAsync()` 之前读取的少量引导数据使用内部 `PlatformPlayerPrefs`；模块文件缓存则使用 `persistentDataPath`。

| 状态 | 存储 | 用途 / 边界 |
|---|---|---|
| `Nova.InstallTimeMs` | `PlatformPlayerPrefs` 原始键 `Nova.InstallTimeMs` | Nova 首次启动记录的 13 位 UTC Unix 毫秒时间戳；公开只读入口 |
| 推荐更新放弃时间 | `PlatformPlayerPrefs` 原始键 `Nova.App.RecommendedDownloadDismissedAtUnixSeconds` | App 模块内部的 UTC Unix 秒级冷却记录 |
| 当前语言 | `Nova.Persist.PlayerPrefs` 的 `LocalizationCommon::LocalizationLanguage` | 正式语言偏好；切换成功后立即保存 |
| 启动期语言镜像 | `PlatformPlayerPrefs` 原始键 `Nova.Localization.BootstrapLanguage.v1` | Persist 就绪前供 Launcher 解析语言 |
| Android 通知权限已请求标记 | Unity `PlayerPrefs` 键 `Nova.Native.NotificationPermissionRequested` | Android 13+ 区分未请求与已拒绝；Native 模块内部 |
| Asset 启动白名单设备 ID | `persistentDataPath/Asset/asset-check-device-id.dat` | Asset 启动白名单路由依据 |
| Asset 本地可启动清单身份 | `persistentDataPath/Asset/{package}.version` | 记录 `PackageVersion` 与 `PackageFilePrefix`，供远端不可达时回退 |
| 远程应用配置快照 | `persistentDataPath/Config/app-custom-config.json` | Config 模块的已校验 Custom 配置缓存 |
| Runtime Debugger 最近邮箱 | Unity `PlayerPrefs` 键 `RUNTIME_DEBUGGER_BUG_REPORT_LAST_EMAIL` | 仅属于调试表单便利状态，不是框架通用 API |

`PlatformPlayerPrefs` 不参与 Persist 的分类索引和 AES 处理，只用于必须在 Persist 初始化前可用的引导级轻量状态，不应扩展为普通业务存档入口。

## 风险点 / 易错点

- 只创建不初始化：在预加载流程完成前就读写存储实现，等于绕过就绪保证。
- 启用 AES 却绕过标准启动链先加载 Persist：会因默认 Key/IV 未就绪直接失败。标准链由 `ProcedureLoadDll` 先加载 Config；自定义链必须先执行 `await Nova.Config.LoadAsync()`，再执行 `await Nova.Persist.LoadAsync()`。
- `LoadAsync()` 失败会记日志并继续向上抛异常，不是静默失败。
- 类型名必须是可实例化的实现类全名，而不是接口或抽象基类。
- 组件销毁时只清引用和任务缓存，不主动替代 `FrameworkManagersGroup` 的正常 shutdown 顺序。

## 继续阅读

关键源码：

- [PersistComponent.cs](../../../../Scripts/Runtime/Modules/Persist/PersistComponent.cs)
- [PersistComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Persist/PersistComponent.Visitors.cs)

相关文档：

- [PlayerPrefsManager.md](PlayerPrefsManager.md)
- [FileFragmentManager.md](FileFragmentManager.md)
- [SQLiteManager.md](SQLiteManager.md)
- [PersistManagerBase.md](PersistManagerBase.md)
