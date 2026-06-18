# Nova Framework Debug 模块技术文档

> **路径：** `Assets/Framework/Scripts/Runtime/Modules/Debug/`
> **用途：** 运行时调试面板，基于 IMGUI (OnGUI) 实现。功能平移到 RuntimeDebuggerger 时以本文件为技术基础。
> **关联文档：** `Assets/Framework/Docs/Runtime/Modules/Debug/DebuggerLegacyReference.md`（迁移参考）

---

## 目录结构（56 个 .cs 文件，约 9211 行）

```
Debug/
├── Definitions/            数据结构、工具类、配置
│   ├── DebugActiveWindowType.cs    enum 窗口激活策略
│   ├── DebugWindowMode.cs          enum 窗口显示模式
│   ├── DevicePerformance.cs        static 设备性能评级工具
│   ├── DevicePerformanceData.cs    [Serializable] 性能评级配置
│   ├── DiskCheckEventData.cs       EventData 磁盘检测事件数据
│   ├── DiskCheckingConfig.cs       [Serializable] 磁盘检测配置
│   ├── FpsCounter.cs               FPS 帧率计数（指数移动平均）
│   ├── GMToolsLib.cs               ScriptableObject GM 工具库配置
│   ├── LogNode.cs                  IReference 日志节点（对象池）
│   ├── RamCounter.cs               RAM 内存采样计数
│   └── ScrollableDebugWindowBase.cs    abstract IDebugWindow 滚动窗口基类
│
├── Interfaces/             协议定义
│   ├── IDebugWindow.cs             调试窗口协议（Initialize/Draw/Update）
│   ├── IDebugWindowGroup.cs        窗口组管理协议（继承 IDebugWindow）
│   ├── IDebugContext.cs            UI 上下文协议（状态+输入控制）
│   ├── IDiskUtilProvider.cs        磁盘空间查询抽象（跨平台）
│   └── IDebugManager.cs            管理器公开契约（入口接口）
│
├── Managers/               三层 Manager 继承链
│   ├── DebugManagerBase.cs         internal abstract，Priority=17
│   ├── DebugManager.cs             internal sealed partial，主实现
│   ├── DebugManager.Visitors.cs    partial：字段/属性
│   ├── DebugManager.Methods.cs     partial：内部方法
│   ├── DebugManager.DebugWindowGroup.cs   nested sealed：窗口容器
│   ├── DebugManagerConfig.cs       public 配置数据类
│   └── IDebugManager.cs            public 接口（见 Interfaces/ 有同名，这是 Managers/ 层的）
│
└── Windows/                UI 组件层（DebugComponent partial 集合）
    ├── DebugComponent.cs                   MonoBehaviour 入口
    ├── DebugComponent.Visitors.cs          字段/属性/配置参数
    ├── DebugComponent.FrameworkRefs.cs     框架依赖引用
    ├── DebugComponent.Definitions.cs       枚举（Direction 等）
    ├── DebugComponent.SubWindows.cs        所有窗口实例化
    ├── DebugComponent.ConsoleWindow.cs     内嵌 ConsoleWindow : IDebugWindow
    ├── DebugComponent.DiskUtil.cs          NullDiskUtilProvider / SimpleDiskUtilProvider
    ├── DebugComponent.DiskAndPerformance.cs    磁盘+性能监控逻辑
    ├── DebugComponent.MetricsAndLogUI.cs       FPS/RAM UI 绘制
    ├── DebugComponent.RuntimeFlow.cs           运行时流程（Init/Update）
    ├── DebugComponent.NovaLabel.cs             标签系统
    ├── DebugComponent.WebGLKeyboard.cs         WebGL 键盘基类
    ├── DebugComponent.WebGLKeyboardForWX.cs    微信 WebGL 键盘
    ├── DebugComponent.WebGLKeyboardForAlipay.cs
    ├── DebugComponent.WebGLKeyboardForDY.cs    抖音 WebGL 键盘
    ├── DebugComponent.GMToolWindow.cs          GM 工具窗口
    │
    └── 信息窗口（继承 ScrollableDebugWindowBase）
        ├── DebugComponent.SystemInformationWindow.cs
        ├── DebugComponent.EnvironmentInformationWindow.cs
        ├── DebugComponent.ScreenInformationWindow.cs
        ├── DebugComponent.GraphicsInformationWindow.cs
        ├── DebugComponent.InputSummaryInformationWindow.cs
        ├── DebugComponent.InputTouchInformationWindow.cs
        ├── DebugComponent.InputAccelerationInformationWindow.cs
        ├── DebugComponent.InputGyroscopeInformationWindow.cs
        ├── DebugComponent.InputCompassInformationWindow.cs
        ├── DebugComponent.PathInformationWindow.cs
        ├── DebugComponent.SceneInformationWindow.cs
        ├── DebugComponent.TimeInformationWindow.cs
        ├── DebugComponent.QualityInformationWindow.cs
        ├── DebugComponent.ProfilerInformationWindow.cs
        ├── DebugComponent.WebPlayerInformationWindow.cs
        ├── DebugComponent.RuntimeMemoryInformationWindow.cs
        ├── DebugComponent.RuntimeMemoryInformationWindow.Sample.cs   nested Sample 类
        ├── DebugComponent.RuntimeMemorySummaryWindow.cs
        └── DebugComponent.RuntimeMemorySummaryWindow.Record.cs       nested Record 类
```

