# TypedDimensionMask

**类签名**：`[Serializable] public sealed class TypedDimensionMask`
**命名空间**：`NovaFramework.Runtime`

带配置类型全名的维度掩码条目；将指定 SDK Plugin 或 Kit 配置类型与其面板维度掩码绑定，供 `ConfigMasterSO.SDKMasks` / `KitMasks` 列表使用。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/TypedDimensionMask.cs` | `TypedDimensionMask` | 带类型全名的掩码条目容器 |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `TypeName` | `string` | `null` | 配置类型全名，与 `EnabledSDKs` / `EnabledKits` 中元素同口径，格式为 `Namespace.ClassName` |
| `Mask` | `PanelDimensionMask` | `new()` | 该类型对应的面板维度掩码；默认全不勾（全局唯一） |

---

## §11 使用示例

```csharp
// 通过 ConfigMasterSO.GetSDKMask 惰性查找或创建
PanelDimensionMask mask = master.GetSDKMask("NovaFramework.WeChat.WeChatSDKPluginConfig");
// GetSDKMask 若列表中无对应条目，自动追加默认全不勾条目后返回

// 直接遍历 SDKMasks 列表
foreach (TypedDimensionMask entry in master.SDKMasks)
{
    Debug.Log($"{entry.TypeName} → ByPlatform={entry.Mask.ByPlatform}");
}
```

---

## §13 关联文档

- [PanelDimensionMask.md](PanelDimensionMask.md)（Mask 字段类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`SDKMasks` / `KitMasks` 字段，`GetSDKMask` / `GetKitMask` 方法）
