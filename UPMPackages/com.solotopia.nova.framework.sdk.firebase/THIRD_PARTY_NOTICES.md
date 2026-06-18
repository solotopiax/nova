# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.nova.framework.sdk.firebase`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `Firebase Unity SDK`
  - Upstream project: `https://github.com/firebase/firebase-unity-sdk`
  - License: `Apache-2.0` (Google)
  - Corresponding content in this repo: `Core/analytics/**`, `Core/application/**`, `Core/crashlytics/**`, `Core/messaging/**`, `Core/remote-config/**`
  - Bundled upstream license files:
    - `Core/analytics/LICENSE.md`
    - `Core/application/LICENSE.md`
    - `Core/crashlytics/LICENSE.md`
    - `Core/messaging/LICENSE.md`
    - `Core/remote-config/LICENSE.md`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova adaptation layer, UPM packaging, and integration documentation.
- These packaging contents do not override the original license boundary of each Firebase sub-package.

## Public distribution requirements
- When distributing publicly, retain all the sub-package license files listed above and this file.
- Project-private configuration or private integration artifacts are not part of this package's third-party license materials and should not be distributed with the public repository.
- If new Firebase sub-modules are added in the future, continue to add the corresponding license entries at the package root.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.nova.framework.sdk.firebase` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `Firebase Unity SDK`
  - 上游项目：`https://github.com/firebase/firebase-unity-sdk`
  - 本仓库内对应内容：`Core/analytics/**`、`Core/application/**`、`Core/crashlytics/**`、`Core/messaging/**`、`Core/remote-config/**`
  - 当前包内许可文件：
    - `Core/analytics/LICENSE.md`
    - `Core/application/LICENSE.md`
    - `Core/crashlytics/LICENSE.md`
    - `Core/messaging/LICENSE.md`
    - `Core/remote-config/LICENSE.md`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的适配层、UPM 封装和接入说明。
- 这些封装内容不覆盖各 Firebase 子包的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留上面列出的各子包许可证文件与本文件。
- 项目私有配置或私有集成产物不属于本包第三方许可材料的一部分，不应随公开仓分发。
- 若未来新增新的 Firebase 子模块，应在包根继续补充对应许可证条目。
