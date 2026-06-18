# AdMobAdPlugin

## 1. 简介

`AdMobAdPlugin` 是 AdMob 渠道插件骨架：

- 继承 `AdChannelPluginBase`
- 通过 `[AdChannel(typeof(AdMobAdChannelConfig))]` 绑定配置类型
- 仅声明渠道身份，不执行真实 SDK 初始化

当前代码适合作为后续接入 Google Mobile Ads SDK 的落点，而不是可直接上线的广告渠道实现。

## 2. 当前公开成员

| 成员 | 说明 |
|---|---|
| `Name => "AdMobAdPlugin"` | 渠道插件名称 |
| `Channel => AdChannelType.AdMob` | 渠道类型标识 |
| `InitChannelSDKAsync(...)` | 当前仅返回 `UniTask.CompletedTask`，不注册广告位、不初始化 SDK |

## 3. 运行时行为

- 即使配置里填写了广告位 ID，当前实现也不会主动把这些广告位注册到 `AdChannelPluginBase`。
- 没有注册广告位时，聚合层不会认为该渠道支持任何广告格式。
- 当前包主要完成了“可被扫描到的渠道类型 + 可序列化配置”两层约定。

## 4. 使用示例

```csharp
// 在 AdPluginConfig 的渠道配置列表里添加 AdMobAdChannelConfig 后，
// AdPlugin 会把它识别为一个候选渠道。
// 但当前渠道仍是骨架实现，不会实际返回可展示广告。
```

## 5. 关联

- 配置类型：[AdMobAdChannelConfig.md](./AdMobAdChannelConfig.md)
