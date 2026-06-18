# IReference

**类签名**：`public interface IReference`
**命名空间**：`NovaFramework.Runtime`

引用接口，所有需要被引用池（ReferencePool）管理的对象必须实现此接口。`Clear()` 方法在对象归还到引用池时被调用，用于重置对象状态以便下次复用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IReference.cs` | 接口定义 |

## 公开 API

```csharp
void Clear();
```

## 关联文档

- [ReferencePool](ReferencePool.md)
- [IReferenceHelper](Interfaces/IReferenceHelper.md)
