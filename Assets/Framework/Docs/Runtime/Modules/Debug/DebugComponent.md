# DebugComponent

**类签名**：`[DisallowMultipleComponent] public sealed partial class DebugComponent : FrameworkComponent`  
**命名空间**：`NovaFramework.Runtime`  
**全局访问**：`Nova.Debug`

Debug 模块当前的运行时入口非常薄：它负责决定是否初始化 RuntimeDebugger，并把磁盘监控配置交给 `IDebugManager`。旧版本那套自绘 IMGUI 窗口、拖拽、滚动、日志虚拟列表已经不属于当前实现。

## 文件组成

| 文件 | 作用 |
|---|---|
| `DebugComponent.cs` | Awake / Start 主流程 |
| `DebugComponent.Visitors.cs` | Inspector 配置与只读属性 |
| `DebugComponent.Methods.cs` | `IsDebuggerActive()` 环境判断 |
| `Definitions/**` | Debug 模块原有定义与事件数据 |
| `Managers/**` | Debug 模块原有 Manager 契约、配置和实现 |
| `Windows/**` | `DebugComponent` 运行时入口文件 |
| `Core/Debugger/**` | 迁入的内置 RuntimeDebugger 实现，已改为 Nova 命名 |
| `Core/Foundation/**` | 迁入的调试器基础工具、服务注册、UI 基础组件 |
| `Core/Options/**` | 迁入的 `DebugOptions` 运行时选项容器 |

`Core/**` 下不再保留独立 Debugger/Foundation asmdef，所有运行时代码统一编译进 `NovaFramework.Runtime`。当前不再保留调试器专属 `Assets/Framework/Scripts/Editor/Debugger` 目录；Debug 模块的编辑期入口只保留框架原有 `DebugComponentInspector`。

## 工作流触发规则

Debug 模块触及运行时调试器、资源目录、程序集边界或旧插件拆分时，必须按 L2 处理：

1. 先触发 `nova-prelookup`，最小范围查询 `Minds` 中的 Debug 图谱、程序集边界和 Docs 同步规则。
2. 同轮更新 `Assets/Framework/Docs/Runtime/Modules/Debug/**` 与全局索引中的 Debug 入口。
3. 如果移动资源、删除 Editor 目录、归并 asmdef、改命名空间或公开 API，必须显式复核 `Docs` 中旧路径、旧术语、旧类名是否残留。
4. 只有产生长期决策或术语口径时才写 `Minds` 正文；普通当前事实写入 `Docs`。

本规则来自当前仓库工作流、`MOC-Debug`、`ADR-020` 与 `PAT-116`：Debug 结构变化属于模块边界与程序集边界变化，不能只改源码。

## 关键字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_DebuggerActiveType` | `DebuggerActiveType` | 控制 RuntimeDebugger 是否启用 |
| `m_MaximumConsoleEntries` | `int` | RuntimeDebugger 控台最大日志条数 |
| `m_CurManagerTypeName` | `string` | `IDebugManager` 实现类全名 |
| `m_DiskCheckingConfigs` | `List<DiskCheckingConfig>` | 各平台磁盘检测配置 |
| `m_DebugManager` | `IDebugManager` | 运行时调试管理器实例 |

## 生命周期

### Awake

1. `base.Awake()`
2. 通过 `Util.TypeCreator.Create<IDebugManager>(m_CurManagerTypeName)` 创建管理器
3. 若管理器无效，记录 `Log.Fatal`
4. 根据 `IsDebuggerActive()` 判断是否调用 `RuntimeDebugger.Init(...)`

RuntimeDebugger 初始化时会注入：

- `LogTagType = typeof(LogTag)`
- `LogTagDescriptionResolver`
- `MaximumConsoleEntries = m_MaximumConsoleEntries`

### Start

如果 `m_DebugManager != null`，则执行：

```csharp
m_DebugManager.Initialize(new DebugManagerConfig
{
    DiskCheckingConfigs = m_DiskCheckingConfigs,
});
```

## 环境命中规则

`IsDebuggerActive()` 的判断完全由 `m_DebuggerActiveType` 决定：

| 枚举值 | 命中规则 |
|---|---|
| `AlwaysEnable` | 始终启用 |
| `OnlyEnableWhenDevelopment` | 仅 `UnityEngine.Debug.isDebugBuild == true` 时启用 |
| `OnlyEnableInEditor` | 仅 `Application.isEditor == true` 时启用 |
| `AlwaysDisable` | 始终禁用 |

## 对外可见属性

```csharp
public DebuggerActiveType DebuggerActiveType { get; }
public int MaximumConsoleEntries { get; }
public string CurManagerTypeName { get; }
```

## 当前实现边界

- `DebugComponent` 不是完整调试 UI 容器，只是 RuntimeDebugger 的激活壳层。
- 磁盘检测、事件发送等逻辑在 `DebugManager`，不在 `DebugComponent`。
- 当前实现只保留 RuntimeDebugger 激活与 `DebugManager` 初始化这条薄入口，不包含自绘调试窗口与拖拽交互体系。
- Runtime 代码不得依赖 Editor 程序集；调试器运行时代码统一在 `NovaFramework.Runtime` 内，Editor 只允许通过 Inspector 或独立 Editor 工具单向引用 Runtime。

## 关联文档

- [DebugManager.md](DebugManager.md)
- [DebuggerActiveType.md](Definitions/DebuggerActiveType.md)
- [DiskCheckingConfig.md](Windows/DiskCheckingConfig.md)
- [RuntimeDebugger.md](Debugger/RuntimeDebugger.md)
- [DebugOptions.md](Debugger/DebugOptions.md)
- [DebuggerAssets.md](Debugger/DebuggerAssets.md)