---

## 接口层（Interfaces/）

### `IDebugWindow`
所有调试窗口必须实现的协议。
```csharp
namespace NovaFramework.Runtime
public interface IDebugWindow
{
    void Initialize(IDebugContext context, params object[] args);
    void Shutdown();
    void OnEnter();               // 窗口被选中时
    void OnLeave();               // 离开当前窗口时
    void OnUpdate(float elapseSeconds, float realElapseSeconds);
    void OnDraw(IDebugContext context, float offsetY);   // IMGUI 绘制入口
    void OnTouchDrag();           // 触摸拖动回调
}
```

### `IDebugWindowGroup : IDebugWindow`
窗口组（多窗口容器），本身也是一个窗口。
```csharp
public interface IDebugWindowGroup : IDebugWindow
{
    int DebugWindowCount { get; }
    int SelectedIndex { get; set; }
    IDebugWindow SelectedWindow { get; }
    string SelectedWindowName { get; }
    string[] GetDebugWindowNames();
    string[] GetFormattedWindowNames();    // 带计数徽标的名称
    IDebugWindow GetDebugWindow(string path);
    void RegisterDebugWindow(string path, IDebugWindow debugWindow);
}
```

### `IDebugContext`
UI 上下文，Manager 实现此接口，窗口通过它访问所有状态与 UI 元素。
```csharp
public interface IDebugContext
{
    IDebugWindow ActiveWindow { get; set; }
    Transform DebugWindowRoot { get; set; }
    DebugWindowMode WinMode { get; set; }
    float CustomButtonHeight { get; set; }
    ScrollRect LogScrollRect { get; set; }
    Scrollbar LogScrollBarRect { get; set; }
    Rect LabelSelPopRect { get; set; }
    string SelLogString { get; set; }
    void RegisterDebugWindow(string path, IDebugWindow debugWindow, params object[] args);
    bool SelectDebugWindow(string path);
    void ControlInput(bool isControl);    // 控制输入锁定
}
```

