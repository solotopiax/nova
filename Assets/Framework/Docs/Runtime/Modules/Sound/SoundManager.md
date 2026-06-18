# SoundManager

`SoundManager` 是声音系统的真实运行核心。

它负责：

- 加载并扁平化声音表数据
- 创建和管理声音组
- 异步加载 `AudioClip` 并调度播放
- 处理停止、暂停、恢复与资源释放

## 什么时候先看这页

- 你在排查“为什么拿到 serialID 但声音没播出来”。
- 你要确认声音表是如何从 Luban 数据变成运行时缓存的。
- 你要看声音组、AudioMixer 路由和代理数量之间的关系。

## 依赖与边界

### 它依赖什么

- `SoundManagerConfig`
- `IAssetManager`
- `IConfigManager`
- Luban `ITable` / `ISoundRow`
- `SoundGroup` / `SoundAgent`

### 它对外负责什么

- 加载声音表数据并缓存为 `m_SoundRows`
- 注册声音组并创建 Helper / Agent 节点
- 统一处理播放与控制逻辑

### 它不负责什么

- 不负责从 Inspector 直接收集配置
- 不负责编辑器导表
- 不保证播放请求一定成功落地

## 核心流程

### 1. Priority 固定为 19

`SoundManagerBase.Priority => 19`。

如果你在比较多个运行时模块的更新 / 关闭顺序，这个值才是源码事实。

### 2. Initialize 只接配置，不主动建组

初始化时只做三件事：

- 记录 `SoundUnitsSettings`
- 记录 `ParentTransform`
- 记录 `AudioMixer`

并顺手拿到 `IAssetManager`。真正的声音组创建是后续 `AddSoundGroup(...)`。

### 3. Load 会把声音表扁平化成 `Name -> Row`

无论同步还是异步加载，核心都是：

1. 读取 `SoundUnitSetting` 对应的数据资源
2. 用 `IConfigManager.Namespace` 反射构建 `SoundTables`
3. 把所有 `ITable<ISoundRow>` 扁平化到 `m_SoundRows`

这意味着按名称播放时，查的是扁平后的总字典，不是逐表遍历。

### 4. 重名 `Name` 会后写覆盖前写

`BuildSoundRowsFromTables()` 直接执行：

- `m_SoundRows[row.Name] = row`

也就是说，多表里如果有相同 `Name`，后写入的行会覆盖前者，源码里没有额外冲突告警。

### 5. 播放是“先拿 serialID，再异步加载资源”

`PlaySound(...)` 的真实语义不是“同步开始播放”，而是：

1. 先生成新的 `serialID`
2. 校验声音组
3. 记录到 `m_SoundsLoading`
4. 异步加载 `AudioClip`
5. 加载完成后再交给 `SoundGroup` 真正播放

## 声音组语义

- 声音组必须先显式创建，否则播放时会得到 `SoundGroupNotExist`
- `AddSoundGroup(...)` 会同时创建组 Helper 和一组 Agent Helper
- 如果给了 `AudioMixer`，组级 Helper 会先尝试路由到 `Master/<Group>`，找不到再退回 `Master`
- 每个 Agent Helper 还会继续尝试更细一层的 `Master/<Group>/<Group>_<Index>`；命不中时才继承组级 MixerGroup

## 资源与控制语义

- 停止加载中的声音，本质是把 `serialID` 放进 `m_SoundsToReleaseOnLoad`
- `ReleaseAllAsset()` 会先停所有加载中和已加载声音，再释放组内资源
- `ReleaseAssetBySerialID()` 先尝试停止，再遍历组释放对应资源

## 风险点 / 易错点

- `serialID` 只代表请求编号，不代表一定播放成功。
- `SoundUnitsSettings` 为空时，加载会直接跳过并返回“无数据可加载”的路径，不是异常。
- `IConfigManager` 缺失会导致表数据加载失败；这不是 Sound 模块自身能兜底的。
- 如果 `SoundGroupShell.AgentCount` 太小，组内很容易因为代理不足而拒绝播放。

## 继续阅读

关键源码：

- [SoundManager.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.cs)
- [SoundManager.Methods.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.Methods.cs)
- [SoundManagerBase.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManagerBase.cs)

相关文档：

- [SoundComponent.md](SoundComponent.md)
- [ISoundManager.md](ISoundManager.md)
- [SoundManagerConfig.md](SoundManagerConfig.md)
- [ISoundRow.md](ISoundRow.md)
- [PlaySoundParams.md](PlaySoundParams.md)
- [PlaySoundErrorCode.md](PlaySoundErrorCode.md)
