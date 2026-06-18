# UIManagerBase

**类签名**：`internal abstract class UIManagerBase : FrameworkManager, IUIManager`
**命名空间**：`NovaFramework.Runtime`

UI 管理器抽象基类，继承 FrameworkManager 并实现 IUIManager 全部接口。为关闭视图、打开视图、分组管理等操作提供便捷重载的默认实现（委托到完整参数版本），核心逻辑方法声明为 abstract 由 UIManager 子类实现。Priority 为 0。

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

**便捷重载（virtual，基类提供默认实现）**：委托到完整参数版本（inObjectPools 默认 `true`）
- `OpenUIViewSync(assetLocation, uiGroupName)` -> 5 参数完整版（pauseCoveredUIView=false, inObjectPools=true, userData=null）
- `OpenUIViewSync(..., bool pauseCoveredUIView)` -> 5 参数完整版（inObjectPools=true, userData=null）
- `OpenUIViewSync(..., object userData)` -> 5 参数完整版（pauseCoveredUIView=false, inObjectPools=true）
- `OpenUIViewSync(..., bool pauseCoveredUIView, object userData)` -> 5 参数完整版（inObjectPools=true）
- `OpenUIViewAsync(...)` -> 同上四个委托
- `CloseUIView(serialID)` -> 委托到 `CloseUIView(serialID, null)`
- `CloseUIViews(serialIDs)` -> 遍历调用 `CloseUIView`
- `CloseAllLoadedUIViews()` -> 委托到 `CloseAllLoadedUIViews(null)`
- `AddUIGroup(name, helper)` -> 委托到 `AddUIGroup(name, 0, helper)`
- `RefocusUIView(uiView)` -> 委托到 `RefocusUIView(uiView, null)`

**核心方法（abstract，子类必须实现）**：
- `Initialize`, `CreateInstancePool`, `Update`, `Shutdown`
- `LoadAsync`
- `OpenUIViewSync<T>` 三重载、`OpenUIViewAsync<T>` 三重载（泛型版，含 `bool pauseCoveredUIView, bool inObjectPools` 完整重载）
- `OpenUIViewSync`（5 参数完整版，带 inObjectPools）, `OpenUIViewAsync`（5 参数完整版，带 inObjectPools）
- `CloseUIView(serialID, userData)`, `CloseUIView(uiView, userData)`
- `CloseAllLoadedUIViews(userData)`, `CloseAllLoadingUIViews`
- `AddUIGroup(name, depth, helper)`
- 所有 Get/Has/Is 查询方法
- `RefocusUIView(uiView, userData)`
- `SetUIViewTargetLocked`, `SetUIViewTargetPriority`
- `GetDeviceSafeArea`

## 关联文档

- [IUIManager](../Interfaces/IUIManager.md)
- [UIManager](../UIManager.md)
- [UIManagerConfig](../Definitions/UIManagerConfig.md)
- [UIComponent](../../UIComponent.md)
