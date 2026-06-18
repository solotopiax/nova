# DebugManagerBase

**类签名**：`internal abstract class DebugManagerBase : FrameworkManager, IDebugManager`
**命名空间**：`NovaFramework.Runtime`

调试管理器抽象基类，继承自 `FrameworkManager` 并实现 `IDebugManager`。当前只声明最小生命周期契约，不再承载旧版调试窗口树接口。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Runtime/Modules/Debug/Managers/Implements/DebugManagerBase.cs` | 抽象基类定义 |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int` | 框架管理器优先级，固定为 `0` |

## 公开 API

```csharp
// 初始化（abstract）
public abstract void Initialize(DebugManagerConfig config);

// FrameworkManager.Update 抽象实现
public abstract override void Update();

// 关闭并清理管理器（abstract）
public abstract override void Shutdown();

```

## 关联文档

- [IDebugManager](IDebugManager.md)
- [DebugManagerConfig](DebugManagerConfig.md)
- [DebugManager](../DebugManager.md)
