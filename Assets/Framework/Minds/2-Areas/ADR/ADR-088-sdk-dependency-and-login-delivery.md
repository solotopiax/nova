---
id: ADR-088
title: SDK 初始化按能力依赖调度，登录资料按当前会话补交
status: accepted
date: 2026-09-24
summary: SDK 依赖显式声明，登录 UID 与属性在插件就绪后交付
category: module
keywords: [ADR-088, SDK, 初始化依赖, 登录补交, 账号切换]
tags: [adr, nova, framework, sdk]
related:
  - "[[ADR-022-sdk-plugin-architecture]]"
  - "[[ADR-070-sdk-enable-via-configmaster-enabledsdks]]"
  - "[[PAT-33-sdk-plugin-sop]]"
---

# ADR-088：SDK 初始化与登录交付

## 背景

按 Priority 分桶使无关 SDK 相互等待。Firebase 初始化较慢时，业务即使只需要 DataMaster，也可能被全局 `InitializeTask` 挡住。登录发生在插件初始化前后均可能出现；只发一次即时事件会让晚就绪插件错过 UID。DataMaster 首次拉取还需要业务属性。

## 决策

- `ConfigMaster.EnabledSDKs` 仍是唯一启用源。插件通过 `ISDKInitializationDependencies` 声明所需的框架能力接口。无依赖者并发启动；必需依赖未就绪则阻止当前插件；可选依赖未启用时不等待。缺失、重复提供者和循环仅影响相关插件。
- `Priority` 保留用于稳定遍历和释放顺序，不再隐式决定初始化先后。
- `WaitForPluginAsync<T>()` 返回目标插件的最终状态；只有 `Ready` 可查询使用。全局 `InitializeTask` 继续表示所有已启用插件都得到最终结果，并不表示全部成功，也不保证任意插件永不挂起。
- `SDKManager.Login(uid, properties)` 保存本次属性快照，将最新会话交给就绪的插件；晚就绪插件得到最新会话。旧 `Login(uid)` 保留。切换账号再次调用 `Login`，登出调用 `EndLoginSession`。插件异步工作必须核对会话编号，不能提交旧账号结果。
- SDK 子包只依赖 Framework 能力接口，不直接依赖其他 SDK 子包。新包接入时必须按同一规则声明真实依赖和账号需求。

## 边界

`Login(uid, properties)` 只能保证 Nova 对接层在 DataMaster 发起首次请求前收到属性。业务仍需改用此入口；已发出的厂商网络请求和厂商内部落库顺序还须在设备与厂商层验证。外部消费者若仍等待全局 `InitializeTask`，会继续等待慢 SDK；应按实际能力改用插件级等待。

本决策引入 Framework 新接口。正式发布时须先发布包含这些接口的 Framework 版本，再把使用它们的 SDK 子包 `package.json.dependencies` 下界提升到该版本；开发仓的 `file:` 依赖不构成消费端版本兼容验证。

## 验证

框架、测试程序集及已安装的 SDK Runtime 程序集静态编译通过；`SDKComponentInitializeTaskTests` EditMode 11/11 通过，覆盖初始化任务共享、必需依赖缺失与登录属性快照。厂商设备回调和弱网时序尚未在本决策记录中验证。
