# DebugManager

**类签名**：`internal sealed partial class DebugManager : DebugManagerBase`
**命名空间**：`NovaFramework.Runtime`

当前 `DebugManager` 不再负责旧版调试窗口树。它现在只承接一条收敛后的运行时能力链：

- 读取 `DebugManagerConfig.DiskCheckingConfigs`
- 按平台选择当前 `DiskCheckingConfig`
- 启动 / 关闭磁盘检测 `UniTask` 循环
- 命中档位后通过 `IEventManager` 发布 `DiskCheckEventData`

## 当前文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `Managers/Interfaces/IDebugManager.cs` | `IDebugManager` | 对 `DebugComponent` 暴露的最小契约 |
| `Managers/Implements/DebugManagerBase.cs` | `DebugManagerBase` | 抽象基类，定义优先级与生命周期抽象方法 |
| `Managers/DebugManagerConfig.cs` | `DebugManagerConfig` | 初始化配置，当前持有 `DiskCheckingConfigs` |
| `Managers/Implements/DebugManager.cs` | `DebugManager` | 初始化、关闭与字段清理 |
| `Managers/Implements/DebugManager.Visitors.cs` | `DebugManager` | 字段定义：配置集合、当前平台配置、事件管理器、取消令牌 |
| `Managers/Implements/DebugManager.Methods.cs` | `DebugManager` | 平台配置选择、磁盘空间查询、循环检测与事件发布 |

## 当前关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_DiskCheckingConfigs` | `List<DiskCheckingConfig>` | 全部平台配置，由 `Initialize` 传入 |
| `m_CurDiskCheckingConfig` | `DiskCheckingConfig` | 当前平台命中的磁盘检测配置 |
| `m_EventManager` | `IEventManager` | 用于发布 `DiskCheckEventData` |
| `m_DiskCheckCts` | `CancellationTokenSource` | 磁盘检测循环取消令牌 |

## 当前公开 API

```csharp
public override void Initialize(DebugManagerConfig config);
public override void Update();
public override void Shutdown();
```

## 当前工作流

### Initialize

1. 校验 `config`
2. 通过 `FrameworkManagersGroup.GetManager<IEventManager>()` 获取事件管理器
3. 缓存 `DiskCheckingConfigs`
4. 按 `Application.platform` 选中当前平台配置
5. 校验阈值列表和检测间隔列表
6. 用 `CheckDiskTotalSpace()` 回填最后一档兜底总空间
7. 若当前配置启用，则启动 `RunDiskCheckLoopAsync`

### RunDiskCheckLoopAsync

- 周期读取可用磁盘空间
- 在 `AvailableSpaces` 中命中当前档位
- 输出调试日志
- 发布 `DiskCheckEventData`
- 按当前档位对应的 `AvailableSpacesIntervals` 延迟下一轮检测

### Shutdown

- 取消并释放 `m_DiskCheckCts`
- 清空当前配置、配置列表和 `IEventManager` 引用

## 旧实现边界

当前源码里已经没有 `IDebugWindow`、`IDebugWindowGroup`、`ConsoleWindow`、`GMToolWindow`、`DebugWindowGroup` 那套运行时窗口树实现。相关页面如果仍然存在，只能视为退役说明，不能再当当前实现事实。

## 关联文档

- [DebugComponent.md](DebugComponent.md)
- [Managers/DebugManagerBase.md](Managers/DebugManagerBase.md)
- [Managers/IDebugManager.md](Managers/IDebugManager.md)
- [Managers/DebugManagerConfig.md](Managers/DebugManagerConfig.md)
- [DiskCheckEventData.md](DiskCheckEventData.md)
