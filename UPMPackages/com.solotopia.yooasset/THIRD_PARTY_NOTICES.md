# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.yooasset`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `YooAsset`
  - Upstream project: `https://github.com/tuyoogame/YooAsset`
  - Upstream baseline: release `3.0.6`, commit `3b4cfb36cc2e81b9558ab74d65fee2cf2eac2804`
  - License: Apache License 2.0; see `Core/LICENSE.md`
  - Copyright notice carried by the upstream license: `Copyright 2018-2021 何冠峰` and `Copyright 2021-2026 TuYoo Games`
  - Corresponding content in this repo: `Core/**`
  - Bundled upstream license file: `Core/LICENSE.md`
- Bundled licenses within sample assets
  - `Core/Samples~/Space Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf`
  - Accompanying license: Apache License 2.0 at `Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt`
  - The 3.0.6 source tree contains no additional `LICENSE`, `NOTICE`, `COPYING`, or `CREDITS` file beyond `Core/LICENSE.md` and this font license (plus their Unity `.meta` files).

## Nova packaging boundary
- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova compatibility layer, UPM packaging, and integration notes.
- These files do not override the original license boundaries of `Core/**` and the bundled assets within the sample directories.

## Public distribution requirements
- When distributing publicly, retain `Core/LICENSE.md`, the license files bundled within the sample assets, and this file together.
- The six functional Nova patches retain `// modify: local fork - ...` notices: `YooAssetConfiguration.SetSettings`, `SettingLoader` explicit-path provider and `LoadSettingDataAtPath`, `BundleCollectorSettingData.ResetCache`, the `NovaFramework.Editor` runtime friend assembly, deferred URL-policy notification in `DownloadAndCacheFileOperation`, and final validated-result URL-policy notification in `SFSDownloadBundleOperation`. The package `CHANGELOG.md` records these changes required by Apache-2.0 section 4(b).
- `ScriptableBuildTasks` now uses upstream SBP version adaptation; its previous functional fork notice is removed while Chinese method documentation is retained. Other local differences are the `IDownloadBackend` documentation, private-package version defines in the runtime asmdef, and Unity-generated importer metadata for `InstantAssetBuildPipelineViewer.uxml`. Assembly definition metadata GUIDs are retained.
- Filesystem operation directory names follow the 3.0.6 upstream `Internal` casing, preserving their upstream metadata GUIDs. The nested upstream `package.json` and its metadata are excluded; the package root owns the private UPM identity and dependencies.
- If the sample content is adjusted, additional example assets are bundled, or the YooAsset version is upgraded in the future, re-review the corresponding notices accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.yooasset` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `YooAsset`
  - 上游项目：`https://github.com/tuyoogame/YooAsset`
  - 上游基线：发布版 `3.0.6`，commit `3b4cfb36cc2e81b9558ab74d65fee2cf2eac2804`
  - 许可证：Apache License 2.0，见 `Core/LICENSE.md`
  - 上游许可文件携带的版权声明：`Copyright 2018-2021 何冠峰`、`Copyright 2021-2026 TuYoo Games`
  - 本仓库内对应内容：`Core/**`
  - 本仓库内许可文件：`Core/LICENSE.md`
- 样例资源中的附带许可证
  - 字体文件：`Core/Samples~/Space Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf`
  - 随附许可证：`Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt`（Apache License 2.0）
  - 3.0.6 源树除 `Core/LICENSE.md` 与该字体许可证（及各自 Unity `.meta`）外，不含其他 `LICENSE`、`NOTICE`、`COPYING` 或 `CREDITS` 文件。

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的兼容层、UPM 封装与接入说明。
- 这些文件不覆盖 `Core/**` 与样例目录内附带资源的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 `Core/LICENSE.md`、样例资源中附带的许可证文件与本文件。
- 6 个 Nova 功能补丁保留 `// modify: local fork - ...` 标记：`YooAssetConfiguration.SetSettings`、`SettingLoader` 显式路径提供器及 `LoadSettingDataAtPath`、`BundleCollectorSettingData.ResetCache`、Runtime 的 `NovaFramework.Editor` 友元、`DownloadAndCacheFileOperation` 延后 URL 策略通知、`SFSDownloadBundleOperation` 在缓存校验最终结果通知 URL 策略。包级 `CHANGELOG.md` 同步记录差异，以满足 Apache-2.0 第 4(b) 条的显著修改声明要求。
- `ScriptableBuildTasks` 已采用上游 SBP 自动版本适配，移除原功能 fork 标记并保留中文方法说明。其余本地差异包括 `IDownloadBackend` 注释、Runtime asmdef 的私包 versionDefines，以及 `InstantAssetBuildPipelineViewer.uxml` 的 Unity 生成 importer 元数据；asmdef 的元数据 GUID 保持不变。
- 文件系统操作目录跟随 3.0.6 上游使用 `Internal` 大小写，并保留对应上游元数据 GUID。不分发嵌套的上游 `package.json` 及其元数据，私包身份及依赖由包根清单维护。
- 若未来调整样例内容、附加示例资源或升级 YooAsset 版本，应同步复核对应 notice。
