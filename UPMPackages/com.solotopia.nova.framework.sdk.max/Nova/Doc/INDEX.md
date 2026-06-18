# Nova Framework - SDK - MAX 文档索引

> 本包为 Nova 框架 MAX 广告聚合插件，提供广告展示服务。
> 基于 `sdk.ad` 父包的 `AdChannelPluginBase` 抽象，实现 MAX 渠道的 RV / Inter / Banner 等广告类型调度。
> 第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads` 提供，mediation adapters 由本包 `package.json` 统一声明；本包只保留 Nova 适配层与构建处理器，不再内置 `Core/MaxSdk`。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `MaxAdPlugin` | MAX 广告聚合插件，实现 `AdChannelPluginBase`，封装 MAX SDK 初始化与广告加载/展示逻辑 | [MaxAdPlugin.md](./MaxAdPlugin.md) |
| `MaxAdPluginBuildProcessor` | 构建处理器（Editor 侧），在打包时自动注入 MAX 所需 manifest / gradle 配置 | [MaxAdPluginBuildProcessor.md](./MaxAdPluginBuildProcessor.md) |

## 相关

- [MaxAdPlugin.md](./MaxAdPlugin.md) — MAX 渠道插件
- [MaxAdPluginBuildProcessor.md](./MaxAdPluginBuildProcessor.md) — 构建预处理器
