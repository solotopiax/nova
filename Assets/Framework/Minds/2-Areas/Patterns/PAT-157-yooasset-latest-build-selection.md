---
id: PAT-157
title: YooAsset 本地最新构建只能按完整性与写入时间选择
type: pattern
status: active
date: 2026-08-07
summary: 先验证完整构建，再按 version 文件写入时间选最新
category: asset
aliases:
  - PAT-157-yooasset-latest-build-selection
  - YooAsset 本地最新版本目录选择
keywords:
  - PAT-157
  - YooAsset 最新版本
  - LastWriteTimeUtc
  - PackageFilePrefix
  - BuildReport
  - 自动关联最新版本
tags:
  - pattern
  - asset
  - editor
  - yooasset
  - cdn
related:
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[RES-003-unity-yooasset-tutorial|RES-003]]"
---

# PAT-157：YooAsset 本地最新构建只能按完整性与写入时间选择

## 适用场景

- Editor 或自动化部署希望从 YooAsset 包根目录自动关联最近一次成功构建，无需每次手动修改具体资源版本目录。
- 热更资源和启动白名单元数据需要引用同一份完整构建产物。
- `PackageVersion` 可能是日期、语义版本或任意业务字符串，不能假设目录名具有可比较语义。

## 核心做法

### 1. 不按版本目录名比较新旧

YooAsset 只要求 `PackageVersion` 非空，并直接将其作为构建目录名和版本文件内容；日期格式只是编辑器默认值，不是版本契约。因此禁止按字符串、日期或 SemVer 对目录名排序。

Nova 将候选目录内 `.version` 文件的 `LastWriteTimeUtc` 作为本地构建完成先后顺序。若两个完整候选的写入时间完全相同，则明确报歧义并停止，不能再以目录名偷偷决胜。

### 2. 排序前先证明候选是完整构建

有效候选必须同时满足：

- `.version` 文件存在，且正文 Trim 后等于目录名。
- 使用 `YooAssetConfiguration` 官方命名 API 解析的 `.bytes`、`.hash` 和 `.report` 均存在。
- `BuildReport` 可反序列化，包名与资源版本和当前候选一致。
- report 引用的每个 Bundle 文件都存在。
- 候选目录、路径组件和引用文件不经过 symlink 或 junction 越出允许根目录。

损坏的 report、缺少 Bundle 的复制中间态和链接越界目录都只能作为无效候选跳过，不能让其较新的时间戳抢占部署入口。

### 3. 热更目录与白名单三文件复用同一选择结果

热更资源自动模式可以把配置锚点写成包根或任一版本目录；解析时回到包根选择最新完整候选。白名单自动模式以已配置 `.bytes` 的父目录为锚点，选择候选后再调用：

- `GetManifestBinaryFileName`
- `GetPackageHashFileName`
- `GetPackageVersionFileName`

生成匹配当前包名、资源版本和 `PackageFilePrefix` 的 `.bytes`、`.hash`、`.version` 路径。三个路径必须来自同一候选，禁止分别扫描或用字符串替换拼接。

ConfigWindow 与 PipifyWindow 都只读展示动态解析结果，但不把具体版本路径回写到稳定锚点配置。窗口打开或重绘时按当前 ConfigMaster 维度、包名、`PackageFilePrefix` 和本地产物重新解析；Pipify CLI/Runner 不依赖窗口状态，在真正构建上传计划时独立再次解析。这样新构建完成后，无论从界面还是命令行执行，都不需要修改持久化配置即可使用最新完整产物。两个自动关联开关继续遵守 [[ADR-058-per-panel-dimension-mask|ADR-058]] 的维度快照规则。

## 原因

- YooAsset 没有“最新 PackageVersion”比较 API，目录名排序会把业务命名误当版本协议。
- 只检查 `.version/.bytes/.hash` 会把尚未复制完 Bundle 的半成品识别为可部署版本。
- 白名单文件名受 `PackageFilePrefix` 影响，手写名称容易与运行时请求不一致。
- UI 展示时解析一次不足以保证真正点击部署时仍是最新构建。
- Pipify CLI 可能在从未打开窗口的进程中运行，因此执行正确性不能依赖窗口缓存或已显示路径。

## 反模式

- 按目录名、日期格式或 SemVer 判断最新版本。
- 发现时间相同后用目录名字典序兜底，掩盖构建时间歧义。
- 只凭目录存在或 `.version` 存在就认为构建成功。
- 手写 `{prefix}_{package}_{version}` 文件名，而不调用当前 YooAsset fork 的命名 API。
- 白名单 `.bytes/.hash/.version` 分别选择，造成元数据跨版本混用。
- 自动模式持续回写解析路径，导致 ConfigMaster 因每次构建产生无意义脏改动。
- 只在 PipifyWindow 打开时刷新显示，却让 CLI/Runner 消费上一次持久化的具体版本路径。

## 验证依据

- 选择与完整性实现：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.LatestVersion.cs`
- 上传计划：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.cs`
- 维度配置：`Assets/Framework/Scripts/Editor/Config/Definitions/CDNEditorConfigs.cs`
- 窗口交互：`Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.CDN.cs`、`Assets/Framework/Scripts/Editor/Windows/PipifyWindow/PipifyWindow.RightPanel.cs`
- 契约测试：`Assets/Tests/Editor/CDN/CdnDeploymentPlannerTests.cs`、`CDNEditorConfigsTests.cs`、`CdnConfigWindowContractTests.cs`、`CdnPipifyStepTests.cs`
- EditMode CDN Editor 回归曾验证 96/96 通过，覆盖最新候选、半成品、损坏 report、同时间歧义、链接越界和前缀命名。

## 关联

- CDN 面板维度规则：[[ADR-058-per-panel-dimension-mask|ADR-058]]
- 白名单元数据部署契约：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- YooAsset 文件命名与多 App 版本线：[[RES-003-unity-yooasset-tutorial|RES-003]]
