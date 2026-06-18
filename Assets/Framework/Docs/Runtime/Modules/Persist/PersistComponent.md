# PersistComponent

`PersistComponent` 是 `Persist` 模块的场景入口。

它不直接做读写，而是在 `Awake` 时反射创建三套后端，在 `LoadAsync()` 里并行初始化，然后把访问入口统一暴露为：

- `Nova.Persist.PlayerPrefs`
- `Nova.Persist.FileFragment`
- `Nova.Persist.SQLite`

## 什么时候先看这页

- 你要确认三套持久化后端是怎么被创建和初始化的。
- 你在排查 `Cur...ManagerTypeName` 配错导致的启动异常。
- 你要决定某类数据该落到哪一种后端。

## 依赖与边界

### 它依赖什么

- `IPlayerPrefsManager`
- `IFileFragmentManager`
- `ISQLiteManager`
- `TypeCreator`
- `ProcedurePreload` 之类会显式等待 `LoadAsync()` 的启动流程

### 它对外负责什么

- 从 Inspector 指定的类型名创建三个后端实例
- 把 Inspector 配置翻译成三个具体 `Config`
- 统一提供三种持久化入口

### 它不负责什么

- 不负责后端内部读写逻辑
- 不负责调度每一帧保存，真正的自动保存由各 Manager 自己处理
- 不负责在 `Awake` 里完成初始化落盘，它只负责创建实例

## 核心流程

### 1. Awake 只做实例创建，不做初始化

`Awake()` 会按三个类型名分别创建：

- `IPlayerPrefsManager`
- `IFileFragmentManager`
- `ISQLiteManager`

任一创建失败都会直接抛 `InvalidOperationException`，不会降级运行。

### 2. LoadAsync 才是真正的后端就绪点

`LoadAsync()` 不是重复执行型方法，而是带缓存的惰性任务：

- 第一次调用时启动 `RunLoadAsync()`
- 后续调用返回同一份 `UniTask`

这保证了外部可以多处 await，而不会重复初始化三套后端。

### 3. 三后端会并行初始化

`RunLoadAsync()` 用 `UniTask.WhenAll(...)` 同时初始化三套后端：

- `PlayerPrefsManagerConfig`
- `FileFragmentManagerConfig`
- `SQLiteManagerConfig`

其中 SQLite 额外接收 `CipherPassword`。

### 4. Inspector 字段决定后端行为

组件层真正持有的是三类信息：

- 当前实现类类型名
- 是否启用 AES
- 自动保存间隔

SQLite 还多一个数据库级 `CipherPassword`。

## 选型边界

- `PlayerPrefs`：适合小体量键值数据，底层仍是统一键值写入。
- `FileFragment`：按 `classify` 切成多个 `.dat` 文件，适合按业务分片存档。
- `SQLite`：按 `classify` 映射成表，适合更强查询和更大体量数据，但有平台与插件前提。

## 风险点 / 易错点

- 只创建不初始化：在预加载流程完成前就读写后端，等于绕过就绪保证。
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

