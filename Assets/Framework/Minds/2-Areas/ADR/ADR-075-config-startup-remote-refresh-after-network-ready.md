---
id: ADR-075
title: 启动应用配置由 Config 非阻塞等待 Network Ready 后单次刷新
status: accepted
date: 2026-07-27
summary: Config 后台等待网络路由就绪并单次刷新磁盘快照
category: arch
aliases:
  - config-startup-remote-refresh-after-network-ready
keywords:
  - ADR-075
  - AppConfig
  - Custom
  - CustomConfigCmdName
  - CustomName
  - JSONPath
  - INetworkReadySignal
  - 启动应用配置
  - 远端配置磁盘缓存
tags:
  - adr
  - nova
  - framework
  - config
  - network
  - startup
supersedes: []
superseded-by: []
related:
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[ADR-057-network-kit-base-sink-into-framework|ADR-057]]"
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-43-optional-remote-check-tolerance|PAT-43]]"
---

# ADR-075：启动应用配置由 Config 非阻塞等待 Network Ready 后单次刷新

## 背景

`ConfigRuntimeSO` 在业务 DLL 与 Network 路由加载前完成加载，而远端应用配置必须通过 NetCmd 解析并发送。
若 `ConfigManager.LoadAsync()` 同步等待 Network，会阻塞或倒置既有启动依赖；若 Network 加载成功后主动调用
Config，又会让原本只负责 HostKey / NetCmd 路由的加载入口产生隐藏副作用。

同时，项目升级不能要求业务 Procedure 增加调用，也不能扩张公开 `IConfigManager` / `INetworkManager`，否则已有
自定义 Manager 实现会在框架升级后失去源码兼容性。

## 决策

1. `ConfigManager.LoadAsync()` 只加载 `ConfigRuntimeSO`、建立本地默认快照并恢复磁盘缓存，随后立即完成。
2. `CustomConfigCmdName` 与 `CustomName` 都不是默认配置。仅当两者均为非空白字符串时，Config 才读取对应磁盘缓存、
   启动后台等待并进入远端请求链；任一项未填写时不等待 Network、不解析 NetCmd、不发送请求。字段由旧开发版
   `GetCustomConfigCmdName` 直接改名为 `CustomConfigCmdName`，不保留旧序列化字段或 API 兼容层。
3. 启用自动刷新时，Config 在内部通过只读 `INetworkReadySignal` 等待标准 NetworkManager 首次成功构建
   HostKey / NetCmd 路由。
4. Network 只在路由构建成功后完成就绪信号，不主动调用 Config；Config 就绪后每次框架启动自动请求一次，
   失败不重试，显式手动刷新仍可通过 `Nova.Config.RefreshAppConfigAsync()` 发起。
5. 自动刷新属于可选增强：配置缺失、Network Manager/就绪信号不存在、NetCmd 无法解析、请求异常、响应失败、
   JSON 非法或缓存不可用时都不得阻断启动，也不得污染当前有效快照。显式手动刷新遇到这些情况安全返回 `false`。
6. `ConfigRuntimeSO.Custom` 只定义本地 JSONPath/string 默认值，不限制云端字段。远端响应必须是以 object 为根的
   完整 JSON，可包含嵌套对象、数组和本地未声明的任意字段；合法结果原子写入磁盘后完整替换内存快照，失败或
   非法 JSON 保留当前快照。
7. 业务通过 `Nova.Config.Custom.GetString / GetInt / GetFloat / GetBool` 按路径读取。顺序为云端快照、本地路径值、
   调用方默认值；远端非 null 值转换失败时回退本地，远端显式 `null` 直接返回调用方默认值；不发送配置变更事件。
8. 网络刷新与路径 getter 由框架内部 `IAppConfigManager` 与 `INetworkReadySignal` 承接，不向公开 Manager
   接口增加方法；公开配置属性按最终命名直接采用 `Custom`，不保留旧开发版 `CustomConfigs` 迁移层。

## 后果

### 正面

- 项目原有 Procedure 和启动调用无需修改，Config 加载与游戏启动不等待远端网络。
- Config 继续拥有配置状态，Network 继续只拥有路由与发送能力，跨模块职责不反转。
- 弱网、离线和协议错误都有稳定回退；服务端删除字段会随完整快照替换而消失，不会残留旧的部分缓存。
- 云端可以独立新增嵌套字段或数组，无需先发布一份本地 key 清单。
- 公开 Manager 接口不增加刷新或 getter 方法，业务读取统一收口在 `Nova.Config.Custom`。

### 负面

- 自定义 NetworkManager 若不具备框架内部就绪信号，只能使用本地/磁盘值并跳过自动刷新。
- `CustomConfigCmdName` 或 `CustomName` 为空时远端刷新完全关闭；这两个字段不提供隐式默认值。
- 已接入旧开发版 `GetCustomConfigCmdName` 字段的资产或代码需要直接改为 `CustomConfigCmdName`；不提供兼容层。
- 已接入旧开发版 `CustomConfigs` 属性的自定义 ConfigManager 需要直接改为 `Custom`；不提供序列化或 API 迁移层。
- 第一版每次启动只自动尝试一次，不提供后台重试、定时刷新或变更事件。
- 本地默认值仍以字符串行维护；需要本地构造的路径仅支持属性段和非负数组下标。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| Config.LoadAsync 同步等待 Network 并拉取 | 会阻塞启动，并在 Network 尚未加载路由时形成依赖倒置 |
| 业务 Procedure 在 Network.LoadAsync 后显式刷新 | 每个项目都要改启动代码，无法无感升级且容易漏接 |
| Network.LoadAsync 成功后主动调用 Config | Network 加载产生隐藏 Config 副作用，职责反转 |
| 扩张公开 IConfigManager / INetworkManager | 破坏项目已有自定义 Manager 的源码兼容性 |
| 用本地 key 白名单裁剪云端 JSON | 阻止后台独立新增字段，要求云端与客户端预先对齐，违背 Custom 的动态配置用途 |
| 远端部分增量覆盖 | 服务端删除字段会残留旧缓存，无法表达一份权威完整快照 |

## 验证依据

- 协议与生成物：`Assets/Framework/Protos/pb_net_app_custom_config.proto`、
  `Assets/Framework/Scripts/Runtime/Modules/Network/Protos/PbNetAppCustomConfig.cs`。
- Config 实现：`ConfigManager` 的本地快照、磁盘原子缓存、后台等待、单次刷新和类型化读取。
- Network 实现：标准 `NetworkManager` 仅在路由成功构建后完成 `INetworkReadySignal`。
- EditMode：`AppConfigRuntimeTests` 覆盖字段默认值、导出、嵌套对象、数组、云端独有路径、完整快照替换、
  显式 null、非法 JSON、磁盘缓存、类型化回退、接口兼容、Prefab 绑定和 Proto 输出路径。
- PlayMode：MainDemo 可进入 `ProcedurePlaying`；未阻塞启动，自动刷新真实发送点每次启动只出现一次；
  2.9 按钮可手动刷新并通过 `Nova.Config.Custom` 显示当前路径值。

## 关联

- [[ADR-017-component-manager-isolation|ADR-017]]：外部能力继续由 Component 门面提供，不暴露 Manager。
- [[ADR-057-network-kit-base-sink-into-framework|ADR-057]]：通用网络协议与路由能力归框架 Network 模块。
- [[PAT-27-config-no-serialize|PAT-27]]：Config DTO 只承载数据，缓存与刷新行为留在 Manager。
- [[PAT-43-optional-remote-check-tolerance|PAT-43]]：可选远端检查失败时降级，不阻断后续启动。