### `IDebugManager`
外部访问 Debug 系统的唯一公开契约。
```csharp
public interface IDebugManager
{
    IDebugWindow ActiveWindow { get; }
    Transform DebugWindowRoot { get; }
    IDebugContext Context { get; }
    float FPS { get; }
    float RAM { get; }
    float AllocatedRam { get; }
    DebugWindowMode WinMode { get; }
    bool UseDevicePerformance { get; }
    bool UseDiskChecking { get; }
    long CachedDiskAvailable { get; }
    long CachedDiskBusy { get; }
    long CachedDiskTotal { get; }
    bool IsInWhiteDevice { get; }
    void Initialize(DebugManagerConfig config);
    void Update(float elapseSeconds, float realElapseSeconds);
    void Shutdown();
    bool IsDebugWinEnable();
    void RegisterDebugWindow(string path, IDebugWindow debugWindow, params object[] args);
    IDebugWindow GetDebugWindow(string path);
    bool SelectDebugWindow(string path);
}
```

### `IDiskUtilProvider`
跨平台磁盘空间查询抽象。
```csharp
public interface IDiskUtilProvider
{
    long GetAvailableSpace();    // 可用空间，字节
    long GetBusySpace();         // 已用空间，字节
    long GetTotalSpace();        // 总空间，字节
}
// 实现：
//   NullDiskUtilProvider  → 全部返回 -1（默认，无外部库时）
//   SimpleDiskUtilProvider → 调用 SimpleDiskUtils 第三方库（#if NOVA_SIMPLEDISKUTILS）
```

---

## 数据结构层（Definitions/）

### 枚举

#### `DebugActiveWindowType`
控制调试窗口在何种环境下激活（Inspector 可配置）。
```csharp
public enum DebugActiveWindowType
{
    AlwaysOpen,                  // 所有环境均开启
    OnlyOpenWhenDevelopment,     // 仅开发模式
    OnlyOpenInEditor,            // 仅编辑器
    AlwaysClose,                 // 永不开启
}
```

#### `DebugWindowMode`
调试面板当前的 UI 尺寸模式。
```csharp
public enum DebugWindowMode
{
    MiniWindow,         // 最小化（仅 FPS/RAM 条）
    MiddleWindow,       // 中等（常用信息）
    FullWindow,         // 全屏
    LogDetailsWindow,   // 日志详情展开
}
```

### `FpsCounter`
帧率计数器，内部使用指数移动平均平滑波动。
```csharp
public sealed class FpsCounter
{
    // 构造
    FpsCounter(float updateInterval)    // 采样间隔，默认 0.5 秒

    // 驱动
    void Update(float elapseSeconds, float realElapseSeconds)

    // 属性
    float CurrentFps { get; }           // 当前采样周期 FPS
    float AverageFps { get; }           // 指数平均 FPS (α=0.1)
    float CurrentMsPerFrame { get; }    // 当前 ms/frame
    float AverageMsPerFrame { get; }    // 平均 ms/frame
}
// 平滑公式：AverageFps = AverageFps * 0.9f + CurrentFps * 0.1f
```

### `RamCounter`
内存采样计数器，每 1 秒调用一次 Profiler API。
```csharp
public sealed class RamCounter
{
    void Update(float elapseSeconds, float realElapseSeconds)

    float AllocatedRam { get; }         // 已分配内存 MB
    float ReservedRam { get; }          // 保留内存 MB
    float UnusedReservedRam { get; }    // 未使用的保留内存 MB
    float AllocatedMonoRam { get; }     // Mono 已用内存 MB
    float ReservedMonoRam { get; }      // Mono 保留内存 MB
}
// 来源：Profiler.GetTotalAllocatedMemoryLong() / GetMonoUsedSizeLong() 等
// 单位转换：字节 ÷ 1048576 = MB
```

### `LogNode : IReference`
日志记录节点，通过 `ReferencePool` 管理，减少 GC。
```csharp
public sealed class LogNode : IReference
{
    // 字段（由 Create 填充）
    LogLevel LogType
    string LogMessage
    string StackTrack
    int FrameCount
    ulong LogIndex             // 全局递增序号
    List<int> LabelList        // 关联标签索引（用于过滤）

    // 工厂方法
    static LogNode Create(LogLevel logType, string logMessage, string stackTrack,
                          ulong logIndex, List<int> labelList)
    static LogNode CreateThreadedNode(LogLevel logType, string logMessage, string stackTrack,
                                      int frameCount, ulong logIndex, List<int> labelList)
    // 线程安全计数刷新（Interlocked）
    void RefreshCount()
    // IReference 协议
    void Clear()
}
```

