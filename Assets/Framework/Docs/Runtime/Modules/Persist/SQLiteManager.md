# SQLiteManager

`SQLiteManager` 是三套持久化后端里最偏“结构化缓存 + 批量提交”的实现。

它仍然沿用统一的 `classify -> item -> value` 逻辑模型，但底层把每个 `classify` 映射为一张 SQLite 表，并通过写缓冲、单次事务批量提交和 WAL 模式提升落盘效率。

## 什么时候先看这页

- 你要承载更大体量、更多分类的本地数据。
- 你在排查为什么写入后直到 `Save()` 才真正进入数据库。
- 你要确认 WebGL 或插件前提下 SQLite 后端的边界。

## 依赖与边界

### 它依赖什么

- `PersistManagerBase<SQLiteManagerConfig>`
- SqlCipher4Unity3D 插件
- 本地 SQLite 连接
- `SQLiteManager.Table`

### 它对外负责什么

- 把分类映射到表，把条目映射到行
- 提供统一 `GetXxx / SetXxx / Save / Remove` 接口
- 用写缓冲和事务批量提交脏表

### 它不负责什么

- 不开放原生 SQL 查询接口给业务层
- 不在每次 `SetXxx()` 时立即执行数据库写入
- 不在 WebGL 上提供降级数据库能力

## 核心流程

### 1. Initialize 会立即建连，但表数据走懒加载

非 WebGL 下初始化顺序是：

1. `InitializeBase(config)`
2. 记录 `CipherPassword`
3. `OpenConnection()`
4. `Load()`

其中 `Load()` 并不会把所有表读进内存，它只确认连接可用。

### 2. classify 必须是合法表名

`ValidateSQLiteClassify(...)` 明确限制：

- 不得为空
- 仅允许字母、数字、下划线

这不是文档建议，而是运行时硬校验。

### 3. 读写通过 GetOrCreateTable 进入缓存表对象

无论读还是写，都会先走：

- `GetOrCreateTable(classify)`

也就是说，表数据采用按需接入，而不是启动时全量建模。

### 4. Save 才真正把写缓冲提交到数据库

`SetXxx()` 系列的核心路径是：

- 写入表缓存
- 标记该表为 dirty

`Save()` 再遍历脏表并 `FlushToDb(...)`。成功的表会从脏集合移除，失败的保留等待下次重试。

### 5. WebGL 是明确不可用，而不是能力弱化

源码里对 `UNITY_WEBGL` 的处理很明确：

- 初始化时给出 warning
- 各操作直接返回默认值或空结果

这意味着它不是“部分可用”，而是“不可作为 WebGL 持久化方案”。

## 适合它的场景

- 分类很多、数据量更大、需要更稳健批量提交
- 你需要比文件分片更适合长期演进的数据组织方式
- 你接受额外插件依赖与平台限制

## 风险点 / 易错点

- 它虽然底层是 SQLite，但对业务暴露的仍是键值持久化语义，不是任意 SQL。
- `Save(classify)` 是按脏表提交，不等于全库事务边界外的复杂一致性保证。
- WebGL 不可用这点必须在选型时提前考虑，不能靠上线后再兜底。
- `CipherPassword` 是数据库级加密密码；同时还有 `UseAESEncrypt` 的值级加密，两者不是一回事。

## 继续阅读

关键源码：

- [SQLiteManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/SQLite/SQLiteManager.cs)
- [SQLiteManager.Methods.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/SQLite/SQLiteManager.Methods.cs)
- [SQLiteManager.Table.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/SQLite/SQLiteManager.Table.cs)

相关文档：

- [PersistComponent.md](PersistComponent.md)
- [ISQLiteManager.md](ISQLiteManager.md)
- [SQLiteManagerConfig.md](SQLiteManagerConfig.md)
- [SQLiteManager.Table.md](SQLiteManager.Table.md)

