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

JSON/Binary 由 DataFormat 决定。null/空 Unit 列表在异步路径返回 true；空地址跳过，Unit 元素本身为 null 则可能抛出。非空缓存构造 SoundTables 时缺 IConfigManager 或表加载失败返回 false；空缓存直接 true，不能据此证明有声音行。每次加载先清旧表和名称索引，加载失败不继续使用上次行配置；新索引完整校验后才一次提交。

同步路径同样检查每个有效 Unit 的读取结果；任一 `ReadDataAssetSync` 返回 false 就立即结束本轮，不再读取后续 Unit，也不把前面成功读取的缓存构建成部分声音表。此时正式表和名称索引保持为空，读取失败由日志说明；`LoadSync()` 没有 bool 返回值。

表资源委托取得 TextAsset 后立即 Release 句柄，再把资产交接收器解析，后续 release 委托为空；不是解析完成后才释放，也没有复制原字节或 finally 的统一补偿。Initialize 只保存配置/依赖引用，不清旧组/加载状态；Update 为空，音频淡变由 Unity Helper 执行。

### 4. 名称唯一，地址入口拒绝歧义

`BuildSoundRowsFromTables()` 先构建局部索引；表未实现 `ITable<ISoundRow>`、`DataList` 为空、空行、空 `Name` 或跨表重名会记录错误并返回 false，不提交部分名称索引。`LoadAsync()` 将构建失败返回 false；`LoadSync()` 没有 bool，通过错误日志和不可用索引体现拒绝。

`PlaySound(name, params)` 首参固定为 `Name`。`PlaySoundByAsset(assetLocation, params = null)` 首参固定为资源地址，使用精确且区分大小写的匹配；无行或多行匹配都返回 `-1`。多个声音名可以共用音频，但必须按名称选择所需播放配置。

### 5. 播放先查表和验配置，再申请 serialID

`PlaySound(...)` 的真实语义不是“同步开始播放”，而是：

1. 按名称或唯一地址找到表行
2. 校验行内地址、组名、声音组及代理容量；不满足时归还运行时参数并返回 `-1`
3. 生成 `serialID` 并记录到 `m_SoundsLoading`
4. 使用行内 `AssetLocation` 异步加载 `AudioClip`
5. 加载完成后交给 `SoundGroup`，直接消费该行的 `Loop / Priority / Volume / SpatialBlend`，再应用进度、静音、淡入、音调和声相等表外参数

没有直接指定组和地址的播放旁路；`PlaySoundParams` 不能覆盖表字段。原 `PlaySound(group, location, params)` 必须迁移到按名称或按唯一地址入口；原参数对象的 `Loop / Priority / VolumeInSoundGroup` 迁入源表并正式导出。返回非负编号仍不等于资源成功加载或实际播放。

## 声音组语义

- 声音组必须先显式创建，否则播放时会得到 `SoundGroupNotExist`
- `AddSoundGroup(...)` 会同时创建组 Helper 和一组 Agent Helper
- 如果给了 `AudioMixer`，组级 Helper 会先尝试路由到 `Master/<Group>`，找不到再退回 `Master`
- 每个 Agent Helper 还会继续尝试更细一层的 `Master/<Group>/<Group>_<Index>`；命不中时才继承组级 MixerGroup

组先登记再逐个创建代理；数量不为正也可能返回创建成功但没有代理，创建异常可能留已登记组和部分 GameObject。组名为空会抛出，重名返回 false；组音量与代理参数没有 Manager 级运行时 clamp/有限性校验。

### 优先级与代理选择

先选择第一个 `!AudioSource.isPlaying` 的代理，因此暂停中的声音也可能被当作空闲而替换。全部忙时选择低于请求优先级的候选；允许同优先级替换时再按最早设置资源时间选择。替换前发送 OnSoundReplaced；回调/参数赋值失败不恢复原声音。框架优先级越大越优先，Helper 用 `AudioSource.priority = 128 - value` 转换，不在此校验 Unity 底层范围。

组 Stop/Pause/Resume 的 serial 查询只看保存编号，停止、Reset 或释放并不统一归零编号；返回 true 表示匹配并调用控制，不证明仍有 clip、正在播放或可听。StopAllLoaded 只停止 IsPlaying 的代理，PauseAll/ResumeAll 则遍历所有代理，不筛曾暂停状态。Resume 对从未 Pause 的代理可能使用初始为 0 的暂停音量。

## 资源与控制语义

- 停止加载中的声音，本质是把 `serialID` 放进 `m_SoundsToReleaseOnLoad`
- `ReleaseAllAsset()` 会先停所有加载中和已加载声音，再释放组内资源
- `ReleaseAssetBySerialID()` 先尝试停止，再遍历组释放对应资源

### 句柄、加载取消与淡变

加载中的 Stop/StopAllLoading 只登记“加载完成后放弃”并从 loading 集合移除，不取消 IAssetManager 的物理加载。GetAllLoadingSoundSerialIDs 数组为副本，列表重载先 Clear 再填充、拒绝 null；顺序没有保证，IsLoadingSound=false 不代表底层任务已停止。

正常异步完成后，放弃/空 clip/无代理分支释放新句柄；成功交 SoundAgent 持有。PlaySoundInfo 在正常/已处理失败链归池时级联回收 params。异步流程没有关闭代次或取消令牌；Shutdown 清放弃标记后，尚未完成的旧请求仍可能使用它捕获的旧 SoundGroup。关闭不销毁 Helper，不能作为所有在飞请求已结束的证明。

SoundAgent.SetSoundAsset 先 Reset 旧资源，再保存新资产/句柄，随后才让 Helper 接受 AudioClip；失败不回滚旧资源。Reset/ReleaseSoundAsset 以当前 Unity 资产非 null 为释放条件，释放后不统一清 serial；单独 Release 不 Stop、不清 AudioSource.clip。自然播放结束也不自动释放句柄，资源通常保留到替换、显式释放或关闭。资源字段与句柄状态应分别核对。

Helper 的 Play/Pause/Resume/Stop 先取消旧淡变，使用 Time.deltaTime 与 Update 做音量插值，受 timeScale 影响；没有统一非负/有限性校验。Stop 淡出后不恢复原音量，取消淡变不恢复已改音量；ResetHelper 只清 clip/暂停音量，不取消淡变。Helper.OnDestroy 只处理其 CTS，句柄仍由上层管理。

最终静音为 `group.Mute || agent.MuteInSoundGroup`，最终音量为 `group.Volume * agent.VolumeInSoundGroup`。属性写入可以只更新到一部分代理后失败；本模块不为逐组/逐代理异常提供事务。

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
