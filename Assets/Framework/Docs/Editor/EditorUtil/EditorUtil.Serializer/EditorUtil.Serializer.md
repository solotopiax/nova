# EditorUtil.Serializer

**类签名**：`public static class EditorUtil.Serializer`
**命名空间**：`NovaFramework.Editor`

反射读写工具，用于在 RuntimeDrawer 中访问目标 Component 的私有/序列化字段。RuntimeDrawer 无法通过 `SerializedObject` 读取运行时值（`SerializedObject` 反映的是序列化快照），通过此工具直接反射字段可获取真实运行时数据。

---

## 文件

| 文件 | 说明 |
|------|------|
| `EditorUtil.Serializer.cs` | 所有方法实现，内部使用 `BindingFlags.NonPublic | BindingFlags.Instance` 反射 |

---

## 完整 API 签名

```csharp
// 读取私有/序列化字段值
// TTarget：Component 类型（如 UIComponent）
// TValue：字段类型（如 IUIManager）
// propertyName：字段名（如 "m_UIManager"）
TValue EditorUtil.Serializer.GetProperty<TTarget, TValue>(TTarget target, string propertyName)

// 写入私有/序列化字段值
void EditorUtil.Serializer.SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
```

**支持的字段类型：**

`int` / `long` / `float` / `double` / `bool` / `string` / `Color` / `Vector2` / `Vector3` / `Vector4` / `Rect` / `AnimationCurve` / `Bounds` / `Quaternion` / `Enum` 子类 / `UnityEngine.Object` 子类

> **枚举读取说明**：`GetProperty` 对枚举类型使用 `property.intValue`（实际整数值）而非 `property.enumValueIndex`（下拉列表显示索引），确保非连续枚举值（如 `None=0, TypeA=10`）也能正确读取。

---

## 使用示例

```csharp
private sealed class UIManagerRuntimeDrawer : IEditorRuntimeDrawer
{
    public void Draw(Object target)
    {
        if (!Application.isPlaying) return;
        var comp = (UIComponent)target;

        // 读取私有 IUIManager 引用
        var mgr = EditorUtil.Serializer.GetProperty<UIComponent, IUIManager>(comp, "m_UIManager");
        if (mgr == null) return;

        EditorUtil.Draw.Label("UIGroup 数量", mgr.UIGroupCount.ToString());
    }
}
```

---

## 注意事项

| 场景 | 说明 |
|------|------|
| 字段名变更 | 反射依赖字段名字符串，重命名字段后需同步更新 Drawer 中的字符串常量 |
| 性能 | 反射有性能开销，仅在 RuntimeDrawer 中（即 `isPlaying` 时）调用；非 `isPlaying` 时不调用 |
| 只读字段 | `GetProperty` 可读取 `readonly` 字段；`SetProperty` 对 `readonly` 字段无效 |

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [IEditorRuntimeDrawer.md](../../Definitions/IEditorRuntimeDrawer.md)
- [BaseComponentInspector.md](../../Inspectors/BaseComponentInspector.md)
