# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.webglsupport`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `WebGLInput`
  - Upstream project: `https://github.com/kou-yeung/WebGLInput`
  - License: `MIT` (Copyright 2019 kou_yeung)
  - Corresponding content in this repo: `Core/WebGLInput/**`
  - Bundled upstream license file: `Core/WebGLInput/LICENSE`
- Nova-added WebGL adaptation content
  - Corresponding content in this package: `Core/WebGLWindow/**`, `Core/WebGLTool.cs`
  - This part is supplementary adaptation code by Solotopia / Nova and is not part of the `WebGLInput` upstream source tree

## Nova packaging boundary
- The root `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, and the Nova-added WebGL adaptation code belong to the Solotopia / Nova UPM packaging and extension layer.
- These files do not change the original MIT license boundary of `Core/WebGLInput/**`.

## Public distribution requirements
- When distributing publicly, you must retain both `Core/WebGLInput/LICENSE` and this file.
- If browser-bridge scripts or platform plugins continue to be added to `Core/` in the future, notices should continue to be supplemented according to their source.

The upstream license original text is retained in the section below.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.webglsupport` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `WebGLInput`
  - 上游项目：`https://github.com/kou-yeung/WebGLInput`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/WebGLInput/**`
  - 本仓库内许可文件：`Core/WebGLInput/LICENSE`
- Nova 自增 WebGL 适配内容
  - 当前包内对应内容：`Core/WebGLWindow/**`、`Core/WebGLTool.cs`
  - 这部分为 Solotopia / Nova 的补充适配代码，不属于 `WebGLInput` 上游源码树

## Nova 封装边界

- 包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 与 Nova 自增 WebGL 适配代码，属于 Solotopia / Nova 的 UPM 封装与扩展层。
- 这些文件不改变 `Core/WebGLInput/**` 的原始 MIT 许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/WebGLInput/LICENSE` 与本文件。
- 若未来继续向 `Core/` 中新增浏览器桥接脚本或平台插件，应按来源继续补充声明。
