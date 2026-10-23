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
| `ErrorCode` | `int` | 原厂错误码或聚合/渠道生成的本地错误码 |
| `ErrorMessage` | `string` | 错误描述 |
| `CustomProps` | `Dictionary<string, object>` | 渠道侧附加属性 |

## 关键语义

- `Success=true` 表示结果产生时已有可用广告，可能是新加载或已有缓存。`PlacementId`、`Revenue` 可用于诊断；缓存可能在之后过期或被消费，展示前仍需按实现查询当前 Ready。
- `Success=false` 表示本次观察的加载结果失败，调用方直接读取 `ErrorCode` 和 `ErrorMessage`。具体实现可能继续后台重试，不能把一次失败结果当成整个渠道已停止；各 ID 的失败事件与聚合请求结果也不是同一粒度。

## 关联文档

- [IAdPlugin.md](IAdPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
