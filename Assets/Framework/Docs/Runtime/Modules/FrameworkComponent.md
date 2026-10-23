# FrameworkComponent

**类签名**：`public abstract class FrameworkComponent : MonoBehaviour, ICoroutineRunner`
**命名空间**：`NovaFramework.Runtime`

所有框架 Component 的基类，`Awake()` 时自动注册到 `FrameworkComponentsGroup`。
同时承载一个场景级 `DevelopMode` 快照字段，该值由 `ConfigWindow` 导出时自动回写，用于启动早期在 `Config.LoadAsync()` 之前决定模块本地路由。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Core/FrameworkComponent.cs` | `abstract FrameworkComponent` | 基类：Awake 注册 + ICoroutineRunner 实现 |
| `Core/FrameworkComponentsGroup.cs` | `static FrameworkComponentsGroup` | 全局注册表，按类型查找实例 |

---

## §5 完整公开 API

```csharp
public abstract class FrameworkComponent : MonoBehaviour, ICoroutineRunner
{
    public DevelopMode DevelopMode { get; }

    /// <summary>子类重写时必须调用 base.Awake()，否则不会注册到 FrameworkComponentsGroup。</summary>
    protected virtual void Awake();

    // ICoroutineRunner 显式实现（非 MonoBehaviour 代码借用协程能力）
    Coroutine ICoroutineRunner.StartCoroutine(IEnumerator coroutine);
    void ICoroutineRunner.StopCoroutine(Coroutine coroutine);
}
```

`FrameworkComponentsGroup` 关键 API：

```csharp
static T GetComponent<T>() where T : FrameworkComponent;
static FrameworkComponent GetComponent(Type type);
static void RegisterComponent(FrameworkComponent component);
static void Clear();
```

## DevelopMode 快照

- 序列化字段名：`m_DevelopMode`
- 展示方式：默认 `HideInInspector`，由各组件 Inspector 顶部统一只读展示
- 写入时机：`ConfigWindow` 点击导出后，自动把当前选中的 `DevelopMode` 回写到当前激活场景中的 `Nova + 所有 FrameworkComponent`
- 运行时用途：给 `AppComponent`、`AssetComponent` 这类启动早期就要决定 Debug/Release 地址的模块提供本地事实，不再依赖尚未加载的 `ConfigRuntimeSO`
- 源码初值是 `Debug`，实际值以当前场景序列化快照为准，不能由初值推断项目当前模式。

## 注册、查询与清理边界

注册以具体组件类型为键；空组件抛 `ArgumentNullException`，重复类型记录 Error 后保留已注册实例。查询只按精确 `Type` 读字典，未注册返回 `null`；空 `Type` 沿字典参数异常，不按继承关系或字符串名称查找。`Clear()` 和子系统注册阶段的静态重置只清除注册引用，不销毁 GameObject，也不代替 Manager 关闭。

`ICoroutineRunner` 的实现仅转发 Unity 协程：启动没有新增空值或运行状态保护；停止时传入 `null` 直接跳过，其余约束由 Unity 决定。它是既有兼容入口，框架主异步模型仍使用 UniTask。

---

## §11 使用示例

```csharp
[DisallowMultipleComponent]
public sealed partial class XxxComponent : FrameworkComponent
{
    private IXxxManager m_XxxManager;

    protected override void Awake()
    {
        base.Awake(); // 必须调用，注册到 FrameworkComponentsGroup
    }

    private void Start()
    {
        m_XxxManager = Util.TypeCreator.Create<IXxxManager>(m_CurXxxManagerTypeName);
        m_XxxManager.Initialize(/* config */);
    }
}
```

---

## §13 关联文档

- [FrameworkManager.md](FrameworkManager.md)
- [Nova.md](Nova/Nova.md)
