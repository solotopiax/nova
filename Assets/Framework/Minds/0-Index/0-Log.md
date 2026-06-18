---
title: Nova 知识层维护日志
date: 2026-06-05
---

# Nova 知识层维护日志

本文件只记录 `Docs / Minds` 的结构治理动作。  
它不是会话记录，不是实现说明，也不是工具运行日志。

旧版明细记录已归档到：

- `4-Archives/Records/0-Log-legacy-2026-06-05.md`

## 记录原则

- 只记结构变化
- 一条只说“改了什么、影响什么”
- 当前实现细节仍回到 `Docs + 源码`

相关入口：

- [Minds 总索引](0-Index.md)
- [审计归档索引](../4-Archives/Audits/INDEX.md)
- [Minds 全量治理审计（2026-06-08）](../4-Archives/Records/2026-06-08-minds-audit.md)

## 2026-06-05

### docs-minds-decouple

- `Docs` 收敛为当前代码事实层。
- `Minds` 收敛为长期知识层。
- 两层不再混写。

### active-layer-cleanup

- `2-Areas` 正文去除旧协作栈语义，回到 Nova 本体知识。
- 历史协作材料统一视为归档背景，不再作为默认入口。

### index-rewrite

- `0-Index` 全部改为极简导航页。
- `ADR / PAT / GLO / MOC / RES / Terms` 入口统一按“先缩范围，再读正文”的方式组织。

### keyword-index-rebuild

- `_keyword-index.json` 按当前活跃知识层重建。
- 索引关键词与现行 `2-Areas / 3-Resources` 对齐，减少旧术语残留对预查命中的干扰。

### moc-navigation-refactor

- `MOC-Pipify / App / Procedure / Config / Network / SDK` 重写为导航型图谱。
- 删除旧版说明书化正文、工具化叙述与过时接口描述，保留模块边界、入口与下钻关系。

### moc-runtime-systems-refactor

- `MOC-Persist / Prefab / Debug / Vibrate / ObjectPool / Sound / Localization` 继续重写为导航型图谱。
- 补齐与当前代码一致的公开入口、启动期旁路、停止/释放边界与运行时例外口径。

### moc-density-pass

- 对 `Config / Procedure / App / Manager / Pipify / ObjectPool / Localization` 再做一轮高密度压缩。
- 保留架构边界与真实公开入口，继续删除重复解释、旧心智和半份手册式正文。

### moc-final-alignment-pass

- 对 `UI / Table / HybridCLR / Inspector` 做代码对照式校正。
- 修正旧 API 名称、旧路径、旧启动流程、错误依赖口径与过时导出入口，统一收敛为“导航型图谱”。

### workflow-active-layer-pass

- 对 `workflow / publish / sample` 活跃条目做去个人化、去工具化与去脚本步骤化收敛。
- 保留长期规则，删除个人标记、旧协作栈指针、特定命令步骤和过重 SOP 细节。

## 2026-06-08

### redundant-content-cleanup

- 将 `PAT-63 / PAT-81 / ADR-038 / ADR-044` 从活跃区迁入 `4-Archives`，保留取代关系与历史背景。
- 将 Unity 学习资料迁入 `3-Resources/Unity`，将旧协作 Canvas 迁入 `4-Archives/Collaboration/Canvas`。

## [2026-06-11 21:44] index-rebuild | 0-Index/0-Index-{ADR,PAT,GLO,RES}.md 重建

扫 `ADR=49 / PAT=97 / GLO=6 / RES=3`；`PAT-139` 已进入 Layer 1 索引。

### active-layer-governance-pass

- 将 `ADR-019`、`PAT-100`、`MOC-Workflow`、`ExternalLinks` 移出活跃层；其中 `keyword-rebuild-suggestions-2026-05-24.md` 作为一次性自动建议表直接删除。
- `0-Index-MOC.md` 补入 `MOC-App`，同时移除已归档的 `MOC-Workflow` 入口。

### frontmatter-normalization

- 扩充 Layer-1 合法 `category` 枚举以覆盖 `config / governance / upm / methodology / demo`。
- 批量修正超长 `summary`、缺失 `type`、遗留 `PAT-DRAFT` alias 和错误 wikilink，降低索引噪音。
- 生成逐项审计结果 `4-Archives/Records/2026-06-08-minds-audit.md`，固化当前 258 篇 Markdown 的保留/优化判定。

### index-rebuild

- 重建 Layer 1 索引与 `_keyword-index.json`；扫 176 篇笔记，`prelookup_keywords` 459 条，`moc_index` 21 条，待补 keywords 0 条。

### legacy-draft-normalization

- `4-Archives/Inbox-Legacy` 下 3 篇 PAT 草稿统一改为归档态，删除 `session_id`、hook 尾注与来源对话摘录。
- 保留可复用规则，避免归档层继续混入会话残留。

### resource-pass-finalization

- `RES-01` 改写为方法摘要页，删除旧协作栈逐条映射。
- `Unity_Addressables_教程` 与 `Unity_YooAsset_教程` 补入使用说明与速览，清理正文顶部的会话化表述。

### final-verification

- 重新执行 `rebuild_index.py --check-bloat`，Layer-1 frontmatter 与索引体量均通过。
- 重新执行 `rebuild_keyword_index.py --write --quiet`，当前 `_keyword-index.json` 为 `450` 条关键词。
- 执行 `.agents/skills/nova-obs/tests` 共 `19` 项单测，全部通过。

