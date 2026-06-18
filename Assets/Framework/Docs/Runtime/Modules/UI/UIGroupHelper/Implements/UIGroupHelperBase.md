# UIGroupHelperBase

**类签名**：`public abstract class UIGroupHelperBase : MonoBehaviour, IUIGroupHelper`
**命名空间**：`NovaFramework.Runtime`

UI 视图分组辅助器抽象基类。每个视图分组对应一个辅助器实例，负责管理分组节点的 Canvas 组件和 sortingOrder。Awake 时自动获取或挂载 Canvas，Start 时将 RectTransform 拉伸铺满父节点。SetDepth 中包含 Canvas 懒初始化（防止 Awake 之前被调用）和 sortingOrder 溢出校验。深度换算系数由 UIComponent Inspector 配置，UIManager 创建分组时通过 `SetDepthFactor` 透传写入。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `UIGroupHelperBase.cs` | 抽象基类实现，位于 `Managers/UIGroupHelper/Implements/` |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_DepthFactor` | `int`（private） | 视图分组深度换算系数，由 UIManager 在创建视图分组时透传写入 |
| `m_Depth` | `int`（private） | 当前视图分组深度 |
| `m_CachedCanvas` | `Canvas`（private） | 缓存的 Canvas 组件引用 |

## 公开 API

```csharp
// 设置视图分组深度换算系数
// 由 UIManager 在创建视图分组时透传写入，乘以视图分组深度后得到 Canvas.sortingOrder
public virtual void SetDepthFactor(int value);

// 设置视图分组深度，更新 Canvas.sortingOrder = m_DepthFactor * depth
// 包含 Canvas 懒初始化和 sortingOrder 溢出校验（short 范围 -32768~32767）
public virtual void SetDepth(int depth);
```

## 生命周期

| 方法 | 说明 |
|------|------|
| `Awake()` | 获取或挂载 Canvas 组件 |
| `Start()` | RectTransform 全屏拉伸（sortingOrder 已在 SetDepth 中统一设置，此处不再冗余赋值） |

## 关联文档

- [IUIGroupHelper](../Interfaces/IUIGroupHelper.md)
- [UIGroupHelper](../UIGroupHelper.md)
- [UIComponent](../../UIComponent.md)
