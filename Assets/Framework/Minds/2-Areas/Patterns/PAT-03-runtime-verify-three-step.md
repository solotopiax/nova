---
id: PAT-03
title: 运行时验证三步法
type: pattern
status: active
date: 2026-05-14
summary: 运行时验证三步编译Inspector PlayMode
category: review
aliases:
  - PAT-03
keywords:
  - PAT-03
  - 运行时验证三步法
tags:
  - pattern
  - methodology
  - qa
  - testing
  - unity
related:
  - "[[PAT-01-defect-severity|PAT-01]]"
  - "[[PAT-02-static-review-four-dim|PAT-02]]"
---

# PAT-03：运行时验证三步法

## 适用场景（When）

- 静态审查（[[PAT-02-static-review-four-dim|PAT-02]]）已通过，需要"跑起来对不对"的最后一道闸门
- 项目有 IDE 编辑器（Unity/UE/Godot/Web Inspector）承载序列化字段，需要验证 Inspector 行为
- 团队希望把运行时验证从"开发者凭记忆点几下"升级到"标准三步可复审"
- 需要让 QA 角色聚焦运行时，不再重复静态审查的工作

## 核心做法（What & How）

运行时验证 = **三步顺序闸门**，前一步不过下一步不进。

### 步骤①：编译通过

- 工具：IDE / build 系统的实时编译反馈（如 UnityMCP `read_console`）
- 关注 error / exception；warning 按严重度判
- **任一 error → 立即 REJECT，回退给 coder，不进入步骤②**
- 编译错误属于 P0（[[PAT-01-defect-severity|PAT-01]]）

### 步骤②：Inspector / 序列化验证（条件触发）

仅当变更涉及序列化字段（`[SerializeField]` / `MonoBehaviour` / `ScriptableObject` / DTO 类）时执行：

- [ ] 新增字段在 Inspector / 配置面板可见
- [ ] 重命名字段的旧序列化值未丢失（如 Unity 的 `FormerlySerializedAs`）
- [ ] 删除字段后无残留引用（场景 / Prefab / 配置）
- 纯逻辑改动可跳过本步骤

### 步骤③：行为验证（Play Mode / 集成 / E2E）

**测试入口规约**：

- 测试文件**始终覆盖写入同一份**（如 `Test.cs`），不新建多文件
- 类名、命名空间、入口方法固定（如 `Test.Start()`）
- 输出统一标记：`[PASS] <描述>` / `[FAIL] <描述>`，便于日志正则抓取
- Inspector 暴露可配置参数（路径、资源名、模拟数据），降低改测试的成本

**执行与判定**：

- 运行 Play Mode / 启动测试 + 捕获日志
- 必要时辅以场景查询、组件状态检查、Asset 存在性查询
- 无 `[FAIL]` + 行为与文档描述一致 → PASS
- 有 `[FAIL]` 或行为偏差 → 打回 coder，从步骤①重新走

### 修复边界

| 场景 | 由谁修 |
|------|--------|
| 测试用例预期写错 | QA 直接改测试 |
| 显而易见的 trivial 编译错（using 遗漏 / 拼写） | QA 直接改 |
| 真实业务逻辑缺陷 | **打回 coder**，QA 不替写 |
| 序列化不一致需补 `FormerlySerializedAs` | 打回 coder |

## 为什么这么做（Why）

- **顺序闸门**：编译不过谈不上 Inspector，Inspector 不对谈不上行为；先后顺序避免无谓的 Play Mode 启动开销
- **测试文件单点写入**：避免"几十个 TestXxx.cs 散落"，便于 LLM/CI 识别；只需关注一份文件的 diff
- **`[PASS]/[FAIL]` 标记**：纯文本协议跨 IDE / CI / LLM 都能解析，比 XUnit XML 更轻量
- **职责瘦身**：QA 不读风格规范，不重审逻辑（[[PAT-02-static-review-four-dim|PAT-02]] 已覆盖），专注"跑起来对不对"
- **修复边界明确**：QA 替修业务逻辑会模糊代码 ownership，必须打回原作者
- **Inspector 步骤的存在**：序列化字段 90% 的"看起来好好的但运行时数据丢了"问题靠这一步堵

## 反模式（Anti-patterns）

- **跳过编译直接跑 Play**：编译错的代码可能用了上次的 dll，跑出"假通过"
- **新建无数测试文件**：每次写一个 `TestFooBar.cs`，三个月后没人知道哪些还在用
- **测试无统一标记**：`Debug.Log("ok")` / `Assert.True(true)` 混用，日志正则抓不到
- **QA 替 coder 修业务逻辑**：QA 偷偷改了 Manager 实现让测试过，Bug 永远没修
- **Inspector 步骤被默认跳过**：覆盖序列化变更也没核对，下个 patch 字段映射错乱
- **PASS 报告无证据**：只写"通过"不附 UnityMCP / 日志截图，下次复审无法对账
- **REJECT 不复现**：只说"不行"不给复现路径，coder 摸不着头脑

## 跨项目复用提示

- **三步思想完全可迁移**：编译 / 配置 / 行为，对应 Web 项目就是 build / env config / E2E
- 步骤②"Inspector"是 Unity 特化；移植到非 GUI 项目时改为：
  - **服务端**：配置文件加载验证 + 数据库 schema 迁移验证
  - **Web 前端**：表单字段渲染 + localStorage / Redux 持久化兼容
  - **嵌入式**：bootloader 启动序列 + 寄存器配置
- 步骤③测试入口约定可以照搬（单文件 + 文本标记 + 入口方法），但要按测试框架调整：
  - 服务端项目可用 pytest / jest 自带 reporter，不需自造 `[PASS]/[FAIL]` 协议
  - 前端 E2E 用 Playwright trace + screenshot 替代日志
- 修复边界规则跨项目通用，强烈推荐保留

## 关联

- 配套：[[PAT-01-defect-severity|PAT-01]] 严重度（编译错=P0） / [[PAT-02-static-review-four-dim|PAT-02]] 静态审查
- 落地要求：验证动作应能被复现与复核，不依赖特定协作工具角色
