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

- 按 DataFormat 从 JSON 或 Binary 数据资产读取
- 借助 `IConfigManager.Namespace` 构建表
- 生成 `Name -> List<Row>` 的缓存
- 每组按 `Order` 升序排序

无效 Unit（null 或空 AssetLocation）跳过；两路分别构造 `VibrateEmphasisTables`、`VibrateCustomTables`，各自聚合全部表，跨表同名行属于同一组。按 `Order` 稳定升序排列，同序号保留表遍历与 DataList 的原顺序；需要确定业务顺序时仍建议使用不同序号。

两路先构建候选缓存，全部有效才一起替换；配置了有效 Unit 但生成表失败、未得到任何表、行接口不匹配或非空行的 Name 为空时构组失败，并保留上次完整缓存。无有效 Unit 的区域成功加载后替换为空缓存；两路均为空时不需要构造 Tables。DataList 为空或 null、空行本身不报错，但没有可播放组；成功不证明指定组存在。

资源委托先取 TextAsset、立即 Release handle，再将同一 TextAsset 返回给接收器，后续 release 委托为空。这里没有复制 bytes 或 finally 保证失败释放；不能把这条数据加载实现理解为持有句柄直至解析结束。同步路径遇到接收器失败就终止，公开同步入口仍不返回 bool；异步路径汇总有效任务再构组，并向上返回构组失败；Initialize 仅保存列表引用、不清旧组、不取消旧播放，Shutdown 也不取消在途数据加载。

### 4. 分组播放不是一次性预设，而是顺序执行一串步骤

- `PlayCustom(name)` 触发一组自定义连续振动
- `PlayEmphasis(name)` 触发一组强调振动步骤

名称命中后，这两条路径都会先取消上一次未完成的播放，再创建新的 `CancellationTokenSource`。名称为空或未命中只警告，不取消当前播放。

Custom 每步等待 PreDuration 后触发持续振动，再等待 Duration；Emphasis 每步等待 PreDuration 后触发强调，再等待 Interval。正值才等待。组播放不经 ByParams 校验，也不在每一步重新检查 Enable/IsSupported；取消异常被吞掉，其余步骤异常记 Warning 后结束。没有插件时组仍可能执行等待，只是不调用设备。

### 5. 插件能力是硬边界

`NOVA_NICEVIBRATIONS` 未开启时：

- `IsSupported` 返回 `false`
- 播放调用不会真正触发底层设备振动

因此这个模块的数据和调用面可以存在，但设备反馈能力并不自动成立。

## 关键行为差异

- `Play(VibrateType)`：走预设枚举映射
- `PlayCustomByParams(intensity, sharpness, preDuration, duration)`：直接播放持续振动，可带前置等待，不读表
- `PlayEmphasisByParams(amplitude, frequency, preDuration)`：直接播放单次强调振动，可带前置等待，不读表
- `PlayCustom(name)` / `PlayEmphasis(name)`：走表驱动组合播放

| 操作 | 取消和停止边界 |
|---|---|
| 无参/预设 Play | 无参默认 PlayConstant(1,1,0.3)，None 不播放，未知预设回退 Selection；不替换组 CTS |
| ByParams 的延迟 | EnsureCts 仅在为空时创建，不取消已有等待，所以数次延迟可以重叠；等待完成时重新检查 Enable |
| Emphasis 时间参数 | ByParams 只有触发前的 preDuration；表行 Interval 独立控制该步触发后的等待 |
| Enable=false | 调用入口短路，不等于取消已有 CTS 或显式 StopAll；无插件时本地开关初始 true，仍不代表支持设备 |
| StopAll | 取消/Dispose 旧 CTS 并新建，再在插件分支调用 HapticController.Stop |
| Shutdown | 取消并释放 CTS、清组和列表引用，但不调用 HapticController.Stop，不置空 IAssetManager，不取消在途载表 |

Cancel 回调或插件异常未被统一捕获，可能阻止后续 Dispose/停止/清理。返回 void 的播放门面没有完成结果或触感成功证明。

旧数值调用需要迁移为 `ByParams`；旧四参数 `PlayEmphasis` 删除从未生效的 `interval`，改为三参数。接口、基类、默认实现、组件门面及自定义实现必须同步迁移，按名调用保持不变。

## 风险点 / 易错点

- `IConfigManager` 缺失会让振动数据加载失败，这不是 Vibrate 模块单独能补救的。
- `name` 未命中分组时只会 warning，不会抛异常。
- 新播放会取消旧的组合振动流程；如果业务期望并发多条振动链，当前实现并不支持。
- ByParams 在 Enable=true 时比较强度/锐度/振幅/频率感是否在 0～1、时间是否非负；纯比较不会拒绝 NaN，正无限时间也没有有限性门。表行不经过这组校验，调用方不能依赖它清洗全部数据。

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
