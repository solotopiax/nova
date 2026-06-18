---
id: ADR-033
title: MainDemo 演示拓扑：各 sample 独立 Nova.prefab + MainDemo 内树形导航
summary: sample 独立闭包；MainDemo 树形导航
category: module
status: accepted
date: 2026-05-22
aliases:
  - ADR-033-maindemo-isolated-topology
keywords: [ADR-033, ADR-033-maindemo-isolated-topology]
tags: [adr, nova, maindemo, sample, upm]
supersedes: []
superseded-by: []
related:
  - "[[PAT-65-demo-coverage-standard|PAT-65]]"
---

# ADR-033：MainDemo 演示拓扑：各 sample 独立 Nova.prefab + MainDemo 内树形导航

## 背景（Context）

Nova Framework 以 UPM 形式分发 framework / sdk / kit 等多个 package，每个 package 自带 sample。
最初想法是 MainDemo 作为统一入口，跨 package 自动收集所有 sample 入口形成单一树形导航，叶子节点根据类型分流到 `Nova.UI.OpenUI<T>()`（内层）或 `Nova.Asset.LoadSceneAsync(..., Additive)`（外层）。

但深入推演后发现"统一聚合"撞天花板——配置融合是不可解决的硬约束：

- **Nova.prefab 共享**：UI 表 / Procedure 表 / Manager 配置全局唯一，跨 package 命名冲突
- **YooAsset 配置共享**：全局只能一份默认 package、host vs DLC 模式互斥
- **HybridCLR AOT 列表**：跨 demo 可能不同
- **业务 dll 命名空间**：sample 之间命名空间冲突
- **每次 SDK / Kit 升级都被迫主框架联调**——UPM 生态反模式

强行聚合 = 把 sample 维护成本指数化。

## 决策（Decision）

**各 sample package 完全独立闭包，互不感知**：

| 维度 | 决策 |
|---|---|
| Nova.prefab | 每个 sample 自带一份，仅含本 sample 演示所需配置 |
| YooAsset 配置 | 每个 sample 自带，独立打 AB |
| 业务 dll | 每个 sample 独立程序集名 |
| Scene 入口 | 每个 sample 一份独立 scene，双击 Play 直接进 |
| 跨 package 自动聚合 | **禁止**——废弃 DemoRegistry 静态注册、跨 package scene LoadSceneAsync 等聚合方案 |

**MainDemo 自身定位**：framework 包的 sample，**仅演示 framework 包对外接口**（Core + 全部 Modules + HybridCLR + Integration）。MainDemo 内部用 Nova.UI 实现树形导航 Panel 跳转本 sample 内 UI，全部走 `Nova.UI.OpenUI<T>()`，**不涉及跨 package 调度**。

**手机端唯一入口**：`MainDemo.unity` 是 framework sample 的唯一入口；外围 sample（如 SdkDemo / KitDemo）有各自的 scene，互不依赖。

**外围 sample scene 的独立 Play**：sample scene 根节点挂 `SampleSceneGuard` MonoBehaviour，`Awake` 检测 `Nova` 运行环境是否已初始化，未初始化时弹窗提示并停 Play（仅 Editor 触发，手机端无此路径）。MainDemo 自身不需要 Guard——它就是入口。

## 后果（Consequences）

### 正面
- 各 sample 互不影响，SDK/Kit 升级与主框架解耦
- 配置融合问题彻底消失（每个 sample 自闭包）
- 符合 Unity 官方 URP / Input System / UGUI samples 的标准做法
- 树形导航 Panel 内零跨 package 引用，IL2CPP / 手机端零额外约束

### 负面
- 每个 sample 都要自带一份 Nova.prefab，存在配置重复
- 用户想"一站式查所有 demo"时需要在多个 scene 间切换
- 外围 sample 不能直接独立 Play（需走 Guard 拦截）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| MainDemo 统一入口 + 跨 package 自动聚合 | 配置融合不可解（UI 表 / Procedure 表 / YooAsset 配置 / AOT 列表全局冲突），SDK 升级被迫主框架联调 |
| `[RuntimeInitializeOnLoadMethod] + DemoRegistry` 静态注册 | 即便解决 IL2CPP strip，配置融合问题仍在，方案治标不治本 |
| `[DemoNode] + 反射扫描` | 同上 + IL2CPP 反射性能开销 + link.xml 维护成本 |
| 外围 sample scene 允许独立 Play | 每个 sample 都要塞完整 Nova.prefab 序列化，sample 体积膨胀且与 MainDemo 入口策略冲突 |

## 验证依据（Verification）

- 代码：`Assets/Samples/MainDemo/MainDemo.unity` 唯一 framework sample 入口
- 代码：`Assets/Samples/MainDemo/Scripts/Runtime/UIs/DemoNavTreePanel.cs`（待落地）
- 代码：MainDemo 内部硬编码树节点静态列表（待落地，位于 MainDemo Scripts/Runtime/）
- grep 关键词：`DemoRegistry` / `DemoNode` / `IDemoEntry` 应在 framework 内零命中（聚合方案废弃）
- SampleSceneGuard：外围 sample package 后续接入时由各自仓库实现

## 关联
- 规范落点：待补充到统一 Sample 拓扑规范（暂未落地）
- 相关 ADR：[[ADR-024-launch-to-app-rename|ADR-024]]（MainDemo.unity 来源）· [[ADR-031-upm-three-piece-mandatory|ADR-031]]（UPM 包结构铁律）
- 相关 Pattern：[[PAT-65-demo-coverage-standard|PAT-65]]（demo 接口覆盖标准）· [[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]
