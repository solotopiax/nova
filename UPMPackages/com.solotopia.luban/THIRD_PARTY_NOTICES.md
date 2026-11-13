# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.luban`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `Luban Unity`
  - Upstream project: `https://github.com/focus-creative-games/luban_unity`
  - License: see [Core/LICENSE](Core/LICENSE)
  - Corresponding content in this repo: [Core/**](Core/)
  - Bundled upstream license file: [Core/LICENSE](Core/LICENSE)
- `SimpleJSON`
  - Upstream project: distributed together with the `Luban` runtime
  - License: see [Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE)
  - Corresponding content in this repo: [Core/Runtime/SimpleJSON/**](Core/Runtime/SimpleJSON/)
  - Bundled upstream license file: [Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE)

## Nova packaging boundary
- [Nova/**](Nova/), the root [package.json](package.json), [README.md](README.md), [CHANGELOG.md](CHANGELOG.md), and [LICENSE.md](LICENSE.md) are the Solotopia / Nova UPM packaging, integration notes, and adaptation layer.
- These files do not override the original license boundaries of [Core/**](Core/) and [Core/Runtime/SimpleJSON/**](Core/Runtime/SimpleJSON/).

## Public distribution requirements
- When distributing publicly, retain [Core/LICENSE](Core/LICENSE), [Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE), and this file together.
- If a new template runtime or third-party parsing library is introduced into [Core/](Core/) in the future, keep appending the corresponding declarations at the package root.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.luban` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `Luban Unity`
  - 上游项目：`https://github.com/focus-creative-games/luban_unity`
  - 许可证：见 [Core/LICENSE](Core/LICENSE)
  - 本仓库内对应内容：[Core/**](Core/)
  - 本仓库内许可文件：[Core/LICENSE](Core/LICENSE)
- `SimpleJSON`
  - 上游项目：随 `Luban` 运行时一并分发
  - 许可证：见 [Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE)
  - 本仓库内对应内容：[Core/Runtime/SimpleJSON/**](Core/Runtime/SimpleJSON/)
  - 本仓库内许可文件：[Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE)

## Nova 封装边界

- [Nova/**](Nova/)、包根 [package.json](package.json)、[README.md](README.md)、[CHANGELOG.md](CHANGELOG.md)、[LICENSE.md](LICENSE.md) 属于 Solotopia / Nova 的 UPM 封装、接入说明与适配层。
- 这些文件不覆盖 [Core/**](Core/) 与 [Core/Runtime/SimpleJSON/**](Core/Runtime/SimpleJSON/) 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 [Core/LICENSE](Core/LICENSE)、[Core/Runtime/SimpleJSON/LICENSE](Core/Runtime/SimpleJSON/LICENSE) 与本文件。
- 若未来继续向 [Core/](Core/) 引入新的模板运行时或第三方解析库，应在包根继续追加对应声明。
