# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.nova.framework.sdk.tga`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `ThinkingData Unity SDK 3.4.6`
  - Upstream project: `https://github.com/ThinkingDataAnalytics/ThinkingSDKForUnity`
  - License: `Apache-2.0` (see `Core/unity-sdk-3.4.6/LICENSE`)
  - Corresponding content in this repo: `Core/unity-sdk-3.4.6/**`
  - Bundled upstream license file: `Core/unity-sdk-3.4.6/LICENSE`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova adaptation layer, UPM packaging, and integration documentation.
- These packaging contents do not change the original license boundary of `Core/unity-sdk-3.4.6/**`.

## Public distribution requirements
- When distributing publicly, retain both `Core/unity-sdk-3.4.6/LICENSE` and this file.
- If the ThinkingData SDK version is upgraded in the future, re-review its license and the `Nova/**` adaptation differences accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.nova.framework.sdk.tga` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `ThinkingData Unity SDK 3.4.6`
  - 上游项目：`https://github.com/ThinkingDataAnalytics/ThinkingSDKForUnity`
  - 许可证：见 `Core/unity-sdk-3.4.6/LICENSE`
  - 本仓库内对应内容：`Core/unity-sdk-3.4.6/**`
  - 本仓库内许可文件：`Core/unity-sdk-3.4.6/LICENSE`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的适配层、UPM 封装和接入说明。
- 这些封装内容不改变 `Core/unity-sdk-3.4.6/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/unity-sdk-3.4.6/LICENSE` 与本文件。
- 若未来升级 ThinkingData SDK 版本，应同步复核其许可证与 `Nova/**` 适配差异。
