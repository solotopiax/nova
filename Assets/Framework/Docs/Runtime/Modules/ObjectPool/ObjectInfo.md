# ObjectInfo

**类签名**：`[StructLayout(LayoutKind.Auto)] public struct ObjectInfo`
**命名空间**：`NovaFramework.Runtime`

对象池中对象的只读快照信息结构体。用于 `ObjectPoolBase.GetAllObjectInfos()` 返回池内所有对象的状态，供调试面板或监控工具读取，不持有实际对象引用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Definitions/ObjectInfo.cs` | 结构体定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 对象在池内的检索名称 |
| `Locked` | `bool` | 对象是否被加锁（加锁后不会被自动回收） |
| `CustomCanReleaseFlag` | `bool` | 对象自定义释放检查标记 |
| `Priority` | `int` | 对象优先级（值越小，优先级越高） |
| `LastUseTime` | `DateTime` | 对象上次使用时间 |
| `RefCount` | `int` | 对象的获取计数（引用计数） |
| `IsInUse` | `bool` | 对象是否正在使用（`RefCount > 0`） |

## 公开 API

```csharp
// 构造函数
public ObjectInfo(string name, bool locked, bool customCanReleaseFlag, int priority, DateTime lastUseTime, int refCount)
```

## 关联文档

- [ObjectPoolBase](ObjectPoolBase.md) -- `GetAllObjectInfos()` 返回该结构体数组
- [ObjectPoolComponent](ObjectPoolComponent.md)
