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
- 编写消费项目或 Sample 的行为验证用例时
- 团队规则要求"测试代码必须站在用户角度"（不许窥探内部）

## 核心做法（What & How）

### 准入清单

消费端行为测试按真实公开调用面编写；一次性脚本与长期测试的命名、保留分别遵守 [[PAT-11-qa-battlefield-cleanup|PAT-11]]：

| 允许 | 禁止 |
|---|---|
| `Nova.Config.LoadAsync()` 等 Component public API | `typeof(XxxManager)` / `typeof(XxxManagerBase)` |
| 由 Component 正式暴露的公开接口与成员 | 为访问内部实现而绕过程序集可见性或私有字段 |
| 验证「用户流程是否走通」类断言 | `Activator.CreateInstance` 创 Manager（绕过生命周期） |
| 验证公开契约声明的失败与边界行为 | 直接反射构造或检查内部继承链 |

接口、抽象骨架和具体实现要区分。`I{Xxx}Manager` 可以是公开契约，不能仅因名字带 `Manager` 判为内部访问；具体成员是否供消费端使用，以当前源码/XML和 Docs 为准。

### 不属于 Test 目录的验证

内部继承链、反射构造和 Manager 私有状态验证应与消费端行为测试分开：

- 使用静态源码审查核对内部结构。
- 必须运行时，落到有明确程序集可见性的框架内部单元测试，不通过反射破坏消费端边界。
- Test 里只验"用户流程通不通"，不验"内部结构对不对"

### 审查判据

- 检查测试是否通过当前公开入口取得已初始化对象、观察行为，并在退出时释放自有资源。
- 不允许为得到测试结果直接构造内部 Manager、读取私有状态或跳过 Component 生命周期。
- 公开 API 的参数校验、取消、失败与资源释放可以作为消费端行为验证，不因它们最终由 Manager 实现而排除。


## 为什么这么做（Why）

- **封装边界完整性**：内部具体实现与公开接口承担不同职责；消费端验证公开行为，不把内部状态当成用户契约
- **编译可行性**：`typeof(ConfigManager)` / `typeof(ConfigManagerBase)` 跨 asmdef 不可见时会触发 CS0122 编译错误
- **真实用例覆盖**：Test 验证的应该是「用户能做什么」而不是「内部怎么搭的」；后者归代码审查
- **抗重构**：内部 Manager 改名/拆分时，Test 不应该崩——因为 Test 不依赖内部
- **qa 三步法对齐**：[[PAT-03-runtime-verify-three-step|PAT-03]] 第三步「Play Mode `[PASS]`/`[FAIL]`」就是消费端视角的运行时验证

## 反模式（Anti-patterns）

- **`typeof(XxxManager)` 验继承链**：内部类型 internal 跨 asmdef 见不到，编译就炸；本质是把内部审查项错放进 Test 目录
- **反射构造 Manager**：`Activator.CreateInstance(typeof(...))` 越过 Component 生命周期，Manager 状态不完整
- **断言内部字段值**：「我要确认 Manager 内 `m_IsLoaded == true`」属于内部状态，不在 Test 目录
- **把内部状态当公开预期**：通过私有实现字段判断成功，却没有检查用户可观察结果
- **出于"代码覆盖率"补的内部触达**：覆盖率指标错误地引导测试触达内部实现，应转为 Framework 内部单测而非 Test
- **只凭名称判断边界**：禁止所有带 `Manager` 的公开接口，或因为接口公开就允许直接构造内部具体实现

## 跨项目复用提示

- **思想完全可迁移**：任何区分「内部实现」与「对外 API」的项目都适用——Java / Kotlin 的 package-private、Rust 的 `pub(crate)`、C++ 的 PIMPL 都同源
- 项目工程上一定要靠语言机制（asmdef / module / namespace 可见性）强制，光靠规约不够
- 如果项目没分 Test 与单测，本规则不适用，先建立 Test = "黑盒消费端" 与 Unit Test = "白盒内部" 的二分
- 调整阈值时注意：Test 不能等同 e2e（e2e 通常允许某些环境检查），但 Test 必须保持「用户能调到的 API 才能用」

## 关联

- 相关 Pattern：[[PAT-03-runtime-verify-three-step|PAT-03]]、[[PAT-11-qa-battlefield-cleanup|PAT-11]]
- [[ADR-001-component-manager-three-layer|ADR-001]]：公开接口与内部抽象、具体实现分层。

## 来源与影响面

- [NetworkComponent](../../../Docs/Runtime/Modules/Network/NetworkComponent.md) 及 [公开成员源码](../../../Scripts/Runtime/Modules/Network/NetworkComponent.Visitors.cs)：组件暴露公开 Manager 接口，证明接口类型名不能等同于内部实现。
- 本条约束消费端与 Sample 测试的封装边界，不限制框架内部正式单元测试对内部结构的必要验证，也不把测试代码存在解释为运行已通过。
