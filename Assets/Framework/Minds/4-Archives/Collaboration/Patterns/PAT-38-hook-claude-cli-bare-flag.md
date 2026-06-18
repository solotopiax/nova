---
id: PAT-38
title: Hook 内调用 claude CLI 必带 --bare 防自递归
type: pattern
status: active
date: 2026-05-19
summary: Hook内调claude CLI必带--bare防自递归
category: workflow
aliases:
  - PAT-38-hook-claude-cli-bare-flag
keywords:
  - Hook 内调用 claude CLI 必带 --bare 防自递归
  - PAT-38
  - PAT-38-hook-claude-cli-bare-flag
tags:
  - pattern
  - hook
  - claude-code
  - anti-recursion
  - cli
related:
  - "[[PAT-16-obs-keyword-trigger|PAT-16]]"
  - "[[PAT-23-obs-archive-signal-keyword-guard|PAT-23]]"
---

# PAT-38：Hook 内调用 claude CLI 必带 --bare 防自递归

## 适用场景（When）

- 在 Claude Code 的 hook 脚本（SessionStart / SessionEnd / UserPromptSubmit / PostToolUse 等）内 fork 子进程调 `claude -p` 做二次推理
- 触发信号：hook 脚本里出现 `claude -p '...'`、`claude --print`、或任何启动新 Claude 会话的 CLI 调用
- 典型用例：SessionEnd hook 里让子 Claude 评判会话是否值得沉淀、UserPromptSubmit hook 里让子 Claude 改写用户输入

## 核心做法（What & How）

**铁律：hook 内任何 `claude` CLI 调用必须加 `--bare` 标志**。

```bash
# ❌ 错：会触发 SessionEnd hook 自递归
ai_json=$(echo "$prompt" | claude -p --output-format text)

# ✅ 对：--bare 让子进程跳过所有 hooks 配置
ai_json=$(echo "$prompt" | claude --bare -p --output-format text)
```

`--bare` 含义：让本次 CLI 子进程**跳过用户 settings.json 里的全部 hooks 配置**，不挂 SessionStart/SessionEnd/UserPromptSubmit/PostToolUse 等会话级周边，只跑一次裸推理。

| 场景 | 是否带 --bare |
|---|---|
| 主进程交互式 `claude` | 不带（要全套 hooks） |
| 主进程脚本里调 `claude -p` 一次性推理 | 看是否需要 hook 加持，通常不带 |
| **hook 脚本内**调 `claude -p` | **必须带** |
| cron / launchd 周期任务调 `claude -p` | 看是否会被 hook 反向触发，谨慎评估 |

> 对照参考：`.claude/hooks/nova-obs-capture.sh:217-219` 的修复实现。

## 为什么这么做（Why）

- **断递归**：hook 是会话生命周期事件回调，hook 内启动新会话 = 新生命周期事件 = 重新触发同一 hook → 无限自调用
- **真实事故**：5/15 SessionEnd hook 上线初版未加 --bare，`/clear` 一次后机器卡死，进程树爆炸到上百个 claude 子进程
  - 完整链路：用户 `/clear` → 主 Claude 触发 SessionEnd → 跑 `capture.sh` → `capture.sh` 调 `claude -p '...'` 起子 Claude → 子 Claude 跑完 `-p` 也要结束 → **再次触发 SessionEnd → 又跑 capture.sh** → 无限递归，进程爆炸 / 机器卡死
- **轻量**：hook 内推理只要"问一次答一次"，不需要 SessionEnd 之类的尾部副作用
- **隔离**：`--bare` 让子进程的失败/超时不会污染主会话的 hook 状态

## 反模式（Anti-patterns）

- **SessionEnd hook 同步调 `claude -p` 不带 --bare**：用户 `/clear` → 跑 SessionEnd hook → fork claude 子进程 → 子进程结束又触发 SessionEnd → 又 fork → 系统资源耗尽，Ctrl+C 都救不回来（5/15 现场事故）
- **UserPromptSubmit hook 内 `claude -p` 改写 prompt 不带 --bare**：子进程启动时本身会触发 UserPromptSubmit（如果有"启动时注入提示"的 hook）→ 同环递归
- **以为"就调一次不会递归"**：递归的触发不取决于调用次数，取决于子进程**生命周期事件**是否被同一 hook 监听。只要监听了，调一次就炸
- **跟 `git clone --bare` 混淆**：误以为是 "不带工作目录" 含义而忽略加上；二者同名不同义，Claude CLI 的 `--bare` 专指 "裸调用、不挂 hooks"
- **只在部分调用点加 bare**：脚本内有多处 `claude` 调用时漏掉一处，依然会触发递归；必须全量统一
- **依赖 fork 后台化 / `&` / `nohup` / `timeout` 兜底当解决方案**：能限制单进程时长或后台化，但拦不住"100 个并发子进程"，治标不治本，根因还是必须 `--bare`

## 跨项目复用提示

- **Claude Code 专属**：`--bare` 是 Claude Code CLI 标志，跨工具搬不动
- **抽象成通用范式**：任何"事件回调里启动同种事件源"的场景都要切递归机制
  - shell hook 里调起会触发同 hook 的命令 → 用环境变量哨兵（如 capture.sh 第 26 行 `NOVA_OBS_BG=1` 模式）
  - git hook 里调 git 命令 → 用 `--no-verify` 或 `GIT_HOOKS_DISABLED=1`
  - cron 里调可能写 crontab 的脚本 → 显式 `crontab -l` 而非 `crontab -e`
  - 其他 AI CLI（codex / aider）：查其等价"跳过 hooks"标志；没有则在脚本侧用 env 变量自旗（如 `export NOVA_HOOK_DEPTH=1` 进入即跳过）实现等价隔离
- **判断信号**：写 hook 时问自己"这个子进程结束/启动会不会再次进入本 hook"，会则必须切环

## 关联

- 现场实现：`.claude/hooks/nova-obs-capture.sh:217-219`（`claude --bare -p`）+ `:26-33`（`NOVA_OBS_BG=1` 哨兵防 fork 递归，互补另一条递归通路）
- 相关 Pattern：
  - [[PAT-16-obs-keyword-trigger|PAT-16]]（同样涉及 UserPromptSubmit hook，是本 Pattern 的反向案例——keyword-guard 不调 claude CLI，所以无需 --bare）
  - [[PAT-23-obs-archive-signal-keyword-guard|PAT-23]]（同属 obs hook 体系的防误触机制）
- 历史背景：本 Pattern 是 2026-05-15 旧 `Docs/Minds/1-Inbox/` 路径下三份 hook 经验草稿（`hook-llm-recursion-guard` / `hook-claude-p-bare-flag` / `hook-self-invoke-bare-flag`）随旧目录删除丢失后的合并补写，原经验自 5/15 起一直只活在 capture.sh 注释里