### `ScrollableDebugWindowBase : IDebugWindow`
所有"信息窗口"的共同基类，提供滚动列表、字节格式化等公共能力。
```csharp
public abstract class ScrollableDebugWindowBase : IDebugWindow
{
    // 子类实现
    protected abstract void OnDrawScrollableWindow(IDebugContext context, float offsetY);

    // 公共绘制工具
    protected void DrawItem(string title, string value)         // 绘制键值对行
    protected void DrawItem(string title, string value, Color color)

    // 字节格式化（带惰性缓存）
    protected string GetByteLengthString(long byteLength)
    // → "1.23 KB" / "456.78 MB" / "1.23 GB"

    // IDebugWindow 默认空实现（子类按需 override）
    public virtual void Initialize(IDebugContext context, params object[] args) { }
    public virtual void Shutdown() { }
    public virtual void OnEnter() { }
    public virtual void OnLeave() { }
    public virtual void OnUpdate(float, float) { }
    public virtual void OnTouchDrag() { }
    public void OnDraw(IDebugContext context, float offsetY)    // 调用 OnDrawScrollableWindow
}
```

### `DevicePerformance`
设备性能评级静态工具，无 UI 耦合。
```csharp
public static class DevicePerformance
{
    static DevicePerformanceLevel GetDevicePerformanceLevel()
    // 判断逻辑：
    //   CPU 核心数 <= 低端阈值 → Low
    //   显存 + 系统内存 <= 中端阈值 → Mid
    //   否则 → High

    static void ModifyQualitySettingsBasedOnPerformanceLevel()
    // 根据评级调整 QualitySettings（分辨率/阴影/特效等）
}

public enum DevicePerformanceLevel { Low, Mid, High }

[Serializable]
public class DevicePerformanceData
{
    int LowCpuCoreCount
    long MidGpuMemory      // 显存阈值，字节
    long MidSystemMemory   // 系统内存阈值，字节
}
```

### `DiskCheckingConfig`
磁盘空间检测配置，通过 Inspector 注入。
```csharp
[Serializable]
public class DiskCheckingConfig
{
    bool Enable
    List<RuntimePlatform> Platforms   // 哪些平台开启检测
    long[] SpaceThresholds            // 空间档位，字节（从高到低）
    float CheckInterval               // 检测间隔，秒
}
```

### `DebugManagerConfig`
所有配置参数的集合，由 DebugComponent 在 Start() 中构建后传给 Initialize()。
```csharp
public class DebugManagerConfig
{
    DebugActiveWindowType ActiveWindowType
    DevicePerformanceData EditorPerformance
    DevicePerformanceData iOSPerformance
    DevicePerformanceData AndroidPerformance
    DiskCheckingConfig DiskCheckingConfig
    bool UseDevicePerformance
    bool UseDiskChecking
    float CustomButtonHeight
    // ... UI 参数（字体大小、皮肤等）
}
```

### `GMToolsLib : ScriptableObject`
GM 工具库配置，存于 Resources，定义 GM 面板的分组和按钮条目。
```csharp
public class GMToolsLib : ScriptableObject
{
    List<Group> Groups

    [Serializable]
    public class Group
    {
        string Name
        List<Item> Items
    }

    [Serializable]
    public class Item
    {
        string Name
        UIFormat Format         // Button / Toggle / InputField
        string LuaCallback      // Lua 函数名（需框架 Lua 支持）
    }

    public enum UIFormat { Button, Toggle, InputField }
}
```

---

## 管理器层（Managers/）

### 继承链
```
FrameworkManager (框架基类，Priority 机制)
  └── DebugManagerBase (internal abstract, Priority = 17)
        └── DebugManager (internal sealed partial)
              └── 同时实现 IDebugContext（作为窗口的 UI 上下文）
```

