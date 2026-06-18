# TextLocalizingAutoMount

**类签名**：`internal static class TextLocalizingAutoMount`
**命名空间**：`NovaFramework.Editor`

TMP 组件添加时自动挂载 TextLocalizing 的编辑器钩子。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Editor/Inspectors/CustomInspectors/TextLocalizingInspector/TextLocalizingAutoMount.cs` | `internal static class TextLocalizingAutoMount` | `[InitializeOnLoadMethod]` 注册 `ObjectFactory.componentWasAdded` 回调，自动挂载 TextLocalizing |

---

## 完整公开 API

本类为 `internal static`，无公开 API。以下为内部方法：

```csharp
// [InitializeOnLoadMethod] 标记，编辑器启动时自动调用
// 注册 ObjectFactory.componentWasAdded 回调（先移除再添加，防止重复注册）
private static void Register()

// 组件添加回调
// 当添加的组件为 TextMeshProUGUI 且节点尚无 TextLocalizing 时：
//   1. Undo.AddComponent<TextLocalizing>
//   2. 通过 SerializedObject 将 m_LocalizingFontMark 设为 c_DefaultFontMark（"Main"）
private static void OnComponentAdded(Component component)
```

---

## 使用示例

无需手动调用。当开发者在编辑器中为任意 GameObject 添加 `TextMeshProUGUI` 组件时，`TextLocalizing` 会自动挂载并将字体标记初始化为 `"Main"`。

```
操作步骤：
1. 选中任意 GameObject
2. Add Component → TextMeshProUGUI
3. 自动挂载 TextLocalizing（FontMark = "Main"）
```

---

## 关联文档

- [TextLocalizing.md](../../../Runtime/Modules/Localization/TextLocalizing.md)
- [TextLocalizingValidator.md](TextLocalizingValidator.md)
- [TextLocalizingInspector.md](TextLocalizingInspector.md)
