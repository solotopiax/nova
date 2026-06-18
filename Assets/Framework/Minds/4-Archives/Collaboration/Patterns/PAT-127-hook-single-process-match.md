---
id: PAT-127
title: hook 热路径禁逐项 fork-grep 改单进程匹配
status: active
summary: 热路径 N 词逐个 grep 改单 python 进程
category: ai-collab
keywords:
  - hook-perf
  - fork-grep
  - single-process-match
  - keyword-guard
  - cost-decouple
tags:
  - nova
  - hooks
  - performance
  - obs
date: 2026-05-30
source: session-end
aliases:
  - PAT-127
  - hook 单进程匹配
related:
  - "[[PAT-99-hook-perf-self-diagnose|PAT-99]]"
supersedes: []
---

# hook 热路径禁逐项 fork-grep 改单进程匹配

## 适用场景

任何 hook / shell 脚本的**热路径**——即每条用户消息（UserPromptSubmit）、每次工具调用（Pre/PostToolUse）都会执行的代码段。这类脚本的耗时直接叠加到 AI 每一轮交互的延迟上。

## 反模式

对 N 个关键字/规则**逐项** fork 子进程做匹配：

```bash
for kw in "${KEYWORDS[@]}"; do
  if printf '%s' "$PROMPT" | grep -qF -- "$kw"; then  # 每词 2 次 fork
    HIT="${HIT}${kw}, "
  fi
done
```

N 个关键字 = **2N 次 fork**（printf + grep）。macOS 上每次 fork 约 5-8ms，137 个关键字实测 **1720ms / 每条消息**，且随关键字增加**线性恶化**（300 词 → 3-4s）。

## 唯一写法

把数据 join 进环境变量，交给**单个 python 进程**遍历匹配：

```bash
_MATCH="$(
  NOVA_PROMPT="$PROMPT" \
  NOVA_KW="$(printf '%s\n' "${KEYWORDS[@]}")" \
  python3 -c '
import os
prompt = os.environ.get("NOVA_PROMPT", "")
kws = [l for l in os.environ.get("NOVA_KW","").split("\n") if l.strip()]
print(", ".join(k for k in kws if k in prompt))  # k in prompt == grep -qF
' 2>/dev/null || printf ''
)"
```

多组结果用 `\x1e`(RS) 分隔输出，bash 端用参数展开 `${V%%$'\x1e'*}` 切回。

## 量化收益

| 场景 | 改前 | 改后 | 降幅 |
|------|------|------|------|
| 命中 | 1720ms | 190ms | −89% |
| 未命中 | 1720ms | 170ms | −90% |
| 加到 300 词 | ~3700ms | ~190ms | 解耦 |

关键认知：**O(N) fork → O(1) fork，耗时与数据量解耦**。越往后加关键字旧方案越慢，新方案纹丝不动。

## 维护性（重要）

匹配逻辑**数据驱动**（python 读数组/JSON，与关键字内容无关）。新增关键字方式**完全不变**——往 bash 数组加一行或刷新 JSON，python 段一字不动，零维护成本。

## 自检三步

1. `grep -nE "for .* in .*KEYWORD|printf.*grep" <hook>` 找热路径逐项 fork；
2. 改前用 `/usr/bin/time -p bash -c "... <hook>"` 量基线；
3. 改后 diff 注入文本**逐字一致**（行为零变更），再贴优化前后耗时。

## 来源

源自 `nova-obs-keyword-guard.sh` 优化（2026-05-30）：137 关键字逐项 fork-grep 1720ms → 单 python 进程 190ms。诊断方法承接 `[[PAT-99-hook-perf-self-diagnose|PAT-99]]`（先量化再优化）；memory `feedback_hook_no_per_item_fork`。

> 注：PAT-99 的瘦身针对**注入文本字节数**（reminder 太长），本 PAT 针对**匹配循环 fork 数**——两者是 keyword-guard 性能的两个独立维度，叠加生效。
