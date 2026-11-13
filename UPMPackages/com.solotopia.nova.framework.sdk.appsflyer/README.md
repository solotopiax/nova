# Nova Framework - SDK - AppsFlyer

> 包名：`com.solotopia.nova.framework.sdk.appsflyer`
> 当前版本：`0.2.0`

AppsFlyer 归因埋点插件，提供事件追踪服务

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.appsflyer": "0.2.0"
}
```

## 文档

- [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)
- [Nova/Doc/AppsFlyerPlugin.md](./Nova/Doc/AppsFlyerPlugin.md)

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留 AppsFlyer 上游许可证 + 包根说明文件”的方式进入公开仓。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- [Core/](Core/) 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
