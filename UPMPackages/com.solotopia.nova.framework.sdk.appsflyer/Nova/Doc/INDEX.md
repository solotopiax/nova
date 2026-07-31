# Nova Framework - SDK - AppsFlyer 文档索引

> 本包为 Nova 框架 AppsFlyer 归因埋点插件，提供事件追踪服务。
> 实现 `IAttributionPlugin`，业务侧通过统一归因接口发送事件、获取归因数据，无需直接依赖 AppsFlyer SDK。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `AppsFlyerPlugin` | AppsFlyer SDK 插件，实现 `IAttributionPlugin`；配置由 SDKManager 按 `ConfigType` 自动注入 | [AppsFlyerPlugin.md](./AppsFlyerPlugin.md) |

## 相关

- 外部依赖管理包：`com.google.external-dependency-manager@1.2.186`
- [AppsFlyerPlugin.md](./AppsFlyerPlugin.md) — AppsFlyer 归因埋点插件
