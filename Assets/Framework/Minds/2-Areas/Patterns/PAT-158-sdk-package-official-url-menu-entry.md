---
id: PAT-158
title: SDK UPM 包必须自带官方 Console 与 Readme 菜单入口
type: pattern
status: active
date: 2026-08-10
summary: SDK 包自带官方后台与文档菜单入口
category: module
aliases:
  - PAT-158-sdk-package-official-url-menu-entry
  - SDK 官方 URL 菜单入口
keywords:
  - PAT-158
  - Open SDK URL
  - SDK Console
  - SDK Readme
  - SDK 官方后台
  - SDK 官方文档
tags:
  - pattern
  - nova
  - sdk
  - upm
  - editor
  - menu
related:
  - "[[PAT-33-sdk-plugin-sop|PAT-33]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-109-upm-package-docs-mandatory|PAT-109]]"
  - "[[PAT-155-unity-menuitem-priority-grouping|PAT-155]]"
---

# PAT-158：SDK UPM 包必须自带官方 Console 与 Readme 菜单入口

## 适用场景

- 新增任何 Nova SDK UPM 包。
- 迁移、拆分或评审现有 SDK 包。
- SDK 厂商更换后台地址或官方接入文档地址。

## 核心做法

1. 每个 SDK 包必须在自己的 `Nova/Scripts/Editor/` 中维护 C# 菜单代码，不把可选 SDK 的 URL 汇总到 Nova 主框架。
2. 每个包至少注册以下两个菜单项：
   - `Nova/Open SDK URL/<SDK> Console`
   - `Nova/Open SDK URL/<SDK> Readme`
3. `Console` 指向该 SDK 官方管理后台，`Readme` 指向该 SDK 官方 Unity 接入或使用文档；URL 必须作为该包 Editor C# 代码的一部分随包发布。
4. 菜单处理函数使用 `Application.OpenURL`。包若尚无 Editor 程序集，则补充仅包含 `Editor` 平台的独立 asmdef。
5. SDK 包安装到 Nova 后，Unity 编译其 Editor 程序集并自动发现 `[MenuItem]`，不需要修改主框架注册表或执行额外初始化。
6. `Open SDK URL` 顶层菜单遵守 [[PAT-155-unity-menuitem-priority-grouping|PAT-155]]：位于 `Clean Hotfix Caches` 与 `Enable Logs` 之间，并与两侧各保留 11 级 priority 差以显示分割线。
7. 每个 SDK 的 `Console + Readme` 视为一组：组内两项 priority 连续；下一 SDK 的 `Console` 必须比上一 SDK 的 `Readme` 至少高 11，从而在每组之间显示分割线。
8. SDK 包可独立安装，因此 `Enable Logs` 使用远高于 SDK 预留区间的 priority；即使只安装较后编号的 SDK 包，`Open SDK URL` 仍保持在 `Clean Hotfix Caches` 与 `Enable Logs` 之间。

## 为什么这样定

- SDK 包是 URL 的所有者；随包维护可以避免卸载 SDK 后主框架仍残留无效入口。
- 新包安装即出现入口，开发者无需查找散落的 README、书签或内部说明。
- 官方后台与官方文档是 SDK 接入、配置和排障的固定起点，统一菜单路径能降低认知成本。
- URL 与对应 SDK Editor assembly 共生，版本升级时更容易在同一包内完成复核和更新。

## 反模式

- 把所有 SDK URL 集中写入 Nova 主框架菜单文件。
- SDK 包只有 README 文件，没有 `Nova/Open SDK URL` 菜单入口。
- 只提供 Console 或只提供 Readme，缺少另一项。
- 所有 SDK 菜单项使用同一 priority，导致不同 SDK 组之间没有分割线。
- 链接到搜索结果、个人书签、二次转载或非官方聚合页。
- 新增 SDK 时依赖人工修改主框架注册表，导致包无法独立安装和卸载。
- 为了菜单入口让 Editor assembly 反向依赖不必要的 Runtime 类型。

## 来源与验证

- 规则来源：2026-08-10 用户明确要求，今后所有 SDK 模块都必须在各自 package 的 C# 中提供官方 Console 和 Readme URL，并在安装到 Nova 后自动展示菜单。
- 当前落地：AppsFlyer、TGA、Firebase、Facebook、MAX、AIHelp 六个 SDK 包分别维护 `*SDKUrlMenuItems.cs`。
- 静态验证：六个包共注册 12 个 `Nova/Open SDK URL` 菜单项；TGA 使用独立 Editor-only asmdef；各 SDK 组内 priority 连续，相邻组边界差 11，`Enable Logs` 从 `10000` 开始以容纳独立安装和后续扩展。

## 关联

- SDK 新增流程：[[PAT-33-sdk-plugin-sop|PAT-33]]
- UPM 包结构：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]
- SDK 包文档：[[PAT-109-upm-package-docs-mandatory|PAT-109]]
- Unity 菜单分组：[[PAT-155-unity-menuitem-priority-grouping|PAT-155]]
