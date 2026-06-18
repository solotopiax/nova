---
id: PAT-115
title: CHANGELOG 写人话
summary: 每项变更一句用户视角概括，禁列字段、签名、文件清单
category: docs
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-115
keywords:
  - PAT-115
  - CHANGELOG 写人话
tags: [pattern, changelog, docs, publish]
related:
  - "[[PAT-62-readme-changelog-dual-sync]]"
  - "[[PAT-53-changelog-grep-script-enforce]]"
---

# PAT-115：CHANGELOG 写人话

## 适用场景

- 每次发版时编写或追加 CHANGELOG
- 包内 CHANGELOG、项目根 CHANGELOG、子包 CHANGELOG
- `Added / Changed / Removed / Fixed / Breaking Changes` 等所有版本节

## 核心规则

每个条目限一句，主语必须是“使用者能看见的能力或行为变化”，不是内部代码细节。

| 维度 | 要求 |
|---|---|
| 长度 | 一句话；多条相关变化合并成一个能力点 |
| 主语 | 用户可见功能 / 模块 / 行为 |
| 禁止内容 | 字段名、方法签名、参数列表、内部链路、文件路径、partial 拆分 |
| Breaking | 描述“使用方要做什么”，不是“内部改了哪几个签名” |
| 详细细节 | 下沉到 `Docs`、ADR、PAT 或提交历史 |

## 为什么这样定

- CHANGELOG 面向升版者和使用者，不是面向作者复盘 diff
- 代码细节一多，真正的行为变化反而被淹没
- “写人话”能让没读过本次代码的人在很短时间内判断：这版我能看到什么变化、我需不需要跟进

## 反模式

- 把 CHANGELOG 写成压缩 diff
- 逐条罗列字段、签名和路径
- 同一能力点拆成很多技术实现小条目
- 直接搬 commit message

## 跨项目复用提示

所有遵循 Keep a Changelog 的项目都适用。Nova 只是在此基础上再强调：详细实现真相回到 `Docs + 源码`，CHANGELOG 只保留用户视角的变化概括。

## 关联

- [[PAT-62-readme-changelog-dual-sync|PAT-62]]
- [[PAT-53-changelog-grep-script-enforce|PAT-53]]
