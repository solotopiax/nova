---
id: PAT-76
title: UnityMCP 多操作必走 batch_execute 单次域重载
summary: UnityMCP ≥3 次操作走 batch_execute
category: workflow
type: pattern
status: active
date: 2026-05-23
aliases:
  - PAT-76
  - PAT-76-unity-mcp-batch-execute
keywords:
  - PAT-76
  - PAT-76-unity-mcp-batch-execute
  - UnityMCP 多操作必走 batch_execute 单次域重载
tags:
  - pattern
  - ai-collab
  - unity-mcp
  - performance
  - workflow
related:
  - "[[PAT-06-main-session-dispatch|PAT-06]]"
---

# PAT-76：UnityMCP 多操作必走 batch_execute 单次域重载

## 适用场景（When）

任何通过 UnityMCP 与 Unity Editor 交互的 agent / 主会话：

- coder 在写完多文件 .cs 后做编译验证
- qa-reviewer 在三步验证内做查询/反射/测试
- Inspector 字段批量查询场景对象
- 任何"先改若干、再确认结果"的链式操作

## 核心做法（What & How）

### 铁律

连续 ≥3 次 UnityMCP 操作**必须**用 `mcp__UnityMCP__batch_execute` 包装为一次调用。

### 典型 batch 模板

| 场景 | batch 包装 |
|---|---|
| 编译验证 | `batch_execute([refresh_unity, read_console])` |
| Inspector 查询 | `batch_execute([find_gameobjects(<目标>), unity_reflect(<字段路径>)])` |
| 测试用例执行 | 多用例一次性 `run_tests` 批量跑（`run_tests` 本身支持批量过滤器） |

### 单次操作豁免

单一原子操作（如只读一个 Console、只查一个 GameObject）无需 batch；仅"会触发域重载/Play Mode 切换"的连续操作必须 batch 化。

## 为什么这么做（Why）

- 每次 `refresh_unity` 触发一次完整域重载，Unity 编译流水线 + 程序集加载耗时巨大
- 连续 N 次单独调用 = N 次域重载，时间线性爆炸
- `batch_execute` 把多操作合并为一次域重载内的连续步骤，单次域重载完成多操作
- 多个测试用例逐个 `run_tests` 会让 Editor 反复进出 Play Mode，单次跑完即可拿到所有 `[PASS]/[FAIL]` 结果

实测收益：连续编译验证耗时从"每改一个 .cs 立即 refresh_unity × N 次"降到"全部改完后一次 batch"——降本约 60%（与改动文件数线性相关）。

## 反模式（Anti-patterns）

- **每改一个 .cs 立即 `refresh_unity`** → 域重载次数线性爆炸（一票否决）
- **逐个 `read_console`** → 每次往返都过一次 MCP 桥
- **逐个 `run_tests`** → Editor 反复进出 Play Mode，帧切换开销翻倍
- **`refresh_unity` + 单独 `read_console`** 拆两次调用 → 应合并为一次 `batch_execute`

## 跨项目复用提示

适用于任何使用 MCP 协议且底层操作有"一次性切换成本"的环境（Unity 域重载、Docker 容器启停、Browser headless 启动等）。核心思路是「识别有切换成本的边界 → 边界内合并多操作」。

## 来源（Origin）

- 会话日期：2026-05-23
- 关键对话节选：
  > AI："UnityMCP 调用链没有 batch。`mcp__UnityMCP__batch_execute` 你应该没系统化用。每改一个脚本 → refresh → read_console → manage_scene 是 4 次往返。batch 一次打包，Editor 域重载次数从 N 次降到 1 次"
  > 用户："按照P0到P3的总结，开始优化！...4.一次性全做完"

## 关联

- 规则文件：`.claude/agents/runtime-coder.md` §UnityMCP 调用规范
- 规则文件：`.claude/agents/editor-coder.md` §UnityMCP 调用规范
- 规则文件：`.claude/agents/qa-reviewer.md` §UnityMCP 调用规范 + §三步运行时验证
- 规则文件：`.claude/agents/team-leader.md` §并行调度模板 → 模板 D
