---
id: PAT-145
title: UPM 发布先审真实安装链再判级联
summary: 发版前沿真实安装入口审计依赖级联
category: workflow
type: pattern
status: active
date: 2026-07-13
source: cur-session
aliases:
  - PAT-145-upm-install-chain-cascade-audit
tags: [pattern, methodology, upm, publishing, dependency, novaspark]
related:
  - PAT-13
---

# PAT-145：UPM 发布先审真实安装链再判级联

## 适用场景（When）

- UPM 包修复了新 Unity 版本下的编译、安装或运行兼容性问题。
- 被修改的是第三方库封装包、wrapper 内核版本或传递依赖，公开 API 本身没有删改。
- NovaSpark、Bootstrap、主框架或其他顶层入口固定了具体包版本。
- 准备依据“没有 breaking API”判断无需级联时。

## 核心做法（What & How）

### 1. 先追踪用户真实安装链

发布范围判断必须从用户入口向下追踪，而不是从本地改动包向上猜测：

```text
NovaSpark / Bootstrap / manifest
  → 顶层 UPM 包的远端已发布版本
  → 该版本 package.json 中的 dependencies
  → 传递依赖的远端已发布物
  → 目标 Unity 版本中的实际编译与运行结果
```

每一层都要读取私仓中“用户实际会拿到的版本”。本地 `package.json`、本地 file dependency 或尚未发布的源码不能替代远端证据。

### 2. 同时审计两类级联

| 级联类型 | 触发条件 | 典型动作 |
|---|---|---|
| API / 契约级联 | 公开 API、序列化、跨包契约或依赖下界发生破坏性变化 | 升消费方版本、适配代码、提升依赖下界 |
| 安装兼容性级联 | 旧依赖在受支持环境中会编译失败、安装失败或运行错误，即使 API 没变 | 升修复包版本，并让所有受支持安装入口解析到修复版本 |

“没有 breaking API”只能排除第一类，不能据此宣布“无级联”。

### 3. 级联范围以受支持入口闭环为准

至少完成以下闭环：

1. 修复包使用未占用的新版本发布，禁止复用远端已有版本号。
2. 顶层入口包更新依赖下界并升版，使新安装能够解析到修复包。
3. NovaSpark / Bootstrap 更新顶层包固定版本；命名、README、安装示例同步。
4. 支持独立安装的兄弟包若仍会把用户带回问题版本，也必须纳入级联；不支持独立安装的包不得为了整齐而全家升版。
5. dry-run 明示“入口 → 顶层包 → 问题依赖 → 修复版本”的新旧解析结果。

### 4. 发布前设置负向门禁

以下任一问题未回答清楚时，禁止确认正式发布：

- 使用上一版 NovaSpark 创建新工程，会安装哪个 Framework？
- 私仓中该 Framework 的依赖实际指向哪个 UniTask？
- 新 NovaSpark 是否明确指向包含修复依赖的 Framework 新版本？
- 在目标 Unity 最低与最高支持版本中，按候选发布物安装是否通过编译？
- 正式发布脚本是否会因 wrapper 版本未变而把修改包判定为“远端已存在”并跳过？

### 5. 与默认不级联原则的关系

[[PAT-13-publish-no-cascade|PAT-13]] 仍用于禁止无依据的“全家 bump”，但必须在本 Pattern 的安装链审计通过后才能应用。安装入口仍解析到已知问题版本，就是明确的级联依据，不属于无意义版本噪声。

## 为什么这么做（Why）

UniTask 2.5.11 升级曾暴露出这一盲区：

- 本地 `com.solotopia.unitask` 已把 core 从 `2.5.10` 升到 `2.5.11`，但 wrapper 版本仍为 `10.0.5`。
- 私仓 `unitask@10.0.5` 仍是 core `2.5.10`，不能被同版本本地源码覆盖。
- 私仓 `framework@0.5.37` 依赖 `unitask@10.0.5`。
- `NovaSpark2.2.cs` 固定安装 `framework@0.5.37`。

因此只发布 `unitask@10.0.6` 不会修复旧安装入口；旧 NovaSpark 在 Unity 6000.5 新工程中仍可能拉取旧 Tracker 代码并触发 `TreeViewItem` 的 `CS0619`。必须同时让 Framework 与新版 NovaSpark 的安装链指向修复版本。

## 反模式（Anti-patterns）

- 只 grep `public` / `protected` 变化，没发现 breaking API 就写“依赖级联：无”。
- 只看本地 file dependency 的消费者测试，不核对私仓已发布元数据。
- 修改 wrapper 内核却不升 wrapper 版本，导致正式脚本跳过发布或 npm 撞号。
- 发布了底层修复包，却没有更新 NovaSpark / Bootstrap 固定的顶层包版本。
- 把“默认不全家升版”误解为“任何非 breaking 改动都不级联”。
- 用新版入口验证通过后，声称旧入口也已修复。

## 跨项目复用提示

适用于所有由安装器、模板、脚手架或顶层聚合包固定依赖版本的多包仓库。判断标准不是“改了几个包”，而是每个承诺支持的用户入口能否稳定解析到兼容发布物。

## 来源（Origin）

- 会话日期：2026-07-13
- 关键对话节选：
  > 用户：com.solotopia.unitask 是否有级联？如果用户继续用老版本的 novaspark 在新版本的 unity 中安装 nova，是否依旧会报错？
  > 用户：沉淀到 obs，这个级联的问题必须重视起来，以后不许再有遗漏。
  > AI：远端 framework@0.5.37 依赖 unitask@10.0.5，旧 NovaSpark 固定 Framework 0.5.37；只发布 unitask@10.0.6 不会改变旧安装链。

## 关联

- 相关 Pattern：[[PAT-13-publish-no-cascade|PAT-13]]
- 发布规则：`.agents/skills/nova-publish/SKILL.md`
- 火种入口：`NovaSpark2.2.cs`
