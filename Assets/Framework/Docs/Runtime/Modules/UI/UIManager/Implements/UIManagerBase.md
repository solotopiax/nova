# UIManagerBase

**类签名**：`internal abstract class UIManagerBase : FrameworkManager, IUIManager`
**命名空间**：`NovaFramework.Runtime`

UI 管理器抽象基类，继承 FrameworkManager 并实现 IUIManager 全部接口。它为打开、关闭和分组管理提供重载转发，核心查表与实例操作由 UIManager 实现。Priority 为 0。该类型是 internal，外部自定义 IUIManager 不能继承它来补齐接口。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `UIManagerBase.cs` | 抽象基类，位于 `Managers/UIManager/Implements/` |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Priority` | `int` | 框架管理器优先级，固定返回 0 |
| `UIGroupCount` | `int` | 视图分组数量（abstract） |
| `InstanceAutoReleaseInterval` | `float` | 对象池自动释放间隔（abstract，读写） |
| `InstanceCapacity` | `int` | 对象池容量上限（abstract，读写） |
| `InstanceExpireTime` | `float` | 对象池过期秒数（abstract，读写） |
| `InstancePriority` | `int` | 对象池优先级（abstract，读写） |
| `DestroyMaxNumPerFrame` | `int` | 每帧最多销毁 UI 数量（abstract，读写） |

## 方法实现策略

基类将 IUIManager 的多重载方法分为两类：

**便捷重载（virtual，基类提供默认实现）**：

- 旧泛型 bool 重载 -> `OpenUIViewSync<T>(userData)` 或 `OpenUIViewAsync<T>(userData)`
- `CloseUIView(serialID)` -> 委托到 `CloseUIView(serialID, null)`
- `CloseUIViews(serialIDs)` -> 遍历调用 `CloseUIView`
- `CloseAllLoadedUIViews()` -> 委托到 `CloseAllLoadedUIViews(null)`
- `AddUIGroup(name, helper)` -> 委托到 `AddUIGroup(name, 0, helper)`
- `RefocusUIView(uiView)` -> 委托到 `RefocusUIView(uiView, null)`

旧泛型 bool 重载保留签名并标记 `[Obsolete]`。暂停和池化参数不再生效，实际值由匹配到的 UI 表行决定；基类不提供 `false/true` 的开关默认值，也不直接加载资源。原非泛型“资源地址 + 分组”重载及字符串 bool 重载均已移除。

**核心方法（abstract，子类必须实现）**：

- `Initialize`, `CreateInstancePool`, `Update`, `Shutdown`
- `LoadSync`, `LoadAsync`
- `OpenUIViewSync<T>(object userData = null)`、`OpenUIViewAsync<T>(object userData = null)`
- `OpenUIViewSync(string uiViewName, object userData = null)`、`OpenUIViewAsync(string uiViewName, object userData = null)`
- `OpenUIViewByAssetSync(string assetLocation, object userData = null)`、`OpenUIViewByAssetAsync(string assetLocation, object userData = null)`
- `CloseUIView(serialID, userData)`, `CloseUIView(uiView, userData)`
- `CloseAllLoadedUIViews(userData)`, `CloseAllLoadingUIViews`
- `AddUIGroup(name, depth, helper)`
- 所有 Get/Has/Is 查询方法
- `RefocusUIView(uiView, userData)`
- `SetUIViewTargetLocked`, `SetUIViewTargetPriority`
- `GetDeviceSafeArea`

打开方法统一依赖已加载的注册表：泛型按 `typeof(T).Name`，无后缀字符串重载按表内行 `Name`，`ByAsset` 按唯一资源地址。名称不是 xlsx 文件名，接口不会自动猜测首参身份。第二个参数始终是 `userData`，字符串也原样传递。配置缺失或地址匹配多行时返回 `-1`，不创建打开请求。查表成功后，资源、分组、暂停和池化全部从同一行读取；共享地址只能按 `Name` 消歧。

异步方法仍同步返回请求编号；后续加载或实例化失败通过 `OnOpenUIViewFail` 通知，主动关闭加载中请求仍按取消处理。

## Breaking：升级迁移

旧泛型调用应去掉 bool 开关参数，将行为维护在 UI 表。旧“资源地址 + 分组”调用应改用 `ByAsset`，地址有歧义时改按 `Name`；新名称入口的第二个字符串是业务数据，不是分组。外部自定义 `IUIManager` 需自行实现新增的两个名称字符串重载和两个 `ByAsset` 方法，不能依赖此内部基类；完整迁移说明见 [IUIManager](../Interfaces/IUIManager.md)。

## 关联文档

- [IUIManager](../Interfaces/IUIManager.md)
- [UIManager](../UIManager.md)
- [UIManagerConfig](../Definitions/UIManagerConfig.md)
- [UIComponent](../../UIComponent.md)
