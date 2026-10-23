# SQLiteManager.Table

**类签名**：`internal sealed partial class Table`（嵌套于 `SQLiteManager`）
**命名空间**：`NovaFramework.Runtime`
**条件编译**：`#if !UNITY_WEBGL`

SQLiteManager 的内嵌类，表示单张 SQLite 表的内存缓存与写缓冲容器。采用写缓冲模式：读操作从 m_Items 内存字典返回，写操作累积到 m_PendingWrites，在 FlushToDb 时通过单次事务批量提交。支持懒加载，首次访问时通过 EnsureLoaded 从数据库全量 SELECT。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/SQLite/SQLiteManager.Table.cs` | CRUD 实现与事务提交逻辑 |
| `Managers/SQLite/SQLiteManager.Table.Visitors.cs` | 字段与属性定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Classify` | `string` | 表名（即 classify 名） |
| `Classify` | `string` | 表名只读属性 |
| `m_Items` | `Dictionary<string, string>` | 内存数据缓存，<条目名, 字符串值> |
| `Items` | `IReadOnlyDictionary<string, string>` | 内存缓存只读属性 |
| `m_PendingWrites` | `Dictionary<string, string>` | 写缓冲，<条目名, 待写入字符串值> |
| `m_PendingDeletes` | `HashSet<string>` | 待删除条目名集合 |
| `m_IsLoaded` | `bool` | 是否已从数据库加载全量数据 |
| `Count` | `int` | 条目数量 |

`Items` 是原字典的只读接口视图，不是副本；其中是已解密或待写的明文字符串。`Count` 不查询数据库，加载尚未完成时不代表磁盘行数。构造器仅保存分类名，不校验或建连。

## 公开 API

```csharp
// 构造
Table(string classify);

// 生命周期
void EnsureLoaded(SQLiteConnection connection, bool useAES);
void FlushToDb(SQLiteConnection connection, bool useAES);

// CRUD
bool HasItem(string item);
bool RemoveItem(string item);
void RemoveAll();
void ClearPendingBuffers();         // 清空写缓冲和删除缓冲（RemoveAll + DROP TABLE 场景）
string[] GetAllItemNames();
void GetAllItemNames(List<string> results);

// 读写
string GetString(string item, string defaultValue = "");
void SetString(string item, string value);
```

`EnsureLoaded()` 在未加载时先 CREATE TABLE，再查询 Name/Value 并逐行解密加入缓存，最后设置已加载标记。查询/解密失败直接传播，已加入的部分缓存不会自动回滚。内部 Row 的 Name/Value 是 SQL 映射 DTO，不验证、解密或提交数据。

`SetString()` 更新缓存与待写缓冲，并撤销同键待删；RemoveItem 反向取消待写并登记待删。容器层 SetString 原样接受 value，公开 Manager 才负责 null 转空串。RemoveAll 只登记当前缓存条目的删除，ClearPendingBuffers 仅清缓冲，两者都不执行 SQL；Manager 的整类删除另行 DROP TABLE。

`FlushToDb()` 无待提交项时直接返回；否则每表一次事务，Name/Value 参数绑定，表名仍来自分类字符串。BeginTransaction 位于 try 外；写入/删除/Commit 失败会尝试 Rollback 并重新抛出，缓冲只在提交成功后清空。列表枚举追加而不清空，传 null 且有条目时会抛出。

## 关联文档

- [SQLiteManager](SQLiteManager.md)
- [ISQLiteManager](ISQLiteManager.md)
