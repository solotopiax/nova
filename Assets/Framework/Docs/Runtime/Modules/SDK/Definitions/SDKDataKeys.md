# SDKDataKeys

**类签名**：`public static class SDKDataKeys`  
**命名空间**：`NovaFramework.Runtime`

SDK 插件间共享数据槽位的 key 常量集合，配合 `SDKPluginBase.PublishData()` 与 `ISDKPlugin.FetchDataAsync()` 使用。

## 当前 key

| 常量 | 类型 | 说明 |
|---|---|---|
| `AppsFlyerId` | `string` | AppsFlyer 设备 ID |
| `TGADevicesId` | `string` | TGA 设备 ID |
| `TGADistinctId` | `string` | TGA distinct ID |
| `TGAAccountId` | `string` | TGA 账号 ID |
| `FirebasePushToken` | `string` | Firebase 推送 token |
| `FirebaseAnalyticsInstanceId` | `string` | Firebase Analytics 实例 ID |

## 使用约束

- 同一个 key 应只对应一种确定的值类型。
- 发布方与消费方必须在业务层约定好强转类型。
- 再次发布同 key 会覆盖旧值，并唤醒当前等待者。

## 关联文档

- [ISDKPlugin.md](./ISDKPlugin.md)
- [SDKPluginBase.md](./SDKPluginBase.md)
