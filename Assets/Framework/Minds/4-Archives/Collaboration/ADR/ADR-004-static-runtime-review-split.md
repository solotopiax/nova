---
id: ADR-004
title: 静态/运行时审查二段制（code-reviewer + qa-reviewer）
status: accepted
date: 2026-05-14
summary: 静态审查与运行时验证两阶段分离
category: review
aliases:
  - ADR-004
keywords: [ADR-004, 静态/运行时审查二段制（code-reviewer + qa-reviewer）]
tags: [adr, nova, agent, review, quality-gate]
supersedes: []
superseded-by: []
related:
  - "[[ADR-003-main-session-as-team-leader|ADR-003]]"
  - "[[PAT-01-defect-severity]]"
  - "[[PAT-02-static-review-four-dim]]"
  - "[[PAT-04-read-what-you-change]]"
---

# ADR-004：静态/运行时审查二段制（code-reviewer + qa-reviewer）

## 背景（Context）

早期 Nova 流程使用三个审查 agent：`check-coder`（逻辑 bug 猎杀）+ `code-reviewer`（风格/架构）+ `qa-reviewer`（运行时验证）。问题：

- 三 agent 职责边界模糊：风格违反与逻辑错误经常重叠，coder 反复返工。
- check-coder 与 code-reviewer 都需要读 `csharp-code-style.md` 和架构规范，token 双倍开销。
- 文档同步检查无明确归属，时常落空。
- qa-reviewer 实际上承担"风格审查 + 运行时验证"，但风格审查没有静态层面深度。

## 决策（Decision）

**审查二段制：静态审查（code-reviewer）+ 运行时验证（qa-reviewer）**。

### code-reviewer（吸并原 check-coder + qa 的静态维度）

- **模型 + 模式**：sonnet + plan
- **写码权限**：仅修确定性缺陷（明显 null 遗漏、风格违反、命名前缀错误）
- **必读规则**：`csharp-code-style.md` + `framework-component-manager-architecture.md` + `framework-sync-docs-after-task.md`
- **审查维度**（四大维度，缺一不可）：
  1. **逻辑正确性**：删/改副作用、null 边界、契约一致性、生命周期对称、async 安全、集合并发、静默失败
  2. **规范与架构**：风格（命名前缀 m_/c_/s_、单语句单行、零对齐空格、XML 注释）、Component+Manager 三层链、partial 拆分、文档同步
  3. **C# 安全**（CRITICAL 一票否决）：命令注入、路径遍历、不安全反序列化、硬编码密钥、空 catch
  4. **性能**（MEDIUM 建议）：StringBuilder、LINQ 热路径、TryGetValue、async void、sync-over-async
- **缺陷分级**：CRITICAL > P0 > P1 > P2 > P3 > P4；P0/P1/CRITICAL 必修
- **通过标志**：`[CHECK-PASS]`

### qa-reviewer（瘦身为纯运行时三步）

- **模型 + 模式**：sonnet + plan
- **写码权限**：仅修测试用例 / trivial 编译错（using 遗漏、拼写错）
- **必读文件**：目标模块 L2 md + 当前模块附近既存的测试脚本（如有），**不**读风格/架构规范
- **三步运行时验证**：
  1. **编译**：UnityMCP `read_console`，有 error 立即 REJECT
  2. **Inspector / 序列化**：`[SerializeField]` 字段可见、`FormerlySerializedAs` 正确（条件性）
  3. **Play Mode**：按需就近创建测试脚本（命名带「关键词+YYYYMMDD」），`Start()` 启动测试，输出 `[PASS]` / `[FAIL]`，跑完按 [[PAT-11-qa-battlefield-cleanup]] 清场
- **通过标志**：`PASS`

### 串行卡点与回环

- 顺序：coder → code-reviewer → qa-reviewer → doc-writer
- code-reviewer 通过后才进 qa-reviewer
- qa-reviewer REJECT 时打回 coder；若原路径跳过了 reviewer，**打回后必须补走 reviewer**

## 后果（Consequences）

### 正面

- 职责分明：code-reviewer 管"写得对不对"，qa-reviewer 管"跑起来对不对"。
- code-reviewer 一次性吸并逻辑 + 规范，避免 coder 在错误代码上纠结格式。
- qa-reviewer 不读风格/架构规范，token 开销显著降低，专注 UnityMCP 验证。
- 文档同步明确归 code-reviewer 维度二，REJECT 直接交 doc-writer 补齐。
- 二段制留出明确回环：REJECT → coder → 重走 code-reviewer → 重走 qa-reviewer。

### 负面

- code-reviewer 单一 agent 维度多（4 大维度 + 7 子项），单次审查负载大。
- 跳 reviewer 快速通道存在心智模型风险（主会话需精准判定低风险），有疑虑回退完整流程是兜底。
- 二段串行使最短交付路径多一跳，简单改动也要走完两关（除非走快速通道）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 单一 reviewer（合并静态 + 运行时） | 单 agent 同时跑 UnityMCP + 静态扫描，上下文过载；运行时与静态规则不必同时驻留 |
| 三段制（保留 check-coder + code-reviewer + qa-reviewer） | 静态维度重复加载规范，token 双倍开销；职责边界模糊 |
| 只保留 qa-reviewer，静态由 coder 自检 | coder 无第三方视角，难以发现 logic/规范遗漏；缺乏质量防线 L2 |
| 用 hook 自动跑 dotnet format / Roslyn 分析器 | 工具规则覆盖不到 Nova 自定义风格（命名前缀、partial 拆分），且无 LLM 语义理解 |

## 验证依据（Verification）

- Agent 定义：`.claude/agents/code-reviewer.md`、`.claude/agents/qa-reviewer.md`
- 流程描述：`.claude/CLAUDE.md` 「Team 编制与流程」+ 「质量防线」
- 通讯协议：`CLAUDE.md` 「[审查] from → to | P0(x) P1(x) ...」
- 通过标志：grep `[CHECK-PASS]`、`PASS` 关键词在 agent 输出

## 关联

- Agent 定义：`.claude/agents/code-reviewer.md`、`.claude/agents/qa-reviewer.md`
- 相关 ADR：[[ADR-003-main-session-as-team-leader|ADR-003]]（主会话调度中心）
- 相关 Pattern：[[PAT-01-defect-severity]]、[[PAT-02-static-review-four-dim]]、[[PAT-04-read-what-you-change]]
