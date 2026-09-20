# 微信小游戏接入

安装 `com.solotopia.nova.framework.wechat.minigame` 即可获得完整微信小游戏转换 SDK。本包将官方 SDK 作为本地副本收录在 `Core/`，不再要求消费工程额外安装 `com.qq.weixin.minigame`。

## 使用边界

- `0.0.1` 内置微信 SDK 功能版本 `0.1.34`，来源基线为官方提交 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`。
- 本包可以常驻 Android、iOS、WebGL 等开发配置；普通浏览器 WebGL 不执行微信小游戏的自动启动行为。
- 普通浏览器 WebGL 默认排除微信可选 Wasm 静态库；微信导出工具会在导出期间按需启用，并在结束后恢复隔离。
- 官方转换工具使用 Nova 包内统一 SDK 根路径，不依赖 `Packages/com.qq.weixin.minigame`。
- 包根 `WebGLTemplates/` 是 `Core/WebGLTemplates/` 的 Unity 模板发现镜像；升级内置 SDK 时两处必须同步。
- SDK 的转换面板、导出流程与运行时 API 以包内官方 README 和 Nova 本地适配说明为准。
- 业务代码引用 `WeChatWASM` API 时必须使用微信小游戏对应的平台宏保护，避免非 WebGL Player 编译引用不存在的类型。

上游文档：<https://wechat-miniprogram.github.io/minigame-unity-webgl-transform/>
