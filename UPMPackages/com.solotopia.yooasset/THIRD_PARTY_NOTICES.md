# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.yooasset`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `YooAsset`
  - Upstream project: `https://github.com/tuyoogame/YooAsset`
  - Upstream baseline: release `3.0.7`, commit `8cb910de899050d95b01644e7a3bd7a643f226c0`, with packaged revision `3.0.7-m1`
  - License: Apache License 2.0; see [Core/LICENSE.md](Core/LICENSE.md)
  - Copyright notice carried by the upstream license: `Copyright 2018-2021 何冠峰` and `Copyright 2021-2026 TuYoo Games`
  - Corresponding content in this repo: [Core/**](Core/)
  - Bundled upstream license file: [Core/LICENSE.md](Core/LICENSE.md)
- Bundled licenses within sample assets
  - [Core/Samples~/Space Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf](Core/Samples~/Space%20Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf)
  - Accompanying license: Apache License 2.0 at [Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt](Core/Samples~/Space%20Shooter/GameRes/UIFont/LICENSE.txt)
  - These are the two license files in the 3.0.7 package source tree, with their Unity `.meta` files.

## Nova packaging boundary
- [Nova/**](Nova/), the root [package.json](package.json), [README.md](README.md), [CHANGELOG.md](CHANGELOG.md), and [LICENSE.md](LICENSE.md) are the Solotopia / Nova compatibility layer, UPM packaging, and integration notes.
- These files do not override the original license boundaries of [Core/**](Core/) and the bundled assets within the sample directories.

## Public distribution requirements
- When distributing publicly, retain [Core/LICENSE.md](Core/LICENSE.md), the license files bundled within the sample assets, and this file together.
- The Nova patches retain `// modify: local fork - ...` notices for explicit settings injection, collector cache reset, the `NovaFramework.Editor` runtime friend assembly, validated-result URL-policy notifications, and build-service selection shared by Bundle Builder and Pipify. See [CHANGELOG.md](CHANGELOG.md).
- `ScriptableBuildTasks` now uses upstream SBP version adaptation; its previous functional fork notice is removed while Chinese method documentation is retained. Other local differences are the `IDownloadBackend` documentation, private-package version defines in the runtime asmdef, and Unity-generated importer metadata for `InstantAssetBuildPipelineViewer.uxml`. Assembly definition metadata GUIDs are retained.
- Existing filesystem operation paths and their upstream metadata GUIDs are preserved. The package root defines the private UPM identity and dependencies in [package.json](package.json).
- If the sample content is adjusted, additional example assets are bundled, or the YooAsset version is upgraded in the future, re-review the corresponding notices accordingly.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.yooasset` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `YooAsset`
  - 上游项目：`https://github.com/tuyoogame/YooAsset`
  - 上游基线：发布版 `3.0.7`，commit `8cb910de899050d95b01644e7a3bd7a643f226c0`，Nova 修订号为 `3.0.7-m1`
  - 许可证：Apache License 2.0，见 [Core/LICENSE.md](Core/LICENSE.md)
  - 上游许可文件携带的版权声明：`Copyright 2018-2021 何冠峰`、`Copyright 2021-2026 TuYoo Games`
  - 本仓库内对应内容：[Core/**](Core/)
  - 本仓库内许可文件：[Core/LICENSE.md](Core/LICENSE.md)
- 样例资源中的附带许可证
  - 字体文件：[Core/Samples~/Space Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf](Core/Samples~/Space%20Shooter/GameRes/UIFont/RobotoCondensed-Bold.ttf)
  - 随附许可证：[Core/Samples~/Space Shooter/GameRes/UIFont/LICENSE.txt](Core/Samples~/Space%20Shooter/GameRes/UIFont/LICENSE.txt)（Apache License 2.0）
  - 这两份是 3.0.7 包源码中的许可证文件，随各自 Unity `.meta` 一同保留。

## Nova 封装边界

- [Nova/**](Nova/)、包根 [package.json](package.json)、[README.md](README.md)、[CHANGELOG.md](CHANGELOG.md)、[LICENSE.md](LICENSE.md) 为 Solotopia / Nova 的兼容层、UPM 封装与接入说明。
- 这些文件不覆盖 [Core/**](Core/) 与样例目录内附带资源的原始许可证边界。

## 公开分发要求

- 对外公开时，必须同时保留 [Core/LICENSE.md](Core/LICENSE.md)、样例资源中附带的许可证文件与本文件。
- 显式设置注入、Collector 缓存重置、`NovaFramework.Editor` 运行时友元、缓存校验结果通知、Bundle Builder 与 Pipify 共用的构建服务筛选保留 `// modify: local fork - ...` 标记。见 [CHANGELOG.md](CHANGELOG.md)。
- `ScriptableBuildTasks` 已采用上游 SBP 自动版本适配，移除原功能 fork 标记并保留中文方法说明。其余本地差异包括 `IDownloadBackend` 注释、Runtime asmdef 的私包 versionDefines，以及 `InstantAssetBuildPipelineViewer.uxml` 的 Unity 生成 importer 元数据；asmdef 的元数据 GUID 保持不变。
- 文件系统操作保留现有路径和上游元数据 GUID。私包身份及依赖由包根 [package.json](package.json) 维护。
- 若未来调整样例内容、附加示例资源或升级 YooAsset 版本，应同步复核对应 notice。
