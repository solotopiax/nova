---
id: ADR-031
title: 每个 UPM 包必须自带 CHANGELOG、LICENSE、README
summary: Nova 自维护 UPM 包缺少三件套时，发版应直接中止
category: workflow
status: accepted
date: 2026-06-05
aliases:
  - ADR-031-upm-three-piece-mandatory
keywords: [ADR-031, UPM, CHANGELOG, LICENSE, README]
tags: [adr, nova, workflow, publish, upm]
related:
  - "[[PAT-13-publish-no-cascade|PAT-13]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-53-changelog-grep-script-enforce|PAT-53]]"
---

# ADR-031：每个 UPM 包必须自带 CHANGELOG、LICENSE、README

## 背景

Nova 的 UPM 包是按包根打包与分发的。  
如果包根缺少独立的变更记录、协议说明或使用入口，使用者在安装后无法直接得到完整上下文。

这不是“文档好不好看”的问题，而是发版产物是否自解释的问题。

## 决策

每个 Nova 自维护 UPM 包根目录都必须具备三件套：

- `CHANGELOG.md`
- `LICENSE.md`
- `README.md`

缺少任意一项时，发版流程应直接中止。

## 进一步约束

### CHANGELOG

- 每次升版都要新增对应版本节
- 包级 CHANGELOG 是该包的权威变更记录

### LICENSE

- 包内必须明确协议文本
- 第三方封装包还应补充上游协议说明，避免把 Nova 封装协议与上游协议混为一谈

### README

- 至少要说明包用途、安装方式、当前版本或核心入口

## 当前实现状态

Nova 当前发布链路已经具备“发版前检查三件套”的实现。  
这意味着 ADR-031 不是纯口头规范，而是已经被工具化执行的发布约束。

## 后果

### 正面

- 包安装后即可直接看到用法、协议与版本变化
- 发版漏写文档不再靠人工记忆兜底
- 包级职责比项目级总文档更清晰

### 负面

- 每次升版都要同步维护更多文件
- 批量包管理时，文档维护成本上升

## 被否定的替代方案

- 只在项目根维护一份总 CHANGELOG
- 只要求 README，不要求 LICENSE / CHANGELOG
- 把包级说明延迟到“以后补”

这些做法都会让最终产物失去包级自解释能力。

## 关联

- [[PAT-41-upm-package-layout-and-manifest|PAT-41]]
- [[PAT-53-changelog-grep-script-enforce|PAT-53]]
