# ObjectPoolManagerBase

**类签名**：`internal abstract class ObjectPoolManagerBase : FrameworkManager, IObjectPoolManager`
**命名空间**：`NovaFramework.Runtime`

对象池管理器抽象基类。继承 `FrameworkManager` 参与框架生命周期管理（`Update` / `Shutdown`），同时实现 `IObjectPoolManager` 接口。将接口中所有方法声明为 `abstract`，由具体实现类 `ObjectPoolManager` 提供实现。框架模块优先级为 2。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Managers/Implements/ObjectPoolManagerBase.cs` | 抽象基类定义 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int` | 框架模块优先级，固定为 `2`（优先级较低的模块优先轮询，关闭操作后执行） |
| `Count` | `int` | 对象池数量（abstract） |

## 公开 API

所有方法均为 `abstract`，签名与 `IObjectPoolManager` 一致，另外增加：

```csharp
// 框架生命周期
public abstract override void Update()     // 管理器轮询
public abstract override void Shutdown()   // 关闭并清理管理器
```

## 继承关系

```
FrameworkManager
  └── ObjectPoolManagerBase (internal abstract)
        └── ObjectPoolManager (internal sealed)
```

## 关联文档

- [IObjectPoolManager](IObjectPoolManager.md) -- 接口定义
- [ObjectPoolManager](ObjectPoolManager.md) -- 具体实现类
- [ObjectPoolComponent](ObjectPoolComponent.md) -- 持有管理器的组件入口
