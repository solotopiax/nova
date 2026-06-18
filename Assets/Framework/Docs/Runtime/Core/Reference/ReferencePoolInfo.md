# ReferencePoolInfo

**类签名**：`public struct ReferencePoolInfo`
**命名空间**：`NovaFramework.Runtime`

引用池信息结构体，记录某个引用类型在引用池中的运行时统计数据。用于调试面板展示引用池的使用状况，包括未使用数量、正在使用数量、累积获取/归还/新增/移除次数。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `ReferencePoolInfo.cs` | 结构体定义 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Type` | `Type` | 引用池管理的引用类型 |
| `UnusedReferenceCount` | `int` | 当前未使用（池中等待复用）的引用数量 |
| `UsingReferenceCount` | `int` | 当前正在使用（已取出未归还）的引用数量 |
| `GetReferenceCount` | `int` | 累积获取引用的次数 |
| `PutReferenceCount` | `int` | 累积归还引用的次数 |
| `AddReferenceCount` | `int` | 累积新增引用的数量 |
| `RemoveReferenceCount` | `int` | 累积移除引用的数量 |

## 公开 API

```csharp
ReferencePoolInfo(
    Type type,
    int unusedReferenceCount,
    int usingReferenceCount,
    int getReferenceCount,
    int putReferenceCount,
    int addReferenceCount,
    int removeReferenceCount);
```

## 关联文档

- [ReferencePool](ReferencePool.md)
- [IReference](IReference.md)
