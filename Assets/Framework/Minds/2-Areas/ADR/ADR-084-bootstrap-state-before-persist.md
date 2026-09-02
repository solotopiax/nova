---
id: ADR-084
title: Persist 就绪前的框架启动状态使用受限 PlatformPlayerPrefs
summary: Persist 前启动状态使用受限 PlayerPrefs
category: arch
status: accepted
date: 2026-09-02
aliases:
  - ADR-084-bootstrap-state-before-persist
keywords:
  - ADR-084
  - PlatformPlayerPrefs
  - Nova.InstallTimeMs
  - Persist.LoadAsync
  - 启动状态
  - 安装时间
tags: [adr, nova, persist, bootstrap, playerprefs]
supersedes: []
superseded-by: []
related:
  - "[[GLO-08-datamaster-user-properties|GLO-08]]"
---

# ADR-084：Persist 就绪前的框架启动状态使用受限 PlatformPlayerPrefs

## 背景（Context）

`Nova.Persist` 是业务通用存储容器，需要完成组件获取、Manager 初始化与 `LoadAsync()` 后才能按正式契约读写。部分框架状态必须更早参与启动决策，不能在 `Nova.Awake()` 或其他引导阶段直接依赖尚未就绪的 Persist。

框架级安装时间是第一个公开消费者：它必须在 SDK 初始化前稳定可读，并统一供 DataMaster 的 `GetInstallTimeMs()`、刷新必传属性和事件用户上下文使用。Unity 没有跨平台一致的真实首次安装时间 API，因此框架只能记录首次启动近似值。

## 决策（Decision）

### 1. 启动状态与业务存档分层

- 普通业务存档必须使用 `Nova.Persist`，并等待 `Persist.LoadAsync()` 完成。
- 只有必须在 Persist 就绪前读取、体量小且结构简单的框架引导状态，才允许使用内部 `PlatformPlayerPrefs`。
- `PlatformPlayerPrefs` 不作为公共业务存档 API，不参与 Persist 的分类索引与 AES 处理。
- 模块需要文件结构、批量数据或事务语义时，仍使用其明确的 `persistentDataPath` 文件契约，不能把数据塞入启动键值区。

### 2. 框架安装时间契约

- `Nova.InstallTimeMs` 是公开只读入口，值为 13 位 UTC Unix 毫秒时间戳。
- Nova 在 `Awake()` 建立该值，静态属性同时保留惰性读取能力；它不依赖 `Nova.Persist` 或 `Persist.LoadAsync()`。
- 原始存储键固定为 `Nova.InstallTimeMs`。有效记录跨启动复用；缺失或非法时使用 `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` 重建并立即落盘。
- 该值表示 Nova 首次成功建立记录的时间，不承诺等同于应用商店或操作系统提供的真实安装时间；清除应用数据后会重新建立。
- 不读取、不迁移旧的 DataMaster 私有键 `Nova_DataMaster_InstallTimeMs`。

### 3. DataMaster 只消费框架值

- DataMaster 的 `GetInstallTimeMs()` 统一返回 `Nova.InstallTimeMs`。
- 每次刷新前自动注入的 `install_time` 与事件上下文中的 `InstallTimeMs` 使用同一来源，避免 SDK 内部形成第二份生命周期不同的安装时间。
- `install_time` 的 DataMaster 字段口径继续由 [[GLO-08-datamaster-user-properties|GLO-08]] 定义。

## 后果（Consequences）

### 正面

- `Awake()` 阶段不会越过 Persist 的初始化与加载契约。
- 框架、SDK 与业务读取到同一个安装时间，单位和持久化生命周期一致。
- 启动期轻量状态拥有明确边界，不会被误解为第二套通用业务存档系统。

### 代价与限制

- `PlatformPlayerPrefs` 数据不享受 Persist 的 AES、分类、索引和统一清理语义。
- “安装时间”是首次启动近似值；卸载、清除数据以及平台备份恢复行为可能影响它的生命周期。
- 新增其他启动状态时仍需逐项证明其必须早于 Persist，而不能因本决策自动获得绕过资格。

## 被排除方案（Rejected Alternatives）

- **在 `Nova.Awake()` 直接使用 `Nova.Persist`**：此时组件静态入口与 Manager 加载尚未完成，违反 Persist 生命周期。
- **等待 Persist 后才建立安装时间**：会让早期 SDK 或启动决策拿不到统一值，并把引导元数据绑定到业务存档加载结果。
- **继续由 DataMaster 保存独立安装时间**：会产生框架与 SDK 两套来源，其他模块也无法复用。
- **迁移 `Nova_DataMaster_InstallTimeMs`**：旧键属于 DataMaster 私有实现，本次明确不保留兼容逻辑。

## 验证依据（Verification）

- `NovaInstallTimeTests` 覆盖首次生成并持久化 13 位毫秒时间戳、已有有效记录跨读取复用。
- DataMaster 源码核对确认 `GetInstallTimeMs()` 返回 `Nova.InstallTimeMs`，刷新属性与事件上下文均复用该入口。
- C# 生成工程构建通过且为 0 error；`git diff --check` 通过。
- 本轮未运行 Nova2 Unity Test Runner：该工程已被另一个 Unity 进程占用，可用 MCP 当前指向另一份 Nova checkout。

## 当前事实来源（Sources）

- `Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.cs`
- `Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Visitors.cs`
- `Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Methods.cs`
- `Assets/Tests/Editor/NovaInstallTimeTests.cs`
- `UPMPackages/com.solotopia.nova.framework.sdk.datamaster.abtest/Nova/Scripts/Runtime/DataMasterPlugin.cs`
- `UPMPackages/com.solotopia.nova.framework.sdk.datamaster.abtest/Nova/Scripts/Runtime/DataMasterPlugin.Methods.cs`
- `Assets/Framework/Docs/Runtime/Modules/Persist/PersistComponent.md`
- `UPMPackages/com.solotopia.nova.framework.sdk.datamaster.abtest/Nova/Doc/DataMasterPlugin.md`
