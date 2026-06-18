# Bases/Extensions

C# 和 Unity 类型的扩展方法，全部为静态类，可在任意层级直接使用。

## Foundation/（基础类型扩展）

**类名**：`NovaExtensionForFoundation`（分部类）

| 文件 | 扩展类型 | 典型方法 |
|------|---------|---------|
| `NovaExtensionForFoundation.Float.cs` | `float` | 区间判断、精度比较等 |
| `NovaExtensionForFoundation.String.cs` | `string` | 空检查、格式化快捷方法等 |

## Unity/（Unity 类型扩展）

**类名**：`NovaExtensionForUnity`（分部类）

| 文件 | 扩展类型 |
|------|---------|
| `NovaExtensionForUnity.Transform.cs` | `Transform` |
| `NovaExtensionForUnity.GameObject.cs` | `GameObject` |
| `NovaExtensionForUnity.Component.cs` | `Component` |
| `NovaExtensionForUnity.Animator.cs` | `Animator` |
| `NovaExtensionForUnity.RectTransform.cs` | `RectTransform` |
| `NovaExtensionForUnity.UI.cs` | `UnityEngine.UI.*` |
| `NovaExtensionForUnity.Vector2.cs` | `Vector2` |
| `NovaExtensionForUnity.Vector3.cs` | `Vector3` |
| `NovaExtensionForUnity.Quaternion.cs` | `Quaternion` |
| `NovaExtensionForUnity.Spine.cs` | Spine `SkeletonAnimation` 等 |