### `DebugManagerBase`
```csharp
internal abstract class DebugManagerBase : FrameworkManager, IDebugManager
{
    public override int Priority => 17;
    public abstract void Initialize(DebugManagerConfig config);
    public abstract override void Update(float elapseSeconds, float realElapseSeconds);
    public abstract override void Shutdown();
    public abstract bool IsDebugWinEnable();
    public abstract void RegisterDebugWindow(string path, IDebugWindow debugWindow, params object[] args);
    public abstract IDebugWindow GetDebugWindow(string path);
    public abstract bool SelectDebugWindow(string path);
}
```

### `DebugManager` 主要字段（来自 Visitors.cs）
```csharp
// 计数器
private FpsCounter m_FpsCounter;            // 帧率（0.5s 采样）
private RamCounter m_RamCounter;            // 内存（1s 采样）

// 窗口系统
private IDebugWindowGroup m_DebugWindowRoot;  // 窗口容器（DebugWindowGroup）

// 磁盘状态（Update 更新）
private long m_CachedDiskAvailable;
private long m_CachedDiskBusy;
private long m_CachedDiskTotal;
private bool m_IsInWhiteDevice;             // 设备是否白名单（高性能）

// IDebugContext 实现所需状态（由 Component 每帧同步）
private IDebugWindow m_ActiveWindow;
private Transform m_DebugWindowRoot;
private DebugWindowMode m_WinMode;
private float m_CustomButtonHeight;
private ScrollRect m_LogScrollRect;
private Scrollbar m_LogScrollBarRect;
private Rect m_LabelSelPopRect;
private string m_SelLogString;
```

### `DebugManager.DebugWindowGroup`（嵌套类）
```csharp
private sealed class DebugWindowGroup : IDebugWindowGroup
{
    private readonly List<KeyValuePair<string, IDebugWindow>> m_Windows;

    // 当前选中索引
    public int SelectedIndex { get; set; }
    public IDebugWindow SelectedWindow => m_Windows[SelectedIndex].Value;
    public string SelectedWindowName => m_Windows[SelectedIndex].Key;

    public void RegisterDebugWindow(string path, IDebugWindow window)
        => m_Windows.Add(new(path, window));

    public IDebugWindow GetDebugWindow(string path)
        => m_Windows.FirstOrDefault(kv => kv.Key == path).Value;

    // 实现 IDebugWindow：转发到 SelectedWindow
    public void OnUpdate(float e, float r) => SelectedWindow?.OnUpdate(e, r);
    public void OnDraw(IDebugContext ctx, float offsetY) => SelectedWindow?.OnDraw(ctx, offsetY);
}
```

---

## 窗口层（Windows/）

### `DebugComponent : FrameworkComponent`
MonoBehaviour 入口，驱动整个 Debug 系统。

**生命周期：**
```
Awake()
  → Util.TypeCreator.Create<IDebugManager>() → DebugManager 反射创建
  → m_DebugManager = IDebugManager 引用

Start()
  → TryCacheFrameworkManagers()    获取 IAssetLoadManager / IEventManager
  → 构建 DebugManagerConfig（收集 Inspector 所有参数）
  → m_DebugManager.Initialize(config)
  → SetActiveWindow()              根据 DebugActiveWindowType 决策是否激活
  → Init()                         注册所有窗口，SelectDebugWindow("Console")

Update()
  → m_DebugManager.Update(Time.deltaTime, Time.unscaledDeltaTime)
      → FpsCounter.Update()
      → RamCounter.Update()
      → DebugWindowGroup.OnUpdate() → SelectedWindow?.OnUpdate()

LateUpdate()
  → UpdateWindowInertia()          惯性滚动衰减（m_SpeedBase 逐帧递减）

OnGUI()
  → if (ActiveWindow && m_DebugManager.ActiveWindow != null)
      → SyncUIState() 同步 Canvas 状态到 DebugManager (IDebugContext)
      → DrawActiveWindowGui()
          → DebugWindowRoot.OnDraw(context, offsetY)
              → SelectedWindow?.OnDraw(context, offsetY)

OnDestroy()
  → m_ConsoleWindow.Shutdown()
  → m_DebugManager.Shutdown()
```

