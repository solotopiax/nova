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

`PlayUISound(int uiSoundID)` 当前方法体没有执行播放逻辑，调用后不会发起声音请求。业务音效需接入 [SoundComponent](../../Sound/SoundComponent.md) 的实际播放与释放链，不能以该预留方法调用完成作为播放成功证据。

## 状态与初始化失败

公开 OnInit 先写序号、地址、分组及策略；复用实例不再调用受保护初始化，新实例才执行该钩子。钩子异常被记录并吞掉，不向 Manager 返回失败，已写字段不回滚，因此后续 OnOpen 仍可能执行。默认钩子增加 Canvas/GraphicRaycaster、记根 Layer/原深度并拉伸 RectTransform，要求有效的 UI 根；自动本地化/主字体应用代码当前未执行，SetMainFont 只缓存旧 UGUI 字体入口。

Available/Visible 是框架状态：Visible 读取 Available 与内部可见字段，不检查 activeInHierarchy，父节点关闭时仍可能返回 true。Visible setter 在不可用时只警告，合法修改先写字段再 SetActive；激活失败不回滚。OnPause/OnResume 默认通过 Visible 禁用/启用 GameObject，不重载数据。

OnClose 把根及全部子对象统一设回最初根 Layer，并不记忆各子对象独立 Layer，再隐藏并清 Available；失败可留下部分状态。OnRecycle 只重置序号/组/深度/策略/可见字段，保留 AssetLocation、Canvas 与 Transform 缓存，不主动 SetActive、销毁或替代 OnClose。默认 OnCover/OnReveal/OnRefocus/OnUpdate 为空，业务行为须由子类实现。

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

这里使用 int 乘加，没有组 Helper 的 short 钳制或 long 溢出保护；缺 Canvas/分组或逐 Canvas 写入失败仍会中断，不能把 Helper 的检查推导成所有子 Canvas 都有同样保护。

## 关联文档

- [IUIView](IUIView.md)
- [IUIGroup](../UIGroupHelper/Definitions/IUIGroup.md)
- [UIManager](../UIManager/UIManager.md)
- [UIComponent](../UIComponent.md)
