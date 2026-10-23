# TableComponent

`TableComponent` 是 Nova 表格系统的场景入口。它的职责很薄：

- 创建并持有 `ITableManager`
- 在 `Start()` 把 加载描述 下发给管理器
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

- 不负责具体格式解码或资源加载
- 不负责生成 `Tables` 的 Codec 解码
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

- `m_TableManager.Initialize(new TableManagerConfig { LoadDescriptions = m_Setting.Runtime.LoadDescriptions })`

这里有一个很重要的边界：

- `Start()` **不会自动调用** `LoadAsync()` 或 `LoadSync()`

也就是说，表格数据是否真正就绪，取决于业务启动流程是否显式触发加载。

### LoadAsync / LoadSync：报告批量加载结果

`LoadAsync()` 和 `LoadSync()` 调用 Manager 的批量加载入口。当前 Manager 返回 `true` 的条件是查询字典非空；它不保证每个加载描述都提供了表，具体注册和失败边界见 [TableManager.md](TableManager.md)。

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

- 获取批量加载结果；必需表是否存在仍需按具体类型检查

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

### 4. 直接注册

- `RegisterTables(ILubanTables tables)`

用途：

- 注册业务使用 Luban 原生构造器自行创建的 Tables 容器
- 在不使用 Nova 资源加载适配时继续复用统一查询门面

## 关键状态

- `m_Setting`：Editor Luban Project 与 Player 加载描述 的配置入口。
- `m_TableManager`：真正执行表加载与表查询的核心实现。
- `m_LoadTcs`：只服务于异步加载去重。
- `IsLoadOver`：保存组件的成功状态；加载返回 `true` 或直接注册成功会置为 `true`。它不是整套表完整性检查，直接注册失败也不会清除已有的 `true`。
- `Count`：透传自 `ITableManager.Count`，表示当前已缓存的表类型数。

## 风险点 / 易错点

- `Start()` 不会自动加载表；如果启动流程没显式调 `LoadAsync / LoadSync`，`GetTable<T>()` 的前提就不成立。
- `IsLoadOver == true` 时，`LoadAsync()` / `LoadSync()` 直接返回 `true`，不会重新加载；直接注册至少一张表也会进入这个状态。
- `LoadAsync()` / `LoadSync()` 捕获 Manager 异常并转成 `false`；失败时可重试，但查询字典可能保留旧表，也可能只含部分新表，组件不会回滚。追查原因需看日志和 `TableManager` 实现。
- `TableComponent` 只负责门面。如果问题在 Binding、原始单表资源、生成 Tables 构造或解码，直接看 `TableManager` 和生成 Binding。

LoadSync 不与在途异步加载合流；直接 RegisterTables 的异常也不在组件中捕获。Count 缺 Manager 时为 0，其余查询通常直接转发而无就绪门；getter 本身不加载。OnDestroy 只置空 TCS/Manager 并清 IsLoadOver，不执行 Manager.Shutdown 或取消任务，不能据组件引用已清证明加载已结束。

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
