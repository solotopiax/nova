# NamespaceOverride

**类签名**：`[Serializable] public sealed class NamespaceOverride`
**命名空间**：`NovaFramework.Runtime`

Namespace 字段的维度 Override 单项容器；当 `ConfigMasterSO.NamespaceMask` 至少勾选一个维度轴时，列表中与当前 Platform / Channel / DevelopMode 匹配的首个条目覆盖顶层 `Namespace` 字段。列表为空或无命中时，使用顶层 `ConfigMasterSO.Namespace` 作为全局默认值。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/NamespaceOverride.cs` | `NamespaceOverride` | Namespace 维度 Override 单项 |

---

## §5 完整公开 API

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Platform` | `PlatformType` | `PlatformType.None` | 平台轴；仅当 `NamespaceMask.ByPlatform == true` 时参与匹配，不参与时设置为 `None` 哨兵 |
| `Channel` | `ChannelType` | `ChannelType.None` | 渠道轴；仅当 `NamespaceMask.ByChannel == true` 时参与匹配，不参与时设置为 `None` 哨兵 |
| `DevelopMode` | `DevelopMode` | `DevelopMode.Debug` | 开发模式轴；仅当 `NamespaceMask.ByDevelopMode == true` 时参与匹配；枚举无 None 哨兵，不参与匹配时维持默认值 |
| `Value` | `string` | `null` | 当前维度组合下的 Namespace Override 值，覆盖顶层 `ConfigMasterSO.Namespace` |

---

## §12 注意事项

- 存储规则：未勾选的轴坐标在 `DimensionProjector` 写入时填 `PlatformType.None` / `ChannelType.None`；`DimensionalResolver.MatchesMask` 匹配时对未勾选轴直接跳过，不比对存储值
- `DevelopMode` 枚举无 `None` 哨兵，此轴的"不参与匹配"语义由掩码 `ByDevelopMode == false` 控制，字段值本身无意义

---

## §11 使用示例

```csharp
// DimensionalResolver.ResolveNamespace 的取数逻辑
string ns = DimensionalResolver.ResolveNamespace(
    master,
    PlatformType.Android,
    ChannelType.Google,
    DevelopMode.Debug);
// 若 NamespaceMask.IsGlobal → 直接返回 master.Namespace
// 否则遍历 master.NamespaceOverrides 找首个匹配条目取 Value，无命中回落 master.Namespace
```

---

## §13 关联文档

- [PanelDimensionMask.md](PanelDimensionMask.md)（`NamespaceMask` 类型）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`NamespaceMask` / `NamespaceOverrides` 字段）
- [EditorUtil.Config.DimensionalResolver.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveNamespace` 取数）
- [EditorUtil.Config.DimensionProjector.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（`OnNamespaceEnabled` / `UpsertNamespaceOverride` 写入）
