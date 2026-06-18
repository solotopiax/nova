---
id: ADR-047
title: Editor 激活 ConfigMaster 锚点（Globals.json + GUID + 四段回退）
summary: Globals.json 锚定 Editor 激活 master
category: module
status: accepted
date: 2026-05-28
aliases:
  - ADR-047-editor-active-master-anchor
  - ADR-047
keywords:
  - ADR-047-editor-active-master-anchor
  - Editor 激活 ConfigMaster 锚点（Globals.json + GUID + 四段回退）
  - ADR-047
tags: [adr, nova, configmaster, editor, anchor]
supersedes: []
superseded-by: []
related:
  - "[[ADR-049-yooasset-settings-via-configmaster]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[PAT-118-atomic-write-json-via-rename]]"
---

# ADR-047：Editor 激活 ConfigMaster 锚点

## 背景

多 sample 共存时，`FindAssets("t:ConfigMasterSO")` 会按 GUID 顺序误选“第一个”，导致编辑器工具和注入链路指向错误的 master。

## 决策

- 用 `ProjectSettings/Nova/Globals.json` 记录当前激活的 ConfigMaster GUID。
- 统一通过 `EditorUtil.Config.WorkspaceActive` 读取，不再直接 `FindAssets`。
- ConfigWindow 在未绑定时显示引导卡片，而不是静默 fallback。
- 资产移动 / 重命名时只要 GUID 不变，就继续可追踪。

## 影响

- 多 sample 工程的激活 master 选择变成显式配置，不再靠字典序运气。
- 团队 clone 后能直接命中当前 master，但首次绑定需要人工确认一次。
- `Globals.json` 必须入 git，不能当作本地缓存丢掉。

## 关联

- 相关 ADR：`ADR-033`、`ADR-049`
- 相关 Pattern：`PAT-118`
