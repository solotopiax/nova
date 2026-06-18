# ITableManager

`ITableManager` 是表格系统的运行时契约。  
调用方真正应该依赖的不是它有哪些成员，而是它承诺了哪些语义：

- 能否初始化
- 能否批量加载整套表
- 加载成功后如何统一查询

## 契约定位

它定义的是“表格管理器应该提供什么能力”，不是“表格实现细节应该怎么做”。

典型调用方：

- `TableComponent`
- 启动流程中的预加载逻辑
- 需要按 `ITable` 类型统一查询表的运行时代码

## 调用方可依赖的语义

### 1. 初始化语义

- `Initialize(TableManagerConfig config)` 只负责接收配置并建立运行时依赖
- 它不是加载动作本身

### 2. 加载语义

- `LoadTablesAsync()`：加载整套表，并返回是否成功
- `LoadTablesSync()`：同步版整套加载

调用方可以依赖：

- 成功后表查询入口可用
- 失败时不会给出“半成功即默认可查”的契约

### 3. 查询语义

- `HasTable<T>() / HasTable(Type)`：判断某类型表是否存在
- `GetTable<T>() / GetTable(Type)`：获取某类型表实例

调用方可以依赖：

- 查询维度是“表类型”
- 查询目标是实现了 `ITable` 的运行时表实例

### 4. 计数语义

- `Count` 表示当前已缓存的表类型数
- 它不是某一张表的数据行数

## 最小 API 面

- 初始化：`Initialize(TableManagerConfig config)`
- 加载：`LoadTablesAsync()` / `LoadTablesSync()`
- 查询：`HasTable<T>()` / `GetTable<T>()`
- 统计：`Count`

## 变更影响面

如果这里的加载或查询语义变化，会直接影响：

- [TableComponent.md](../TableComponent.md)
- [TableManager.md](../TableManager.md)
- 所有依赖 `Nova.Table.GetTable<T>()` 的业务代码

尤其是：

- 同步 / 异步加载边界
- 加载成功后的可查询保证
- 查询键从“表类型”改成别的形式

这些都属于高影响变更。

## 相关实现

关键源码：

- [ITableManager.cs](../../../../../Scripts/Runtime/Modules/Table/Managers/Interfaces/ITableManager.cs)

相关文档：

- [TableComponent.md](../TableComponent.md)
- [TableManager.md](../TableManager.md)
- [TableManagerBase.md](../Implements/TableManagerBase.md)
- [TableManagerConfig.md](../Definitions/TableManagerConfig.md)
- [ITable.md](../../../Core/Table/ITable.md)
