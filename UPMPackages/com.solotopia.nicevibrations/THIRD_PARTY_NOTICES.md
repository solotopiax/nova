# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.nicevibrations`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `Nice Vibrations`
  - Upstream project: `https://github.com/Lofelt/NiceVibrations`
  - License: `MIT`
  - Corresponding content in this repo: `Core/**`
  - Upstream material directly verifiable in this package: `Core/readme.txt`, `Core/3RD-PARTY-LICENSES.md`
- `Lofelt Studio SDK` and its third-party dependencies
  - Distributed together with the `Nice Vibrations` upstream repository
  - Corresponding content in this repo: including but not limited to `Core/Plugins/Android/libs/LofeltHaptics.aar`
  - Third-party dependency manifest: `Core/3RD-PARTY-LICENSES.md`

## Nova packaging boundary
- The root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are used only for Solotopia / Nova UPM packaging, integration notes, and release maintenance.
- These packaging files do not override the original license boundary in `Core/**` and `Core/3RD-PARTY-LICENSES.md`.

## Public distribution requirements
- When distributing publicly, you must retain this file, `Core/3RD-PARTY-LICENSES.md`, and the upstream notice material distributed with `Core/**`.
- If `LofeltHaptics.aar` is replaced or a new platform native library is added in the future, the root-level notices must be re-completed according to the new version.

The upstream license original text is retained in the section below.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.nicevibrations` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `Nice Vibrations`
  - 上游项目：`https://github.com/Lofelt/NiceVibrations`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/**`
  - 当前包内可直接确认的上游材料：`Core/readme.txt`、`Core/3RD-PARTY-LICENSES.md`
- `Lofelt Studio SDK` 及其第三方依赖
  - 随 `Nice Vibrations` 上游仓库一并分发
  - 本仓库内对应内容：包括但不限于 `Core/Plugins/Android/libs/LofeltHaptics.aar`
  - 第三方依赖清单：`Core/3RD-PARTY-LICENSES.md`

## Nova 封装边界

- 包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 仅用于 Solotopia / Nova 的 UPM 封装、接入说明与发版维护。
- 上述封装文件不覆盖 `Core/**` 与 `Core/3RD-PARTY-LICENSES.md` 中的原始许可边界。

## 公开分发要求

- 对外公开时，必须同时保留本文件、`Core/3RD-PARTY-LICENSES.md`，以及随 `Core/**` 分发的上游说明材料。
- 若未来替换 `LofeltHaptics.aar` 或新增平台原生库，应按新版本重新补齐包根声明。

## Nice Vibrations 上游 MIT 许可证原文

```text
MIT License

Copyright (c) Meta Platforms, Inc. and affiliates.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
