# WebGL Support

> 包名：`com.solotopia.webglsupport`
> 当前版本：`1.0.7`
> Unity：`2021.3`

网页端输入与窗口适配工具：封装 [WebGLInput](https://github.com/kou-yeung/WebGLInput) 提供 WebGL 平台的输入框支持，并补充 Nova 自增的 WebGL 窗口与工具适配（`Core/WebGLWindow`、`Core/WebGLTool`）。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.webglsupport": "1.0.7"
}
```

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 许可与第三方声明

- 本包采用 **MIT** 许可证（见 [LICENSE.md](./LICENSE.md)），覆盖 Nova UPM 封装层与自增 WebGL 适配代码（`Core/WebGLWindow/**`、`Core/WebGLTool.cs`）。
- `Core/WebGLInput/**` 为上游 **WebGLInput**（MIT，Copyright (c) 2019 kou_yeung），遵循其原始许可证，许可文件见 `Core/WebGLInput/LICENSE`。
- 完整第三方声明见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。对外公开 / 再分发时必须同时保留上游许可文件与本声明。
