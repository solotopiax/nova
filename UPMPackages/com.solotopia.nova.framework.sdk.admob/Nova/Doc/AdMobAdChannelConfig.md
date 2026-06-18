# AdMobAdChannelConfig

## 1. 简介

`AdMobAdChannelConfig` 是 AdMob 渠道配置对象，实现 `IAdChannelConfig`，在 `AdPluginConfig` 的渠道配置列表中以 `[SerializeReference]` 方式存储。

## 2. 配置字段

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否启用该渠道 |
| `AppIdAndroid` | Android 平台 App ID |
| `AppIdIOS` | iOS 平台 App ID |
| `RVPlacementIds` | 激励视频广告位 ID 列表 |
| `InterPlacementIds` | 插屏广告位 ID 列表 |
| `BannerPlacementIds` | Banner 广告位 ID 列表 |
| `AppOpenPlacementIds` | 开屏广告位 ID 列表 |

## 3. 契约行为

- `Channel` 固定返回 `AdChannelType.AdMob`
- `PluginType` 固定返回 `typeof(AdMobAdPlugin)`
- 列表为空时表示对应广告格式未启用

## 4. 当前限制

配置对象已经可以被序列化和扫描，但当前 `AdMobAdPlugin` 还不会读取这些列表去注册广告位，所以它们现在主要承担“数据结构对齐”的作用。

## 5. 关联

- 渠道插件：[AdMobAdPlugin.md](./AdMobAdPlugin.md)
