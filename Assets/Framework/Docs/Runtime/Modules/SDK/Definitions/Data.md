# SDK 数据类型总览

本文只覆盖当前 `Assets/Framework/Scripts/Runtime/Modules/SDK/Definitions/Data/` 下仍存在的公共数据类型。

## 当前数据类型

| 类型 | 说明 |
|---|---|
| `AdFormat` | 广告格式枚举 |
| `BannerPosition` | Banner 停靠位置枚举 |
| `AttributionData` | 归因结果数据 |
| `AuthResult` | 登录结果数据 |
| `PushToken` | 推送 token 数据 |
| `TrackEvent` | 埋点事件载荷 |

## 非本目录但与 SDK 强相关的数据

| 类型 | 位置 | 说明 |
|---|---|---|
| `AdLoadResult` | `Plugins/Ad/AdLoadEvent.cs` | 广告加载统一结果，成功/失败由 `Success` 区分 |

## 已迁出 Framework 的广告类型

`AdEventType`、`AdResult`、`AdEvent` 仍然存在，当前归属广告聚合包 `com.solotopia.nova.framework.sdk.ad` 的 `Nova/Scripts/Runtime/Types/`，命名空间为 `NovaFramework.SDK.AdPlugin.Runtime`。它们由该包的事件文档维护，不属于 Framework 的 `Definitions/Data/`。

`AdFormat`、`BannerPosition` 和 `AdLoadResult` 继续归属 `NovaFramework.Runtime`；`AdLoadResult` 的文件名仍为 `Plugins/Ad/AdLoadEvent.cs`。

## 已移除的旧类型

以下旧类型没有当前同名实现；迁出类型不能按删除处理：

- `AdLoadEvent`
- `AdLoadFailEvent`
- `PurchaseResult`
- `ProductInfo`

## 关联文档

- [../Plugins/Ad/IAdPlugin.md](../Plugins/Ad/IAdPlugin.md)
- [../Plugins/Ad/AdLoadEvent.md](../Plugins/Ad/AdLoadEvent.md)
