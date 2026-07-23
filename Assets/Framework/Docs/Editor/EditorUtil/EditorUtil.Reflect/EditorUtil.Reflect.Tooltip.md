# EditorUtil.Reflect.Tooltip

**类签名**：`public static class Reflect`（`EditorUtil` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Reflect`

编辑器反射工具层；负责按"类型全名.字段名"缓存的字段 `TooltipAttribute.tooltip` 读取，用于解决 `SerializeReference` 托管引用下 `SerializedProperty.tooltip` 始终为空的 IMGUI 显示问题。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Reflect/EditorUtil.Reflect.Tooltip.cs` | `EditorUtil.Reflect` | 全部逻辑：`GetFieldTooltip(object, string)` / `GetFieldTooltip(Type, string)` + 静态 Tooltip 缓存 |

---

## §5 完整公开 API

```csharp
// 通过反射从目标对象的字段上读取 TooltipAttribute.tooltip
// 结果按"类型全名.字段名"缓存，相同字段只反射一次
// SerializeReference 托管引用的 SerializedProperty.tooltip 始终为空，需通过本方法读取原始 C# 字段上的 [Tooltip]
// <param name="target">持有该字段的托管对象实例</param>
// <param name="fieldName">字段名（含 m_ 前缀等实际声明名称）</param>
// <returns>Tooltip 文本；字段无 TooltipAttribute 或 target 为 null 时返回 null</returns>
public static string GetFieldTooltip(object target, string fieldName);

// 通过反射从指定类型的字段上读取 TooltipAttribute.tooltip
// 结果按"类型全名.字段名"缓存，相同字段只反射一次
// <param name="targetType">声明该字段的类型</param>
// <param name="fieldName">字段名（含 m_ 前缀等实际声明名称）</param>
// <returns>Tooltip 文本；字段无 TooltipAttribute 或 targetType 为 null 时返回 null</returns>
public static string GetFieldTooltip(Type targetType, string fieldName);
```

### 私有实现细节（供阅读，不可调用）

| 成员 | 形态 | 说明 |
|------|------|------|
| `s_TooltipCache` | `private static readonly Dictionary<string, string>` | 字段 Tooltip 缓存；key 为"类型全名.字段名"，value 为 `TooltipAttribute.tooltip`；字段无 `TooltipAttribute` 时缓存 null，避免重复反射。缓存依赖 C# 静态字段语义，Domain Reload 后自动清空，无需手动失效 |

字段查找使用 `BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance`，即只匹配实例字段（含 `m_` 私有字段），不匹配静态字段。

---

## §11 使用示例

```csharp
// 场景：SerializeReference 托管引用绘制的 PropertyDrawer 内
// Unity 的 SerializedProperty.tooltip 对托管引用始终为空，业务侧需展示原始 C# 字段上的 [Tooltip]
public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
{
    // property.managedReferenceValue 是当前实际托管实例
    object target = property.managedReferenceValue;
    string tooltip = EditorUtil.Reflect.GetFieldTooltip(target, "m_ConfigId");
    if (!string.IsNullOrEmpty(tooltip))
    {
        label.tooltip = tooltip;
    }
    EditorGUI.PropertyField(position, property, label);
}

// 场景：已知字段声明类型时，直接走 Type 重载（无须实例）
// 适合批量预热或在绘制前一次性读取多个字段
Type configType = typeof(MyConfig);
string tip1 = EditorUtil.Reflect.GetFieldTooltip(configType, "m_ConfigId");
string tip2 = EditorUtil.Reflect.GetFieldTooltip(configType, "m_Description");
```

注意事项：

- `fieldName` 必须是 C# 实际声明名（含 `m_` 前缀），不是 `SerializedProperty.propertyPath` 里的 `configId` 这种序列化名。
- 返回 null 是正常分支（字段无 `[Tooltip]` 或入参为 null），调用方需自行判空。
- 相同字段只会反射一次，热点路径（如每帧 Inspector 重绘）也可安全直调。

---

## §13 关联文档

- [Editor.md](../../Editor.md)（Framework Editor 层总览）
- [EditorUtil.md](../EditorUtil.md)（EditorUtil 子工具聚合入口）
