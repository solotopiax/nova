# ObjectPoolBase

**类签名**：`public abstract class ObjectPoolBase`
**命名空间**：`NovaFramework.Runtime`

对象池非泛型抽象基类。为管理器提供统一的非泛型入口，使 `ObjectPoolManagerBase` 能够以统一类型持有和遍历所有不同泛型参数的对象池。所有属性和方法均为抽象声明，由 `ObjectPool<T>` 提供具体实现。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Definitions/ObjectPoolBase.cs` | 抽象基类定义 |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 对象池名称（构造时确定，只读） |
| `FullName` | `string` | 对象池完整名称（通过 `TypeNamePair` 组合类型与名称） |
| `ObjectType` | `Type` | 对象池对象类型（abstract） |
| `Count` | `int` | 对象池中对象数量（abstract） |
| `CanReleaseCount` | `int` | 可释放的对象数量（abstract） |
| `AllowMultiGet` | `bool` | 是否允许对象被多次获取（abstract） |
| `AutoReleaseInterval` | `float` | 自动释放间隔秒数（abstract） |
| `AutoReleaseTimeCounter` | `float` | 自动释放时间计数器当前值（abstract） |
| `Capacity` | `int` | 对象池容量（abstract） |
| `ExpireTime` | `float` | 对象过期秒数（abstract） |
| `Priority` | `int` | 对象池优先级（abstract） |

## 公开 API

```csharp
// 构造
public ObjectPoolBase()
public ObjectPoolBase(string name)

// 释放
public abstract void Release()
public abstract void Release(int toReleaseCount)
public abstract void ReleaseAllUnused()

// 信息查询
public abstract ObjectInfo[] GetAllObjectInfos()

// 生命周期
public abstract void Update()
public abstract void Shutdown()
```

## 继承关系

```
ObjectPoolBase (abstract)
  └── ObjectPool<T> (internal sealed) : ObjectPoolBase, IObjectPool<T>
```

## 关联文档

- [ObjectPool\<T\>](ObjectPool.md) -- 具体实现类
- [IObjectPool\<T\>](IObjectPool.md) -- 泛型接口（与本类互补）
- [ObjectInfo](ObjectInfo.md) -- `GetAllObjectInfos()` 返回值类型
