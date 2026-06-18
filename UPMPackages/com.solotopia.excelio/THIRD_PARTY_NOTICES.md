# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.excelio`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).
- As of 2026-06-11, the legacy `EPPlus` / `OfficeOpenXml` and `Microsoft.IO.RecyclableMemoryStream` have been removed from the current package; the package now retains only the `ExcelDataReader` upstream source and Nova's editor wrapper layer.

## Upstream components and licenses
- `ExcelDataReader`
  - Upstream project: `https://github.com/ExcelDataReader/ExcelDataReader`
  - License: `MIT`
  - Corresponding content in this repo: `Core/ExcelDataReader-master/**`
  - Bundled upstream license file: `Core/ExcelDataReader-master/LICENSE`

## Nova packaging boundary
- The root `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, and the editor-side calling code are Solotopia / Nova packaging or adaptation content.
- These packaging files do not override the original license boundary of `Core/ExcelDataReader-master/**`.

## Public distribution requirements
- When distributing publicly, retain this file together with `Core/ExcelDataReader-master/LICENSE`.
- If a new third-party Excel read/write library or binary is reintroduced later, add a corresponding entry at the package root rather than reusing the conclusions of this file as-is.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.excelio` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 截至 2026-06-11，旧版 `EPPlus` / `OfficeOpenXml` 与 `Microsoft.IO.RecyclableMemoryStream` 已从当前包体移除；当前包仅保留 `ExcelDataReader` 上游源码与 Nova 的编辑器包装层。

## 上游组件与许可证

- `ExcelDataReader`
  - 上游项目：`https://github.com/ExcelDataReader/ExcelDataReader`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/ExcelDataReader-master/**`
  - 本仓库内许可文件：`Core/ExcelDataReader-master/LICENSE`

## Nova 封装边界

- 包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 以及编辑器侧调用代码属于 Solotopia / Nova 的封装或适配内容。
- 这些封装文件不改变 `Core/ExcelDataReader-master/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须与 `Core/ExcelDataReader-master/LICENSE` 一并保留本文件。
- 若后续重新引入新的 Excel 读写第三方库或二进制，应在包根新增对应条目，不得直接沿用本文件结论。
