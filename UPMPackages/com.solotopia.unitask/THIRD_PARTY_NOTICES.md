# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.unitask`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `UniTask`
  - Upstream project: `https://github.com/Cysharp/UniTask`
  - License: `MIT`
  - Corresponding content in this repo: `Core/**`
  - Bundled upstream license file: `Core/LICENSE`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova UPM packaging, integration notes, and release-maintenance content.
- These packaging files do not override the original license boundary of `Core/**`.

## Public distribution requirements
- When distributing publicly, you must retain both `Core/LICENSE` and this file.
- If the UniTask version under `Core/` is upgraded in the future, re-review the license and the local-modification boundary accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.unitask` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `UniTask`
  - 上游项目：`https://github.com/Cysharp/UniTask`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/**`
  - 本仓库内许可文件：`Core/LICENSE`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的 UPM 封装、接入说明与发版维护内容。
- 这些封装文件不覆盖 `Core/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/LICENSE` 与本文件。
- 若未来升级 `Core/` 中的 UniTask 版本，应同步复核许可证和本地改动边界。
