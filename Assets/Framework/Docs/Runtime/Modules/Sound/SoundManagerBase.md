# SoundManagerBase

`SoundManagerBase` 是声音管理器的抽象基类。

它的价值很集中：

- 固定调度优先级
- 把 `ISoundManager` 契约收敛到 `FrameworkManager` 体系里
- 强制具体实现补齐全部运行逻辑

## 什么时候先看这页

- 你要自定义声音管理器实现。
- 你在确认 Sound 模块在框架调度里的优先级。
- 你在区分接口契约和默认实现的边界。

## 核心语义

### 1. Priority 固定为 19

这是它唯一自带的运行时行为。

含义是：

- 值越小，越早 `Update`
- 值越小，越晚 `Shutdown`

### 2. 这里只定义骨架，不包含播放逻辑

它不会：

- 加载声音表
- 创建声音组
- 播放 `AudioClip`

这些都留给 `SoundManager`。

### 3. 这是框架内部基类

`SoundManagerBase` 为 `internal abstract`，仓外自定义声音后端不能直接继承它；应实现公开的 `ISoundManager` 并满足框架管理器调度要求。播放契约包括按 `Name` 的 `PlaySound` 和按唯一 `AssetLocation` 反查表的 `PlaySoundByAsset`。

## 风险点 / 易错点

- 把它当成“有半套默认行为的基类”是错的；除了优先级，它基本不提供业务默认实现。
- 仓外实现需要自己处理 `FrameworkManager` 调度契约。

## 继续阅读

关键源码：

- [SoundManagerBase.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManagerBase.cs)

相关文档：

- [ISoundManager.md](ISoundManager.md)
- [SoundManager.md](SoundManager.md)
- [FrameworkManager.md](../FrameworkManager.md)

