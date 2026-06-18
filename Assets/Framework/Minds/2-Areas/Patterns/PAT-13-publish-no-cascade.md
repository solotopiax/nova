---
id: PAT-13
title: UPM 发版默认不级联依赖
type: pattern
status: active
date: 2026-05-16
summary: UPM发版不级联连带兄弟包
category: workflow
aliases:
  - PAT-13-publish-no-cascade
keywords:
  - PAT-13
  - PAT-13-publish-no-cascade
  - UPM 发版默认不级联依赖
tags:
  - pattern
  - upm
  - publishing
  - semver
related: []
---

# PAT-13：UPM 发版默认不级联依赖

## 适用场景（When）

- 多包 UPM monorepo（Nova 这种 N 个独立 package + 内部依赖关系）
- 每次发版只改其中 1-2 个包，其余兄弟包代码未动
- 团队有"全家版本号对齐"或"顺手 bump"的历史习惯
- 使用统一发布入口或批量发版流水线

## 核心做法（What & How）

### 基本规则

**纯内部升版不触发依赖级联。** 兄弟包 `dependencies` 指向旧版本号继续跑完全合法，不要为了"版本号整齐"就拖上 kit.network / firebase 全家一起升。

### 触发级联的两个条件

仅在以下两种情况下才提示级联：

| 条件 | 说明 |
|---|---|
| **破坏性变更** | 本轮含 public API 删改、序列化结构变更、跨包契约变更，**且**兄弟包引用了被破坏的 API |
| **用户主动要求** | 消息明确出现「全家对齐 / 顺手把依赖对齐 / 都升一下」 |

非以上两种情况，默认 dry-run：
- 不扫描兄弟 package.json
- 不列出依赖调整
- 不碰兄弟包版本号

## 为什么这么做（Why）

- **避免无意义噪声**：纯内部 patch（修文档 / 改注释 / 重命名 private 方法）不会破坏兄弟包，强行 bump 制造大量假提交
- **commit history 可读性**：「全家对齐铁律」会让 git log 充满 `chore: bump kit.network 1.2.3 → 1.2.4`，淹没真正有意义的变更
- **下游消费方误判**：兄弟包版本号变了，下游以为有改动会去看 changelog，结果什么都没有，浪费下游时间
- **SemVer 语义不对**：patch bump 表示有 bug fix，但实际没动；语义被破坏后 SemVer 整个失效
- **历史教训**：只因为“顺手”就触发兄弟包级联，最终会让版本噪声远大于真实变更

## 反模式（Anti-patterns）

- **每次发版全家 bump**：`kit.framework` 改了一行注释 → 把 `kit.network` / `kit.config` / `kit.ui` 全部 patch++
- **改 README 也 bump 版本**：纯文档改动也走 patch++ 流程
- **依赖锁死最新版**：兄弟包 `dependencies` 写死 `^1.2.3`，每次升级都要全家级联
- **用 patch++ 当 "我改了" 的信号**：发版本号不是给自己看的进度条
- **不区分破坏性 vs 非破坏性**：一律 patch++，破坏性改动该 major 的反而不 major
- **静默级联**：只要求发布一个包，流水线却默默把兄弟包一起升版

## 跨项目复用提示

- **思想完全可迁移**：所有 monorepo（npm workspace / yarn workspace / pnpm / Cargo workspace / Maven multi-module）都适用
- 关键判据：被改包的**对外契约（public API / wire format / file format）**是否变了
- 工具支持：lerna 的 `--force-publish` 默认行为反而是反模式，需要显式禁用
- changesets / nx 的"affected projects"算法本身就符合本规则
- 不适合的场景：版本号有外部强耦合（如配套游戏客户端 + 服务端 + SDK 必须同版本上线）的发版
- semver-major 边界：破坏性变更同样应该谨慎级联——如果兄弟包没引用被破坏的 API，仍然不需要 major bump

## 关联

- 相关原则：发布阶段禁止无依据的兄弟包级联改动
