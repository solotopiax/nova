# IEditorRuntimeDrawer

**接口签名**：`public interface IEditorRuntimeDrawer : IDisposable`  
**命名空间**：`NovaFramework.Editor`

## 作用

Inspector 运行时附加绘制接口。  
它不是所有 Inspector 的基类契约，而是具体 Inspector 可选采用的一种扩展模式。

## 当前接口

```csharp
public interface IEditorRuntimeDrawer : IDisposable
{
    void Draw(UnityEngine.Object target);
}

public interface IEditorRuntimeDrawer<TComponent> : IEditorRuntimeDrawer
    where TComponent : MonoBehaviour
{
    void DrawTyped(TComponent component);
}
```

## 当前使用方式

- 某些 Inspector 可以自己维护 `List<IEditorRuntimeDrawer>`
- 在运行时区域手动循环调用 `Draw(target)`
- 不使用该模式的 Inspector 完全可以不接入这套接口

当前 `EventComponentInspector` 就是实际用例。

## 重要边界

- `BaseComponentInspector` 当前不会统一调度 `IEditorRuntimeDrawer`
- `Draw()` 何时调用、是否只在 PlayMode 调用，由具体 Inspector 自行决定
- 实现类通常需要自行处理目标类型检查与运行时状态判断

## 关联

- [BaseComponentInspector.md](../Inspectors/BaseComponentInspector.md)
- [EventComponentInspector.md](../Inspectors/EventComponentInspector/EventComponentInspector.md)
