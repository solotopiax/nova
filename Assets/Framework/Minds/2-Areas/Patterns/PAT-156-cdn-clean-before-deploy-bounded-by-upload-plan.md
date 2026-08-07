---
id: PAT-156
title: CDN 上传前清理必须受本次上传计划约束
type: pattern
status: active
date: 2026-08-07
summary: 远端清理只覆盖本次精确文件和目录前缀，失败时零上传
category: editor
aliases:
  - PAT-156-cdn-clean-before-deploy-bounded-by-upload-plan
keywords:
  - PAT-156
  - CDN 上传前清理
  - 清理云端文件和目录
  - Object Key
  - PresetOSSPath
  - clean before deploy
tags:
  - pattern
  - nova
  - editor
  - cdn
  - oss
  - pipify
  - safety
---

# PAT-156：CDN 上传前清理必须受本次上传计划约束

## 适用场景

- ConfigWindow 或 Pipify 在上传资源前提供“清理云端文件和目录”选项。
- 一次部署同时包含精确文件和目录树，例如版本检查文件、热更资源目录、白名单 JSON 与 YooAsset 版本文件目录。
- 远端存储使用对象键和前缀表达文件与目录，没有真实目录边界。

## 核心做法

1. 先完整构建并校验本次上传计划，再从计划中推导清理目标；不得从 `PresetOSSPath` 单独推导清理范围。
2. 单文件只删除本次计划中的精确 Object Key。
3. 目录只清理本次计划对应的远端目录前缀，并强制补齐 `/` 边界，避免相似前缀被误删。
4. 远端目录为空时拒绝清理，绝不能退化为清空整个 `PresetOSSPath`。
5. 对象列举必须处理分页；批量删除遵守供应商单批上限，阿里云 OSS 每批最多删除 1000 个对象。
6. 清理与上传是 fail-fast 顺序：清理全部成功后才开始上传；任一清理失败都立即终止，保证本次执行零上传。
7. 清理开关默认关闭。ConfigWindow 使用会话状态，Pipify 使用当次 Step 参数快照，均不得回写 `ConfigMasterSO`。

## 为什么这样定

对象存储的“目录”只是键前缀。若把配置根路径直接当清理目标，空后缀、缺少 `/` 边界或相似目录名都可能扩大删除范围。以上传计划作为唯一清理边界，可以让“将要上传什么”与“允许先删除什么”保持同一事实来源；先清理后上传且失败零上传，则避免清理只完成一部分时继续发布新文件，形成混合版本。

## 反模式

- 直接列举并删除整个 `PresetOSSPath`。
- 远端目录为空时回退到配置根路径继续清理。
- 使用 `foo/bar` 作为前缀而不补 `/`，误删 `foo/bar-old`。
- 边列举边上传，或清理失败后只记 Warning 继续上传。
- Pipify 为本次清理修改持久化 `ConfigMasterSO`。

## 来源与验证

- 实现：`EditorUtil.CDN` 先构建上传计划，再删除精确键和带目录边界的远端前缀；阿里云 OSS 适配层负责分页列举与分批删除。
- 入口：ConfigWindow 的资源部署、白名单部署，以及 Pipify `cdn.deploy`、`cdn.whitelist.deploy` 共用同一执行语义。
- 验证：`CdnDeploymentPlannerTests`、`CdnDeploymentExecutionTests`、`CdnPipifyStepTests` 覆盖边界、失败停止、默认值与当次参数快照；目标 EditMode 测试程序集通过 84/84。

