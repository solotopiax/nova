# License Notice

This file only describes the licensing boundary of `com.solotopia.nova.framework.sdk.firebase`; it does NOT
re-license the whole package as a single Solotopia MIT grant.

## Boundary

- The contents under `Core/**`, together with the upstream `LICENSE` / `NOTICE`
  / `THIRD_PARTY` files bundled there, remain under their original upstream
  license, EULA, or third-party notices.
- The UPM packaging-layer files (`package.json`, `README.md`, `CHANGELOG.md`,
  this `LICENSE.md`, `THIRD_PARTY_NOTICES.md`) and any content under `Nova/**`
  or explicitly marked as Solotopia / Nova are authored by Solotopia and
  provided under the MIT License (Copyright (c) 2026 Solotopia). They do NOT
  override the original license boundary of `Core/**`.

## Redistribution

- When distributing publicly, retain the upstream license chain under `Core/**`
  first, then add separate license text for the Solotopia-authored layer as
  needed.

---

# 许可说明

本文件仅用于说明 `com.solotopia.nova.framework.sdk.firebase` 的许可边界，不将整个包重新声明为 Solotopia 的单一 MIT 授权包。

## 边界

- `Core/` 目录及其中附带的 `LICENSE`、`LICENSE.md`、`NOTICE`、`README`、`Third Party Notices`、`3RD-PARTY-LICENSES` 等文件，继续遵循对应上游项目的原始许可证、EULA 或第三方声明。
- `Core/` 内进一步携带的第三方源码、二进制、资源和依赖，也继续遵循其各自的原始许可链。
- 包内用于 UPM 封装、版本管理、接入说明的包装层文件，以及明确标注为 Solotopia 的自增内容，不反向覆盖 `Core/` 及其内部第三方内容的许可证边界。

## 当前约束

- 本包不应再被表述为“整包均为 Copyright (c) 2026 Solotopia 的 MIT 包”。
- 如需对外公开或再分发，应先保留上游原始许可链，再根据实际自增内容补充单独的许可证文本与修改说明。
