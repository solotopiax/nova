---
id: PAT-154
title: Editor 热更缓存重置必须限定沙盒与框架版本记录
type: pattern
status: active
date: 2026-08-06
summary: 一键重置仅清 Editor 沙盒与 version 文件
category: asset
aliases:
  - PAT-154-editor-hotfix-cache-reset-boundary
  - Editor 热更缓存重置边界
keywords:
  - PAT-154
  - Clean Hotfix Caches
  - YooAsset Editor 沙盒
  - GetYooFolderName
  - version 文件
  - asset-check-device-id.dat
  - 热更缓存清理
tags:
  - pattern
  - asset
  - editor
  - yooasset
  - hotfix
  - cache
related:
  - "[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]"
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[PAT-125-runtime-incremental-three-steps|PAT-125]]"
  - "[[MOC-Inspector|MOC-Inspector]]"
---

# PAT-154：Editor 热更缓存重置必须限定沙盒与框架版本记录

## 适用场景

- HostPlayMode 或 Editor 资源联调受到历史 Bundle、Manifest 或本地可启动版本记录影响。
- 需要给开发者提供“一键回到无热更缓存状态”的 Inspector 或菜单入口。
- 清理操作同时涉及 YooAsset 自有沙盒和 Nova 框架自主保存的文件，必须明确两者所有权。

## 核心做法

### 1. YooAsset Editor 沙盒路径必须动态解析

清理入口通过 `YooAssetConfiguration.GetYooFolderName()` 读取当前配置，再以 `Application.dataPath` 推导出的项目根目录解析绝对路径。不得把 `yooasset` 或任何机器绝对路径写死。

递归删除前至少拒绝以下目标：空路径、文件系统根目录、Unity 项目根目录、项目外路径，以及 `Assets`、`Packages`、`ProjectSettings`、`Library`、`UserSettings`、`.git` 等项目关键目录。

### 2. 框架自主文件只按明确后缀删除

框架侧仅删除 `persistentDataPath/Asset` 第一层的 `*.version`：

```text
persistentDataPath/Asset/{package}.version
```

这些文件是框架提交的“本地可启动版本”记录。不要递归清空整个 `Asset` 目录，也不要把目录中所有文件统称为热更缓存。

特别保留：

```text
persistentDataPath/Asset/asset-check-device-id.dat
```

该文件只记录稳定 DeviceID，用于后续启动期白名单判断，不是白名单缓存，也不是热更资源缓存。其他未知或业务文件同样默认保留。

### 3. Editor 入口共享同一清理能力

AssetComponent Inspector 与 `Nova/Clean Hotfix Caches` 菜单统一调用 `EditorUtil.Asset.Cache.ClearAllHotfixResources()`，避免不同入口逐渐形成不同删除范围。

- Inspector 按钮和说明 HelpBox 受“启用热更新”开关约束。
- 菜单与 Inspector 在 Play Mode 或即将切换 Play Mode 时禁止清理。
- 当前交互点击后直接执行，不弹二次确认；完成或失败后显示结果。

## 与运行时清理的边界

本模式是开发期的“完整重置”，不同于 [[ADR-052-asset-cache-two-layer-cleanup|ADR-052]] 中运行时按 Manifest 清理未使用 Bundle：

| 操作 | 删除范围 | 目的 |
|---|---|---|
| `ClearUnusedCacheAsync` | 当前 Manifest 不再引用的旧 Bundle | 运行时回收磁盘空间 |
| `Clean Hotfix Caches` | 整个 YooAsset Editor 沙盒 + 框架 `*.version` | 开发联调回到无历史热更资源状态 |

两者不得复用同一个含糊的“Cleanup”语义，也不得让 Editor 完整重置入口进入 Runtime。

## 反模式

- 写死 `{ProjectRoot}/yooasset`，忽略 YooAsset 配置可变。
- 递归删除 `persistentDataPath/Asset`，顺带删掉 DeviceID 或其他框架、业务文件。
- 把 `asset-check-device-id.dat` 称为“白名单缓存”。
- Inspector 按钮在关闭热更新时仍可执行，造成操作归属不清。
- Inspector 与菜单各写一套删除逻辑，导致保护规则漂移。

## 验证依据

- 统一实现：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.cs`
- Inspector 入口：`Assets/Framework/Scripts/Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.Methods.cs`
- 菜单入口：`Assets/Framework/Scripts/Editor/Menus/AssetCacheMenuItems.cs`
- 隔离路径契约：`Assets/Tests/Editor/AssetEditorCacheTests.cs`
- 临时目录测试验证沙盒整体删除、顶层 `*.version` 删除、DeviceID 与其他文件保留；相关 EditMode 回归曾为 14/14 通过。

## 关联

- 运行时磁盘缓存清理：[[ADR-052-asset-cache-two-layer-cleanup|ADR-052]]
- DeviceID 与本地可启动版本语义：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- YooAsset 缓存寻址：[[PAT-125-runtime-incremental-three-steps|PAT-125]]
- Editor 入口图谱：[[MOC-Inspector|MOC-Inspector]]
