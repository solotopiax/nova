# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.hybridclr`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `HybridCLR`
  - Upstream project: `https://github.com/focus-creative-games/hybridclr`
  - License: `MIT`
  - Corresponding content in this repo: `Core/**`
  - Bundled upstream license file: `Core/LICENSE`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova UPM packaging, integration notes, and adaptation layer.
- These files do not override the original license boundary of `Core/**`.

## Public distribution requirements
- When distributing publicly, retain `Core/LICENSE` and this file.
- If the upstream version inside `Core/` is replaced in the future, re-review the license and the local difference notes accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.hybridclr` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `HybridCLR`
  - 上游项目：`https://github.com/focus-creative-games/hybridclr`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/**`
  - 本仓库内许可文件：`Core/LICENSE`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 属于 Solotopia / Nova 的 UPM 封装、接入说明与适配层。
- 这些文件不覆盖 `Core/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须保留 `Core/LICENSE` 与本文件。
- 若未来替换 `Core/` 内上游版本，应同步复核许可证与本地差异说明。
