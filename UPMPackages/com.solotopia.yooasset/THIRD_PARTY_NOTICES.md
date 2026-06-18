# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.yooasset`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `YooAsset`
  - Upstream project: `https://github.com/tuyoogame/YooAsset`
  - License: see `Core/LICENSE.md`
  - Corresponding content in this repo: `Core/**`
  - Bundled upstream license file: `Core/LICENSE.md`
- Bundled licenses within sample assets
  - License file directly confirmable in the current package: `Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt`

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova compatibility layer, UPM packaging, and integration notes.
- These files do not override the original license boundaries of `Core/**` and the bundled assets within the sample directories.

## Public distribution requirements
- When distributing publicly, retain `Core/LICENSE.md`, the license files bundled within the sample assets, and this file together.
- If the sample content is adjusted, additional example assets are bundled, or the YooAsset version is upgraded in the future, re-review the corresponding notices accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.yooasset` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `YooAsset`
  - 上游项目：`https://github.com/tuyoogame/YooAsset`
  - 许可证：见 `Core/LICENSE.md`
  - 本仓库内对应内容：`Core/**`
  - 本仓库内许可文件：`Core/LICENSE.md`
- 样例资源中的附带许可证
  - 当前包内可直接确认的许可文件：`Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt`

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的兼容层、UPM 封装与接入说明。
- 这些文件不覆盖 `Core/**` 与样例目录内附带资源的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/LICENSE.md`、样例资源中附带的许可证文件与本文件。
- 若未来调整样例内容、附加示例资源或升级 YooAsset 版本，应同步复核对应 notice。
