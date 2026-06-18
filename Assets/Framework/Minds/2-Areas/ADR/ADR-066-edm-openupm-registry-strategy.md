---
id: ADR-066
title: EDM 公共依赖与 OpenUPM 工程固定策略
summary: OpenUPM 工程固定，registry 链须覆盖传递依赖
category: workflow
status: accepted
date: 2026-06-17
source: cur-session
aliases:
  - ADR-066-edm-openupm-registry-strategy
tags: [adr, nova, workflow, asset]
supersedes: []
superseded-by: []
related: []
---

# ADR-066：EDM 公共依赖与 OpenUPM 工程固定策略

## 背景（Context）

`com.solotopia.nova.framework.sdk.max` 依赖 AppLovin MAX SDK（`com.applovin.mediation.ads` 等），而 applovin 传递依赖 Google EDM（`com.google.external-dependency-manager`）。在 MyTest 工程通过 PlugPals 装 max 时报错：

> Package `com.applovin.mediation.ads@8.5.0` has invalid dependencies: `com.google.external-dependency-manager@1.2.182` cannot be found

排查发现：
- AppLovin 官方 UPM registry 地址最初配成 `https://unity.applovin.com`（npm 404，不可用）。
- EDM 不止 max 用——`firebase`（5 个 `*Dependencies.xml`）、`appsflyer` 也直接依赖 EDM，它是多个移动 SDK 的**公共依赖**。
- EDM 来源是 OpenUPM（`https://package.openupm.com`），消费工程缺该 registry 时整条 applovin 依赖链拉不到。

EDM（External Dependency Manager for Unity / EDM4U，Google 维护）作用：读各 SDK 的 `*Dependencies.xml`，在 build 时把 iOS CocoaPods / Android Maven 原生依赖配置进原生工程。移动广告 / 分析 / 推送 SDK 普遍依赖它。

## 决策（Decision）

1. **AppLovin 正确 registry**：`https://unity.packages.applovin.com/`（**不是** `unity.applovin.com`），scope `com.applovin.mediation.ads` / `com.applovin.mediation.adapters` / `com.applovin.mediation.dsp`。
2. **EDM 由 OpenUPM 供给**：`com.google.external-dependency-manager` 来源 `https://package.openupm.com`，scope `com.google.external-dependency-manager`。
3. **OpenUPM 工程级固定，各包不得声明**：Nova 消费工程 `Packages/manifest.json` 固定 OpenUPM registry（团队约定，无单独模板仓），且 max/firebase/appsflyer 等依赖方**禁止**在各自 `nova.scopedRegistries` 重复声明 OpenUPM——EDM 是公共依赖，工程级固定一次覆盖所有依赖方更 DRY。**双保险方案已废弃**（max 0.0.11 曾保留 OpenUPM scope 作双保险，后移除）：PlugPals 卸载某包时按其 `nova.scopedRegistries` 删 registry，靠引用计数（`CollectRegistryUrlsDeclaredByOtherInstalled` 扫其它已装包的声明）判断是否共用；EDM 公共依赖恰好"工程固定、各包不声明"，故若 max 单方声明 OpenUPM，卸 max 时引用计数扫不到 firebase/appsflyer 的声明（它们本就不声明），会连带删掉工程固定的 OpenUPM registry，殃及同样依赖 EDM 的 firebase/appsflyer。结论：公共依赖的 registry 只能工程固定，绝不能由单个包声明。
4. **PlugPals 写顶层 + 卸载连带移除**：安装时把被声明 registry scope 覆盖的依赖（如 `com.applovin.*`）显式写入 `manifest.dependencies` 顶层——仅作主包传递依赖时 UPM 不保证拉取 scoped-registry 包；卸载时连带移除。`com.solotopia.*` 传递依赖（如 sdk.ad）不写顶层，走 UPM 自动回收。
5. **registry 链完整覆盖铁律**：封装带传递依赖的第三方包时，工程 / 包的 scopedRegistries 必须覆盖**整条依赖链**所需的所有 registry，否则传递依赖拉取失败。

## 后果（Consequences）

### 正面
- EDM 一处固定，所有依赖方（firebase/appsflyer/max）共享，避免每包重复声明。
- AppLovin 全 30+ adapter 经正确 registry + 顶层写入可一键装齐。

### 负面
- 新建 Nova 工程必须预置 OpenUPM registry，漏则装 max/firebase/appsflyer 时报 EDM 找不到（属约定执行问题，非设计缺陷）。
- 移除 max 的 OpenUPM 双保险后，max 不再能在"工程未固定 OpenUPM"的环境单独装齐 EDM——但这正是红线要求工程必固定 OpenUPM 的前提，故非倒退。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 各包各自声明 OpenUPM scope | EDM 是 3+ SDK 公共依赖，每包重复声明不 DRY |
| 用 `unity.applovin.com` 作 registry | npm/UPM 下 404，包根本拉不到 |
| 本地 embed `com.solotopia.external-dependency-manager`（solotopia 版 EDM） | 与 applovin 要求的 `com.google` 版同名程序集冲突（Google.*.dll 重复），不能与 com.google 版并存；已删本地 + 云端 unpublish |

## 验证依据（Verification）

- `npm view com.applovin.mediation.ads --registry https://unity.packages.applovin.com/` 可解析；`unity.applovin.com` 返回 404。
- `npm view com.google.external-dependency-manager@1.2.182 --registry https://package.openupm.com` 可解析。
- MyTest `Packages/manifest.json` 固定 OpenUPM + AppLovin registry 后 applovin + EDM 装齐。

## 来源（Origin）

- 会话日期：2026-06-17
- 关键对话节选：
  > 用户：安装完 max 报了个错……`com.google.external-dependency-manager@1.2.182` cannot be found
  > 用户：EDM 目前除了 max 用到了，还有其他模块需要它吗？
  > 用户：工程模板固定 OpenUPM（你最初的想法）
  > AI 落地：max 0.0.11 曾补回 OpenUPM scope 作双保险；OpenUPM/EDM 公共依赖红线写入 `.nova/RULES.md`
  > 后续修订（2026-06-17 同会话）：发现卸载引用计数与"公共依赖各包不声明"策略冲突——单包声明 OpenUPM 会使卸载时误删工程固定的 registry，殃及其它 EDM 依赖方，故移除 max 的 OpenUPM 声明，公共依赖 registry 只工程固定

## 关联
- 相关 ADR：[[ADR-064-plugpals-dependency-detection|ADR-064]]
- 规则文件：`.nova/RULES.md`「OpenUPM / EDM 公共依赖红线」「破坏性变更与依赖版本下界红线」
