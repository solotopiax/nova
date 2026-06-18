---
id: PAT-99
title: API socket closed 时主动排查 hook 链路
status: active
summary: socket closed 先量 hook 与 tail
category: ai-collab
keywords:
  - hook-perf
  - socket-closed
  - prelookup-tail
  - keyword-guard-injection
  - stats-report
tags:
  - nova
  - hooks
  - obs
  - performance
date: 2026-05-25
source: session-end
aliases:
  - PAT-99-hook-perf-self-diagnose
related:
  - "[[PAT-92-vault-log-append-only|PAT-92]]"
---

# PAT-99：API socket closed 时主动排查 hook 链路

## 适用场景

Claude Code 会话出现以下症状时直接走本流程，**禁止**先认定是网络问题：

- `API Error: The socket connection was closed unexpectedly`
- 单次 Edit/Write 工具调用前后明显卡顿（>1s）
- 长会话（transcript >10MB）后越往后越频繁出现

## 核心做法（三步自检，<2 分钟）

不等用户问，遇到上述症状立即按序量化：

### 第 1 步：跑 stats 复盘

```bash
python3 .claude/hooks/stats_report.py --tail 1
```

看哪个 hook avg ms 异常（正常 <100ms；>200ms 即怀疑）。

### 第 2 步：量 prelookup transcript tail 体积

```bash
TRANSCRIPT="${HOME}/.claude/projects/$(pwd | sed 's|/|-|g')/<session>.jsonl"
wc -c <(tail -50 "$TRANSCRIPT")
```

>500 KB 即异常——`nova-prelookup-audit.sh` 同步跑 python3 解析，stdin 越大越慢。**长会话主因**。

### 第 3 步：量 keyword-guard 注入字节

```bash
INPUT='{"prompt": "<本轮用户原话>", "session_id": "smoke"}'
echo "$INPUT" | bash .claude/hooks/nova-obs-keyword-guard.sh 2>/dev/null | wc -c
```

>5 KB 即异常——多路命中（bizlang+prelookup+rule+archive+log）会叠加挤压 context。

## 处置策略

| 异常项 | 缓解动作 |
|---|---|
| prelookup tail 过大 | `tail -200` → `tail -50` 或 `tail -30`；倒序遍历早退 |
| keyword-guard 注入过大 | 各 heredoc 段瘦身：保留触发信号 + 关键路径，规则全文移到 `0-Index.md` 用指针指 |
| stats 显示某 hook avg >300ms | 检查是否串了同步外部命令（claude CLI / git / curl）；改成异步 / 缓存 / 删冗余 |
| 某 hook 命中率 0% 但触发数高 | 触发条件过宽，缩白名单 |

## 反模式

- ❌ 第一时间归因网络问题，要求用户重试
- ❌ 不量化直接拍脑袋改 hook
- ❌ 改完不 smoke test 就声明已修
- ❌ 只压 keyword-guard 不查 prelookup（实测 prelookup 才是长会话主因）

## 来源对话摘录

> 2026-05-25 会话中第三次出现 `API Error: socket closed unexpectedly`，用户问"是我目前优化 obs 相关工作流程导致的吗？"主动排查后定位 prelookup-audit 的 `tail -200` 在 18MB transcript 下读取 600KB stdin 是主因，keyword-guard 全命中 7KB 注入是次因。改 prelookup → tail -50 + 倒序早退，keyword-guard 各段瘦身 72%。用户随即要求"下次自动排查不要等我问"。

## 关联

- [[PAT-92-vault-log-append-only|PAT-92]]：本类排查动作完成后追加 0-Log
- 相关 memory：`feedback_rules_hook_machine_enforcement`（Pre/PostToolUse hook 双层兜底设计原则）
- 工具：`.claude/hooks/_stats.sh` + `.claude/hooks/stats_report.py`
