# PlaySoundErrorCode

`PlaySoundErrorCode` 是 `SoundManager` 内部播放失败码枚举。

它不是公开返回值协议，而是用于日志和内部失败分支标记。读这页的重点不是“怎么拿到错误码”，而是“运行时到底会在哪些点失败”。

## 什么时候先看这页

- 你在看播放日志里的 `errorCode=...`。
- 你要区分“声音组不存在”和“代理不足”这类失败原因。
- 你在排查为什么有 serialID 但最终没有声音输出。

## 失败类型

当前内部错误码包括：

- `Unknown`
- `SoundGroupNotExist`
- `SoundGroupHasNotEnoughAgent`
- `LoadAssetFailure`
- `IgnoredDueToLowPriority`
- `SetSoundAssetFailure`

## 它出现在什么地方

- 组不存在、代理不足这类失败会在 `PlaySound(...)` 或组内调度阶段出现
- 资源加载失败和设置音频资源失败发生在异步加载与真正落到 Agent 的过程
- 这些错误码主要通过日志侧暴露，而不是接口返回值

## 风险点 / 易错点

- 业务层不要把它当稳定 API 依赖；它是 `internal` 枚举。
- 即便没有拿到错误码，仍可能从日志上定位真实失败阶段。
- `serialID` 生成成功不等于没有错误码；两者不是同一个层面的结果。

## 继续阅读

关键源码：

- [SoundManager.PlaySoundErrorCode.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.PlaySoundErrorCode.cs)
- [SoundManager.cs](../../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.cs)

相关文档：

- [SoundManager.md](SoundManager.md)
- [ISoundManager.md](ISoundManager.md)
- [PlaySoundInfo.md](PlaySoundInfo.md)

