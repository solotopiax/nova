---
id: ADR-022
title: SDK 模块插件架构决策（去 Composite + 具体类型驱动 + SetConfig + UPM 边界 + 去 MonoBehaviour + UniTask 主线程切换）
status: accepted
date: 2026-05-18
summary: SDK插件以UPM包+ISDKPlugin注册架构
category: module
aliases:
  - sdk-plugin-architecture
  - sdk-adrs
keywords: [ADR-022, sdk-adrs, sdk-plugin-architecture]
tags:
  - adr
  - nova
  - framework
  - sdk
  - architecture
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-008-managerbase-internal-abstract]]"
  - "[[ADR-018-json-via-util-json]]"
  - "[[PAT-27-config-no-serialize]]"
---

# ADR-022：SDK 模块插件架构决策

> 本 ADR 整合 SDK 模块 S 级重构期间拍板的 6 条决策（原 SDK/ARCHITECTURE.md §18 ADR-001~006），统一登记到长期决策档案区。原 ADR-007（Diagnostics 中间层）已废弃，独立走 ARC-DRAFT。

## 背景

`Assets/Framework/Scripts/Runtime/Modules/SDK/` 旧实现存在三层中间件、字符串键查询、MonoBehaviour Plugin 和 Inspector 直填机密等问题，因此收敛为纯 C# Plugin + 具体类型查询 + 运行时注入。

## 决策

### 决策 1：废弃 Composite + Service 中间件

业务接口（`ITrackPlugin` / `IAdPlugin` / `IPurchasePlugin` 等）**直接继承 `ISDKPlugin`**，删除 `BaseComposite<T>` 与 `CompositeDispatchMode`。

- **理由**：两层映射复杂度高于收益，调度策略也不统一。
- **代价**：业务层需显式 `GetAll<T>` 扇出
- **被排除**：保留 Composite 兼容层 → 双接口并存、新旧混用、新人困惑

### 决策 2：具体类型驱动查询 `Get<T>()`

`SDKManager` 持 `Dictionary<Type, ISDKPlugin>`，废弃 `GetPlugin(string name)` 与 `GetPlugin<T>()` 遍历匹配。

- **理由**：强类型 IDE 重构友好；字符串键易拼写错误、重命名漏改
- **代价**：业务代码必须 using 具体 Plugin 命名空间 → 通过独立 UPM 包隔离可控
- **被排除**：`Get(string)` 保留兜底 → 诱导滥用

### 决策 3：独立 UPM 包边界

每个 SDK 对接一个 UPM 包 `com.nova.sdk.<vendor>`，**仅依赖核心包**；子包之间零依赖、不得修改/继承 `SDKManager` / `SDKComponent`、不得创建新业务接口（必要时走 ADR 评审进核心包）。

- **理由**：按需安装、依赖隔离、版本独立、机密隔离。
- **代价**：发布流水线复杂（Verdaccio 多包管理）→ 已有 `Tools/Publish/publish_packages.py`
- **被排除**：单 asmdef + `#if` 宏 → 代码膨胀、宏管理复杂

### 决策 4：Plugin 去 MonoBehaviour 化

`SDKPluginBase` 继承纯 C# 对象（**非 MonoBehaviour**），通过 Inspector `SDKPluginEntry` + 反射 `Activator.CreateInstance` 实例化。

- **理由**：无场景依赖，支持单测，配置集中。
- **代价**：失去 Unity 单 Plugin 级 Inspector 编辑（如 AppID Drag-Drop）→ 由 SetConfig + Config 模块覆盖
- **被排除**：保留 MonoBehaviour → 场景预制体依赖、多环境切换需多 Prefab

### 决策 5：SetConfig 物料注入

运行时通过 `Nova.SDK.SetConfig<TPlugin>(ISDKPluginConfig)` 注入 AppID/Secret 等机密；**Inspector 不显示任何机密字段**，只管「哪些 Plugin 启用 + Priority」。

- **理由**：机密不入 Git，多环境通过 Config 表切换。
- **代价**：业务层必须保证 `SetConfig → InitializeAsync` 顺序；之后调用 `SetConfig` 仅 `Log.Warning` 忽略；Plugin 声明 `ConfigType != null` 但未注入则 `InitializeAsync` 抛 `SDKConfigMissingException`
- **被排除**：Inspector 直填 → 安全/多环境/热修复全部受损

### 决策 6：UniTask 主线程切换（不自建 MainThreadDispatcher）

Plugin 内部允许任意线程执行（iOS/Android Native 回调常在非主线程），但 UniTask 返回点 + Event/Action 触发前**必须 `await UniTask.SwitchToMainThread(ct)`**。框架不提供 MainThreadDispatcher。

- **理由**：UniTask 原生支持切主线程，现有网络模块也已采用同模式。
- **代价**：强依赖 UniTask 不变更该 API（低风险）
- **被排除**：自建 Dispatcher → 重复造轮子，维护成本高无收益

## 引申约束

- **Plugin 实现红线**：纯 C# 类、显式 `Priority`、`ConfigType` 虚属性声明依赖、`OnInitializeAsync` / `OnDisposeAsync` 必须主线程完成
- **业务消费红线**：先 `SetConfig`，再 `await Nova.SDK.InitializeTask`，最后 `Get<T>` / `GetAll<TInterface>`；查询 API 仅主线程调用
- **失败隔离**：单 Plugin 初始化失败仅置 `IsAvailable=false`，不影响其他 Plugin 与主流程；失败必须 `Log.Error` 留痕
- **Missing 类型容忍**：`Type.GetType(entry.TypeName)==null` 时 `entry.IsMissing=true`，Editor 红字 + 「清理 Missing」按钮，避免误清用户已配 Priority

## 反模式

- ❌ Plugin 继承 `MonoBehaviour` 或挂在 Prefab 上（违反决策 4）
- ❌ `SDKManager.Get(string name)` 字符串查询（违反决策 2）
- ❌ Inspector 字段直填 AppID/Secret 提交 Git（违反决策 5）
- ❌ Plugin 完成回调未切主线程，业务侧拿到非主线程 callback（违反决策 6）
- ❌ 子 UPM 包反向引用其他子包（违反决策 3 平级零依赖）

## 来源

- 文档：`Assets/Framework/Docs/Runtime/Modules/SDK/ARCHITECTURE.md` §18 ADR 摘要（L1109-1149）+ §7.1 时序约束 + §8 线程模型
- 用户决策：2026-04 SDK S 级重构期间拍板，本次 docs 迁移识别为应入 ADR

---
