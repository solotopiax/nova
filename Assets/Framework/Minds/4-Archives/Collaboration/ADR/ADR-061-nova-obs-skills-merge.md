---
id: ADR-061
title: nova-obs-* 18 个子 skill 合并为 nova-obs 单一路由入口
summary: 18 obs skill 合并 nova-obs 路由入口
category: ai-collab
status: accepted
date: 2026-06-03
aliases:
  - ADR-061-nova-obs-skills-merge
tags: [adr, draft, ai-collab, skill, refactor]
supersedes: []
superseded-by: []
related: []
---

# ADR-061：nova-obs-* 18 个子 skill 合并为 nova-obs 单一路由入口

## 背景（Context）

会话开始时观察到 SessionStart 注入的 context 占比偏高，逐项排查发现 skill 列表里 18 个独立的 `nova-obs-*` 子 skill（adr-to-inbox / cur-to-all / health 等）每个都注入 ~80 字符压缩后的 description，仅 description 部分就吃掉约 1500-2000 字符（≈ 600-800 token），且名字前缀冗余（每条都重复 `nova-obs-`）。

同时这 18 个 skill 在功能上是同一族（都围绕 Obsidian Vault `Assets/Framework/Minds/` 操作），按生命周期（起草 / 入库 / 归档 / 查询 / 维护）分布，外部引用（hook / 文档 / wikilink）已存在大量 `/nova-obs-xxx` 形式命令名，维护分散。

## 决策（Decision）

**合并为单一 skill `nova-obs`，通过手动子命令派发**：

```
/nova-obs <subcommand>             # 例：/nova-obs cur-to-all
/nova-obs <subcommand> [args]      # 例：/nova-obs query <关键词>
```

目录结构：
```
.claude/skills/nova-obs/
├── SKILL.md                  # 路由表 + 18 子命令简介（短，~70 行）
├── subcommands/<cmd>.md      # 18 个子文档（原 SKILL.md 去 frontmatter）
├── scripts/                  # rebuild_index.py / lint_extras.py /
│                             # rebuild_keyword_index.py / obs_preflight.py
└── tests/                    # rebuild_index 18 case 单测
```

**执行协议**：SKILL.md 解析 args 第一个 token → Read `subcommands/<arg>.md` → 按子文档步骤执行。剩余 args 作为参数传入。

**`disable-model-invocation: true`**：不让 AI 自主推断意图触发，必须手动指定子命令。

## 后果（Consequences）

### 正面
- skill 列表精简：18 项 → 1 项 description，省 ~2000 字符 / ~700 token
- 单一目录维护：路径统一在 `nova-obs/`，子命令查找/对照都在 `subcommands/<cmd>.md` 一处
- 子命令可演化：路由 SKILL.md 不变，新增 / 重命名 / 删除子命令只动 `subcommands/`
- 测试集中：原 `nova-obs-health/tests/` 18 个 case 现统一到 `nova-obs/tests/`
- Python 脚本统一：4 个脚本（`rebuild_index` / `lint_extras` / `rebuild_keyword_index` / `obs_preflight`）全集中 `nova-obs/scripts/`

### 负面
- 子文档多了 1 层路径深度：`nova-obs/scripts/<file>.py` 比原 `nova-obs-<x>/<file>.py` 深一级，脚本里 `Path(__file__).parents[N]` 计数要 +1（已修：`rebuild_index.py` / `lint_extras.py` 从 parents[3] 改 parents[4]）
- 老命令名 `/nova-obs-xxx` 形式遗留：hook 注入 / Vault 80+ 文档 / wikilink 中存在大量引用，需一次性同步替换（已完成 350+ 处，剩余仅 hook 文件名 `.sh` 路径合理保留）
- SKILL.md 路由解析新增一步：调用时多 1 次 Read 子文档（与原直接调用 skill 等价）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|------|----------|
| 方案 A：所有子命令全塞 SKILL.md | 单文件 3500+ 行，每次调用都加载全部子命令上下文，重 |
| 方案 C：保留老 skill，新建 nova-obs 路由层调度 | 没真正合并，skill 列表仍 19 项；用户希望"合并"语义 |
| 保留 nova-obs-* / 老命令兼容别名 | redirect stub 仍占 skill 列表，反而膨胀 |

## 验证依据（Verification）

- `python3 -m unittest discover -s .claude/skills/nova-obs/tests/` → 18/18 PASS
- `python3 .claude/skills/nova-obs/scripts/rebuild_keyword_index.py --quiet` → `[OK] index written: ... (723 keywords)`
- `python3 .claude/skills/nova-obs/scripts/lint_extras.py` → 矛盾 + 数据空缺扫描全部输出
- hook 干跑：`echo '{"prompt":"obs ...","session_id":"test"}' | bash .claude/hooks/nova-obs-keyword-guard.sh` → 注入新格式 `/nova-obs patterns-to-inbox` 等
- 残留扫描：`grep -rn "nova-obs-" Assets/Framework/Minds/ .claude/` 仅剩 hook 文件名（`.sh` / `.log` 路径）合理保留

## 来源（Origin）

- 会话日期：2026-06-03
- 关键对话节选：
  > 用户：现在看，context占比还是很高啊
  > 用户：把当前所有的 nova-obs-**** 技能合并成一个 skill，名字叫：nova-obs，将来我需要调用的时候手动传参数，比如 /nova-obs cur-to-all /nova-obs inbox-to-all 等等

## 实施清单（Implementation）

- [x] 建 `nova-obs/` 骨架（SKILL.md 路由表 + subcommands/ + scripts/ + tests/）
- [x] 提取 18 个原 SKILL.md 正文（去 frontmatter）到 `subcommands/<cmd>.md`
- [x] 子文档内 `/nova-obs-<cmd>` → `/nova-obs <cmd>` 命令引用同步（含花括号缩写 `{adr,patterns}-to-inbox` 形式）
- [x] 附属文件迁移：health（rebuild_index.py / lint_extras.py / tests）+ keyword-rebuild（rebuild_keyword_index.py）+ inbox-to-areas（obs_preflight.py）→ 统一 `scripts/` / `tests/`
- [x] Python 脚本路径深度修复（parents[3] → parents[4]）
- [x] 6 个 hook 同步：`nova-obs-keyword-guard.sh` 注入提示、`nova-obs-capture.sh` next_skill、`nova-prelookup-audit.sh` / `nova-rules-pretooluse.sh` 文档说明
- [x] CLAUDE.md / redlines.md / 0-Index.md / Vault 80+ 文档 / Canvas 引用同步
- [x] git rm 18 个老 `nova-obs-*` 目录 + nova-obs-README.md（与新 SKILL.md 路由表重合）
- [x] 重跑 keyword-rebuild 刷新 `_keyword-index.json`（标识符更新）
- [x] 18 单测全 PASS / 三脚本可跑 / hook 干跑验证

## 关联

- 相关 ADR：（无直接相关）
- 相关 Pattern：[[PAT-92-vault-log-append-only|PAT-92]]（0-Log.md 追加约束）、[[PAT-86-direct-promote-bypass-inbox|PAT-86]]（直接入库铁律——本草稿走默认 Inbox 通道，不直接入库）
- 老 skill 路径迁移映射：18 项 `.claude/skills/nova-obs-<cmd>/SKILL.md` → `.claude/skills/nova-obs/subcommands/<cmd>.md`
- hook 文件名保留：`nova-obs-keyword-guard.sh` / `nova-obs-capture.sh`（与 skill 命令名空间无关）