### `ConsoleWindow : IDebugWindow`（内嵌于 DebugComponent.ConsoleWindow.cs）
完整的日志控制台窗口实现。

**核心字段：**
```csharp
private List<LogNode> m_AllLogNodes;           // 全量日志（对象池节点）
private List<int> m_InFilterLogs;              // 过滤后的可见日志索引
private string m_InputSearchInfo;              // 搜索输入字符串
private bool[] m_LogLevelFilter;               // 各级别过滤开关 [Info/Debug/Warning/Error/Fatal]
private List<LabelNode> m_LabelTree;           // 标签树（分类过滤）
private float m_DragOffsetY;                   // 当前滚动偏移
private float m_SpeedBase;                     // 惯性速度
private bool m_IsAutoScroll;                   // 是否自动滚到最新

// 日志计数
private int m_InfoCount, m_DebugCount, m_WarningCount, m_ErrorCount, m_FatalCount;
```

**日志捕获（线程安全）：**
```csharp
// 注册
Application.logMessageReceivedThreaded += UnityLogCallback;

void UnityLogCallback(string logMessage, string stackTrace, LogType type)
{
    // Interlocked 操作保护计数器
    LogNode node = LogNode.CreateThreadedNode(level, logMessage, stackTrace,
                                              frameCount, logIndex++, labelList);
    lock (m_Lock) m_AllLogNodes.Add(node);
    // 主线程 OnDraw 消费
}
```

**搜索/过滤：**
```csharp
void RefreshFilter()
{
    m_InFilterLogs.Clear();
    foreach (var node in m_AllLogNodes)
    {
        if (!m_LogLevelFilter[(int)node.LogType]) continue;
        if (!string.IsNullOrEmpty(m_InputSearchInfo) &&
            !node.LogMessage.Contains(m_InputSearchInfo)) continue;
        // 标签过滤
        m_InFilterLogs.Add(index);
    }
}
```

**惯性滚动：**
```csharp
// LateUpdate：速度衰减
m_DragOffsetY += m_SpeedBase * Time.deltaTime;
m_SpeedBase *= 0.9f;   // 每帧衰减 10%
if (Mathf.Abs(m_SpeedBase) < 0.1f) m_SpeedBase = 0;
```

### 信息窗口（17 个，继承 `ScrollableDebugWindowBase`）

| 窗口类 | 显示内容 |
|--------|---------|
| `SystemInformationWindow` | 设备型号、OS版本、CPU、内存、分辨率 |
| `EnvironmentInformationWindow` | Unity 版本、平台、开发构建标志 |
| `ScreenInformationWindow` | 屏幕尺寸、DPI、安全区域、方向 |
| `GraphicsInformationWindow` | GPU 型号、显存、API 版本、最大纹理 |
| `InputSummaryInformationWindow` | 输入设备摘要（触摸点数/鼠标/键盘） |
| `InputTouchInformationWindow` | 每个触摸点实时坐标/状态/delta |
| `InputAccelerationInformationWindow` | 加速度传感器实时数据 |
| `InputGyroscopeInformationWindow` | 陀螺仪实时数据 |
| `InputCompassInformationWindow` | 指南针/磁力计数据 |
| `PathInformationWindow` | persistentDataPath / dataPath / streamingAssetsPath 等 |
| `SceneInformationWindow` | 当前场景名、已加载场景列表 |
| `TimeInformationWindow` | Time.time / deltaTime / frameCount / realtimeSinceStartup |
| `QualityInformationWindow` | 当前质量等级、阴影、AA、各向异性等 |
| `ProfilerInformationWindow` | GC 调用次数、GC 分配总量等 |
| `WebPlayerInformationWindow` | WebGL 专用信息 |
| `RuntimeMemoryInformationWindow<T>` | 泛型：枚举指定类型的运行时对象内存 |
| `RuntimeMemorySummaryWindow` | 所有对象内存汇总排名 |

