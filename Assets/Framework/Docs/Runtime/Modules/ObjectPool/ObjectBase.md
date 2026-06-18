# ObjectBase

**类签名**：`public abstract class ObjectBase : IReference`
**命名空间**：`NovaFramework.Runtime`

对象池中可被池化对象的抽象基类。分离"管理信息"与"真实资源"：`ObjectBase` 持有名称、锁定状态、优先级、最后使用时间等管理元数据，真实资源通过 `Target` 属性引用（如 GameObject、Texture 等）。业务需继承该类并实现 `Release(bool isShutdown)` 方法来定义资源清理逻辑。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Definitions/ObjectBase.cs` | 抽象基类定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 对象在池内的检索名 |
| `Target` | `object` | 真实的被池化物体（如 GameObject、Texture 等） |
| `Locked` | `bool` | 锁定保护标记，防止被自动回收 |
| `Priority` | `int` | 默认释放排序优先级；数值越小越早进入默认释放候选 |
| `LastUseTime` | `DateTime` | 最后使用时刻，用于过期判断 |
| `CustomCanReleaseFlag` | `bool` | 业务自定义"此时能否被释放"标记（默认 `true`，可 override） |

## 公开 API

```csharp
// 初始化（protected，供子类调用）
protected void Initialize(object target)
protected void Initialize(string name, object target)
protected void Initialize(string name, object target, bool locked)
protected void Initialize(string name, object target, int priority)
protected void Initialize(string name, object target, bool locked, int priority)

// IReference 实现
public virtual void Clear()

// 生命周期回调（protected internal，供池内部调用）
protected internal virtual void OnGet()   // 对象被取出时回调
protected internal virtual void OnPut()   // 对象被归还时回调

// 资源清理（子类必须实现）
protected internal abstract void Release(bool isShutdown)
```

## 继承关系

```
IReference
  └── ObjectBase (abstract)
        └── 业务子类（如 MySoundObject、MyAssetObject 等）
```

## 关联文档

- [IObjectPool\<T\>](IObjectPool.md) -- 泛型约束 `where T : ObjectBase`
- [Object\<T\>](Object.md) -- 内部引用计数包装器
- [ObjectPool\<T\>](ObjectPool.md) -- 持有 `Object<T>` 的对象池实现
