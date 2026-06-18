# UIGroupHelper

**类签名**：`internal sealed class UIGroupHelper : UIGroupHelperBase`
**命名空间**：`NovaFramework.Runtime`

UI 视图分组辅助器，每个 UIGroup 对应一个实例，控制 Canvas 渲染层级。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Interfaces/IUIGroupHelper.cs` | `interface IUIGroupHelper` | 深度设置接口 |
| `Implements/UIGroupHelperBase.cs` | `abstract UIGroupHelperBase : MonoBehaviour, IUIGroupHelper` | 基类：Canvas 缓存、深度计算、全屏 RectTransform 初始化 |
| `Implements/UIGroupHelper.cs` | `internal sealed class UIGroupHelper : UIGroupHelperBase` | 默认实现（空继承，可扩展） |

---

## §5 完整公开 API

```csharp
public interface IUIGroupHelper
{
    void SetDepth(int depth);
}
```

`UIGroupHelperBase` 提供常量（`c_DepthFactor = 10000`）和 `SetDepth` 实现，`UIGroupHelper` 无额外成员。

---

## §11 使用示例

```csharp
// UIManager 创建分组时注入，不需手动操作
// 深度公式：Canvas.sortingOrder = GroupDepth × 10000 + ViewDepth × 100
IUIGroupHelper helper = uiGroup.Helper;
helper.SetDepth(1); // Popup 分组 → sortingOrder = 10000
```

---

## §13 关联文档

- [UIManager.md](../UIManager/UIManager.md)
- [UIComponent.md](../UIComponent.md)
