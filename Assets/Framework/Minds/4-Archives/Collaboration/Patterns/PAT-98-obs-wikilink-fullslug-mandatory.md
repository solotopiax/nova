---
id: PAT-98
status: active
date: 2026-05-25
title: obs wikilink 必带 fullslug，禁纯短链
summary: obs 双链必带 fullslug，禁纯短链
category: obs
aliases:
  - "PAT-98-obs-wikilink-fullslug-mandatory"
keywords: [wikilink, dangling, fullslug, obsidian, short-link, 短链, 双链, ghost-stub, 幽灵节点]
tags: [obs, ai-collab, lint]
related:
  - "[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]"
  - "[[PAT-85-vault-search-synonym-expansion|PAT-85]]"
  - "[[PAT-92-vault-log-append-only|PAT-92]]"
---

# PAT-98：obs wikilink 必带 fullslug，禁纯短链

## 反模式（一票否决）

- ❌ 写 `[[ADR-002]]` / `[[PAT-09]]` / `[[GLO-02]]`（纯短链）
- ❌ 写 `[[feedback-xxx]]`（横线 slug）指向 `~/.claude memory`——memory 文件名是下划线
- ❌ 在 vault 根目录留 0 字节 stub（Obsidian 点击 dangling 链接默认会自动建）

## 正确写法

| 场景 | 写法 |
|---|---|
| 关联段落 / 正文引用 | `[[ADR-002-manager-priority-system\|ADR-002]]` |
| 0-Index 索引内裸链 | `[[ADR-002-manager-priority-system]]`（aliases 自动渲染） |
| 指向 memory（跨域指针） | inline code `` `feedback_xxx` ``，不要 wikilink |

## 为什么是 bug

Obsidian 的 wikilink 解析规则是 **shortest path when possible**：

1. 写 `[[ADR-002]]` 时，Obsidian 先在 vault 中查 basename = `ADR-002` 的文件
2. 如果 vault **根目录**有 `ADR-002.md`（哪怕 0 字节），立刻命中——不会继续搜深层目录
3. 没有则建 dangling stub 节点（图谱里能看到，点开是空白）

Obsidian 默认设置「Default location for new notes: vault root」+「点击 dangling 自动建」联动会持续生成幽灵 0B stub。Nova 2026-05-25 复盘时发现 vault 根目录 3 个空 stub（GLO-02 / GLO-03 / feedback-obs-rules-pre-action-lookup），导致 vault 内 32 处 `[[ID-NN]]` 短链**全部指向空白页**而不是真实笔记。

## 防御机制（已落地）

1. **起草侧**：7 份 obs 起草 skill 顶部加铁律「起草前必先 `Skill obsidian-markdown` 查 wikilink/embed/callout 语法」
2. **入库侧**：`/nova-obs inbox-to-areas` 步骤 5.5 反向链回填加硬校验，扫到草稿正文含 `[[(ADR|PAT|GLO|RES|ARC)-\d+]]` 纯短链直接 ABORT
3. **巡检侧**：`/nova-obs health` 加：
   - § 3.3 纯短链 dangling lint
   - § 3.4 vault 根 0 字节幽灵 stub 检查（建议 rm 删除）
4. **Obsidian 设置建议**：关掉「自动建 dangling 链接」或改默认建文件位置到 `1-Inbox/`，避免根目录被污染

## 何时使用本 PAT

- 起草新 obs 笔记时 wikilink 选择写法
- 评审已有笔记 / 入库时校验
- code-reviewer / qa 看到产出含 obs 双链时复用本规则

## 关联

- 上游：[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]（rules 与 obs 协作）
- 协同：[[PAT-85-vault-search-synonym-expansion|PAT-85]]（搜索同义词扩张，里面的 `[[PAT-79]]` 是演示性 dangling，是本 PAT 的反例使用）
- 时序：[[PAT-92-vault-log-append-only|PAT-92]]（修复动作必落 0-Log）
- memory 指针：`feedback_obs_short_link_dangling_bug`

---
