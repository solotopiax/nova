# VibrateManager

`VibrateManager` 是振动系统的真实运行核心。

它负责：

- 读取 Emphasis / Custom 两类振动数据
- 构建按 `Name` 分组、按 `Order` 排序的缓存
- 调用 Nice Vibrations 预设或连续振动接口
- 管理延迟播放与取消逻辑

## 什么时候先看这页

- 你在排查振动数据为什么加载后仍然播不出来。
- 你要确认 `PlayCustom(name)` 和 `PlayEmphasis(name)` 的真实执行链。
- 你要了解插件缺失或设备不支持时会发生什么。

## 依赖与边界

### 它依赖什么

- `VibrateManagerConfig`
- `IAssetManager`
- Nice Vibrations（`NOVA_NICEVIBRATIONS` 编译开关）
- `IVibrateRow` / `IVibrateEmphasisRow` / `IVibrateCustomRow`
- 构建表阶段会通过 `FrameworkManagersGroup` 额外读取 `IConfigManager.Namespace`

### 它对外负责什么

- 加载并解析振动数据
- 构建 Emphasis / Custom 分组缓存
- 统一管理播放、停止、启用状态与设备支持判断

### 它不负责什么

- 不负责场景层配置采集
- 不负责编辑器导表
- 不保证没有底层插件时依然能真实振动

## 核心流程

### 1. Priority 固定为 18

`VibrateManagerBase.Priority => 18`。

如果要比较跨模块顺序，应直接以各自 `Priority` 为准；当前不要把它和其他模块的相对关系写死在文档里。

### 2. Initialize 只接配置并拿 `IAssetManager`

初始化阶段只记录两组单元设置：

- `EmphasisUnitsSettings`
- `CustomUnitsSettings`

随后拿到 `IAssetManager`，并不在此时自动加载数据，也不会在这一阶段缓存 `IConfigManager`。

### 3. Load 会把两类数据都转成分组缓存

同步和异步加载最终都指向同一目标：

- 从数据资产读入 JSON
- 借助 `IConfigManager.Namespace` 构建表
- 生成 `Name -> List<Row>` 的缓存
- 每组按 `Order` 升序排序

### 4. 分组播放不是一次性预设，而是顺序执行一串步骤

- `PlayCustom(name)` 触发一组自定义连续振动
- `PlayEmphasis(name)` 触发一组强调振动步骤

这两条路径都会先取消上一次未完成的播放，再创建新的 `CancellationTokenSource`。

### 5. 插件能力是硬边界

`NOVA_NICEVIBRATIONS` 未开启时：

- `IsSupported` 返回 `false`
- 播放调用不会真正触发底层设备振动

因此这个模块的数据和调用面可以存在，但设备反馈能力并不自动成立。

## 关键行为差异

- `Play(VibrateType)`：走预设枚举映射
- `PlayCustom(float...)`：直接播放持续振动，可带前置等待
- `PlayEmphasis(float...)`：直接播放强调振动，可带前置等待
- `PlayCustom(name)` / `PlayEmphasis(name)`：走表驱动组合播放

## 风险点 / 易错点

- `IConfigManager` 缺失会让振动数据加载失败，这不是 Vibrate 模块单独能补救的。
- `name` 未命中分组时只会 warning，不会抛异常。
- 新播放会取消旧的组合振动流程；如果业务期望并发多条振动链，当前实现并不支持。
- 数值参数都要求在 `0~1` 或非负范围内，越界会直接抛异常。

## 继续阅读

关键源码：

- [VibrateManager.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManager.cs)
- [VibrateManager.Methods.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManager.Methods.cs)
- [VibrateManagerBase.cs](../../../../Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManagerBase.cs)

相关文档：

- [VibrateComponent.md](VibrateComponent.md)
- [IVibrateManager.md](IVibrateManager.md)
- [VibrateManagerConfig.md](VibrateManagerConfig.md)
- [VibrateSettings.md](VibrateSettings.md)
- [IVibrateRow.md](IVibrateRow.md)
- [VibrateType.md](VibrateType.md)
