---
id: ADR-025
title: YooAsset 远端 URL 模板占位符设计：编译宏决定 Platform，可选占位符不依赖运行时配置
status: accepted
date: 2026-05-19
summary: YooAsset URL占位符编译宏决定Platform
category: asset
aliases:
  - ADR-025-yooasset-url-template-placeholders
keywords: [ADR-025]
tags: [adr, nova, asset, yooasset, hotupdate]
supersedes: []
superseded-by: []
related: []
---

# ADR-025：YooAsset 远端 URL 模板占位符设计：编译宏决定 Platform，可选占位符不依赖运行时配置

## 背景（Context）

`AssetRemoteService` 负责把 Inspector 配置的 URL 模板替换为实际请求地址，需要避免启动期依赖 ConfigRuntimeSO、统一 `{Version}` 语义，并允许占位符按需省略。

## 决策（Decision）

1. **`{Platform}` 使用 `PlatformType` 枚举名，由编译宏决定**
   ```csharp
   private static PlatformType ResolvePlatform()
   {
   #if UNITY_ANDROID
       return PlatformType.Android;
   #elif UNITY_IOS
       return PlatformType.iOS;
   #elif UNITY_WEBGL
       return PlatformType.WebGL;
   #else
       return PlatformType.None;
   #endif
   }
   ```
   - **理由**：编译期决定，零运行时依赖；启动期早于任何配置反序列化即可用。
   - 不使用 `Application.platform`（其字符串与 PlatformType 枚举名不一致，破坏跨模块统一）。
   - 不使用 `ConfigRuntimeSO.Platform`（启动期循环依赖）。

2. **`{Version}` 取 `Application.version`**，与 CDN 上 App 大版本目录对齐。

3. **`{Package}` 取当前请求的资源包名**
   - 由 `BuildHostOptions` / `BuildWebOptions` 在构造 `AssetRemoteService` 时传入。

4. **三占位符全部可选**，模板里没有占位符就不替换。

5. **删除 `"Latest"` 硬编码**。
6. **HelpBox 文案精确化**，明示三占位符来源。

## 后果（Consequences）

### 正面
- 启动期零配置依赖，可在 ConfigRuntimeSO 反序列化之前发起远端请求
- URL 模板灵活，业务方按 CDN 目录约定自定义
- HelpBox 精确说明杜绝"{Version} 是不是 YooAsset 资源版本"类反复提问

### 负面
- 新增/修改 PlatformType 枚举值时需同步更新 `ResolvePlatform` 的宏分支（小成本，但是约束）
- 业务方手写错误占位符（如 `{platform}` 小写）时 `String.Replace` 不报错，配置错误只能在请求 404 时暴露——可在 doc 中加示范

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| `Application.platform.ToString()` 取 Platform | 字符串与 PlatformType 枚举名不一致，破坏跨模块语义统一 |
| 从 ConfigRuntimeSO 取 Platform/Version | 启动期循环依赖（ConfigRuntime 自己也是 AB 内资产） |
| 三占位符强制必填 + 校验 | 限死业务 CDN 目录结构；框架不应越界规定业务路径 |
| 自定义 token 解析器（`{Platform:lower}`） | 当前需求不需要；KISS |

## 验证依据（Verification）

- 代码：`Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Definitions/AssetRemoteService.cs`
- 调用点：`AssetManager.Methods.cs` `BuildHostOptions` / `BuildWebOptions`
- Inspector 文案：`AssetComponentInspector.Methods.cs` 主机服务器地址 HelpBox
