---
id: PAT-62
title: README / CHANGELOG 双层同步铁律
summary: 发版前工程根与包内两层文档全字段对齐，禁单层更新
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-62-readme-changelog-dual-sync
keywords:
  - PAT-62
  - PAT-62-readme-changelog-dual-sync
  - README / CHANGELOG 双层同步铁律
tags: [nova, publish, docs]
---

# PAT-62：README / CHANGELOG 双层同步铁律

## 适用场景

- Nova Framework 每次发版前
- 同时存在项目根文档与包内文档两层 README / CHANGELOG
- 需要确保对外口径与包产物口径一致

## 两层文档定位

- 项目根 README / CHANGELOG：团队聚合视图
- 包内 README / CHANGELOG：随包产物发出，外部用户直接可见

## 核心检查项

发版前，两层文档必须同时核对：

1. 当前版本号
2. Unity 版本声明
3. 依赖表
4. 模块一览
5. Samples 路径
6. 当前版本对应的 CHANGELOG 节

## 为什么这样定

- 只维护包内文档，会让团队聚合视图长期腐烂
- 只维护项目根文档，会让最终包产物对外失真
- README 与 CHANGELOG 的双层同步，本质上是“聚合视图”和“交付视图”都必须讲同一套事实

## 反模式

- 只更新其中一层文档
- 模块一览靠记忆填写
- 依赖表不对齐 `package.json`
- 保留历史模块名或旧路径
- README 写实际工程细节而不是当前对外声明

## 关联

- [[PAT-115|PAT-115]]
- [[PAT-53|PAT-53]]