### zero-link-scan

- 扫描 `Minds` 全部 Markdown 的双链关系后，零关联文件由 `15` 收口到 `4`。
- 新增 `4-Archives/Audits/INDEX.md`，并给 `0-Index.md` / `0-Log.md` 补历史入口，减少纯结构性孤点。
- 剩余 `4` 篇均为归档层孤岛：`PAT-100` 与 `Inbox-Legacy` 三篇历史草稿。

### zero-link-prune

- 删除零关联且已无现实检索价值的 `4-Archives/Collaboration/Patterns/PAT-100-base-filter-prefix-whitelist.md`。
- 当前零关联文件收口为 `3` 篇，均为 `Inbox-Legacy` 历史草稿，继续保留留底。

### glossary-moc-tightening

- 收紧 `GLO-04 / GLO-06 / MOC-App / MOC-Debug`，删除阶段性实现口吻，回到长期术语定义与导航边界。
- 活跃层继续保持“知识入口页”职责，不在 MOC / Glossary 中混入当前完成度和临时状态描述。

### moc-runtime-tightening

- 收紧 `MOC-Event / MOC-Table / MOC-Sound / MOC-Vibrate`，将半手册化表述收回为“入口 + 边界 + 去哪里继续看”。
- 统一把旧 `ADR-019` 的资源释放口径替换为当前 `ADR-042` 的 handle 语义入口。

### ui-pattern-tightening

- 收紧 `PAT-102 / PAT-103 / PAT-105 / PAT-117`，删除现场复盘口吻，改回通用 UI 规则表述。
- 同时将分类从 `inspector` 调整到更贴近实际的 `demo / runtime`，减少索引噪音。

### workflow-pattern-tightening

- 将 `PAT-119` 改名为去个人化的 `PAT-119-upm-private-fork-local-diff-marking`，主 ID 回到标准 `PAT-119`，旧别名继续兼容。
- 收紧 `PAT-120`，移除对归档协作规则的依赖，回到样例脚手架命名规则本体。

### review-pattern-tightening

- 收紧 `PAT-128 / PAT-129 / PAT-134`，补齐重置策略、判定输出和验证顺序，减少“只记事故不记方法”的风险。
- `PAT-125` 暂保留在 `optimize`，后续再按参考稿方式继续压缩。

### resource-doc-tightening

- 收紧 `MOC-Localization / MOC-ObjectPool / MOC-Persist / MOC-Asset / MOC-Prefab`，统一切到当前 `ADR-042` 句柄语义。
- 收紧 `PAT-28 / PAT-109 / PAT-111`，删除重复说明和未落地的演进注释。

### sop-tightening

- 收紧 `PAT-31 / PAT-32 / PAT-33`，去掉文档落点与来源说明里的重复语句，保留 SOP 本体。

### pattern-compact-pass-2

- 继续收紧 `PAT-30 / PAT-58 / PAT-59 / PAT-69 / PAT-70 / PAT-77 / PAT-79 / PAT-84 / PAT-87 / PAT-88`，删除重复适用场景、长篇复盘与迁移背景，保留可执行规则与反模式。
- 这一轮以 `redline`、`demo`、`review` 三类高噪音条目为主，进一步压缩长期知识层的说明性段落。

### adr-tightening-pass-1

- 收紧 `ADR-014 / ADR-015`，删除重复背景和过长后果说明，保留字段拆分、启动序列和接管契约。

### pattern-compact-pass-3

- 继续收紧 `PAT-109 / PAT-111`，把 UPM 文档目录与命名规则压成规则本体，删掉重复背景铺陈。

### adr-obsolete-background-pass-1

- 收紧 `ADR-020 / ADR-021 / ADR-022 / ADR-023 / ADR-024 / ADR-025`，删去大段背景、替代方案与验证描述，保留契约本体。
- 收紧 `PAT-16 / PAT-17 / PAT-23 / PAT-125`，把旧协作通道、归档守卫和增量下载模式压成规则本体。

### adr-workflow-tightening-pass-1

- 收紧 `ADR-026 / ADR-027 / ADR-028`，删除批锁复盘与 AOT 实验日志，保留运行时批处理、规则层禁用和 BuildPlayer 顺序契约。

## [2026-06-16 12:11] ingest | ADR-064 PlugPals 依赖检测与可选库三原则
起草并入库 ADR-064（editor）。确立可选库三原则：dependencies 权威不遗漏 / requiredLibraries 仅展示 / 宏交 asmdef versionDefines；PlugPals 改 registry 内存命中判缺库、命中自动配 scope 安装、未命中购买引导并中止、废弃宏注入机制。重建 0-Index-ADR + _keyword-index.json（461 keywords）。无 supersedes、无 MOC 精确匹配。

## [2026-06-17 18:40] direct-ingest | ADR-066 EDM 公共依赖与 OpenUPM 工程固定策略
直接入库 1 份 ADR-066（workflow）：EDM 经 OpenUPM 工程固定供给、scoped-registry 链须完整覆盖传递依赖、AppLovin 正确 registry（unity.packages.applovin.com/）、PlugPals 安装写顶层。跳过 Inbox 中转；rebuild-index + keyword（463 keywords）已收口。
