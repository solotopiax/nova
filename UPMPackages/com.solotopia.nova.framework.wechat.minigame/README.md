# Nova Framework - WeChat Mini Game

微信小游戏转换 SDK 的 Nova 本地集成包。

## 安装

通过 Nova PlugPals 安装 `com.solotopia.nova.framework.wechat.minigame` 即可。微信官方 SDK 已完整收录在本包 `Core/` 中，不需要再向消费工程的 `Packages/manifest.json` 添加 `com.qq.weixin.minigame`。

## 版本与更新

当前包版本为 `0.0.1`，内置微信 SDK 功能版本为 `0.1.34`，来源基线为官方提交 `a09d4b29daa1dd8358b09b5b5639554ab08cfdc2`。上游 `package.json` 仍声明 `0.1.1`，Nova 的 `coreVersion` 以官方 CHANGELOG 中的实际功能版本为准。升级时应重新导入完整官方文件、重放带有 `modify: local fork` 标记的适配，并完成跨平台验证。

## 跨平台常驻

本包可以常驻开发工程：

- Android、iOS 与其他非 WebGL Player 构建中，微信 Runtime 类型由原厂平台宏排除。
- 微信 `.jslib` 与运行库不参与 Android、iOS 链接；普通浏览器 WebGL 默认排除微信可选 Wasm 静态库。
- 使用微信导出工具时，工具会按其配置临时启用所需静态库，导出结束后自动恢复普通 WebGL 隔离。
- 普通浏览器 WebGL 不执行微信小游戏的自动隐藏启动页与键盘设置逻辑。
- 原厂 Editor 程序集始终可用，但只提供微信转换菜单与配置入口；未主动执行微信转换时，不接管 Android、iOS 构建流程。

业务代码引用 `WeChatWASM` API 时仍须放在微信小游戏对应的平台宏内，不能让 Android、iOS 代码直接引用仅在 WebGL Player 中存在的类型。

## 目录边界

- `Nova/Docs/`：Nova 接入说明。
- `Core/`：微信官方 SDK 完整本地副本，以及带 `modify: local fork` 标记的最小 Nova 适配。
- `WebGLTemplates/`：从 `Core/WebGLTemplates/` 同步的模板镜像，保留在包根以供 Unity 的 `PROJECT:` 模板发现机制使用；升级 SDK 时必须与 `Core` 同步更新。

上游仓库：<https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk>

变更记录见 [CHANGELOG.md](./CHANGELOG.md)，许可说明见 [LICENSE.md](./LICENSE.md) 与 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
