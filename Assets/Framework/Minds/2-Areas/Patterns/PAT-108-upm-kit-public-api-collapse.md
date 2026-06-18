---
id: PAT-108
title: UPM Kit 包对外 API 收口模式
summary: 跨 asmdef public API 从 IDE 补全隐藏的解法
category: arch
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-108
  - PAT-108-upm-kit-public-api-collapse
keywords:
  - PAT-108
  - UPM Kit 包对外 API 收口模式
  - PAT-108-upm-kit-public-api-collapse
tags: [pattern, arch, upm, kit, naming]
related:
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[ADR-020-assembly-dependency-direction|ADR-020]]"
---

# PAT-108：UPM Kit 包对外 API 收口模式

## 适用场景（When）

- 设计 / 重构 UPM Kit 包（`com.solotopia.nova.framework.kit.*`）的对外 API 时
- 一个类型 / 方法**必须 public**（跨 asmdef 给同包的 sibling Kit 包调用，比如 `kit.network.login` 调 `kit.network.NetService`）
- 但**业务侧不应该看到**它（不应在业务侧 IDE 自动补全里出现，避免业务代码绕过 Service 门面直接拿底层工具）
- 同时希望避免依赖 `[Obsolete]` shim 或反射桥接等反模式

## 核心做法（What & How）

三层收口策略，按"对外可见度"由高到低组合使用：

### 1. internal — 包内私用类型

完全不被外部访问的类型，标记 `internal sealed class` 或 `internal static class`。例：
- `NetParser`（解密 + ParseResponse 一并）
- `NetResult`（基础响应解析结果数据类）

只有同包内 `NetService` 等编排器能用，业务侧、sibling Kit 包都看不到。

### 2. public + `[EditorBrowsable(EditorBrowsableState.Never)]` — 跨包内部 API

需要跨 asmdef 给 sibling Kit 包用，但业务侧不应直接调的：
- 类型级标 `[EditorBrowsable(Never)]`（如 `NetBuilder`）
- 方法级标（如 `NetService.SendAsync`、`NetService.SetUid`）

```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetBuilder { ... }

public static class NetService
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetUid(string uid) { ... }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static async UniTask<NetResponse<TResp>> SendAsync<...>(...) { ... }
}
```

效果：
- 编译期：`kit.network.login` 的 `Login` 类正常调 `NetBuilder.BuildHeader()` / `NetService.SetUid()` / `NetService.SendAsync()`，跨 asmdef 不报错；
- IDE 体验：业务侧（main project / 业务 DLL）按 `NetService.` 后**不会出现** `SendAsync` / `SetUid` 等内部方法，看到的只剩 `Uid` / `IsDebugMode` / `SetDebugMode` 等正向 API。

### 3. public 不加修饰 — 业务侧入口

留给业务直接调的字段、属性、方法：
- `NetService.Uid`（只读）
- `NetService.IsDebugMode`、`NetService.SetDebugMode(bool)`
- `Login.Async(...)`、`Login.UID`、`Login.IsLoggedIn`、`Login.Logout()`
- `NetResponse<T>` / `NetErrorCode` / `LoginErrorCode`
- `NetworkComponentKitExtensions.SetDebugMode(this NetworkComponent, bool)`（扩展方法挂在主框架 Component 上）

### 业务侧最终接入形态

```csharp
Nova.Network.SetDebugMode(true);
var resp = await Nova.Network.Kit<Login>().Async(cmdRow, ChannelType.Google, openId);
```

业务侧根本看不到 NetService / NetBuilder / NetParser / NetRequest / Header 之类的字眼。

## 为什么这么做（Why）

- **跨 asmdef 限制**：C# 没有「程序集集合」概念，asmdef 之间只有 public/internal 二选一，sibling Kit 包之间共享内部 API 只能 public。
- **IDE 体验问题**：如果一组 public API 全部裸奔在业务侧补全列表里，业务很容易绕过 Service 门面直接调底层 Builder/Parser，腐烂入口形成。
- **对比方案：`[Obsolete]` shim**：`[Obsolete]` 是给"已废弃 API"用的，把它拿来"伪装隐藏"会污染 build warning（除非 `[Obsolete(error: false)]`），而且语义错位。
- **对比方案：反射桥接**：跨 asmdef 反射调底层 API 会引入运行时类型查找开销 + IL2CPP stripping 风险（默认裁剪 + link.xml 维护），代价远大于一个 attribute。
- **`[EditorBrowsable(Never)]` 的副作用近乎零**：仅影响 IDE 补全列表（VS / Rider 默认尊重），不影响编译、不影响运行、不影响反射查询、不留 warning。

## 反模式（Anti-patterns）

- ❌ Kit 包内本可 internal 的类型为了"将来可能跨包用"提前 public。先 internal，跨包需求出现再 public。
- ❌ 给跨 asmdef 共享但不希望业务调的方法加 `[Obsolete]`。语义错位 + warning 污染。
- ❌ 在业务侧文档里"靠注释"约束业务"不要调 NetBuilder"。注释不是 enforcement。
- ❌ 业务侧调用形态出现 `NetBuilder.` / `NetParser.` / `NetService.SetUid(` 字面。出现即说明收口失败。

## 跨项目复用提示

非 Unity / 非 Nova 的 .NET 项目同样适用：库内部 API + 跨程序集共享但不暴露给消费方时，`[EditorBrowsable(Never)]` 是 .NET BCL 自身大量使用的 idiom（如 `Task.GetAwaiter` / `IAsyncResult.AsyncWaitHandle` 部分实现）。可放心搬。

