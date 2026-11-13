# KitsViewWindow

**类签名**：`internal sealed partial class KitsViewWindow : EditorWindow`  
**命名空间**：`NovaFramework.Editor`  
**菜单入口**：`Nova/Open Kits View`

`KitsViewWindow` 用于浏览当前工程中已安装的 Kit 包。它会扫描 `Packages/packages-lock.json` 与 `Packages/manifest.json`，筛出 `com.solotopia.nova.framework.kit` 前缀的包，并展示包信息与 [按包定位：Nova/Protos/*.proto](../../Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现) 内容。

## 文件拆分

| 文件 | 说明 |
|---|---|
| [KitsViewWindow.cs](../../../Scripts/Editor/Windows/KitsViewWindow/KitsViewWindow.cs) | `Open`、`OnEnable`、`OnGUI`、`OnDestroy` |
| [KitsViewWindow.Visitors.cs](../../../Scripts/Editor/Windows/KitsViewWindow/KitsViewWindow.Visitors.cs) | 常量、样式、滚动状态 |
| [KitsViewWindow.Definitions.cs](../../../Scripts/Editor/Windows/KitsViewWindow/KitsViewWindow.Definitions.cs) | `KitEntry`、`ProtoFileEntry` |
| [KitsViewWindow.Methods.cs](../../../Scripts/Editor/Windows/KitsViewWindow/KitsViewWindow.Methods.cs) | 数据收集、package.json 解析、Proto 收集、条目绘制 |

## 当前行为

- `Open()` 打开窗口并设置最小尺寸 `600 x 400`。
- `OnEnable()` 调用 `CollectKitEntries()`。
- `CollectKitEntries()` 会：
  - 从 `Packages/packages-lock.json` 读取所有已解析依赖；
  - 从 `Packages/manifest.json` 判断某个包是否是 `file:` 开发引用；
  - 在开发模式下解析本地包目录，在消费者模式下解析 `Library/PackageCache`；
  - 读取 `package.json` 的名称、显示名、版本、描述、依赖；
  - 收集 [按包定位：Nova/Protos/*.proto](../../Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现) 文件内容。

## 关键边界

上述目录候选描述窗口当前实现，不是 Agent 通用的包根规则。需要读取实际 Kit 时按 [SDK / Kit 包文档发现](../../Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现) 核对当前 `resolvedPath`；缓存中存在目录不能单独证明它是当前安装版本。

- 这是一个只读浏览窗口，不修改 `manifest.json`。
- 它只关心 Kit 包，不展示普通 Framework 包。
- Proto 内容直接来自包目录中的文本文件，不做额外编译或缓存。

## 关联文档

- [../Editor.md](../Editor.md)
- [./PlugPalsWindow.md](./PlugPalsWindow.md)

## 本地发现的精度

清单只从 lock dependencies 取包名，不核 version/source/depth、注册错误或实际 resolvedPath；Kit 前缀未检查点边界。仅直接 manifest 的小写 file: 按 Packages 相对解析，其他来源选择 PackageCache 中第一个 package@* 目录，未按 lock 精确版本匹配，也未覆盖 embedded/传递 file 引用。因此窗口内容是候选目录的本地元数据，不能证明实际加载的包版本或依赖满足。

Proto 只读 Nova/Protos 顶层文件，不递归、无排序。解析/IO 异常直接传播，可留下部分列表；刷新仍在 GUI 同步执行，已打开窗口再次 Open 不一定重收集。展示的是完整原文，按 LF 数估算高度，不是协议编译或正式导出；GUI 缩进/ScrollView 没有完整 finally，动态字体生命周期未做专门释放。
