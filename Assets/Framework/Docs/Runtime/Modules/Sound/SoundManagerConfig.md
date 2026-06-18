# SoundManagerConfig

`SoundManagerConfig` 是 `SoundComponent.Start()` 传给 `SoundManager.Initialize(...)` 的运行时配置包。

它只保留运行时真正需要的三项输入：

- `SoundUnitsSettings`
- `ParentTransform`
- `AudioMixer`

## 什么时候先看这页

- 你要搞清组件层哪些配置会真正进入 Manager。
- 你在判断某个新增参数应不应该成为声音系统初始化参数。
- 你要排查 AudioMixer 路由为什么没有生效。

## 配置语义

### 1. `SoundUnitsSettings` 决定数据加载来源

这是声音表数据的运行时输入列表。Manager 会据此读取数据资产，并构建 `ISoundRow` 缓存。

### 2. `ParentTransform` 决定 Helper 节点挂载位置

声音组和声音代理对应的 Helper GameObject 都会挂到这个父节点下，不会自己找别的宿主。

### 3. `AudioMixer` 是可选路由信息

有 `AudioMixer` 时，Manager 会在建组时尝试匹配：

- `Master/<Group>`
- `Master/<Group>/<Group>_<Index>`

没配到时再回退到更宽泛的路径。

## 设计边界

- 这个配置对象不包含声音组列表；声音组来自组件层的 `SoundGroupShell[]`
- 这个配置对象不包含播放时参数；播放时参数属于 `PlaySoundParams`
- 它只解决“系统如何启动”，不解决“某次播放如何执行”

## 风险点 / 易错点

- 把声音组信息误以为来自 `SoundManagerConfig`，会直接误读启动链路。
- `AudioMixer` 为空是允许的，不能把它当必填项。
- 如果后续把一次性播放参数塞进这个类，会把初始化配置和调用时配置混在一起。

## 继续阅读

关键源码：

- [SoundManagerConfig.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Definitions/SoundManagerConfig.cs)
- [SoundComponent.cs](../../../../Scripts/Runtime/Modules/Sound/SoundComponent.cs)

相关文档：

- [SoundComponent.md](SoundComponent.md)
- [SoundManager.md](SoundManager.md)
- [SoundSettings.md](SoundSettings.md)
- [PlaySoundParams.md](PlaySoundParams.md)

