# VibrateType

`VibrateType` 是预设振动类型枚举。

它服务的是：

- `VibrateComponent.Play(VibrateType type)`
- `IVibrateManager.Play(VibrateType type)`

## 什么时候先看这页

- 你只需要一个现成的系统级触觉反馈，而不想维护振动表。
- 你在确认某个业务场景应该映射到哪种预设触觉。
- 你要核对 `None` 的行为。

## 枚举语义

当前预设包含：

- `Selection`
- `Success`
- `Warning`
- `Failure`
- `LightImpact`
- `MediumImpact`
- `HeavyImpact`
- `RigidImpact`
- `SoftImpact`

以及显式的：

- `None`

## 运行时含义

- `None` 会被直接当成 no-op
- 其他值在 Nice Vibrations 开启时，会映射到对应的 `HapticPatterns.PresetType`

## 风险点 / 易错点

- 这个枚举只覆盖预设振动，不覆盖表驱动组合振动。
- 没有底层插件或设备不支持时，传入这些枚举也不会产生真实振动反馈。
- 不要把业务语义直接和某个预设一一绑定死，复杂反馈更适合走 `Emphasis` / `Custom` 数据。

## 继续阅读

关键源码：

- [VibrateType.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateType.cs)
- [VibrateManager.Methods.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManager.Methods.cs)

相关文档：

- [VibrateManager.md](VibrateManager.md)
- [IVibrateManager.md](IVibrateManager.md)
- [IVibrateRow.md](IVibrateRow.md)
