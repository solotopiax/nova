# TableComponent

`TableComponent` 是 Nova 表格系统的场景入口。它的职责很薄：

- 创建并持有 `ITableManager`
- 在 `Start()` 把 `TableSettings` 下发给管理器
- 对业务层暴露统一的加载与查询门面

它本身不做表构造细节、资源加载细节、Luban 反射细节，这些都在 `TableManager` 和 `Core/Table` 层。

## 什么时候先看这页

优先看这页的场景：

- 你要确认表格系统入口在哪。
- 你要判断 `LoadAsync / LoadSync / IsLoadOver` 的组件层语义。
- 你要知道什么时候该看 `TableManager`，什么时候该看 `TableSettings`。

如果你已经确认问题在两阶段加载、Luban 表反射、资源读取或缓存构造里，直接继续看 [TableManager.md](TableManager.md)。

## 依赖与边界

### 它依赖什么

- `ITableManager`
- `TableSettings`

### 它对外暴露什么

- 表格批量加载入口
- 表格存在性判断
- 表格查询门面

### 它不负责什么

- 不负责具体 JSON / 资源加载
- 不负责 `TableTables` 反射构造
- 不负责 `ITable` 具体实现行为
- 不负责单表配置的详细解释

## 核心流程

### Awake：校验设置并创建管理器

`Awake()` 做两件事：

1. 校验 `m_Setting` 不为空
2. 通过 `Util.TypeCreator.Create<ITableManager>` 创建 `m_TableManager`

这一步失败通常意味着：

- Inspector 没配 `TableSettings`
- 或 `m_CurManagerTypeName` 指向了无效实现

### Start：只初始化，不自动加载

`Start()` 只做：

- `m_TableManager.Initialize(new TableManagerConfig { UnitSettings = m_Setting.TableUnitsSettings })`

这里有一个很重要的边界：

- `Start()` **不会自动调用** `LoadAsync()` 或 `LoadSync()`

也就是说，表格数据是否真正就绪，取决于业务启动流程是否显式触发加载。

### LoadAsync / LoadSync：把“所有表可查询”这件事做完

`LoadAsync()` 和 `LoadSync()` 的目标不是加载某一张表，而是把整个表系统切到“可查询”状态。

`LoadAsync()` 的组件层语义：

- 成功后把 `IsLoadOver` 置为 `true`
- 已成功加载后直接复用结果
- 并发调用通过 `m_LoadTcs` 合并
- 管理器抛异常时，组件层会记日志并返回 `false`

`LoadSync()` 的组件层语义：

- 没有并发合并逻辑
- 同样会在成功后更新 `IsLoadOver`

## 高价值 API 面

### 1. 加载

- `LoadAsync()`
- `LoadSync()`
- `IsLoadOver`

用途：

- 判断整个表系统是否已经进入可查询状态

### 2. 存在性判断

- `HasTable<T>()`
- `HasTable(Type type)`

用途：

- 在真正取表前做保护性判断

### 3. 查询

- `GetTable<T>()`
- `GetTable(Type type)`
- `Count`

用途：

- 获取已经加载好的 `ITable`
- 看当前管理器里缓存了多少种表

## 关键状态

- `m_Setting`：表格系统的数据源配置入口，尤其是 `TableUnitsSettings`。
- `m_TableManager`：真正执行表加载与表查询的核心实现。
- `m_LoadTcs`：只服务于异步加载去重。
- `IsLoadOver`：表示“整套表系统是否加载成功”，不是单表状态。
- `Count`：透传自 `ITableManager.Count`，表示当前已缓存的表类型数。

## 风险点 / 易错点

- `Start()` 不会自动加载表；如果启动流程没显式调 `LoadAsync / LoadSync`，`GetTable<T>()` 的前提就不成立。
- `IsLoadOver` 只在最近一次成功加载后为 `true`；失败时仍可重试。
- `LoadAsync()` 的异常已经在组件层吞并并转成 `false`，如果要追真正原因，要继续看 `TableManager` 日志和实现。
- `TableComponent` 只负责门面。如果问题在单表资源、Luban 反射、类型名拼接、两阶段缓存构造，直接看 `TableManager`。

## 继续阅读

关键源码：

- [TableComponent.cs](../../../../Scripts/Runtime/Modules/Table/TableComponent.cs)
- [TableComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Table/TableComponent.Visitors.cs)
- [ITableManager.cs](../../../../Scripts/Runtime/Modules/Table/Managers/Interfaces/ITableManager.cs)

相关文档：

- [TableManager.md](TableManager.md)
- [ITableManager.md](Interfaces/ITableManager.md)
- [TableSettings.md](Definitions/TableSettings.md)
- [ITable.md](../../Core/Table/ITable.md)
