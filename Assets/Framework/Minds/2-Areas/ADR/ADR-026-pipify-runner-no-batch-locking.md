---
id: ADR-026
title: Pipify Runner 不冻结 Domain Reload、不进入 Asset Editing
summary: Runner 全程裸跑，禁两组批锁 API
category: editor
status: accepted
date: 2026-05-20
aliases:
  - ADR-026-pipify-runner-no-batch-locking
keywords: [ADR-026, ADR-026-pipify-runner-no-batch-locking]
tags: [adr, nova, pipify, hybridclr, yooasset, editor]
supersedes: []
superseded-by: []
related:
  - "[[PAT-51-sbp-buildcache-vs-lock-reload-incompat|PAT-51]]"
  - "[[PAT-48-editor-refcount-api-leak-drain|PAT-48]]"
  - "[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]"
  - "[[ADR-027-rule-ban-editor-refcount-batch-apis|ADR-027]]"
---

# ADR-026：Pipify Runner 不冻结 Domain Reload、不进入 Asset Editing

## 背景（Context）

Pipify `EditorUtil.Pipify.Runner.RunBatchAsync` 历史上把整个 Batch 包在两组引用计数 API 内：
- `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()`：合批 importer，减少跳闪
- `EditorApplication.LockReloadAssemblies()` / `UnlockReloadAssemblies()`：防止 Domain Reload 把 Console 日志和 Window 状态冲掉

实际复现表明：bundle 会落后一轮，且跑批后修改 cs 可能不再触发编译。

## 决策（Decision）

`Runner.RunBatchAsync` 全程裸跑，不调用任何引用计数式批锁 API。单 Step 拷贝后立即同步导入，让 SBP 读到新 `contentHash`。

## 后果（Consequences）

### 正面
- bundle 内嵌 dll 当批生效。
- 跑完批后 cs 仍可正常触发编译。
- SBP 看到的 contentHash 与 AssetDatabase 同步。

### 负面
- 多次 ImportAsset 不再合批，速度略慢。
- Console 在 Domain Reload 时可能被清空。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 Lock/Start 包裹整个 Batch | 会重现 bundle 延迟和编译冻结 |
| 保留 Lock/Start，再在每步后切换 | 复杂度高且容易漏配 |
| 改 YooAsset BundleBuilder 让它自己 Refresh hash | 跨第三方包改动，超出可控边界 |
| 让用户跑两次批兜底 | 违反"一次跑批应当当批生效"的产品预期 |

## 验证依据（Verification）

- 文件：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.Runner.cs`
- 文件：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.EDM4U.cs`（Step 不再做"临时豁免"假设）
- grep 关键词：`LockReloadAssemblies` / `StartAssetEditing` 应只出现在解释性注释里，无实际调用点
- 复现路径：PipifyWindow → "更新bundle" / "自动部署测试-全量" → 运行 → 改一行 cs → Unity 自动编译
