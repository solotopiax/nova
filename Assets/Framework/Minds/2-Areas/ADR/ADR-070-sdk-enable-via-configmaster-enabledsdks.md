---
id: ADR-070
title: SDK 启用真相源归 ConfigMaster.EnabledSDKs，插件统一按启用配置实例化
summary: EnabledSDKs 唯一启用源，插件按配置统一实例化
category: module
status: accepted
date: 2026-07-05
source: cur-session
aliases:
  - ADR-070-sdk-enable-via-configmaster-enabledsdks
  - sdk-enable-configmaster
keywords:
  - ADR-070
  - EnabledSDKs
  - SDK启用真相源
  - ConfigMaster
tags: [adr, nova, framework, sdk, config]
supersedes: []
superseded-by: []
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[ADR-056-runtimeprovider-config-select-via-workspaceactive|ADR-056]]"
---

# ADR-070：SDK 启用真相源归 ConfigMaster.EnabledSDKs

## 背景（Context）

SDK 模块存在两个正交维度，此前只有能力族插件能同时满足：

- **实例化维度**：`SDKComponent.m_PluginEntries`（插件类型 + Enabled）→ `SDKManager.Initialize` 反射实例化。面板 `SDKComponentInspector` 按**能力族**（普通埋点 / 变现埋点 / 广告 / 归因 / 账号 / 云服务 / 支付）分组绘制单选，`SyncEntries` 扫描所有 `ISDKPlugin` 追加条目，非族插件（不实现任何能力接口）**不属任何族、面板不显示、Enabled 默认 false**。
- **配置注入维度**：`ConfigMaster.EnabledSDKs` → 导出 `ConfigRuntime.EnabledSDKConfigs` → `IConfigManager.GetSDKPluginConfig(type)` 按 `RequiredConfigType` 注入。

后果：像 DataMaster 这类纯远程配置 / ABTest SDK（`DataMasterPlugin` 只继承 `SDKPluginBase`、不实现能力接口），在 ConfigWindow 勾选了 `DataMasterPluginConfig`（配置维度满足），却因面板无法勾选启用（实例化维度不满足）而从未实例化，`TryGet<DataMasterPlugin>` 恒为 false。

## 决策（Decision）

**`ConfigMaster.EnabledSDKs` 作为 SDK 启用的唯一真相源。** 在 `SDKManager.InitializeAsync`（Config 已加载后）使用 `InstantiateEnabledPluginsFromConfig` 作为唯一实例化入口：

- 取 `IConfigManager.GetAllPluginConfigs()` 得已启用配置类型集合；
- 反射枚举当前已加载程序集中可实例化的 `ISDKPlugin` 实现（`EnumerateConcreteSDKPluginTypes`）；
- 对 `RequiredConfigType` 命中启用集合、且尚未覆盖该 ConfigType 的插件，实例化并纳入 Priority 分桶。

能力族与非能力族 SDK 都不再通过 `m_PluginEntries` 决定运行时启用；`m_PluginEntries` 仅保留 SDK 面板选型元数据。所有 SDK 只需在 ConfigMaster 勾选启用即可被实例化。

**时机铁律**：插件实例化必须放在 `InitializeAsync`（对应 Procedure 预加载 Step「初始化 SDK」，此时 Config 已加载），**不能**放在 `SDKComponent.Start` 期的 `Initialize`——后者早于 Config 加载，`GetAllPluginConfigs()` 返回空。

## 后果（Consequences）

### 正面
- 非能力族 SDK（远程配置 / ABTest 类）无需为凑面板归类而实现无关能力接口，语义诚实。
- ConfigMaster 成为唯一启用入口，与配置注入同源，消除"配了 config 却没实例化"的割裂。
- 能力族 SDK 零影响（ConfigType 去重）。

### 负面
- `InitializeAsync` 启动期一次性反射扫描全部已加载程序集的 `ISDKPlugin` 实现，并临时实例化候选以读 `RequiredConfigType`，启动开销小幅增加（SDK 插件数量少，可接受）。
- IL2CPP / HybridCLR 下的全程序集 `GetTypes()` 行为需真机回归（Editor 已验证）。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 给非族插件实现某能力接口（如 IRemoteConfigPlugin）挤进能力族 | 为凑面板归类扭曲语义；DataMaster 的 ABTest 二级寻址/曝光超出扁平接口表达力；用户明确否决 |
| SDK 面板加「通用 / 其他」兜底族 | 改 Editor，且启用语义仍留在面板而非 ConfigMaster，未统一真相源 |

## 验证依据（Verification）

- `Assets/Framework/Scripts/Runtime/Modules/SDK/Managers/Implements/SDKManager.cs`（Initialize / InitializeAsync）
- `SDKManager.Methods.cs`（`InstantiateEnabledPluginsFromConfig` / `EnumerateConcreteSDKPluginTypes`）
- DataMasterDemo Play 实测：日志「SDK 插件按 ConfigMaster 启用实例化：…DataMasterPlugin」→「DataMaster 初始化完成」→ 点按钮 `TryGet` 成功、上报 `demo_button_click success`。

## 来源（Origin）

- 会话日期：2026-07-05
- 关键对话节选：
  > 用户：真正的启用开关应该放在 ConfigMaster 中。所以你并不需要针对 DataMaster 封装在 SDK 面板上的接口。
  > 用户：让我改框架：EnabledSDKs 驱动实例化。
  > AI：SDKManager.InitializeAsync 使用 InstantiateEnabledPluginsFromConfig，按 EnabledSDKs 统一实例化 SDK 插件；时机在 Config 加载后。

## 关联

- 相关 ADR：[[ADR-022-sdk-plugin-architecture|ADR-022]]（SDK 插件架构基础；本 ADR 补充其启用机制。注：ADR-022 决策 5 的 `SetConfig` 运行时注入已演进为 ConfigMaster / IConfigManager 拉取）
- 相关 ADR：[[ADR-056-runtimeprovider-config-select-via-workspaceactive|ADR-056]]（ConfigMaster 经 WorkspaceActive 选取）
- 相关 Pattern：[[PAT-33-sdk-plugin-sop|PAT-33]]（SDK 插件接入 SOP）
