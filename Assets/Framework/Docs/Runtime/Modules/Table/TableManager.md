# TableManager

`TableManager` 是表格系统的运行时构建器。  
它做的事情可以概括成两步：

1. 把所有配置好的 JSON 单元读进 `LubanDataCache`
2. 用 `Namespace + TableTables` 把整套 `ITable` 实例构建出来

它不承载业务查询策略，也不负责“外层是否已经加载完成”的门控。

## 什么时候先看这页

优先看这页的场景：

- 你要排查表格为什么没加载出来。
- 你要看 `LoadTablesAsync` 和 `LoadTablesSync` 的真实差异。
- 你要确认表格构建为什么依赖 `ConfigManager.Namespace`。
- 你要判断 `GetTable<T>()` 取到的对象到底是怎么来的。

如果你要看组件入口和外观门面，先看 [TableComponent.md](TableComponent.md)。  
如果你要看契约语义，继续看 [ITableManager.md](Interfaces/ITableManager.md)。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `IConfigManager`
- `TableManagerConfig`
- `TableUnitSetting`
- `LubanDataReceiver`
- `LubanTablesLoader`

### 它对外负责什么

- 接收表格单元配置
- 加载整套表格 JSON 资源
- 构建 `Type -> ITable` 缓存
- 提供按类型查询

### 它不负责什么

- 不负责 Unity 场景组件生命周期
- 不负责外层的幂等状态门控
- 不负责业务代码的缓存策略
- 不负责配置导出

## 核心流程

### Initialize：建立一次加载所需的运行时上下文

`Initialize(TableManagerConfig config)` 会：

1. 记录 `m_UnitSettings`
2. 获取并缓存 `IAssetManager`
3. 清空 `m_Tables`
4. 新建 `m_DataCache`

这一步不加载表，只是把“去哪里读”和“把数据放哪儿”准备好。

### LoadTablesAsync：两阶段异步构建

第一阶段：

1. 为 `DataReceiver` 组装异步 `loadFunc`
2. 遍历 `m_UnitSettings`
3. 跳过 `AssetLocation` 为空的单元
4. 并发执行 `InternalLoadUnitAsync(...)`
5. 任何一个 unit 失败，整体返回 `false`

第二阶段：

1. 调 `BuildTablesFromCache()`
2. 通过 `IConfigManager.Namespace` 解析 `TableTables`
3. 把生成结果写入 `m_Tables`

### LoadTablesSync：同步版仍然是同一套两阶段模型

区别只有一个：

- 第一阶段的资源读取换成 `m_AssetManager.LoadSync<TextAsset>()`

表格缓存结构、构建入口和查询语义都不变。

### BuildTablesFromCache：真正把“原始数据”变成“可查表实例”

这里有三个关键事实：

1. 它不是直接反序列化某一张表，而是加载固定入口类名 `TableTables`
2. 它依赖 `IConfigManager.Namespace`
3. 成功后会 `m_DataCache.Clear(); m_DataCache = null;`

这说明它更像“一次构建器”，不是随时可无损重复执行的流式加载器。

## 高价值 API 面

### 1. 初始化与加载

- `Initialize(TableManagerConfig config)`
- `LoadTablesAsync()`
- `LoadTablesSync()`

### 2. 查询

- `HasTable<T>()`
- `HasTable(Type type)`
- `GetTable<T>()`
- `GetTable(Type type)`

### 3. 统计

- `Count`

## 关键状态

- `m_UnitSettings`：本次加载会读哪些数据单元
- `m_AssetManager`：表资源读取入口
- `m_DataCache`：Phase 1 的临时 JSON 缓存
- `m_Tables`：最终运行时表缓存，键是 `Type`
- `c_TableTablesClassName`：固定入口 `"TableTables"`

## 风险点 / 易错点

- `BuildTablesFromCache()` 强依赖 `IConfigManager`；场景里没有 `ConfigComponent` 时会直接失败。
- `Namespace` 不是从 `TableManagerConfig` 取，而是运行时实时从 `IConfigManager.Namespace` 取。
- `Count` 表示“已缓存的表类型数”，不是某张表的数据行数。
- 成功构建后 `m_DataCache` 会被清空并置空；当前实现更适合一次性加载，不应该假设同一生命周期里可以无条件重复全量加载。
- `GetTable<T>()` 查不到会返回 `null`，不是抛异常；业务层如果把它当成必定存在，空引用会在更后面才暴露。

## 继续阅读

关键源码：

- [TableManager.cs](../../../../Scripts/Runtime/Modules/Table/Managers/Implements/TableManager.cs)
- [TableManager.Methods.cs](../../../../Scripts/Runtime/Modules/Table/Managers/Implements/TableManager.Methods.cs)
- [TableManager.Visitors.cs](../../../../Scripts/Runtime/Modules/Table/Managers/Implements/TableManager.Visitors.cs)

相关文档：

- [TableComponent.md](TableComponent.md)
- [ITableManager.md](Interfaces/ITableManager.md)
- [TableManagerConfig.md](Definitions/TableManagerConfig.md)
- [TableManagerBase.md](Implements/TableManagerBase.md)
