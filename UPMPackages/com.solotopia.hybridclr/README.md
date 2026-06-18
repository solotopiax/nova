# HybridCLR

> 包名：`com.solotopia.hybridclr`
> 当前版本：`10.0.4`

全平台 C# 原生热更新方案，零成本、高性能、低内存

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.hybridclr": "10.0.1"
}
```

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留 `Core/` 内上游许可文件 + 包根说明文件”的方式进入公开仓。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- `Core/` 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。
