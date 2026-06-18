# UIView

**类签名**：`public abstract partial class UIView : MonoBehaviour, IUIView`
**命名空间**：`NovaFramework.Runtime`

UI 视图抽象基类，业务层所有视图均继承此类。提供完整的生命周期默认实现（初始化、打开/关闭、暂停/恢复、遮挡、激活、轮询、深度变化），内置 Canvas 管理、Layer 递归设置和深度增量计算。子类通过重写生命周期方法实现具体视图逻辑。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `UIView.cs` | 公开方法与 IUIView 生命周期接口实现 |
| `UIView.Methods.cs` | 非公开实现方法：`OnInit(userData)` 钩子、`InternalSetVisible`、`InternalSetLayerRecursively` |
| `UIView.Visitors.cs` | 字段与属性访问器：框架状态字段、常量 |

## 关键字段/属性

| 字段/属性 | 类型 | 说明 |
|-----------|------|------|
| `AssetLocation` | `string` | 视图的 Asset 地址 |
| `Name` | `string` | 视图名称（即 GameObject.name，读写） |
| `SerialID` | `int` | 视图序列编号，回收时重置为 0 |
| `UIGroup` | `IUIGroup` | 视图所属的视图分组 |
| `DepthInUIGroup` | `int` | 视图在分组中的深度 |
| `PauseCoveredUIView` | `bool` | 是否暂停被覆盖的视图 |
| `InObjectPools` | `bool` | 是否启用对象池缓存。true 关闭后回收到对象池复用，false 关闭后直接销毁 |
| `Target` | `object` | 视图实例（即当前 GameObject） |
| `CachedTransform` | `Transform` | 缓存的 Transform 引用 |
| `Available` | `bool` | 视图是否可用（已初始化且未关闭） |
| `Visible` | `bool` | 视图是否可见（需 Available 为 true） |
| `OriginalDepth` | `int` | Canvas 初始 sortingOrder，用于深度增量计算 |
| `Depth` | `int` | 当前 Canvas.sortingOrder |

## 公开 API

```csharp
// ===== IUIView 生命周期（框架驱动调用） =====

// 框架初始化入口，维护框架侧字段，新实例时触发 OnInit(userData) 钩子
void OnInit(int serialID, string assetLocation, IUIGroup uiGroup, bool pauseCoveredUIView, bool inObjectPools, bool isNewInstance, object userData);

// 视图回收，重置框架字段（含 m_Available、m_Visible）；子类重写时应调用 base.OnRecycle()
virtual void OnRecycle();

// 视图打开，设置可用/可见状态
virtual void OnOpen(object userData);

// 视图关闭，还原 Layer 并设置不可用/不可见状态
virtual void OnClose(bool isShutdown, object userData);

// 视图暂停，默认隐藏视图
virtual void OnPause();

// 视图暂停恢复，默认显示视图
virtual void OnResume();

// 视图遮挡（空实现，子类可重写）
virtual void OnCover();

// 视图遮挡恢复（空实现，子类可重写）
virtual void OnReveal();

// 视图激活（空实现，子类可重写）
virtual void OnRefocus(object userData);

// 视图轮询（空实现，子类可重写）
virtual void OnUpdate();

// 视图深度改变，增量更新所有子 Canvas 的 sortingOrder
virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup);

// ===== 业务层调用 =====

// 关闭视图
void Close();

// 播放 UI 音效（预留接口）
void PlayUISound(int uiSoundID);

// 设置全局主字体
static void SetMainFont(Font mainFont);
```

## 非公开方法（UIView.Methods.cs）

```csharp
// 视图初始化钩子（仅新实例首次创建时触发）
// 挂载 Canvas，RectTransform 全屏拉伸
protected virtual void OnInit(object userData);

// 设置视图可见性，默认通过 GameObject.SetActive 实现
protected virtual void InternalSetVisible(bool visible);

// 递归设置 GameObject 及子节点的 Layer
private static void InternalSetLayerRecursively(GameObject go, int layer);
```

## 深度计算逻辑

`OnDepthChanged` 使用增量方式更新所有子 Canvas 的 sortingOrder：

```
targetDepth = m_UIGroup.GroupDepthFactor * uiGroupDepth + m_UIGroup.ViewDepthFactor * depthInUIGroup
deltaDepth  = targetDepth - (oldDepth - OriginalDepth)
```

遍历所有子 Canvas 统一加同一个 delta，保留了 View 内部子 Canvas 的相对层级关系。两个换算系数由 UIComponent Inspector 配置，UIManager 透传至 UIGroup，再由 UIView 通过 `m_UIGroup` 直接读取。

## 关联文档

- [IUIView](IUIView.md)
- [IUIGroup](../UIGroupHelper/Definitions/IUIGroup.md)
- [UIManager](../UIManager/UIManager.md)
- [UIComponent](../UIComponent.md)
