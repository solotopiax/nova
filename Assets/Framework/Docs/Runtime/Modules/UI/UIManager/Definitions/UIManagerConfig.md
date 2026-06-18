# UIManagerConfig

**类签名**：`public class UIManagerConfig`
**命名空间**：`NovaFramework.Runtime`

UI 管理器配置，由 UIComponent 在 Start 阶段构造并传入 `IUIManager.Initialize`。当前 `UIComponent` 会写入实例池参数、回收队列配置、深度换算系数和 `UnitSettings`；`SafeAreaProvider` 保留为扩展注入点，默认不由 `UIComponent` 赋值。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `UIManagerConfig.cs` | 配置数据类，位于 `Managers/UIManager/Definitions/` |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `InstanceAutoReleaseInterval` | `float` | 视图实例对象池自动释放可释放对象的间隔秒数 |
| `InstanceCapacity` | `int` | 视图实例对象池的容量上限 |
| `InstanceExpireTime` | `float` | 视图实例对象在对象池中的过期秒数 |
| `InstancePriority` | `int` | 视图实例对象池的优先级 |
| `DestroyMaxNumPerFrame` | `int` | 每帧最多销毁的 UI 数量（回收队列每帧处理上限） |
| `GroupDepthFactor` | `int` | 视图分组深度换算系数（由 UIComponent Inspector 配置，UIManager 透传到 UIGroup 与 UIGroupHelper） |
| `ViewDepthFactor` | `int` | 视图内部深度换算系数（由 UIComponent Inspector 配置，UIManager 透传到 UIGroup） |
| `UnitSettings` | `List<UIUnitSetting>` | UI 注册表单元设置列表，每个 unit 对应一个注册表 JSON 文件 |
| `SafeAreaProvider` | `ISafeAreaProvider` | 安全区域数据提供者；null 时使用 DefaultSafeAreaProvider（基于 Screen.safeArea）；WebGL 平台可注入平台 SDK 实现 |

## 关联文档

- [IUIManager](../Interfaces/IUIManager.md)
- [UIManagerBase](../Implements/UIManagerBase.md)
- [UIComponent](../../UIComponent.md)
- [UISettings](UISettings.md)
- [ISafeAreaProvider](../Interfaces/ISafeAreaProvider.md)