**`RuntimeMemoryInformationWindow<T>` 特殊说明：**
```csharp
// 内存采样节点
public sealed class Sample
{
    string Name;
    long Size;          // 字节
    bool IsSame;        // 与上次相同（用于 diff 显示）
}

// 使用 Resources.FindObjectsOfTypeAll<T>() 枚举对象
// 按 Size 降序排列
// 支持 diff 对比（重新采样时标记变化项）
```

---

## 关键流程

### 初始化与窗口注册
```
DebugComponent.Start()
  → DebugManager.Initialize(config)
      → m_DebugWindowRoot = new DebugWindowGroup()
      → m_FpsCounter = new FpsCounter(0.5f)
      → m_RamCounter = new RamCounter()
  → SetActiveWindow()           根据 DebugActiveWindowType + 当前环境决策
  → Init() 注册窗口：
      RegisterDebugWindow("Console",     m_ConsoleWindow)
      RegisterDebugWindow("System",      m_SystemInformationWindow)
      RegisterDebugWindow("Environment", m_EnvironmentInformationWindow)
      RegisterDebugWindow("Screen",      m_ScreenInformationWindow)
      RegisterDebugWindow("Graphics",    m_GraphicsInformationWindow)
      RegisterDebugWindow("Input",       m_InputSummaryInformationWindow)
      RegisterDebugWindow("Path",        m_PathInformationWindow)
      RegisterDebugWindow("Scene",       m_SceneInformationWindow)
      RegisterDebugWindow("Time",        m_TimeInformationWindow)
      RegisterDebugWindow("Quality",     m_QualityInformationWindow)
      RegisterDebugWindow("Profiler",    m_ProfilerInformationWindow)
      RegisterDebugWindow("Memory",      m_RuntimeMemorySummaryWindow)
      RegisterDebugWindow("Memory/Texture",   m_RuntimeMemoryTextureWindow)
      RegisterDebugWindow("Memory/Mesh",      m_RuntimeMemoryMeshWindow)
      RegisterDebugWindow("Memory/Material",  m_RuntimeMemoryMaterialWindow)
      RegisterDebugWindow("Memory/AudioClip", m_RuntimeMemoryAudioClipWindow)
      RegisterDebugWindow("Memory/AnimationClip", m_RuntimeMemoryAnimClipWindow)
      RegisterDebugWindow("GMTools",     m_GMToolWindow)
      → SelectDebugWindow("Console")    默认选中 Console
```

### 磁盘检测流程

> 2026-05-18：磁盘检测主循环已落地于 DebugManager（UniTask + CancellationToken），行为对齐 Solar DebuggerComponent.DiskChecking。

```
DebugManager.Initialize()
  → SelectCurrentConfig()       按 Application.platform 选取 DiskCheckingConfig（[0]Editor/[1]Android/[2]iOS）
  → AvailableSpaces[Count-1] = CheckDiskTotalSpace()    写入兜底总空间档
  → RunDiskCheckLoopAsync(CancellationToken)
      → CheckDiskAvailableSpace()                       查询可用空间（MB）
      → 遍历 AvailableSpaces 命中档位 curIndex
      → m_EventManager.Fire(this, DiskCheckEventData.Create(availableSpace, AvailableSpaces[curIndex]))
      → UniTask.Delay(AvailableSpacesIntervals[curIndex])
      → 循环直至 CancellationToken 取消

DebugManager.Shutdown()
  → m_DiskCheckCts.Cancel() / Dispose()                 退出主循环
```

