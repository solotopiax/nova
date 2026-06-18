---
id: PAT-119
title: UPM 私有 fork 必须显式标注本地改动
summary: 私有 fork 改动必须留本地标注与包级变更记录
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-119
  - PAT-119-upm-private-fork-local-diff-marking
  - PAT-119-upm-private-fork-by-taoye
keywords:
  - PAT-119
  - PAT-119-upm-private-fork-local-diff-marking
  - PAT-119-upm-private-fork-by-taoye
  - UPM 私有 fork 必须显式标注本地改动
tags: [pattern, upm, fork, maintainability]
related:
  - "[[ADR-049-yooasset-settings-via-configmaster]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
---

# PAT-119：UPM 私有 fork 必须显式标注本地改动

## 适用场景

- `UPMPackages/` 下存在 Nova 自维护的私有 fork
- 为框架集成而改动了包内源码、编辑器工具或配置入口
- 未来仍需要和上游版本持续同步

## 核心规则

本地 fork 改动必须留下两类痕迹：

1. 文件级改动标注
2. 包级变更记录

### 1. 文件级改动标注

改动过的源码文件头部追加一句稳定说明，例如：

```csharp
// modify: local fork - 暴露外部注入配置的扩展点
```

目的不是逐行解释，而是让阅读者第一眼知道：这不是上游原版行为。

### 2. 包级变更记录

包内 `CHANGELOG.md` 为本地 fork 改动补一段明确记录，例如：

```markdown
## [x.x.x] - 2026-05-28
### Changed (local fork)
- 增加 Nova 集成所需的扩展点
- 补充本地路径加载能力
```

版本号尽量与上游版本树同轨，不平行发散一套独立大版本体系。

## 为什么这样定

- 上游同步时能快速识别 Nova 本地差异
- review 时不会把本地扩展误认成上游原始能力
- 差异有统一入口，不会淹没在大 diff 里
- fork 维护责任更清晰

## 反模式

- 静默改包代码不留痕
- 只留局部注释，不做文件级说明
- 代码改了但包级 CHANGELOG 不记
- 改包同时自行发散独立大版本号
- 把 `.meta` / GUID 这种高风险资产改动混进“普通私改”

## 跨项目复用提示

适用于任何“引入上游包，但又必须本地 fork 维护”的项目。关键不在于注释具体写哪种字样，而在于必须形成稳定、可 grep、可审查的本地差异标记。

## 关联

- [[ADR-049-yooasset-settings-via-configmaster]]
- [[PAT-41-upm-package-layout-and-manifest|PAT-41]]
