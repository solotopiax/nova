---
id: PAT-54
title: 改代码前必先扫 obs PAT 索引 + rules（禁凭记忆 / 沿袭老代码）
summary: 改代码前必先扫obs+rules索引禁凭记忆
category: ai-collab
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-54-obs-rules-pre-action-lookup
keywords:
  - PAT-54
  - PAT-54-obs-rules-pre-action-lookup
  - 改代码前必先扫 obs PAT 索引 + rules（禁凭记忆 / 沿袭老代码）
tags:
  - pattern
  - ai-collab
  - workflow
  - obs
  - rules
  - inspector
  - anti-recurrence
related:
  - "[[PAT-04-read-what-you-change|PAT-04]]"
  - "[[PAT-16-obs-keyword-trigger|PAT-16]]"
  - "[[PAT-25-rules-obs-patterns-collaboration|PAT-25]]"
  - "[[PAT-31-inspector-sop|PAT-31]]"
  - "[[PAT-39-editor-draw-discipline-enforcement|PAT-39]]"
  - "[[PAT-46-iteration-grep-self-check|PAT-46]]"
  - "[[PAT-52-cc-obs-four-layer-enforcement|PAT-52]]"
---

# PAT-54：改代码前必先扫 obs PAT 索引 + rules（禁凭记忆 / 禁沿袭老代码）

## 适用场景（When）

- AI 在 Nova 项目内对任一**已有规范覆盖区域**（Inspector / EditorUtil / Manager / Procedure / SDK Plugin / 配置序列化 / UPM 等）做新增或修改
- 触发信号：本次任务路径命中 `.claude/rules/*.md` 任一 `paths:` 通配符 → 该路径**必有 rules**；rules 关联到 obs 多条 PAT
- 反例信号（必须立刻打住）：「这条规范我应该记得」「周围老代码就是这么写的，跟着写就行」「先改完再核对」

## 核心做法（What & How）

### 一、动手三问（强制）

改任一文件前，必须依次回答三个问题，缺一即停手：

1. **本次改动的路径在 `.claude/rules/` 是否有 paths 命中？**
   - 命中 → Read 该 rule 全文（即使内存里"觉得读过"也必须重读，rules 会演进）
2. **该 rule 关联了哪些 obs PAT？**
   - 翻 rule 末尾「来源 / 关联」节 → 收集所有 PAT 编号
   - 在 `Assets/Framework/Minds/0-Index/0-Index-PAT.md` 用 grep / Read 拉到"动手前关键节"
3. **是否有"SOP 类协同 PAT"统辖本次改动？**
   - Inspector → PAT-31 §四（统辖 PAT-09/20/21/24）
   - Runtime 模块 → PAT-32
   - SDK Plugin → PAT-33
   - 协同 PAT 是改动前**唯一根索引**，必须从这里出发

### 二、提交前自检 grep（强制 + 写入 PR 自检清单）

每条 PAT 都附带反模式特征字符串。改完后**必须 grep 本次改动文件**，命中任一即返工：

| 规范 | 反模式 grep | 命中处置 |
|---|---|---|
| PAT-24 | `GUILayout\.Width\(17[0-9]\)` | 替换为 `180f` |
| PAT-09 §二 | `IncreaseIndentLevel\|DecreaseIndentLevel`（在 Inspector/） | 改 `Layout.Horizontal + Space(16f)` |
| PAT-21 | `HelpBox.*"[^"]*[。；;][^"]+"` | 拆 `(1)(2)(3)` |
| PAT-35 | `EditorGUILayout\.\|GUILayout\.\|EditorGUI\.`（业务侧 Inspector） | 走 `EditorUtil.Draw` 封装，缺则扩封装 |
| PAT-37 | `YooAsset` 字面词在非 Asset 模块 | 改"资源系统"等抽象表述 |

### 三、禁沿袭老代码（PAT-39 强化）

**"周围老代码就是这么写的"不构成合规理由**——老代码可能是历史欠账。判据：

- 改动文件内出现的写法 **vs** rules / PAT 文档里的范式不一致 → **以 rules / PAT 为准**
- 历史欠账可顺手清理（同一函数内的违规一起改），但不得复制反模式

### 四、规范关键字命中 ≠ 规范已遵守

obs-keyword-guard hook 是**用户消息**触发的被动提醒，**不**等同于"我自己改代码时已经看过"。AI 自身的"先读 obs"动作必须**主动**发起，触发条件就是"动手三问"。

## 为什么这么做（Why）

### 失误链根因（2026-05-21 复盘）

修 `ProcedureComponentInspector.Methods.cs` 一处 HelpBox 时连续违反 3 条规范（PAT-21 / PAT-24 / PAT-09 §二三），都是用户指认才修。事后查证：

- obs 早就完整沉淀（PAT-09/20/21/24/31 都在索引）
- memory 也有指针（feedback_inspector_helpbox_multiline / feedback_inspector_row_alignment）
- `.claude/rules/framework-inspector-alignment.md` hook 早就自动注入

**没读 = 失责。**根因不是"obs 不全"，是 AI 把 obs 当"被指认时再回查"而非"动手前必读"。

### 损失代价

- 用户连指 3 次才到位，注意力被错位规范消耗
- 反复修改污染 git diff，模糊真实 bug 上下文
- 信任成本——AI"承诺合规"但实际靠人盯，规范变摆设

### 杠杆点排序

- L1（最强）：rules 文件由 hook 自动注入 → 改 rule 内容比改 AI 行为更可靠
- L2：obs 协同 PAT（如 PAT-31）作为"动手根索引" → 单点入口辐射多条
- L3：自检 grep 写进 PR 模板 → 机械化兜底
- L4（最弱）：依赖 AI 记忆 → 不可信

## 反模式（Anti-patterns）

- ❌ "这条规范我记得，不用查了"——rules / obs 一直在演进，凭记忆 = 凭运气
- ❌ "周围都这么写，跟着写就行"——可能是历史欠账或反模式
- ❌ "先改完再核对"——错位规范修一次污染一次 diff
- ❌ "obs-keyword-guard 没触发，所以不用查"——hook 只在用户消息上工作
- ❌ "rules 已读过就不用看 obs"——rules 是切面，obs 是全貌；rules 缺漏时 obs 是兜底
- ❌ 看到 PAT-24 就只查 PAT-24，不去 PAT-31 §四看协同节——单点查询漏网

## 跨项目复用提示

- "动手三问 + 提交自检 grep"是 AI 协作通用范式，不限 Nova
- 任何项目只要建立"rules 自动注入 + obs 长期沉淀"双层结构，都可套用
- 协同 PAT（SOP 类）是单点根索引——是把多条规范打包进一个查询入口的关键设计

## 关联

- 上游：[[PAT-04-read-what-you-change]]（改什么读什么）+ [[PAT-25-rules-obs-patterns-collaboration]]（rules 与 obs 协作）
- 协同 PAT 范例：[[PAT-31-inspector-sop]] §四 / [[PAT-32-runtime-module-sop]] / [[PAT-33-sdk-plugin-sop]]
- 自检手段：[[PAT-39-editor-draw-discipline-enforcement]]（先查再扩再用 + 自检 grep）+ [[PAT-46-iteration-grep-self-check]]（多轮迭代 grep）
- memory 指针：`feedback_obs_rules_pre_action_lookup`
- 触发事件：2026-05-21 ProcedureComponentInspector HelpBox 三连失误复盘

---
