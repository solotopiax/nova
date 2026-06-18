---
id: PAT-DRAFT
title: Prefab 实例内子节点迁移禁用 SetParent 改用 Instantiate 拷贝
summary: Prefab 实例跨边界迁移子节点时改用拷贝而非 reparent
status: archived
date: 2026-06-04
archived-date: 2026-06-08
type: pattern
category: runtime
aliases:
  - PAT-DRAFT-2026-06-04-prefab-instance-child-migration
tags:
  - pattern
  - archive
  - prefab
  - unity-mcp
---

# PAT-DRAFT：Prefab 实例内子节点迁移禁用 SetParent 改用 Instantiate 拷贝

## 归档说明

- 保留为历史草稿，供后续若需转正式 PAT 时复用结论。
- 已移除会话与 hook 残留，仅保留可复用规则。

## 适用场景
- 将独立 prefab 转换为 Prefab Variant，需把旧 Content 下的业务子节点搬到新 Variant 实例时
- 任何「跨 prefab 实例边界迁移已存在子节点」的批量操作
- 信号：源节点位于某个 prefab instance 内部，目标父节点属于另一个 prefab/instance

## 核心做法
1. **禁用 `Transform.SetParent` 做 reparent**——Unity 会拒绝跨 prefab 实例改父级（warning：`Setting the parent of a transform which resides in a Prefab instance is not possible`），结果是子节点静默丢失。
2. 改用 `Object.Instantiate(go, newContent, false)` 把子节点**拷贝**进新父级。
3. 用**相对路径重绑**字段：记录旧绑定字段相对 Content 的路径，迁移后 `newContent.Find(relPath)` 重新拿引用并回填序列化字段。
4. 迁移后程序化校验 `biz=N/N`（业务字段全绑）比肉眼截图更可靠；prefab stage 的 scene view 截图对 UI 布局验证无效（视角在远处天空盒看不到 Canvas 平面）。
5. 误操作后第一时间 `git checkout -- <prefab>` 还原 + `refresh_unity`，再换正确方案重做。

## 反模式
- 用 `SetParent` reparent prefab 实例子节点 → 节点丢失、字段 biz=0/N（本会话 Login prefab 实际被这样破坏过）。
- 依赖 scene view 截图验证 prefab stage 内 UI 布局（视角不对，看不到）。
- 普通非 prefab-instance 的 GameObject 不受此限制，可直接 reparent——不要把本规则误推广到所有场景。
