---
id: ADR-027
title: 规则层禁用 LockReloadAssemblies / StartAssetEditing 引用计数 API
summary: 风格规范 §4·1 列入两组批锁 API 零容忍
category: quality
status: accepted
date: 2026-05-20
aliases:
  - ADR-027-rule-ban-editor-refcount-batch-apis
keywords: [ADR-027]
tags: [adr, nova, editor, quality, rule]
supersedes: []
superseded-by: []
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[PAT-48-editor-refcount-api-leak-drain|PAT-48]]"
---

# ADR-027：规则层禁用 LockReloadAssemblies / StartAssetEditing 引用计数 API

## 背景（Context）

Pipify Runner 历史故障复盘后发现两组 Editor API 共享同一个反模式：
- `EditorApplication.LockReloadAssemblies` / `UnlockReloadAssemblies`
- `AssetDatabase.StartAssetEditing` / `StopAssetEditing`

它们都是引用计数式 API，一旦异常、取消或强退漏跑 finally，就会导致 Domain Reload 或 ImportAsset 状态卡死。

## 决策（Decision）

`csharp-code-style.md §4·1` 直接禁用这两组 API；替代方案是把 Step 设计成不依赖批锁，导入后实时跑。

零容忍。新增违例由 code-reviewer 直接打回。

## 后果（Consequences）

### 正面
- code-reviewer 能在静态审查阶段拦下违例。
- 隐性批锁约定被彻底剔出，Step 设计回归自洽。

### 负面
- 历史工具若真需要合批，需要单独提 ADR。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 仅在 Pipify 模块禁，不上规则 | 同样错误会被复制到其他 Editor 工具（如 ConfigWindow / 资源批处理脚本） |
| 允许使用但强制 try/finally + 自检 | 防线仍不可靠 |
| 仅禁 `LockReloadAssemblies`，保留 `StartAssetEditing` | 两个 API 故障模式同构，不一并禁会留半个坑 |

## 验证依据（Verification）

- 规范落点：统一工程约束中的 Unity API 禁用清单（已落地）
- grep 全仓：`LockReloadAssemblies` / `StartAssetEditing` 现仅出现在解释性注释里
- code-reviewer 加载 `csharp-code-style.md` → 静态审查触发禁用条目时报红
