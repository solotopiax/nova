# Nova Framework - SDK - AdMob 文档索引

> 本包提供 Google AdMob 渠道适配层，基于父包 `com.solotopia.nova.framework.sdk.ad` 的 `AdChannelPluginBase` 接入广告聚合链路。
> 当前实现仍是渠道骨架：完成渠道声明与配置承载，但未注册任何广告位，也未接入实际 SDK 初始化流程。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `AdMobAdPlugin` | AdMob 渠道插件骨架，声明 `Channel=AdMob`，当前不实际加载广告 | [AdMobAdPlugin.md](./AdMobAdPlugin.md) |
| `AdMobAdChannelConfig` | AdMob 渠道配置，承载 App ID 与 Rewarded / Inter / Banner / AppOpen 广告位列表 | [AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md) |

## 当前状态

- `InitChannelSDKAsync()` 直接返回 `CompletedTask`，当前不会调用 `RegisterAdUnits(...)`。
- `AdPlugin` 仍可扫描到该渠道配置，但因为没有注册广告位，运行时默认视为该渠道不支持任何 `AdFormat`。
- 适合先完成 ConfigWindow 配置链路验证，再逐步补齐真实 AdMob SDK 接入。

## 相关

- [AdMobAdPlugin.md](./AdMobAdPlugin.md) — AdMob 渠道插件骨架
- [AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md) — AdMob 渠道配置
