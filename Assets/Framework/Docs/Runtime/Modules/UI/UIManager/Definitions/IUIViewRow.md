# IUIViewRow

**类签名**：`public interface IUIViewRow`
**命名空间**：`NovaFramework.Runtime`

UI 视图数据行接口，Luban 生成的 UI 视图 bean 类须实现此接口，框架侧通过接口读取行字段，不对各字段使用反射；替代已删除的 `UIViewEntry` 中间类型。所有打开入口都从匹配行读取资源地址、分组、暂停和池化配置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Runtime/Modules/UI/Managers/UIManager/Definitions/IUIViewRow.cs` | `interface IUIViewRow` | UI 视图数据行接口定义 |

---

## §3 继承关系

```
IUIViewRow (interface)
```

---

## §5 完整公开 API

```csharp
public interface IUIViewRow
{
    /// <summary>表内行名称；作为注册表 Key，泛型入口要求与 typeof(T).Name 对齐。</summary>
    string Name { get; }

    /// <summary>视图资源地址（格式由业务层约定，框架侧透传）。</summary>
    string AssetLocation { get; }

    /// <summary>视图所属的视图分组名称。</summary>
    string UIGroupName { get; }

    /// <summary>是否暂停被当前视图覆盖的下层视图。</summary>
    bool PauseCoveredUIView { get; }

    /// <summary>是否启用对象池缓存。true 关闭后回收到对象池等待复用，false 关闭后直接销毁。</summary>
    bool InObjectPools { get; }
}
```

---

`Name` 是 UI 表某一行的值，不是 xlsx 文件名。泛型入口按短类名 `typeof(T).Name` 查找；`OpenUIViewSync/Async(string uiViewName, object userData = null)` 直接按该值查找。`OpenUIViewByAssetSync/Async` 则在整个注册表内匹配 `AssetLocation`，要求只有一行。多行共享地址时须按各行 `Name` 打开，分组始终取所选行的 `UIGroupName`。

无后缀字符串入口只按名称，`ByAsset` 只按资源地址，不自动检测或互相回退；第二个字符串始终是业务数据。原“地址 + 分组”调用须迁移到 `ByAsset`，地址不唯一时改按 `Name`。

`PauseCoveredUIView` 和 `InObjectPools` 始终以所选行配置为准；旧泛型 bool 重载已标记 `[Obsolete]`，传入值不再覆盖表格。`Desc` 等说明列可存在于生成类型中，但不属于此运行时接口。表的导出校验要求这五列存在且每条有效数据行均非空，两个开关必须能解析为 bool；运行时找不到配置时返回 `-1`，不补默认行继续打开。

## §11 使用示例

以下示意行类型包含全部五个接口成员；实际业务数据应维护在权威 UI 表并正式导出，不手改生成代码。

```csharp
public sealed class UIViewRowExample : IUIViewRow
{
    public string Name { get; set; }
    public string AssetLocation { get; set; }
    public string UIGroupName { get; set; }
    public bool PauseCoveredUIView { get; set; }
    public bool InObjectPools { get; set; }
}
```

注册表加载后，由公开入口查找已导出的行；暂停和池化配置自动取自该行：

```csharp
// UI 表的 Name 列值。
int byName = Nova.UI.OpenUIViewAsync("DemoToastView", "Hello");

// UI 表的 AssetLocation 列值，须在已加载注册表内唯一。
int byAsset = Nova.UI.OpenUIViewByAssetAsync("DemoToastView");
```

---

## §13 关联文档

- [UIManager.md](../UIManager.md)（消费者，持有 `Dictionary<string, IUIViewRow>` 注册表）
- [ITable.md](../../../../Core/Table/ITable.md)（表容器接口）
