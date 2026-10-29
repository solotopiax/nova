# PersistComponent

`PersistComponent` 是 `Persist` 模块的场景入口。

它不直接做读写，而是在 `Awake` 时反射创建三种存储实现，在 `LoadAsync()` 里并行初始化，然后把访问入口统一暴露为：

- `Nova.Persist.PlayerPrefs`
- `Nova.Persist.FileFragment`
- `Nova.Persist.SQLite`

这里描述的是现有实现事实。三个属性分别直接返回 `IPlayerPrefsManager`、`IFileFragmentManager`、`ISQLiteManager`，当前 `PersistComponent` 没有对应的业务 CRUD/保存转发面；按 [NA-001/002](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md)，这属于普通业务消费入口缺口，不能因属性公开或从 Component 取得就认定合规。新增或修改业务存储调用时先核对当前版本是否已补齐 Component 入口；未补齐则记录所需读写、保存、删除和耐久语义，暂停依赖该入口的接入，不自建第二套存储或注入 Manager 绕行。现有调用只作事实诊断，未经授权不批量迁移；框架内部、隔离测试和受控维护仍按各自职责与合同判断。

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
- 首次任务通过 `ToAsyncLazy()` 共享，后续调用返回同一完成源支持的 `UniTask`

这保证了初始化未完成时的并发等待与完成后的重复等待都不会重复初始化三种存储实现。成功、异常或取消结果由共享任务保留；失败后的再次等待不会自动重新初始化。Editor 隐私维护门仍在读取缓存前执行。

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

`CurPlayerPrefsManagerTypeName`、`CurFileFragmentManagerTypeName`、`CurSQLiteManagerTypeName` 的源码初始值分别为 `NovaFramework.Runtime.PlayerPrefsManager`、`NovaFramework.Runtime.FileFragmentManager`、`NovaFramework.Runtime.SQLiteManager`；getter 只返回序列化字段，不验证类型或创建对象。三个 Manager 访问属性同样只返回当前引用，在 `Awake` 前或 `OnDestroy` 后可能为 null；引用非 null 不代表初始化或读写成功。

`RunLoadAsync()` 完成只说明三条 `Initialize` 调用已完成。默认 SQLite 的建连异常会在 `OpenConnection()` 内记录并清空连接，Initialize 仍可正常完成；WebGL SQLite 也仅警告并完成。需要实际 SQLite 可用性时还应核对所选实现的 `Load()` 结果及业务读写，不能把组件共享任务完成当作三种存储耐久性证明。

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


Editor 对现有标准 SQLite 库的非空 `CipherPassword` 轮换复用 `nova-project-integrate-persistence` 的同三 Action，独立 `operation="rotate-sqlite-cipher"`，可自动生成新凭据。macOS Editor 先用项目 Keychain 封存旧/新和独立备份秘密，再同库 rekey 并保存唯一组件密码字段；跨重载恢复仍绑定原迁移 token。源空/明文库转换、设备重启耐久性以及 Privacy AES Key/IV 跨存储轮换另有边界，见 [存档维护契约](../../../Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)。


Editor 的 `LoadAsync()` 在检查已有加载任务之前执行 `PrivacyAesMaintenanceGuard.AssertReady()`；未结束、损坏或无法核实的当前项目维护标记会拒绝读取默认 AES 存档，禁用 Domain Reload 也不能绕过。该门禁仅 `UNITY_EDITOR`，不改变 Player 初始化链。
