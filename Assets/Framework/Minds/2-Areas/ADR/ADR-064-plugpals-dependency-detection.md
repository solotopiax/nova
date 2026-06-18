---
id: ADR-064
title: PlugPals 依赖检测与可选库三原则
summary: 依赖进 dependencies，宏由 asmdef 配置
category: editor
status: accepted
date: 2026-06-16
aliases:
  - ADR-064-plugpals-dependency-detection
tags: [adr, nova, editor, plugpals]
supersedes: []
superseded-by: []
related:
  - "[[feedback-no-yooasset-outside-asset-module]]"
---

# ADR-064：PlugPals 依赖检测与可选库三原则

## 背景（Context）

PlugPals（消费端 UPM 包管理 Editor 工具）安装包时需处理「必须的商业可选库」——典型如 BestHTTP / TLS Security，这类库本地未必安装，且需购买或到内部云仓库自取。

早期实现把这类库放在包 `package.json` 的 `nova.requiredLibraries`（而非 `dependencies`），并由 PlugPals 负责注入宏（`requiredLibraries.defineSymbols`）+ 维护 `PlugPalsInjectedDefines.json` 账本 + domain-reload 后台审计弹窗。该路线有结构性缺陷：

- 依赖不在 `dependencies` → UPM 解析层面「看不见」它们，既不会自动安装，缺库检测（曾基于 dependencies / scope 已注册判据）也对它们沉默；
- PlugPals 自己注入宏，与 Unity 原生 `versionDefines`（包存在即自动定义宏）职责重复，且账本维护成本高、宏来源与依赖来源脱节。

触发点：在消费端工程 `MyTest` 实测发现，安装 `com.solotopia.nova.framework.besthttp` 时，其依赖的 best/tls 既不被自动安装、也不被缺库检测提示——因为它们被声明在 `requiredLibraries` 而非 `dependencies`。

不做此决策：缺库检测与自动安装无可靠数据源，可选商业库始终处于「UPM 不装、工具不报」的三不管地带。

## 决策（Decision）

确立可选库三条原则 + PlugPals 新检测/安装机制。

**三原则（包作者契约）**

1. **`dependencies` 权威、不遗漏**：所有真实依赖（含商业可选库）一律声明在对应包的 `dependencies`。这是缺库检测与 UPM 解析的唯一权威源。
2. **`nova.requiredLibraries` 仅展示/提醒**：只承载 `displayName` / `purchaseUrl`，供缺库引导窗显示；不参与判定，不再含 `defineSymbols`。
3. **宏交给 asmdef**：用不用宏由各 asmdef 的 `versionDefines` / `defineConstraints`（Unity 原生「某包存在 → 自动定义某宏」）决定，PlugPals 不再注入或管理任何宏。

**PlugPals 工具机制（`NovaFramework.Editor`）**

- 点安装/更新 → `CheckDependencies` 遍历包 `dependencies`，用 `PlugPalsWindow` 打开时已 fetch 到内存的外网(4873)+内部云(4874) registry 包列表判命中（零额外联网）。
- 每个依赖按序短路：本地已装 / 前缀 `com.solotopia.` / 前缀 `com.unity.` / 命中 registry（→ `ToAutoScope`）；皆不满足 → 缺失库（`Missing`）。
- 有缺失库 → `PlugPalsMissingRequiredLibrariesWindow` 弹购买/内部云引导，**中止安装不写 manifest**。
- 无缺失库 → 对命中依赖按来源 `EnsureScopedRegistry` 自动配 scope，再为主包配 scope、写 `manifest.dependencies`、`ResolvePackages`，命中依赖随主包被 UPM 一并解析安装。

**移除**：PlugPals 宏注入链（`SyncRequiredLibraryDefineSymbols` 等）、`PlugPalsInjectedDefines.json` 账本、后台审计（`RunRequiredLibraryAudit` / `[InitializeOnLoad]`）、会话级抑制、「scope 已注册」判据。

**适用范围**：`NovaFramework.Editor` 的 PlugPals 工具 + 各 UPM 包的 `package.json`/asmdef 契约；`com.solotopia.nova.framework.besthttp` 为首个落地样板。

## 后果（Consequences）

### 正面

- 单一权威源（`dependencies`），与 UPM 原生解析行为对齐，杜绝「不在 dependencies 就漏报/不装」。
- 净删约 1000 行宏注入/账本/审计机制，宏交还 Unity 原生 `versionDefines`，职责清晰。
- 命中判定复用窗口内存数据，零额外联网，规避「安装前同步 HTTP 卡主线程」。
- `requiredLibraries` 职责收敛到只剩展示；命中自动装、未命中购买引导，安装体验完整。

### 负面

- **registry fetch 失败时会误报**：命中判定依赖窗口内存 registry 列表，若 fetch 失败/列表为空，非豁免依赖会被一律判为缺失并中止安装。已知弱点，待补「registry 未就绪则不拦截」降级。
- **仅 PlugPals 流程内有效**：用户绕过 PlugPals 手写 manifest 装包时无此拦截，UPM 会因缺失依赖直接报错。
- **软可选库迁移债**：`UniWebView` / `SimpleDiskUtils` / `WebGLSupport` / `NiceVibrations` 等仍在主框架 `requiredLibraries` 靠旧宏体系；移除 PlugPals 宏注入后其宏暂失效，需逐个迁移（进各自 `dependencies` + 引用方 asmdef 配 `versionDefines`）才恢复。
- 命中即自动写 manifest 的 `scopedRegistries`（有写副作用，但沿用既有 `EnsureScopedRegistry`）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| scope 已注册判据 + 不自动装（v1） | 「本地 scope 已配」不等于「registry 真有」；且命中只放行、被动等 UPM 不主动装，体验差；对 `requiredLibraries`-only 的库仍漏报 |
| 保留 PlugPals 宏注入机制 | 与 Unity 原生 `versionDefines` 重复，账本维护成本高，宏来源与依赖来源脱节 |
| 安装前异步 fetch registry 再判定 | 改异步流程改动面大；窗口打开时已有内存 registry 数据可直接复用，无需重拉 |

## 验证依据（Verification）

- 文件：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.RequiredLibraries.cs`（`CheckDependencies`）、`EditorUtil.PlugPals.cs`（`InstallPackage`）、`PlugPalsWindow/PlugPalsWindow.Methods.cs`（`BuildKnownRegistryPackages`）、`UPMPackages/com.solotopia.nova.framework.besthttp/package.json` 与 `Nova/Runtime/NovaFramework.BestHTTP.Runtime.asmdef`。
- grep 自查：`CheckDependencies` / `RegistrySource` 存在；全仓 `defineSymbols` / `PlugPalsInjectedDefines` / `RunRequiredLibraryAudit` 应零残留。
- 测试：`PlugPalsRequiredLibraryTests`（`CheckDependencies_*`）+ `BestHttpOptionalDependencyTests`，EditMode 59/59 全绿。
- 设计/计划：`docs/superpowers/specs/2026-06-15-plugpals-missing-required-libraries-design.md`（v2）、`docs/superpowers/plans/2026-06-15-plugpals-missing-required-libraries.md`（v2）。

## 关联

- 相关 memory：[[feedback-no-yooasset-outside-asset-module]]（资源系统封装口径，同属「外部库在框架内的呈现/封装」约束族）。
- 待补关联（入库后）：软可选库迁移落地后补一条迁移 Pattern；fetch 失败降级若实现，补充到本 ADR 验证段或新增条目。
