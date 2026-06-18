# IVibrateManager

`IVibrateManager` 定义了振动系统公开的运行契约。

它覆盖两类能力：

- 数据加载
- 播放控制与设备状态

## 什么时候先看这页

- 你要替换振动管理器实现，但不改组件和业务调用面。
- 你要确认某个振动能力是不是框架稳定契约的一部分。
- 你在比较“预设振动”和“表驱动分组振动”的边界。

## 契约语义

### 1. 既支持即时调用，也支持数据驱动调用

接口同时公开：

- `Play()` / `Play(VibrateType)` 这种即时预设调用
- `PlayCustom(name)` / `PlayEmphasis(name)` 这种按表驱动的分组调用
- `PlayCustom(...)` / `PlayEmphasis(...)` 这种直接传参数的即时调用

### 2. 数据加载是显式阶段，不是隐式首调加载

- `LoadVibrateDataSync()`
- `LoadVibrateDataAsync()`

说明振动表数据不是在第一次播放时自动补载，而是由上层决定预热时机。

### 3. 设备与开关状态属于公开状态面

- `Enable`
- `IsSupported`

都属于业务可以直接感知和切换的契约。

## 契约边界

- 这个接口不公开组缓存、取消令牌、Nice Vibrations 细节
- 它只承诺“能否播放 / 怎样播放”，不承诺具体平台下都有效
- 如果某实现没有底层插件支持，也仍要维持这套调用面的可预测行为

## 风险点 / 易错点

- `PlayCustom(name)` / `PlayEmphasis(name)` 未命中数据时是 warning + 静默返回，不是抛异常。
- `IsSupported` 取决于底层平台与编译条件，不是“只要有组件就支持”。
- 如果新实现把数据加载偷偷做成懒加载，会改变当前模块清晰的初始化语义。

## 继续阅读

关键源码：

- [IVibrateManager.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Interfaces/IVibrateManager.cs)

相关文档：

- [VibrateManager.md](VibrateManager.md)
- [VibrateComponent.md](VibrateComponent.md)
- [VibrateSettings.md](VibrateSettings.md)
- [VibrateType.md](VibrateType.md)

