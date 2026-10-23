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

## 平台、失败与事件边界

平台配置按列表位置选择：WindowsEditor/OSXEditor 取第 0 项、Android 取第 1 项、IPhonePlayer 取第 2 项；不读取 PlatformName 匹配。列表不足三项、选中项为空或其他平台均不启循环。LinuxEditor、Standalone 与 WebGL 没有独立命中项。

配置与列表直接保存引用，没有快照。初始化只检查阈值非空及两列表等长，不检查阈值升序、间隔为正或有限值；随后会原地把最后阈值写成总磁盘空间。重复 Initialize 在提前返回/禁用分支不会统一取消旧循环；只有成功进入启用分支才更换令牌。

查询需 NOVA_SIMPLEDISKUTILS，当前宏分支支持 macOS、Windows 和 Android，未支持路径返回 -1；即使 iOS 可选中配置，空间查询也可能为 -1。Windows 从 persistentDataPath 截取根路径，Android 向插件传 true。Busy 查询保留但主循环不消费。

循环按第一个 `availableSpace <= threshold` 命中，否则使用末项；-1 同样参与比较，不会被自动当作失败过滤。它先记日志、Fire 池化事件，再等待对应间隔。只捕获 Delay 的 OperationCanceledException；查询、并发改列表、Fire、TimeSpan 或其他 Delay 错误可中断 Forget 循环，Update 的空实现不会重启它。

Shutdown 按顺序 Cancel/Dispose 并清引用，异常可阻止后续清理；不关闭 RuntimeDebugger UI、清 Console 或提交报告。调试 UI 和磁盘循环是两条独立生命周期。

## 旧实现边界

当前源码里已经没有 `IDebugWindow`、`IDebugWindowGroup`、`ConsoleWindow`、`GMToolWindow`、`DebugWindowGroup` 那套运行时窗口树实现。相关页面如果仍然存在，只能视为退役说明，不能再当当前实现事实。

## 关联文档

- [DebugComponent.md](DebugComponent.md)
- [Managers/DebugManagerBase.md](Managers/DebugManagerBase.md)
- [Managers/IDebugManager.md](Managers/IDebugManager.md)
- [Managers/DebugManagerConfig.md](Managers/DebugManagerConfig.md)
- [DiskCheckEventData.md](DiskCheckEventData.md)
