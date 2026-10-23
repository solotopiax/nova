# License Notice

This file only describes the licensing boundary of `com.solotopia.nova.framework.sdk.max`; it does NOT
re-license the whole package as a single Solotopia MIT grant.

## Boundary

- This package no longer contains `Core/MaxSdk` or bundles the AppLovin MAX
  Unity Plugin. The SDK and adapters are supplied by the official UPM packages
  declared in [package.json](./package.json), and remain under their own upstream
  license, EULA, and third-party notices.
- The UPM packaging-layer files (`package.json`, `README.md`, `CHANGELOG.md`,
  this `LICENSE.md`, `THIRD_PARTY_NOTICES.md`) and any content under `Nova/**`
  or explicitly marked as Solotopia / Nova are authored by Solotopia and
  provided under the MIT License (Copyright (c) 2026 Solotopia). They do NOT
  override the license boundary of the separately supplied official AppLovin
  SDK and adapter packages.

## Redistribution

- Public distribution of the Nova-authored integration layer does not grant
  permission to redistribute the AppLovin SDK or adapters. If upstream content
  is separately redistributed, retain its original license chain and comply
  with the applicable terms; keep the Solotopia-authored license separate.

---

# 许可说明

本文件仅用于说明 `com.solotopia.nova.framework.sdk.max` 的许可边界，不将整个包重新声明为 Solotopia 的单一 MIT 授权包。

## 边界

- 本包已移除 `Core/MaxSdk`，不再内置 AppLovin MAX Unity Plugin。SDK 和 adapters 由 [package.json](./package.json) 声明的官方 UPM 包分别供给，继续遵循其原始许可证、EULA 和第三方声明。
- 官方 UPM 包内的第三方源码、二进制、资源和依赖，也继续遵循其各自的原始许可链。
- 本包 UPM 包装层文件和 `Nova/**` 中 Solotopia 编写的适配内容沿用上述 MIT 授权；该授权不覆盖另行供给的 AppLovin SDK / adapter 包。

## 当前约束

- 本包不应再被表述为“整包均为 Copyright (c) 2026 Solotopia 的 MIT 包”。
- 公开 Nova 适配层不代表已获得 AppLovin SDK / adapters 的再分发权。另行再分发上游内容时必须保留其原始许可链并遵守对应条款，Nova 层许可证与修改说明独立保留。
