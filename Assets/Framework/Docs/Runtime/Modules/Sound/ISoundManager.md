# ISoundManager

`ISoundManager` 定义了声音系统真正对外稳定的运行契约。

它覆盖了四类能力：

- 初始化与表数据加载
- 播放入口
- 播放状态控制
- 声音组与资源管理

## 什么时候先看这页

- 你要替换声音管理器实现，但不想改变组件和业务层调用面。
- 你要确认某个能力是不是声音系统公开契约的一部分。
- 你要核对 `PlaySoundParams` 的所有权语义。

## 契约语义

### 1. 这是完整运行契约，不是标记接口

和 `Persist` 那几个后端接口不同，`ISoundManager` 不是空壳，它明确约束了：

- 何时初始化
- 何时加载声音表
- 如何播放
- 如何暂停 / 恢复 / 停止
- 如何管理声音组和资源

### 2. `PlaySoundParams` 存在所有权转移

接口注释里已经把红线写得很清楚：

- 一旦传入 `PlaySound(...)`
- `PlaySoundParams` 的所有权转入 Manager
- 调用方不应再持有，也不应再 `ReferencePool.Put`

这是这套契约里最容易被误用的一点。

### 3. 组管理是公开契约的一部分

`AddSoundGroup(...)`、`HasSoundGroup(...)`、`SetSoundGroupVolume(...)` 说明这套系统不是“纯查表播放器”，而是显式维护声音组运行时实体。

## 契约边界

- 这个接口公开的是声音系统运行面，不公开内部 `SoundGroup` / `SoundAgent` 实现
- 它允许按名称查表播放，也允许直接按 `group + assetLocation` 播放
- 它不把播放失败细节暴露为返回码；失败更多通过日志和行为结果体现

## 风险点 / 易错点

- `PlaySound(...)` 返回 `serialID` 不代表一定播放成功；资源加载、组不存在、代理不足都可能导致最终没播出来。
- `LoadSync()` 不回传结果，适合明确知道数据环境正确的路径。
- 如果新实现没有严格遵守 `PlaySoundParams` 的引用池所有权约定，很容易引发重复回收或泄漏。
- 调用面虽然大，但不要把编辑器导表、Inspector 配置等职责继续塞进这个接口。

## 继续阅读

关键源码：

- [ISoundManager.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Interfaces/ISoundManager.cs)

相关文档：

- [SoundManager.md](SoundManager.md)
- [SoundComponent.md](SoundComponent.md)
- [PlaySoundParams.md](PlaySoundParams.md)
- [PlaySoundInfo.md](PlaySoundInfo.md)

