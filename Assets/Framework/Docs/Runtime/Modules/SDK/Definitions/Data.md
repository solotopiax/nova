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

## 已移除的旧类型

以下条目不再是当前框架代码事实，不应继续作为实现依据：

- `AdEventType`
- `AdResult`
- `AdEvent`
- `AdLoadEvent`
- `AdLoadFailEvent`
- `PurchaseResult`
- `ProductInfo`

## 关联文档

- [../Plugins/Ad/IAdPlugin.md](../Plugins/Ad/IAdPlugin.md)
- [../Plugins/Ad/AdLoadEvent.md](../Plugins/Ad/AdLoadEvent.md)
