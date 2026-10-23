# Util.SQLite

`NovaFramework.Runtime.Util.SQLite` 在 `!UNITY_WEBGL` 编译；同步包装 `SqlCipher4Unity3D.SQLiteConnection`。源码位于 `Runtime/Core/Util/Util.SQLite/`。

## 连接与维护

`CreateConnection(databasePath, password=null, synchronous=Normal)` 先规范路径登记连接，再创建原厂连接、设置 PRAGMA synchronous 并开启事务。password 为 null 沿用插件的明文行为；SynchronousLevel 为 Off=0、Normal=1、Full=2，不另外验证任意强转枚举。构造失败释放登记，PRAGMA/Begin 失败尝试 Dispose 后传播异常。内部 `CreateRegisteredConnection` 只打开并登记，不自行 Begin。

`SQLiteMaintenanceLease` 以 GetFullPath 后路径为进程内键，Windows 大小写不敏感、其他平台 Ordinal；同一锁内维护要求无维护/已登记连接，维护期间拒绝新登记。Scope 冻结路径和类别，Dispose 幂等释放；RegisteredConnection 在原厂 Dispose 的 finally 释放登记。它不关闭其他连接，不覆盖未参与登记的外部连接，也不提供跨进程排他锁。

`CloseConnection(null)` 无操作；否则依次提交现事务、尝试 WAL checkpoint、Close、Dispose。只有 checkpoint 异常转 Warning；Commit 或 Close 失败会中断后续释放。`CommitAndBeginTransaction(null)` 无操作，其他情况提交已有事务后立即开启新事务，不是原子提交及回滚。

## 表、行与 SQL

除上述两个可空入口外，连接仅检查非 null，不证明已打开、未释放或线程安全。表/列名要求 1..128 个 Unicode 字母、数字或下划线且首字符非数字，返回原文本并以方括号包裹；不检查业务权限、表存在或 Name 值。行的 `Name` 自动属性接受 null，不主动读写 SQL。

CreateTable 固定 `Name TEXT PRIMARY KEY`，追加列仅映射 int/bool→INTEGER、string→TEXT、float/double→REAL；不迁移已存在表。columns 为 null、Type 为 null、重复 Name 或不支持类型可失败。Insert/InsertOrReplace/Update 分开枚举字段 Keys 和 Values，不防并发变化；null 字典会失败，空字典可生成非法 SQL。Name 与字段值通过参数绑定；REPLACE 可替换整行并丢失未提供列值，Update 缺行不插入。CRUD 不自动 Commit。GetAllData 无排序；GetTableNames 只排除 sqlite_sequence，不保证排除全部内部表。

`ExecuteQuery<T>` 原样执行并映射 SQL 与 params；`ExecuteCommand` 返回 void，丢弃插件受影响行数。两者不验证 SQL 只读、目标表列或授权范围，参数绑定也不校验数量和类型。`ClearDatabase` 逐表 DROP，中途失败可留下部分结果，没有整体事务。

## 事务与删除

`ExecuteTransaction` 在 try 外先提交旧事务并 Begin，再执行 action、Commit；异常时 Rollback 并包装，finally 再 Begin。它不是 savepoint，不会回滚先前提交；null action 也在 try 内失败，Rollback 或 finally Begin 的异常可覆盖原结果。常规返回后仍保留新的活动事务。

`DeleteDatabase` 依次调用 SysIO 删除主文件、-shm、-wal、-journal，不先关连接或自动回滚。空路径只 Fatal 后返回；底层重试失败也可能仅记录日志，因此 void 返回不证明所有文件已删除。调用前应由真实连接所有者关闭，并验证所需删除结果。

## 关联文档

- [Util.SysIO.md](Util.SysIO.md)
- [SQLiteManager.md](../Modules/Persist/SQLiteManager.md)
