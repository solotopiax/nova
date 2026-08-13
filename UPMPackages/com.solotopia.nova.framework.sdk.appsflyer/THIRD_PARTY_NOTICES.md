# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.nova.framework.sdk.appsflyer`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `AppsFlyer Unity Plugin 6.18.1`
  - Upstream project: `https://github.com/AppsFlyerSDK/appsflyer-unity-plugin`
  - License: `MIT` (declared by `Core/appsflyer-unity-plugin-6.18.1/package.json`)
  - Corresponding content in this repo: `Core/appsflyer-unity-plugin-6.18.1/**`
  - Bundled upstream package metadata: `Core/appsflyer-unity-plugin-6.18.1/package.json`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova adaptation layer, UPM packaging, and integration documentation.
- These packaging contents do not change the original license boundary of `Core/appsflyer-unity-plugin-6.18.1/**`.

## Public distribution requirements
- When distributing publicly, retain `Core/appsflyer-unity-plugin-6.18.1/package.json` and this file; if a future upstream release bundles a separate license file, retain that file as well.
- If the AppsFlyer Unity Plugin version is upgraded in the future, re-review its license file and the `Nova/**` adaptation differences accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.nova.framework.sdk.appsflyer` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `AppsFlyer Unity Plugin 6.18.1`
  - 上游项目：`https://github.com/AppsFlyerSDK/appsflyer-unity-plugin`
  - 许可证：由 `Core/appsflyer-unity-plugin-6.18.1/package.json` 声明为 `MIT`
  - 本仓库内对应内容：`Core/appsflyer-unity-plugin-6.18.1/**`
  - 本仓库内上游包元数据：`Core/appsflyer-unity-plugin-6.18.1/package.json`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的适配层、UPM 封装和接入说明。
- 这些封装内容不改变 `Core/appsflyer-unity-plugin-6.18.1/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/appsflyer-unity-plugin-6.18.1/package.json` 与本文件；若后续上游版本重新携带独立许可文件，也必须一并保留。
- 若未来升级 AppsFlyer Unity Plugin 版本，应同步复核其许可证文件与 `Nova/**` 适配差异。
