# IUIView

**类签名**：`public interface IUIView`
**命名空间**：`NovaFramework.Runtime`

UI 视图接口，定义了视图的完整生命周期契约。包括初始化、打开、关闭、暂停/恢复、遮挡/恢复、激活、轮询、深度变化等回调，以及视图的基本属性（序列编号、资源路径、分组、深度等）。所有具体视图均须实现此接口。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IUIView.cs` | 接口定义，位于 `Modules/UI/Definitions/` |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `AssetLocation` | `string` | 视图的 Asset 地址（只读） |
| `Name` | `string` | 视图名称（只读） |
| `SerialID` | `int` | 视图序列编号，由框架分配（只读） |
| `UIGroup` | `IUIGroup` | 视图所属的视图分组（只读） |
| `DepthInUIGroup` | `int` | 视图在分组中的深度（只读） |
| `PauseCoveredUIView` | `bool` | 是否暂停被覆盖的视图，全屏独占界面通常为 true（只读） |
| `InObjectPools` | `bool` | 是否启用对象池缓存。true 关闭后回收到对象池复用，false 关闭后直接销毁（只读） |
| `Target` | `object` | 视图实例对象（只读） |

## 公开 API

```csharp
// 初始化视图
void OnInit(int serialID, string assetLocation, IUIGroup uiGroup, bool pauseCoveredUIView, bool inObjectPools, bool isNewInstance, object userData);

// 视图回收
void OnRecycle();

// 视图打开
void OnOpen(object userData);

// 视图关闭
void OnClose(bool isShutdown, object userData);

// 视图暂停
void OnPause();

// 视图暂停恢复
void OnResume();

// 视图遮挡
void OnCover();

// 视图遮挡恢复
void OnReveal();

// 视图激活（Refocus）
void OnRefocus(object userData);

// 视图轮询（每帧调用）
void OnUpdate();

// 视图深度改变
void OnDepthChanged(int uiGroupDepth, int depthInUIGroup);
```

## 关联文档

- [UIView](UIView.md)
- [IUIGroup](../UIGroupHelper/Definitions/IUIGroup.md)
