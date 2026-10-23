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
    void SetDepthFactor(int value);
    void SetDepth(int depth);
}
```

`UIGroupHelperBase` 保存从 UIComponent→UIManager→UIGroup 下发的深度换算系数，不再有固定 `c_DepthFactor=10000` 常量。SetDepthFactor 仅存字段，不重算已有 Canvas；SetDepth 才计算 `factor * depth`、启用 overrideSorting 并应用 sortingOrder。UIGroup 构造先写 factor，再通过 Depth setter 设置深度；初始 depth=0 与字段相同时不会首次调用 SetDepth。

SetDepth 懒 GetOrAdd Canvas，计算结果超 short 范围时记录并钳制，但 int 乘法发生在范围判断前，没有 long 保护。字段先赋值、Canvas 后修改，异常不回滚。Awake 只确保 Canvas，Start 拉伸 RectTransform，不额外重算深度。

---

## §11 使用示例

```csharp
// UIManager 创建分组时注入，不需手动操作
// 组根排序由配置 factor 与组 depth 决定；视图内增量由 UIView 计算
IUIGroupHelper helper = uiGroup.Helper;
helper.SetDepthFactor(uiGroup.GroupDepthFactor);
helper.SetDepth(1);
```

---

## §13 关联文档

- [UIManager.md](../UIManager/UIManager.md)
- [UIComponent.md](../UIComponent.md)
