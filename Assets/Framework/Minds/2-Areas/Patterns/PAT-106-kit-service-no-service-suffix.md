---
id: PAT-106
title: Kit Service 类去 Service 后缀
summary: Kit Service 类禁带 Service 后缀
category: naming
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-106
  - PAT-106-kit-service-no-service-suffix
keywords:
  - PAT-106
  - Kit Service 类去 Service 后缀
  - PAT-106-kit-service-no-service-suffix
tags: [pattern, naming, upm, kit]
related:
  - "[[PAT-42-naming-concrete-deduplicate|PAT-42]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
---

# PAT-106：Kit Service 类去 Service 后缀

## 适用场景（When）

- 在 UPM Kit 包（`com.solotopia.nova.framework.kit.*`）内定义业务级 Service 类时
- 业务侧通过 `Nova.<Component>.Kit<T>()` 拿到该 Service 实例时
- 给新建的 Kit Service 类命名 / 重命名既有 Service 类时

## 核心做法（What & How）

**铁律**：Kit 包内的业务级 Service 类**禁止**带 `Service` / `Manager` / `Helper` 后缀。

### 命名对照

| ❌ 旧 | ✅ 新 |
|---|---|
| `LoginService` | `Login` |
| `AccountService` | `Account` |
| `PaymentService` | `Payment` |
| `ChatService` | `Chat` |

### 业务侧调用形态对照

```csharp
// ❌ 旧
var resp = await Nova.Network.Kit<LoginService>().Async(cmdRow, ChannelType.Google, openId);
string uid = Nova.Network.Kit<LoginService>().UID;

// ✅ 新
var resp = await Nova.Network.Kit<Login>().Async(cmdRow, ChannelType.Google, openId);
string uid = Nova.Network.Kit<Login>().UID;
```

### 何时是例外

只有以下情况允许带后缀：
- 类是 **AOT 框架基类**（如 `NetServiceBase` 已废弃，过去基于 CRTP 单例约束子类）—— 但这种基类应直接质疑是否还需要存在
- 类是 **Editor 工具**（如 `EditorWindow` / `Inspector`）—— 不属于 Kit Service 范畴
- 类是 **静态工具**（如 `NetBuilder` / `NetParser`）—— 静态工具不走 `Kit<T>()`，不在本规则约束内

## 为什么这么做（Why）

- **调用形态自带语义**：`Nova.Network.Kit<Login>()` 已经表达"网络模块的 Login 服务"，再叫 `LoginService` 等于"login service service"。
- **去后缀符合 [[PAT-42-naming-concrete-deduplicate|PAT-42]] 第三条「去类型重复」**：Kit + 服务类 = 显式语境，类名重复 Service 字眼是冗余。
- **与项目其他模块对齐**：业务 UI 类已强制去 `Panel/Window/Dialog/View*` 后缀（强制带 View 后缀的反向对应：归类性后缀只允许这一种）；Kit Service 应同等待遇。
- **类名简短，调用更顺**：`Kit<Login>()` 比 `Kit<LoginService>()` 易读、易补全、易在文档中引用。

## 反模式（Anti-patterns）

- ❌ 新建 Kit Service 类时本能地起 `XxxService` 名字
- ❌ 看到 `Login` 短类名觉得"语义不全要补 Service 表明用途"—— 用途由 `Kit<T>()` 调用语境表达，类名不重复
- ❌ 把 `Service` 改名理由当作「保留向后兼容」的借口加 `[Obsolete]` shim（违反 [[PAT-104-no-obsolete-shim-rule|PAT-104]]）—— 一次提交全量改完是正解
- ❌ 改名后忘记同步测试代码、CHANGELOG、文档示例

