---
id: PAT-121-publish-sample-rewrite-symmetric
title: 发版自动化主/子包路径重写逻辑必须对称
summary: 主包专属步与子包同款步禁单边演进，扫描集与镜像策略全集对齐
category: workflow
type: pattern
status: active
date: 2026-05-29
aliases:
  - PAT-121-publish-sample-rewrite-symmetric
  - PAT-121
keywords:
  - PAT-121-publish-sample-rewrite-symmetric
  - 发版自动化主/子包路径重写逻辑必须对称
  - PAT-121
tags: [pattern, workflow, publish, sample, asset, yooasset, upm]
related:
  - "[[PAT-13-publish-no-cascade|PAT-13]]"
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
---

# PAT-121：发版自动化主/子包路径重写逻辑必须对称

## 适用场景（When）

- Nova 发版脚本维护时
- 新增"sample 内某类资产路径需要从开发工程前缀重写到 Sample 内相对前缀"的字段/扩展名/镜像目录
- 主框架（com.solotopia.nova.framework）与子包（kit.* / sdk.*）共享同一套 Sample 出包思路

## 核心做法（What & How）

发版脚本里凡是涉及 sample 内"配置/资产路径重写"的逻辑，**主框架专属代码与子包通用代码必须保持参数集一一对齐**：

| 维度 | 主框架代码 | 子包代码 | 对齐铁律 |
|---|---|---|---|
| 扫描扩展名集合 | `populate_sample_path_manifest` 的 `SAMPLE_PATH_SCAN_EXTS` | `_populate_pkg_sample_manifest` 的 `scan_exts` | 必须完全一致（`.prefab/.asset/.unity/.json`） |
| YooAssetSettings 镜像 | `copy_yooasset_settings_to_resources`（MainDemo only） | `_copy_pkg_yooasset_settings_to_resources(sample_dir)` | 凡是用 YooAsset 的 sample 都得跑，不能只对 MainDemo 跑 |
| ConfigRuntime 字段防呆 | （MainDemo 编辑期由 ConfigWindow 守门） | `_copy_sample_to_pkg` 内 Namespace + EntranceProcedure 校验 | 子包要有等价校验，主包靠 ConfigWindow 守门 |
| Prefab override 注入 | `inject_overrides_into_maindemo`（NovaPrefab 业务字段脱敏 + 还原） | 子包按需复用同一函数族，禁自造分叉 | 同一字段集走同一注入函数 |

实施 checklist：

1. 任何"先在 MainDemo 实现一版"的 sample 处理函数，落地 review 时 reviewer 必须问"子包 sample 是否需要等价处理？"
2. 共享的常量（扫描扩展名、跳过目录）必须抽到模块顶层常量；子包代码引用同一常量，禁字面量再写一遍
3. 新增字段到 manifest 时，主/子包扫描入口必须在同一个 PR 内同步改

## 为什么这么做（Why）

发版脚本的主/子包流水**写法上是两条路径**，但**语义上是同一个动作**——把"开发工程绝对路径"重写为"Sample 相对路径"。一旦单边演进：

- 主包扫 `.json` 子包不扫 → MainDemo 没事，子包 Luban 导出的 `Jsons/UIs.json` 在外部工程里仍指向 `Assets/Samples/MainDemo/...` 旧前缀，运行时 LoadAsync 直接找不到资源
- 主包做 YooAssetSettings.asset Editor→Resources 镜像，子包不做 → 用户 import 子包 sample 后，runtime `Resources.Load<YooAssetSettings>` 返回 null，YooAsset 启动失败
- 子包加了 ConfigRuntime 字段防呆，主包没加 → 主包出错时无早期告警，发版后才在外部工程暴露

这类问题**本地 dev 工程跑不出来**（dev 路径还是对的），只在外部工程 import sample 后才暴露，调试链路极长。

## 反模式（Anti-patterns）

- 写新主包专属逻辑时不在 module 顶部 TODO 标注"子包等价点"
- 子包同款逻辑用字面量复读主包常量，导致只改一边
- reviewer 只看主包改动，子包流水放过审

## 跨项目复用提示

任何 monorepo + 多包共出 Sample/Demo 的发版工具（不限 Unity），都适用本铁律：把"主仓专属"与"子包同款"的处理函数视作"双胞胎契约"，参数集必须共版本演进。

