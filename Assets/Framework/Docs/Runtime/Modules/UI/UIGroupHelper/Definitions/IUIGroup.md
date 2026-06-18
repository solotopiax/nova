# IUIGroup

**类签名**：`public interface IUIGroup`
**命名空间**：`NovaFramework.Runtime`

UI 视图分组接口，定义了视图分组的核心契约。每个分组管理一组具有相同层级归属的视图，提供按序列编号或资源路径查询、遍历视图的能力，并暴露分组名称、深度、暂停状态等属性。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IUIGroup.cs` | 接口定义，位于 `Managers/UIGroupHelper/Definitions/` |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Name` | `string` | 视图分组名称（只读） |
| `Depth` | `int` | 视图分组深度（读写） |
| `Pause` | `bool` | 视图分组是否暂停（读写） |
| `UIViewCount` | `int` | 分组中视图数量（只读） |
| `CurrentUIView` | `IUIView` | 当前最顶层的视图（只读） |
| `Helper` | `IUIGroupHelper` | 分组辅助器（只读） |
| `GroupDepthFactor` | `int` | 视图分组深度换算系数（只读，由 UIComponent Inspector 配置，UIManager 透传） |
| `ViewDepthFactor` | `int` | 视图内部深度换算系数（只读，由 UIComponent Inspector 配置，UIManager 透传） |

## 公开 API

```csharp
// 按序列编号判断视图是否存在
bool HasUIView(int serialID);

// 按 Asset 地址判断视图是否存在
bool HasUIView(string assetLocation);

// 按序列编号获取视图
IUIView GetUIView(int serialID);

// 按 Asset 地址获取视图
IUIView GetUIView(string assetLocation);

// 按 Asset 地址获取视图列表（数组返回）
IUIView[] GetUIViews(string assetLocation);

// 按 Asset 地址获取视图列表（List 输出）
void GetUIViews(string assetLocation, List<IUIView> results);

// 获取分组中所有视图（数组返回）
IUIView[] GetAllUIViews();

// 获取分组中所有视图（List 输出）
void GetAllUIViews(List<IUIView> results);
```

## 关联文档

- [IUIGroupHelper](../Interfaces/IUIGroupHelper.md)
- [UIGroupHelperBase](../Implements/UIGroupHelperBase.md)
- [UIGroupHelper](../UIGroupHelper.md)
- [IUIView](../../Definitions/IUIView.md)
