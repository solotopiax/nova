# AdLoadResult

**命名空间**：`NovaFramework.Runtime`

广告加载结果载荷，定义在源码文件 `Plugins/Ad/AdLoadEvent.cs`。文件名沿用旧名以保持 Unity 资产路径稳定，公开类型已收口为 `AdLoadResult`。

## 当前类型

### AdLoadResult

| 字段 | 类型 | 说明 |
|---|---|---|
| `Success` | `bool` | 是否加载成功 |
| `Format` | `AdFormat` | 广告格式 |
| `PlacementId` | `string` | 广告位唯一标识 |
| `Network` | `string` | 实际投放网络名 |
| `Revenue` | `double` | 预测收入，单位 USD |
| `Currency` | `string` | 货币代码，当前固定为 `USD` |
| `ErrorCode` | `int` | SDK 错误码 |
| `ErrorMessage` | `string` | 错误描述 |
| `CustomProps` | `Dictionary<string, object>` | 渠道侧附加属性 |

## 关键语义

- `Success=true` 表示加载成功，`PlacementId`、`Revenue` 等字段可用于后续展示和诊断。
- `Success=false` 表示加载失败，调用方直接读取 `ErrorCode` 和 `ErrorMessage`，不再依赖单独的失败事件才能取得错误详情。

## 关联文档

- [IAdPlugin.md](IAdPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
