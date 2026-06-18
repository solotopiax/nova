# IUIGroupHelper

**类签名**：`public interface IUIGroupHelper`
**命名空间**：`NovaFramework.Runtime`

UI 视图分组辅助器接口，负责将逻辑深度映射到 Canvas.sortingOrder。每个视图分组持有一个 IUIGroupHelper 实例，深度换算系数由 UIManager 通过 `SetDepthFactor` 透传写入；自定义实现的 `SetDepth` 必须与 UIView.OnDepthChanged 的层级计算保持一致。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IUIGroupHelper.cs` | 接口定义，位于 `Managers/UIGroupHelper/Interfaces/` |

## 公开 API

```csharp
// 设置视图分组深度换算系数（由 UIManager 在创建视图分组时透传）
void SetDepthFactor(int value);

// 设置视图分组深度
void SetDepth(int depth);
```

## 关联文档

- [UIGroupHelperBase](../Implements/UIGroupHelperBase.md)
- [UIGroupHelper](../UIGroupHelper.md)
- [IUIGroup](../Definitions/IUIGroup.md)
- [UIComponent](../../UIComponent.md)
