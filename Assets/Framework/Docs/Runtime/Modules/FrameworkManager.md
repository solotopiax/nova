# FrameworkManager

**类签名**：`public abstract class FrameworkManager`
**命名空间**：`NovaFramework.Runtime`

所有框架 Manager 的纯 C# 基类，构造时自动注册到 `FrameworkManagersGroup`，并按 `Priority` 插入有序链表。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Core/FrameworkManager.cs` | `abstract FrameworkManager` | 基类：Priority / Update / Shutdown |
| `Core/FrameworkManagersGroup.cs` | `static FrameworkManagersGroup` | 全局调度表，按 Priority 驱动 Update，逆序 Shutdown |

---

## §5 完整公开 API

```csharp
public abstract class FrameworkManager
{
    /// <summary>优先级，值越小越先 Update、越后 Shutdown（默认 0）。</summary>
    public virtual int Priority => 0;

    /// <summary>构造时自动注册到 FrameworkManagersGroup。</summary>
    public FrameworkManager();

    public abstract void Update();
    public abstract void Shutdown();
}
```

`FrameworkManagersGroup` 关键 API：

```csharp
static void Update();                                        // Nova.Update() 每帧调用
static void Shutdown();                                      // Nova.OnDestroy() 调用，逆序关闭
static T GetManager<T>() where T : class;                    // 按接口类型查找，传具体类型抛异常
static FrameworkManager GetManager(Type managerType);
static void RegisterManager(FrameworkManager manager);       // FrameworkManager 构造器自动调用
static void UnregisterManager(FrameworkManager manager);     // 构造异常时回滚用
```

---

## §11 使用示例

```csharp
// 三层继承模板（固定模式）
internal abstract class XxxManagerBase : FrameworkManager, IXxxManager
{
    public override int Priority => 0;
    public abstract void Initialize(XxxManagerConfig config);
    public abstract override void Update();
    public abstract override void Shutdown();
}

internal sealed partial class XxxManager : XxxManagerBase
{
    // 构造器无参，base ctor 自动注册
    public XxxManager() { }
    public override void Update() { }
    public override void Shutdown() { }
}
```

---

## §13 关联文档

- [ARCHITECTURE.md](../../ARCHITECTURE.md)
- [FrameworkComponent.md](FrameworkComponent.md)
