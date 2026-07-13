# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.nova.framework.sdk.aihelp`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `AIHelp Unity SDK` (version 6.0)
  - Upstream: AIHelp (aihelp.net)
  - License: AIHelp commercial SDK license (see AIHelp's official terms of service / SDK license agreement)
  - Corresponding content in this repo:
    - `Core/AIHelp/**` — managed C# wrapper (Unity SDK layer)
    - `Core/Plugins/iOS/AIHelpSDK/**` — iOS native library (`AIHelpSupportSDK.framework`, `AIHelpUnity.mm`, `AIHelpUnity.h`)
  - Android native library is **not** vendored in this repo: `AIHelpBuildProcessor` (`IPostGenerateGradleAndroidProject`) injects the Maven coordinate `net.aihelp:android-aihelp-aar:6.0.+` into the exported Android gradle project (`unityLibrary/build.gradle`) at build time. This does not use External Dependency Manager for Unity (EDM4U); there is no Dependencies.xml in this package.

## Nova packaging boundary
- The root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are used only for Solotopia / Nova UPM packaging, integration notes, and release maintenance.
- These packaging files do not override the original license boundary of the AIHelp SDK.
- Nova's adaptation layer under `Nova/` (plugin config / lifecycle orchestration / documentation) is written by Solotopia and does not modify any file under `Core/**`.

## Public distribution requirements
- When distributing publicly, you must retain this file and any upstream notice material distributed with `Core/**`.
- The AIHelp SDK is a commercial SDK; redistribution must comply with AIHelp's SDK license agreement. Do not remove or alter AIHelp's copyright/license headers inside `Core/**`.
- If the AIHelp SDK version bundled in `Core/**` is upgraded in the future, the root-level notices must be re-completed according to the new version.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.nova.framework.sdk.aihelp` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `AIHelp Unity SDK`（版本 6.0）
  - 上游：AIHelp（aihelp.net）
  - 许可证：AIHelp 商业 SDK 许可协议（详见 AIHelp 官方服务条款 / SDK 许可协议）
  - 本仓库内对应内容：
    - `Core/AIHelp/**` —— managed C# 封装层（Unity SDK 层）
    - `Core/Plugins/iOS/AIHelpSDK/**` —— iOS 原生库（`AIHelpSupportSDK.framework`、`AIHelpUnity.mm`、`AIHelpUnity.h`）
  - Android 端原生库**未随包分发**：由本包 `AIHelpBuildProcessor`（`IPostGenerateGradleAndroidProject`）在构建期把 Maven 坐标 `net.aihelp:android-aihelp-aar:6.0.+` 注入导出后的 Android gradle 工程（`unityLibrary/build.gradle`）；不经 External Dependency Manager for Unity（EDM4U），包内也无 Dependencies.xml。

## Nova 封装边界

- 包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 仅用于 Solotopia / Nova 的 UPM 封装、接入说明与发版维护。
- 上述封装文件不覆盖 AIHelp SDK 的原始许可边界。
- Nova 在 `Nova/` 目录下的适配代码（插件配置 / 生命周期编排 / 文档）由 Solotopia 编写，不修改 `Core/**` 下任何文件。

## 公开分发要求

- 对外公开时，必须同时保留本文件，以及随 `Core/**` 分发的上游说明材料。
- AIHelp SDK 为商业授权 SDK，再分发须遵循 AIHelp 的 SDK 许可协议；不得删除或篡改 `Core/**` 内 AIHelp 的版权 / 许可声明。
- 若未来升级 `Core/**` 内随包的 AIHelp SDK 版本，应按新版本重新补齐包根声明。
