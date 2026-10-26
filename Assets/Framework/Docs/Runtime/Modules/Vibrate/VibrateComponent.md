# VibrateComponent

`VibrateComponent` 是 `Vibrate` 模块的场景入口。

它负责：

- 在 `Awake` 创建 `IVibrateManager`
- 在 `Start` 把 `VibrateSettings` 变成 `VibrateManagerConfig`
- 暴露默认振动、预设振动、自定义振动与强调振动的组件门面

## 什么时候先看这页

- 你要确认振动系统如何初始化。
- 你在排查 `VibrateSettings` 配置为什么没有进入运行时。
- 你要看 `LoadAsync()`、`PlayCustom()`、`PlayEmphasis()` 的调用入口。

## 依赖与边界

### 它依赖什么

- `IVibrateManager`
- `VibrateSettings`

### 它对外负责什么

- 创建与初始化振动管理器
- 暴露统一的组件级振动入口
- 维护 `IsLoadOver` 这一层的加载门面状态

### 它不负责什么

- 不负责实际解析振动表数据
- 不负责播放队列、取消令牌和设备能力判断
- 不负责具体插件调用

## 核心流程

### 1. Awake 只创建 Manager

`Awake()` 使用 `TypeCreator.Create<IVibrateManager>(m_CurManagerTypeName)` 创建实现。

失败时直接抛 `InvalidOperationException`。

### 2. Start 把配置拆成 Emphasis / Custom 两路

`Start()` 会把 `m_Settings` 中的：

- `EmphasisUnitsSettings`
- `CustomUnitsSettings`

打包进 `VibrateManagerConfig`，交给 `m_VibrateManager.Initialize(...)`。

### 3. LoadAsync 是合流式单次加载

和声音组件类似，`LoadAsync()` 的语义是：

- 已完成就直接返回 `true`
- 正在加载则等待同一个 `UniTaskCompletionSource`
- 真正加载只会进入 Manager 一次

成功后重复调用直返 true；进行中等待同一完成源，失败/异常转换为 false 后允许重试。IsLoadOver 只记录异步结果，不证明表组非空、插件已编译或设备真实振动；销毁不会由这个标志取消在途加载。

### 4. 组件层是纯门面

组件公开的 `Play()`、`Play(VibrateType)`、`PlayCustom(name)`、`PlayEmphasis(name)`、`PlayCustomByParams(...)`、`PlayEmphasisByParams(...)`、`StopAll()`、`Enable`、`IsSupported`，本质都只是透传给 Manager。

`PlayCustom(name)` 与 `PlayEmphasis(name)` 分别查找对应表的 `Name`，使用组内全部行和字段；业务按表配置振动时使用这两个入口。手工数值能力显式使用 `ByParams`，无需加载表。旧数值 `PlayCustom` 改为 `PlayCustomByParams`；旧数值 `PlayEmphasis` 改为 `PlayEmphasisByParams(amplitude, frequency, preDuration)`，移除原来不参与播放的 `interval` 参数；按名调用不变。

## 风险点 / 易错点

- `VibrateSettings` 为空会直接抛异常，不是忽略配置。
- 组件层不区分插件可用性；设备支持与插件编译开关由 Manager 决定。
- `LoadSync()` 直接同步调用 Manager，不返回 bool，不设置 IsLoadOver，也不与异步加载合流；它不是异步 fire-and-forget。

## 继续阅读

关键源码：

- [VibrateComponent.cs](../../../../Scripts/Runtime/Modules/Vibrate/VibrateComponent.cs)
- [VibrateComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Vibrate/VibrateComponent.Visitors.cs)

相关文档：

- [VibrateManager.md](VibrateManager.md)
- [IVibrateManager.md](IVibrateManager.md)
- [VibrateManagerConfig.md](VibrateManagerConfig.md)
- [VibrateSettings.md](VibrateSettings.md)

