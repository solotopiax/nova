# SQLiteManager

`SQLiteManager` 是三种持久化存储实现里最偏“结构化缓存 + 批量提交”的实现。

它仍然沿用统一的 `classify -> item -> value` 逻辑模型，但底层把每个 `classify` 映射为一张 SQLite 表，并通过写缓冲、单次事务批量提交和 WAL 模式提升落盘效率。

## 什么时候先看这页

- 你要承载更大体量、更多分类的本地数据。
- 你在排查为什么写入后直到 `Save()` 才真正进入数据库。
- 你要确认 WebGL 或插件前提下 SQLite 存储实现的边界。

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

其中 `Load()` 并不会把所有表读进内存，只返回连接是否非 null。`OpenConnection()` 打开或设置 WAL 失败时会记录 Error、清空连接，Initialize 仍返回完成任务，且不检查 Load 的 bool；初始化任务完成不能证明数据库可查询。后续 `GetOrCreateTable()` 遇到空连接会抛出。

### 2. classify 必须是合法表名

`ValidateSQLiteClassify(...)` 明确限制：

- 不得为空
- 仅允许字母、数字、下划线

字符判断使用 `char.IsLetterOrDigit`，不限于 ASCII，也未禁止数字开头。`Save(classify)`、Has/Remove/Count 和条目枚举会调用这段校验；当前 GetXxx/SetXxx 经 `ReadRaw/WriteRaw` 只执行基础非空校验，尚未覆盖这段字符校验。业务应使用固定合法分类名，不能把任意用户输入当作分类；表名被拼入 SQL，只有条目名和值使用参数绑定。

### 3. 读写通过 GetOrCreateTable 进入缓存表对象

无论读还是写，都会先走：

- `GetOrCreateTable(classify)`

也就是说，表数据采用按需接入，而不是启动时全量建模。

### 4. Save 才真正把写缓冲提交到数据库

`SetXxx()` 系列的核心路径是：

- 写入表缓存
- 标记该表为 dirty

`Save()` 再遍历脏表并 `FlushToDb(...)`。成功的表会从脏集合移除，失败的保留等待下次重试。

每张表独立开启并提交事务，没有跨表原子性。无参 Save 逐表捕获异常并汇总 false；`Save(classify)` 对未缓存表返回 false，提交异常直接抛出。

### 5. 删除和读取也有明确的缓存边界

`GetOrCreateTable()` 会先把新 Table 加入缓存，再调用 EnsureLoaded；首次读取可能创建空表并执行 SELECT，SQL 或解密失败可留下尚未加载完成的缓存对象。`GetAllClassifyNames()` 查询数据库表名，其他条目枚举/Count 查询该表缓存。

`RemoveItem()` 只改缓存与待删缓冲，等待 Save；`RemoveAll(classify)` 只有在该表已经缓存时才执行 DROP TABLE，未访问的磁盘表不会被这次调用删除。DROP 之前缓存及缓冲已清空，SQL 失败没有自动恢复原缓存。

公开 `SetString(null)` 与 `SetBytes(null)` 都转为空串，不删除条目；SQLite 已存空串可原样读回。布尔非 `"1"` 为 false，数值失败与 JSON/Base64 解析规则见 [PersistManagerBase.md](PersistManagerBase.md#扩展类型默认实现)。

### 6. WebGL 是明确不可用，而不是能力弱化

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
- `Shutdown()` 尝试 Save，随后关闭/释放连接并清空缓存及脏集合；失败不会保留可供下一轮业务重试的缓存。Close/Dispose 位于同一 try，Close 抛出可能跳过 Dispose，异常会记录后继续清引用。

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

## Editor 值维护与连接边界

运行时 `OpenConnection` 使用 `Util.SQLite.CreateRegisteredConnection`，Inspector 仍用现 `CreateConnection`；两者连接都在创建前登记到 `SQLiteMaintenanceLease`，任意 Close/Dispose 对称释放。已登记连接存在时拒绝维护，不自动 Save 或关闭；维护期间拒绝新登记连接。该门仅约束参与 Nova 协议的连接，raw SQL 写者仍由真实 SQLite 事务写锁约束，不声明操作系统绝对排他。普通 factory BeginTransaction 与 CloseConnection Commit/checkpoint 语义保持。

同三个存档 Action 的 SQLite 值 AES 分支在原库 `BEGIN IMMEDIATE` 事务内逐行转换，不删除或替换正式库；先生成原厂一致逻辑备份，schema/原新值/单字段与恢复日志完整冻结。未知结构拒绝，CipherPassword 与 Config AES Key/IV 轮换不支持。Plan/Verify 的真实连接可能改变伴随文件，默认文件元数据检查仍严格不开库。详见 [存档维护](../../../Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)。
