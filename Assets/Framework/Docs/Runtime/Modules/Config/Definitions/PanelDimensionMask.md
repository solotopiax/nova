# PanelDimensionMask

**类签名**：`[Serializable] public sealed class PanelDimensionMask`
**命名空间**：`NovaFramework.Runtime`

ConfigWindow 单个配置面板的维度启用掩码；记录该面板是否按平台类型 / 渠道类型 / 开发模式分别配置。全部为 false 时表示全局唯一配置（`IsGlobal == true`）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/PanelDimensionMask.cs` | `PanelDimensionMask` | 维度掩码数据容器 |

---

## §5 完整公开 API

| 字段 / 属性 | 类型 | 默认值 | 说明 |
|-------------|------|--------|------|
| `ByPlatform` | `bool` | `false` | 勾选后该面板数据随 PlatformType 维度独立存储 |
| `ByChannel` | `bool` | `false` | 勾选后该面板数据随 ChannelType 维度独立存储 |
| `ByDevelopMode` | `bool` | `false` | 勾选后该面板数据随 DevelopMode 维度独立存储 |
| `IsGlobal` | `bool`（只读属性） | 依赖三字段 | 三轴全为 false 时为 true，表示全局唯一配置；`=> !ByPlatform && !ByChannel && !ByDevelopMode` |

---

## §11 使用示例

```csharp
// 检查 Common 面板是否为全局唯一配置
PanelDimensionMask mask = master.CommonMask;
if (mask.IsGlobal)
{
    // 全局唯一：直接用顶层字段值
}

// 在 DimensionProjector 中按面板种类取掩码
PanelDimensionMask sdkMask = master.GetSDKMask("NovaFramework.WeChat.WeChatSDKPluginConfig");
bool byPlatform = sdkMask.ByPlatform;
```

---

## §13 关联文档

- [TypedDimensionMask.md](TypedDimensionMask.md)（带类型全名的掩码条目）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（`CommonMask` / `SDKMasks` / `KitMasks` / `NamespaceMask` / `HybridCLRMask` / `YooAssetMask` 字段）
- [EditorUtil.Config.DimensionProjector.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md)（掩码消费方）
- [EditorUtil.Config.DimensionalResolver.md](../../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（掩码消费方）
