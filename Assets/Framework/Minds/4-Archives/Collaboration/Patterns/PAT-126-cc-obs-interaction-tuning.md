---
id: PAT-126
title: CC×obs 交互体系评估与调优范式
status: active
summary: 约束放对层而非加防御工事
category: ai-collab
keywords:
  - hook-signal-noise
  - constraint-placement
  - preflight-validation
  - keyword-source-quality
  - data-driven-rebuild
tags:
  - nova
  - hooks
  - obs
  - workflow
date: 2026-05-30
source: session-end
aliases:
  - PAT-126
  - CC obs 交互调优
related:
  - "[[PAT-99-hook-perf-self-diagnose|PAT-99]]"
supersedes: []
---

# CC×obs 交互体系评估与调优范式

## 适用场景

定期评审 Claude Code 侧强制约束体系（hook + skill + 规则）与 obs 知识库的交互效能，
识别"约束放错层"的结构性病灶并修复。不是加更多防御，而是把约束放对位置。

## 核心判断框架（评估三问）

评估一个 hook/skill 约束是否合理，问三件事，而非看命中率：

1. **该智能的地方是否在用蛮力？** —— 如 keyword-guard 对每条提交全文无差别 grep，
   连系统通知 `<task-notification>`、粘贴的大段日志都触发注入洪水，淹没真信号。
   命中率高（44.7%）≠ 健康，大半是误触。
2. **该硬拦的地方是否只在提醒？** —— 如直写 `2-Areas/` 仅 systemMessage 软提醒、
   API 禁用 20+ 条只 grep 了 5 条。红线机器覆盖率仅 60-70%，其余靠 AI 自觉。
3. **该前置的地方是否在事后补？** —— 如入库 29% 是返工（编号冲突/summary 超长/
   superseded-by 回填延迟），质量卡点设在 lint 而非起草/入库前置校验。

> 守卫型工具命中率低恰恰是它在生效（AI 多守规矩所以不触发），不能拿命中率当裁撤理由。

## 四个落地杠杆（本轮已验证）

| 病灶 | 杠杆 | 验证收益 |
|------|------|----------|
| hook 信噪比（系统通知/粘贴触发洪水） | 加来源过滤闸（`<task-notification>`/`<system-reminder>` 开头、>2000 字静默）+ 命中上限（prelookup/bizlang >6 裁剪） | 系统通知从满屏注入→静默 |
| 红线只提醒不硬拦 | 直写 protected 目录 systemMessage → **exit 2 硬拦**，配 PAT-86 智能豁免（读本 session user 消息含"直接入库"则放行） | 红线覆盖 60%→90%，例外不误伤 |
| 入库事后返工 | 共享 **preflight 脚本**：入库前一次性校验 summary≤30 字 + 全局唯一编号（同时扫 2-Areas 已入库号 + Inbox 其他草稿号） | 当场抓出 4 处 summary 超长 + 3 份草稿自动分配不撞号 |
| 关键字数据源噪音 | 生成脚本数据驱动过滤：`id` 取编号前缀（ADR-011 而非 full-slug）、`title`/`aliases` 加 token 长度+整句过滤 | 整句噪音 214→0，独立模块词（EventManager）正确进表 |
| 数据源刷新靠人记得（孤儿脚本） | keyword-rebuild 挂到全部 6 个改条目集的入口（inbox-to-areas / inbox-to-all / cur-to-all / areas-to-archives / resources-to-archives / all-to-archives）的 `--rebuild-index` 收尾旁；加 `--quiet` 抑制 suggestions 污染 Inbox | 条目集一变关键字索引同步刷新，用户零手动；新条目不再漏命中前置查询 |

## 关键认知

- **exit 2 不误伤 skill 通道**：入库/归档 skill 写 2-Areas/4-Archives 用 `mv`/`git mv`（Bash），
  不经 `matcher: Edit|Write|MultiEdit` 的 hook；故硬拦 Edit/Write 与 skill 的 mv 天然分流。
- **智能豁免优于一刀切**：硬拦 + 读 transcript 检测豁免词，既守红线又不误伤用户授权的合法路径。
- **数据源质量是上游**：keyword-guard 再快，喂它整句噪音也白搭。先修生成脚本抽取逻辑，再清索引。
- **孤儿脚本必须挂触发点**：一个"需在某事件后刷新"的脚本若无任何流程自动调它，等于没有——
  全靠人自觉的环节实际无人执行。把它挂到既有收尾步骤（如 `--rebuild-index` 旁），并用静默标志
  消除副作用，才算真正闭环。识别孤儿：`grep -rn <脚本名> hooks/ skills/ settings.json` 看有无调用方。
- **改流程先查协作约定**：给被调度的子 skill 加收尾步骤前，先确认调度器是否有"子 skill 跳过、
  调度器统一收口"约定（避免重复执行）；把新步骤放进同一命令块即随既有跳过逻辑一起生效。
- **验证后才沉淀**：评估报告本身不预先起草 obs；等改进落地验证有效，再蒸馏方法论，避免造未验证草稿。

## 来源

源自 2026-05-30 CC×obs 交互机制全面评审会话：先并行两个 Explore agent 取证
（hook 强制体系 / obs skill 体系），交叉验证后定位三病灶，落地五杠杆并逐项 benchmark 验证。
其中 keyword-rebuild 自动化由"它需要定期手动跑吗"一问引出——查证发现是孤儿脚本，遂挂全部入口。
配套：`feedback_hook_no_per_item_fork`（fork 优化）、`[[PAT-99-hook-perf-self-diagnose|PAT-99]]`（先量化再优化）。
