---
id: PAT-19
title: Test 目录只能消费端视角
type: pattern
status: active
date: 2026-05-16
summary: 测试代码以消费端视角写驱动用例
category: review
aliases:
  - PAT-19-test-consumer-view
keywords:
  - PAT-19
  - PAT-19-test-consumer-view
  - Test 目录只能消费端视角
tags:
  - pattern
  - testing
  - encapsulation
  - nova
related:
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-11-qa-battlefield-cleanup|PAT-11]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
---

# PAT-19：Test 目录只能消费端视角

## 适用场景（When）

- 框架/库项目同时维护内部实现与对外 API，且通过 asmdef 隔离 internal 类型
- 在 `Test/` 目录里有"运行时验证用例"或"sample 集成测试"
- qa-reviewer / runtime-coder 写 Play Mode `[PASS]`/`[FAIL]` 用例时
- 团队规则要求"测试代码必须站在用户角度"（不许窥探内部）

## 核心做法（What & How）

### 准入清单

所有就近创建的测试/验证脚本（命名带「关键词+YYYYMMDD」，详见 [[PAT-11-qa-battlefield-cleanup|PAT-11]]）一律按消费端用户视角写：

| 允许 | 禁止 |
|---|---|
| `Nova.Config.LoadAsync()` 等 Component public API | `typeof(XxxManager)` / `typeof(XxxManagerBase)` |
| `Assets/Framework/Scripts/Runtime/Modules/**/Component*.cs` 上的 `public` 成员 | `IXxxManager` 类型名出现在 Test 代码 |
| 验证「用户流程是否走通」类断言 | `Activator.CreateInstance` 创 Manager（绕过生命周期） |
| 命名带「内容关键词 + YYYYMMDD」（见 [[PAT-11-qa-battlefield-cleanup|PAT-11]]） | 直接反射构造或检查继承链 |

### 不属于 Test 目录的验证

涉及"继承链 / 反射创建 / 参数校验 / Manager 内部状态"这类**根本不应该存在于 Test 目录**：

- 改由 `qa-reviewer` 做静态代码审查（grep + diff 分析源码）
- 或落到 `Framework/` 内部单元测试（同 asmdef 内部，能见 internal）
- Test 里只验"用户流程通不通"，不验"内部结构对不对"

### 派活时的硬约束

主会话给 `qa-reviewer` / `runtime-coder` 分派 Test 目录任务，prompt 必须明示：

> 只准动 Component 的 public 接口；不许出现 `typeof(*Manager*)` / `IXxxManager` / `Activator.CreateInstance`。

Review Test.cs 时看到 `Manager` 字样（除非是注释）立刻 REJECT。

## 为什么这么做（Why）

- **封装边界完整性**：Manager 是框架内部实现，对用户不可见；Test 代表"用户如何使用框架"的真实场景。Test 能摸到 Manager = 默认了消费端会破封装
- **编译可行性**：`typeof(ConfigManager)` / `typeof(ConfigManagerBase)` 跨 asmdef 不可见时会触发 CS0122 编译错误
- **真实用例覆盖**：Test 验证的应该是「用户能做什么」而不是「内部怎么搭的」；后者归代码审查
- **抗重构**：内部 Manager 改名/拆分时，Test 不应该崩——因为 Test 不依赖内部
- **qa 三步法对齐**：[[PAT-03-runtime-verify-three-step|PAT-03]] 第三步「Play Mode `[PASS]`/`[FAIL]`」就是消费端视角的运行时验证
- **历史踩坑**：2026-04-30 Config 收尾时 qa-reviewer1 写的 Test.cs 用 `typeof(ConfigManager)` 验继承链，触发 CS0122 + 违反消费端视角双重错误

## 反模式（Anti-patterns）

- **`typeof(XxxManager)` 验继承链**：内部类型 internal 跨 asmdef 见不到，编译就炸；本质是把内部审查项错放进 Test 目录
- **反射构造 Manager**：`Activator.CreateInstance(typeof(...))` 越过 Component 生命周期，Manager 状态不完整
- **断言内部字段值**：「我要确认 Manager 内 `m_IsLoaded == true`」属于内部状态，不在 Test 目录
- **验参数校验**：`Assert.Throws<ArgumentNullException>(() => manager.Foo(null))`——参数校验是 Manager 内部职责，Test 不需要重复验
- **出于"代码覆盖率"补的内部触达**：覆盖率指标错误地引导测试触达内部实现，应转为 Framework 内部单测而非 Test
- **写 sample 时顺手用了 Manager 接口**："反正都在自家项目"——下次内部接口改名时 sample 全炸

## 跨项目复用提示

- **思想完全可迁移**：任何区分「内部实现」与「对外 API」的项目都适用——Java / Kotlin 的 package-private、Rust 的 `pub(crate)`、C++ 的 PIMPL 都同源
- 项目工程上一定要靠语言机制（asmdef / module / namespace 可见性）强制，光靠规约不够
- 如果项目没分 Test 与单测，本规则不适用，先建立 Test = "黑盒消费端" 与 Unit Test = "白盒内部" 的二分
- 调整阈值时注意：Test 不能等同 e2e（e2e 通常允许某些环境检查），但 Test 必须保持「用户能调到的 API 才能用」

## 关联

- 相关 Pattern：[[PAT-03-runtime-verify-three-step|PAT-03]]、[[PAT-11-qa-battlefield-cleanup|PAT-11]]
- 历史源头：2026-04-30 Config Wave 3 收尾，qa-reviewer1 错把 `typeof(ConfigManager)` 写进 Test
