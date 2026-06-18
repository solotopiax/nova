# Nova Framework - SDK - MAX

> 包名：`com.solotopia.nova.framework.sdk.max`
> 当前版本：`0.0.7`

MAX 广告聚合插件，提供广告展示服务。

本包只包含 Nova MAX 适配层，第三方 MAX SDK 由官方 UPM 包 `com.applovin.mediation.ads` 提供，不再内置 `Core/MaxSdk`。MAX mediation adapters 也由本包 `package.json` 统一声明为 AppLovin 官方 UPM 依赖。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.max": "0.0.7"
}
```

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：不应直接进入公开仓；公开版只应保留 Nova 适配层与接入文档，不应继续提交 AppLovin MAX 插件本体。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- AppLovin SDK 本体、adapter、资源与上游声明由官方 UPM 包提供。
