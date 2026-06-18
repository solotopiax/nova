---
id: ADR-056
title: RuntimeProvider 配置选取统一收口 WorkspaceActive 锚点
summary: 配置选取改走 WorkspaceActive 锚点
category: editor
status: accepted
date: 2026-06-01
aliases:
  - ADR-056-runtimeprovider-config-select-via-workspaceactive
keywords:
  - ADR-056
  - RuntimeProvider 配置选取统一收口 WorkspaceActive 锚点
  - ADR-056-runtimeprovider-config-select-via-workspaceactive
tags: [adr, nova, editor, config, namespace]
supersedes: []
superseded-by: []
related:
  - "[[ADR-047-editor-active-master-anchor|ADR-047]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-049-yooasset-settings-via-configmaster|ADR-049]]"
---

# ADR-056：RuntimeProvider 配置选取统一收口 WorkspaceActive 锚点

## 背景

RuntimeProvider 过去靠 `FindAssets` 和三维匹配选配置，多 Demo 共存时会稳定选错，导出命名空间和构建期配置都可能错到 LoginDemo。

## 决策

- `GetNamespace()` 改为直接读 `WorkspaceActive.Get()` 对应的 master。
- `GetCurrent()` 也改为通过 `WorkspaceActive` 解析当前激活的 `ConfigRuntimeSO`。
- 全部 `FindAssets` / first 兜底从 RuntimeProvider 链路里移除。

## 影响

- Namespace 和 ConfigRuntimeSO 从同一锚点读取，避免语义分叉。
- 多 Demo 工程的导出和构建不再靠字典序碰运气。
- 仍依赖固定目录约定，布局变动时要同步调整解析逻辑。

## 关联

- 相关 ADR：`ADR-047`、`ADR-005`
