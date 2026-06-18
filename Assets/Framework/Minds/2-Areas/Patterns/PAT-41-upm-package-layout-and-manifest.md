---
id: PAT-41
title: Nova UPM 包布局与元数据基线
type: pattern
status: active
date: 2026-06-05
summary: Nova UPM 包需稳定布局与元数据
category: upm
aliases:
  - PAT-41-upm-package-layout-and-manifest
keywords:
  - PAT-41
  - UPM 包布局
tags:
  - pattern
  - upm
  - package
related:
  - "[[PAT-109-upm-package-docs-mandatory|PAT-109]]"
  - "[[PAT-123-upm-sample-displayname-equals-samplename|PAT-123]]"
---

# PAT-41：Nova UPM 包布局与元数据基线

## 适用场景

- 新建或整理 `UPMPackages/` 下的 Nova 自维护包
- 评估某个包是否满足发版所需的最小结构
- 设计 package 元数据与 sample 元数据的职责边界

## 核心规则

### 1. 包根必须有稳定身份文件

- `package.json` 是包身份与依赖声明的唯一入口。
- Nova 自维护包应同时维护 `README.md` 与 `CHANGELOG.md`，避免“可安装但不可理解、可发布但不可追踪”。

### 2. Sample 元数据与包元数据分层

- 包本身的身份、版本、依赖留在 `package.json`。
- 样例描述使用独立样例元数据维护，再在发布阶段投影到真正对外的 `samples` 字段。
- 不把开发态 sample 目录结构直接硬编码进长期包结构说明。

### 3. 目录布局追求稳定，不追求花哨

- 包内公开内容、编辑器内容、第三方依赖与样例应各自收口。
- Nova 自维护包必须保留 `Nova/` 与 `Core/` 两个顶层目录：`Nova/` 放 Solotopia / Nova 自有代码，`Core/` 放第三方源码或原厂内容（如有）。
- 即使当前包没有第三方源码，也应保留 `Core/` 槽位，避免后续补入第三方内容时破坏既有结构。
- 开发态临时产物、发布中间态目录不应成为长期结构的一部分。

## 当前项目事实

- `UPMPackages/` 下 Nova 自维护包普遍以 `package.json` 为根。
- 存在 `coreVersion`、`displayName`、`nova-samples.json` 等元数据协同关系。
- 新建包应按 `Nova/` + `Core/` 顶层结构组织内容，不能把 Runtime/Editor/第三方源码直接平铺在包根。
- 样例相关字段在发布链路中会被整理为对外可识别的 `samples` 结构，而不是长期直接手维护所有中间态目录。

## 为什么这样定

- 包身份、文档、样例如果混写在一处，最终会同时损坏安装、阅读和发布三条链路。
- 把开发态元数据与发布态元数据分层，能降低 sample 系统的耦合度。
- 目录基线稳定后，增量扩包和发版脚本都更容易收敛。

## 反模式

- 只有 `package.json`，没有 README/CHANGELOG
- 把自有代码直接放到包根 `Runtime/` / `Editor/`，跳过 `Nova/` 顶层归属
- 把第三方源码混进 `Nova/`，导致自有适配代码和原厂源码边界不清
- 把发布中间目录当成长期仓库结构
- 用同一份字段同时承载“开发态 sample 来源”和“发布态 sample 暴露”

## 关联

- [[PAT-109-upm-package-docs-mandatory|PAT-109]]
- [[PAT-123-upm-sample-displayname-equals-samplename|PAT-123]]
