---
id: PAT-166
title: Editor 重载期 ScriptableObject 配置惰性初始化
summary: 重载期不预热全部配置，同步异步入口共用惰性初始化
category: runtime
type: pattern
status: active
date: 2026-08-21
source: source-runtime-probe-and-release-verification
aliases:
  - PAT-166-editor-reload-scriptableobject-lazy-initialization
keywords:
  - PAT-166
  - DidReloadScripts
  - ScriptableObject
  - SingletonScriptableObject
  - AssetDatabase
  - 配置预热
  - 惰性初始化
tags: [pattern, runtime, editor, scriptableobject, lifecycle, initialization, upm]
related:
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]"
  - "[[PAT-136-symptom-driven-debug-trap|PAT-136]]"
---

# PAT-166：Editor 重载期 ScriptableObject 配置惰性初始化

## 适用场景

- Unity Editor 通过 `[DidReloadScripts]`、`delayCall` 或类似回调执行包级初始化。
- 配置由 `AssetDatabase` 查找并通过 `SingletonScriptableObject<T>` 等入口加载。
- 本地化或其他派生状态依赖配置实例，但配置可能在 UPM 导入、脚本重载或 AssetDatabase 刷新期间暂时不可发现。
- 同一配置同时提供同步和异步获取入口。

## 核心做法

1. 脚本重载回调不通过反射枚举并访问所有配置单例。重载完成不等于所有包资源已经稳定可发现。
2. 把依赖配置实例的本地化或派生状态初始化放到配置真正创建成功之后。
3. 同步 `Instance` 与异步 `GetInstanceAsync` 必须汇入同一个初始化函数，避免只修复一个入口。
4. 初始化函数应支持重复调用，并以已经加载成功的实例作为前置条件；不得用吞掉缺失异常的方式掩盖加载竞态。
5. 回归测试分别覆盖同步首次访问和异步首次访问，验证两条路径产生一致的派生状态。

## 为什么这么做

`DidReloadScripts` 只表示脚本域重载完成。UPM 资源导入、AssetDatabase 刷新与延迟回调之间没有足以支撑“此刻所有 ScriptableObject 都能被找到”的稳定顺序。此时反射访问所有静态 `Instance` 会把尚未真正使用的配置也强制加载，导致间歇性的 `ScriptableObject '<Config>' not found in the project`。

删除全局预热不会删除初始化能力。把初始化移动到实例创建成功后的共同路径，既避开 Editor 导入窗口，也保证实际消费者首次访问时状态已经就绪。

## 反模式

- 在 `[DidReloadScripts]` 中反射扫描程序集并读取所有配置类型的静态 `Instance`。
- 为了让 Console 暂时安静而捕获并忽略配置缺失异常。
- 只在同步 getter 中补初始化，遗漏异步加载入口。
- 用一次手工 Clear Console 后不再复现作为修复依据。
- 删除预热代码后不补首次访问与异步路径测试。

## 验证方式

- Unity 完整编译后 Console 为零错误。
- 同步首次加载与异步首次加载的定向测试均通过。
- 运行时探针确认两条路径得到相同的本地化结果。
- 发布时检查 signed tgz 包含修复源码、两条路径的回归测试和 `.attestation.p7m`。

## 来源与验证依据

- 问题现场：`FigmageConfig` 在编译后的 Editor 延迟回调中间歇性报告未找到，堆栈由 `InternalLocalizator.InitializeEditorConfigs` 反射访问 `SingletonScriptableObject<T>.Instance` 触发。
- 修复实现：`Nova-internal/UPMPackages/com.da-assets.figma-converter/Core/DA-Assets/DA-Shared/Runtime/Scripts/Singleton/AssetConfig.cs`。
- 回归测试：`InternalLocalizatorRuntimeTests` 覆盖同步和异步实例创建路径。
- 运行时探针：同步与异步路径均返回 `Hello`；Unity 编译零错误。
- 发布验证：`com.da-assets.figma-converter@1.0.1`，tag `upm-release-2026.08.21-01`，registry `latest` 与版本一致，signed tgz 已包含 attestation、修复源码和测试。

## 关联

- 运行时验证：[[PAT-03-runtime-verify-three-step|PAT-03]]
- 私有 fork 差异记录：[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]
- 同源问题全链路分析：[[PAT-136-symptom-driven-debug-trap|PAT-136]]
