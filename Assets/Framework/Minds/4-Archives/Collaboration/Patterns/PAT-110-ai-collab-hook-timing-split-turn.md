---
id: PAT-110
title: AI 协作 hook 时序问题的拆 turn 绕过方案
status: active
summary: 关键节文本 turn 先发，工具 turn 后跟
category: ai-collab
keywords:
  - hook-timing
  - pretooluse
  - transcript-flush
  - prelookup-audit
  - split-turn
tags:
  - nova
  - hooks
  - ai-collab
  - workflow
date: 2026-05-26
source: session-end
aliases:
  - PAT-110-ai-collab-hook-timing-split-turn
related:
  - "[[PAT-99-hook-perf-self-diagnose|PAT-99]]"
---

# PAT-110：AI 协作 hook 时序问题的拆 turn 绕过方案

## 适用场景

PreToolUse hook 通过读 transcript 校验"本轮是否输出了某关键节"（典型如 `nova-prelookup-audit.sh` 检查【知识库前置匹配】节），同 turn 内既输出关键节又调用工具时必然撞墙：

- transcript 落盘是按 turn flush 的——本 turn 工具调用先于 transcript 写入
- hook 读 transcript 看不到本 turn 的文本输出
- 工具调用必被拦截，agent 进入死循环或要求关闭 hook

## 核心做法（拆 turn 模式）

**铁律：关键节文本与工具调用永不同 turn。**

### 标准时序

| Turn | 内容 | 限制 |
|---|---|---|
| Turn N | 纯文本：含【知识库前置匹配】节 + 任务确认 | 零工具调用 |
| Turn N+1 | 工具调用密集：Read / Edit / Write / Bash 任意组合 | 无须重复关键节 |

### 落地脚本（agent prompt 模板）

```
本轮 turn 模式：TEXT ONLY
1. 输出【知识库前置匹配】节（5-10 条匹配/冲突说明）
2. 输出本轮任务确认与计划纲要
3. 不调任何工具，turn 结束

下一轮 turn 模式：TOOL CALLS
1. 直接连续工具调用，按计划落地
2. 不再重复前置匹配节
```

## 反模式

- ❌ 同 turn 内输出【知识库前置匹配】节 + 调 Edit/Write —— hook 必撞
- ❌ 撞 hook 后建议用户「临时关闭 hook」 —— 屏蔽问题不是解决问题
- ❌ 第二 turn 重复输出关键节 —— transcript 已包含，无效冗余
- ❌ 把关键节藏进 tool call 参数（如 Bash 注释）—— hook 读 transcript 不读工具参数

## 适用范围

跨 AI 协作平台通用——只要 hook 工作机制是「读 transcript 验当前 turn 内容」，本模式就生效。Nova 项目内具体生效的 hook：

- `nova-prelookup-audit.sh`：前置匹配节强制
- `nova-rules-grep-guard.sh`：规则关键字命中后注入
- 其他 PreToolUse hook 具备 transcript 读取行为时

## 来源对话摘录

> 2026-05-26 会话中 runtime-coder 反复撞 PreToolUse hook，输出"这一轮 turn 只输出文字，不做 tool call"分析报告。用户回复"前置声明已写入 transcript，hook 关卡过了"——但实际本 turn 文本与工具调用同发，transcript 还没刷新，hook 拿不到关键节。最终下达拆 turn 流程后才解开死锁。

## 关联

- [[PAT-99-hook-perf-self-diagnose|PAT-99]]：hook 性能自查；本模式是 hook 时序问题的对症方案
- 相关 memory：`feedback_rules_hook_machine_enforcement`（Pre/PostToolUse hook 设计原则）
- 工具：`.claude/hooks/nova-prelookup-audit.sh` / `.claude/hooks/nova-rules-grep-guard.sh`
