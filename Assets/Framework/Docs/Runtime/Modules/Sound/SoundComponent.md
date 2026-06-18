# SoundComponent

`SoundComponent` 是 `Sound` 模块的场景入口。

它负责三件事：

- 在 `Awake` 里创建 `ISoundManager`
- 在 `Start` 里把 `SoundSettings`、父节点和 `AudioMixer` 组装成 `SoundManagerConfig`
- 把常用播放控制能力以组件门面形式暴露给 `Nova.Sound`

## 什么时候先看这页

- 你要确认声音系统在场景里如何启动。
- 你在排查为什么声音组没有注册成功。
- 你要判断 `LoadSync / LoadAsync`、`PlaySound`、`StopSound` 应该从组件层还是 Manager 层切入。

## 依赖与边界

### 它依赖什么

- `ISoundManager`
- `SoundSettings`
- `SoundGroupShell[]`
- 可选的 `AudioMixer`

### 它对外负责什么

- 创建并初始化声音管理器
- 把 Inspector 配置变成运行时声音组
- 暴露播放、暂停、恢复、停止等门面方法

### 它不负责什么

- 不负责真正加载和解析声音表数据
- 不负责声音组内部调度与资源生命周期
- 不负责 `AudioClip` 异步加载

## 核心流程

### 1. Awake 只校验并创建 Manager

`Awake()` 使用 `TypeCreator.Create<ISoundManager>(m_CurManagerTypeName)` 创建实例。

如果类型名无效，会直接抛 `InvalidOperationException`，不会默默降级。

### 2. Start 才把配置推给 Manager

`Start()` 会先校验 `m_Settings`，随后调用：

- `m_SoundManager.Initialize(new SoundManagerConfig { ... })`

这里真正传入的关键内容是：

- `SoundUnitsSettings`
- `ParentTransform = transform`
- `AudioMixer`

### 3. 声音组来自 Inspector 的 `SoundGroupShell[]`

组件不会自动扫描声音组；它只遍历 `m_SoundGroupShells` 并逐个调用 `AddSoundGroup(...)`。

如果某个组添加失败，只会记错误日志，不会中断整个组件启动。

### 4. LoadAsync 是带并发合流的单次加载门面

`LoadAsync()` 的语义是：

- 已完成则直接返回 `true`
- 正在加载则 await 同一个 `UniTaskCompletionSource`
- 真正加载逻辑只会进入 Manager 一次

这点和“每次调都重新加载”完全不同。

## 高价值门面

- 数据加载：`LoadSync()` / `LoadAsync()`
- 播放：`PlaySound(name)`、`PlaySound(name, params)`、`PlaySound(group, assetLocation, params)`
- 控制：`Stop*`、`Pause*`、`Resume*`
- 状态：`IsLoadOver`、`SoundGroupCount`

## 风险点 / 易错点

- `Start()` 之前没有把声音组注册到 Manager；太早调用播放很容易命中“组不存在”。
- `SoundSettings` 为空会直接抛异常，不是静默跳过。
- `LoadSync()` 是 fire-and-forget 门面，不返回成功状态。
- 组件层只是门面；如果你要排查表数据加载、播放失败码、资源释放，应该继续看 `SoundManager`。

## 继续阅读

关键源码：

- [SoundComponent.cs](../../../../Scripts/Runtime/Modules/Sound/SoundComponent.cs)
- [SoundComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/Sound/SoundComponent.Visitors.cs)

相关文档：

- [SoundManager.md](SoundManager.md)
- [ISoundManager.md](ISoundManager.md)
- [SoundManagerConfig.md](SoundManagerConfig.md)
- [SoundSettings.md](SoundSettings.md)
- [SoundGroupShell.md](SoundGroupShell.md)