### 设备性能评级流程
```
DebugComponent.Start()
  → 若 UseDevicePerformance：
      → DevicePerformance.GetDevicePerformanceLevel()
          → 读取 DevicePerformanceData（对应平台配置）
          → 判断 CPU 核心数 / 显存 / 系统内存
          → 返回 Low / Mid / High
      → DevicePerformance.ModifyQualitySettingsBasedOnPerformanceLevel()
          → QualitySettings.SetQualityLevel()
```

---

## 平台适配

```csharp
// 磁盘工具
#if NOVA_SIMPLEDISKUTILS
    // SimpleDiskUtilProvider：调用第三方 SimpleDiskUtils 库
#else
    // NullDiskUtilProvider：全部返回 -1
#endif

// WebGL 键盘
#if UNITY_WEBGL
    // DebugComponent.WebGLKeyboard*.cs
    // WX / Alipay / DY 三个小程序平台各有专属实现
#endif

// 平台信息显示
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    // Windows/Editor 特有信息
#elif UNITY_STANDALONE_OSX || UNITY_IOS
    // Mac/iOS 特有信息
#elif UNITY_ANDROID
    // Android 特有信息
#endif
```

---

## 外部依赖

| 依赖 | 来源 | 用途 |
|------|------|------|
| `FrameworkManager` | 框架底层 | 基类，Priority=17 |
| `FrameworkComponent` | 框架底层 | MonoBehaviour 包装，Awake/Start 模板 |
| `Util.TypeCreator.Create<T>()` | 框架工具 | 反射创建 DebugManager |
| `ReferencePool.Get<T>()` / `.Release()` | 框架内存 | LogNode 对象池 |
| `Log` / `LogTag` | 框架日志 | 内部日志输出 |
| `IAssetLoadManager` | 跨模块 | Component 可选依赖（资源加载） |
| `IEventManager` | 跨模块 | 磁盘检测事件发布 |
| `SimpleDiskUtils` | 第三方（可选） | 磁盘空间查询（`#if NOVA_SIMPLEDISKUTILS`） |
| `UnityEngine.Profiling.Profiler` | Unity 标准 | RamCounter 内存采样 |
| `System.Threading.Interlocked` | BCL | LogNode 线程安全计数 |

---

## 注意事项（移植时重点关注）

1. **`ConsoleWindow` 是线程安全的**：日志回调在 `logMessageReceivedThreaded`（多线程），使用 `lock + Interlocked` 保护，移植时必须保持同等线程安全级别

2. **`LogNode` 依赖 `ReferencePool`**：移植时需确认 RuntimeDebuggerger 侧是否有对应的对象池，或改为直接 `new LogNode()` 并接受 GC 压力

3. **`IDebugContext` 是 Manager 的双重角色**：`DebugManager` 既实现 `IDebugManager`（对外业务接口）又实现 `IDebugContext`（对内 UI 状态），移植时可拆分这两个职责

4. **`IDebugWindow.OnDraw()` 是 IMGUI**：RuntimeDebuggerger 使用 UGUI，移植时所有 `GUILayout.*` 调用都需重写为 UGUI 组件

5. **`DebugActiveWindowType` 逻辑需对接 RuntimeDebuggerger Settings**：`AlwaysOpen/OnlyOpenInEditor/AlwaysClose` 等策略需映射到 RuntimeDebuggerger 的 `Settings.IsEnabled`

6. **`GMToolWindow` 依赖 Lua 回调**：若目标环境无 Lua 层，此窗口需特殊处理（改为 C# 委托或跳过）

7. **`RuntimeMemoryInformationWindow<T>` 用 `Resources.FindObjectsOfTypeAll<T>()`**：这个 API 在移动端性能较差，移植时考虑改为按需触发而非每帧刷新

8. **`DevicePerformance` 完全无 UI 耦合**，可直接复用，无需任何修改

9. **`FpsCounter / RamCounter` 完全无 UI 耦合**，可直接复用，接入 RuntimeDebuggerger Profiler 即可
