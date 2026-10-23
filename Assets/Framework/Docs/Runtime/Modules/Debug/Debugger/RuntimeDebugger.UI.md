# RuntimeDebugger UI 与布局

内置调试器使用 UGUI Prefab。必需字段由 DebugMonoBehaviourEx 检查/填充，但不能代替 Prefab 完整性；多数显示函数按顺序修改组件，遇到资源或回调异常不会回滚此前状态。显示、activeSelf、CanvasGroup 和服务期望状态应分别理解。

## Tab 与面板附属控件

DebuggerTabController.Start 从 Resources 加载 DebugTab，过滤 DisabledTabs 与 IEnableTab，克隆并注册；预先请求的 Tab 优先，否则尝试 Console，再取首个可用页。DefaultTab 当前未被这段启动逻辑消费；Start 前 OpenTab 返回 true 只表示排队，不证明该 Tab 最终存在。

DebugTabController.AddTab 先设置父级/隐藏，再创建侧栏按钮、登记并按 SortIndex 冒泡排序；相同顺序保持稳定，重复 Key/对象未拒绝。MakeActive 会隐藏旧页、保存并显示新页，最后同步发 ActiveTabChanged；同页也重新执行。null/非成员不能当作“清空 Tab”，回调异常会留下已切页状态。Tabs 是原 DebugList 视图；ActiveTab 的字符串到枚举解析也可能失败。

DebugTabButton 用固定的活动/悬浮/普通颜色改 Background、Border、Icon 和 Text，关闭原 Button transition；不把 interactable 当作悬浮判断门。SetIcon 只作用于 Image，null sprite 隐藏 Image。Tag 行的 SetSelected 仅改视觉，不做选择互斥。

MobileMenuController.Enable 时临时忽略布局，按父宽/PeekAmount/MaxMenuWidth 设置左侧菜单，创建透明覆盖关闭按钮并订阅 Tab 切换；Open/Close 只改变动画目标，Update 用 SpringLerp 移动。重启用不统一复位目标位置，也不随尺寸变化自动重建全部几何；附加关闭 Canvas 没有完整继承 DebugLayer/相机配置，异常可留部分对象。

InfoTab 以分类创建 InfoBlock，不删除消失分类；单次刷新或每帧刷新都会执行动态 InfoEntry.Value。某些格式判断后会再次读取 getter，同一项可执行多次；动态值若改变类型可在格式化时失败。私有项仍显示，标题/值直接进入 rich text，未统一转义。

VersionTextBehaviour 以 string.Format(Format, RuntimeDebugger.Version) 写文字，坏格式会失败；LoadingSpinner 按 Duration/Count 逐步旋转，Duration=0、负步长等值可能令 while 不前进。ScrollSettingsBehaviour 只在 Awake 设置滚动速度和非移动平台的 Clamped/无惯性，不随设置动态重配。

## 虚拟列表

VirtualVerticalLayoutGroup 保存条目列表、可见行和行缓存；行通过 IVirtualView.SetDataContext 接受真实数据，ConsoleEntryView/MemoryRowView 各自转换。AddItem 允许 null/重复；ClearItems 和回收行保留 Data/View/对象引用，缓存对象仍 active，只移动到可视区域外，不等于释放数据或停掉组件 Update。

| 操作 | 当前行为与限制 |
|---|---|
| ItemHeight | 懒读 Prefab preferredHeight/rect，近似 0 时回退 10；负 preferredHeight 不统一修正，尺寸变化不自动使缓存失效 |
| ScrollUpdate | 由 ScrollRect anchoredY、行高/间距与 RowPadding 计算可见区；实时重建行并置布局脏，stride 非正没有完整校验 |
| GetRow/PopulateRow | 优先复用同数据/同奇偶缓存，否则克隆；缺 IVirtualView 可能警告后继续失败，样式绑定或上下文异常可留部分行 |
| RemoveItem | 只移首匹配，行索引用 IndexOf 重算；重复对象和可见索引缓存可发生不一致 |
| SelectedItem | 按引用与当前条目查找，状态先写再通知；EnableSelection=false 也会阻止清选择，Clear 后通常等 Update 再清 |
| 点击行 | 用命中对象 Transform 的位置换算行号，不直接用指针局部坐标，不统一包含 padding/spacing/bottom 对齐 |
| 布局 | minHeight 包含每项间距，含末尾间距；垂直定位实际按顶部索引，不另实现 AlignBottom；无完整负尺寸保护 |

可见项 cull 循环删除当前元素后未统一修正下一索引，边界增删与缓存不能当作稳定快照。Awake 的 ScrollRect 监听没有独立 OnDestroy 解除；SetDirty 在未激活时也不保证排下一轮刷新。Console 的 dirty 合并和三帧后滚到底只控制显示，不构成采集同步。

## 尺寸、布局与安全区

| 组件 | 职责与特殊边界 |
|---|---|
| FlowLayoutGroup | 按自身 rect 宽、padding/spacing 和子项 preferred 尺寸换行；Lower 反向、Center/Right 分配余量。LayoutRow 实际使用共享行缓存，SetLayout 的 width 参数并非工作宽来源；失败可留共享缓存 |
| ContentFitText | 委托 CopySource 的布局值并监听 LayoutDirty；当前 minHeight 读取 flexibleHeight。缺源 getter 可返回 -1，但 CalculateLayoutInput 与订阅不全具备同样空值保护；换源不主动退旧监听 |
| CopyLayoutElement / CopyPreferredSize | 从源 LayoutUtility 取最小/首选尺寸，加轴向 padding，通常优先级 2、无 flexible |
| CopyPreferredSizes | 对多个源的首选尺寸取 Min/Max；空/坏源、非法操作和循环依赖未统一规范化 |
| CopySizeIntoLayoutElement | 取源实际 rect 尺寸；不是自动订阅所有源变化 |
| Copy/FloatOverElement | 复制锚点、位置、offset、size、scale、pivot；不同 parent 不等于世界重合，不复制 rotation，却用 tracker 驱动 All，未统一 OnDisable 清 tracker |
| SafeAreaSizer | 布局计算时按 Screen.safeArea/Canvas.pixelRect 换本地边距并缩放；Update 清宽高，依赖之后布局重新计算，不监听所有屏幕变化；像素尺寸为零/Canvas offset 等边界没有完整保护 |
| ResponsiveBase | 尺寸变更排下轮刷新，Editor 非 Play 每帧刷新；失败保留部分状态，刷新中再置脏可被尾部清标记覆盖 |
| ResponsiveEnable | 正阈值才参与 Above/Below 判断，两个维度 AND，顺序切 GameObject/Behaviour；重复目标以后项覆盖，可触自身生命周期 |
| ResponsiveResize | 选择不超过当前宽度的最大阈值，同阈值首项；只应用正 ElementWidth，无匹配保留旧宽 |
| DragHandle | 拖动按 delta/CanvasScaler.scaleFactor 写 preferred 或 sizeDelta；拖中夹 Min/Max，结束只夹 Min，再提交当前 rect，可能读到布局尚未重建的值 |
| ScrollToBottomBehaviour | 控制上下端按钮 CanvasGroup 并写 normalizedPosition；top 模式底部也隐藏；缺引用在 Start 前的 OnEnable 仍可失败，没有独立退滚动监听 |

Unity 6000.6 / uGUI 2.6 的 `ILayoutElement` 还要求 `maxWidth`、`maxHeight`。ContentFitText、CopyLayoutElement 与 SafeAreaSizer 均返回 `-1f`，表示本组件不提供最大尺寸约束；现有最小、首选、弹性尺寸及优先级保持原计算，不额外复制源的最大值或限制安全区边距。 FlowLayoutGroup 与 VirtualVerticalLayoutGroup 向 `SetLayoutInputForAxis(totalMin, totalMax, totalPreferred, totalFlexible, axis)` 写入时，第二项同样使用 `-1f`，保持原最小、首选、弹性尺寸与轴次序。

这些布局查询可递归访问其他布局对象，源自身/循环引用没有统一检测。SetLayerFromSettings 只在 Start 递归应用当前图层，之后新增子对象不自动同步。

## 输入和数值交互

RuntimeDebuggerUtil.EnsureEventSystemExists 先看设置和当前 EventSystem，再查首对象，必要时新建；返回 true 表示执行创建路径，不保证输入模块已就绪。双输入宏按 Settings.UIInputMode 选模块，单宏按编译分支；未知模式或无有效宏可能只有 EventSystem。InputSystemUIInputModule 创建后用禁用/启用对象促初始化；创建异常不回收此前对象。Legacy 分支保留在既有调试器中，不构成新增业务代码回退旧输入的许可。

| 控件 | 行为与边界 |
|---|---|
| MultiTapButton | 默认 0.5 秒窗口、三次点击后调用基类；先计数再交基类检查，非左键/不可交互点击也可能参与计数 |
| LongPressButton | 默认按住约 0.9 秒发一次长按并抑制普通 click；按下后的 interactable/启停变化不统一重置状态 |
| DebugNumberButton | 首次立即 Apply，约 0.4 秒后重复、约 0.2 秒起逐步加速；解析当前 culture、加步进、夹范围并触发 onEndEdit；Disable/变不可交互后没有统一清按住状态 |
| DebugNumberSpinner | 横拖解析当前文字并调值，纵拖转父事件；类型范围夹在拖路径，整数转 int；结束可能发 EndEdit 和 Submit 两次通知。公开文字输入由 NumberControl 另处理 |
| DebugSpinner | 横向累计超过阈值后按整数次数同步发增减事件；threshold 非正、极小值或巨量 amount 可造成大量同步工作 |
| Unselectable | 收到选择后排下轮清 EventSystem 当前选择；当前 EventSystem 缺失可能失败 |
| FlashGraphic | Flash 后按实时渐变回 Default，Hold 等下一次按下；编辑态也可能覆盖颜色，不是数据状态或授权门 |
| InheritColour | 每帧复制 From.canvasRenderer 的颜色，而非单纯 From.color；缺 Graphic/源会影响刷新 |

输入、布局、事件 handler 大多同步执行，异常会传播或留已修改部分；不要用 UI 禁用状态替代公开方法的参数与业务验证。

## Canvas、相机与样式

ConfigureCanvasFromSettings.Start 配置专用相机，保存 CanvasScaler 原比例并乘 UIScale、订阅 Settings.PropertyChanged；只有 Canvas 的 RequireComponent 不保证存在 CanvasScaler。WorldSpace 模式删除 scaler 后原配置组件仍可能收到设置事件。DebugRetinaScaler 按屏幕 DPI 与设计 DPI 推导半档比例，iOS 非 Editor 低 DPI 分支取 321；它与 UIScale 的写入顺序会影响比例，设计 DPI 非正未统一拒绝。ScaleWithScreenSize 早返回不更新 DPI 缓存，可能持续尝试。

StyleSheet 的 keys/styles 按索引配对，GetStyle 返回原 Style，缺键可递归 Parent；Parent 环、数量不一致或空键没有统一修复。Style.Copy 复制颜色与 Sprite 引用，不深拷 Sprite。Editor Add/Delete 改本资产列表但不自动保存/通知。

StyleRoot 在 Sheet 引用变化时 BroadcastMessage(DebugStyleDirty)，同一资产内部内容变化不会自动被引用比较发现。StyleComponent 按父 StyleRoot 查样式并作用于 Graphic/Selectable/Image，找不到资源时可继续 pending；禁用父根的查找有上限但并不保证永远选到有效启用根。切空键或缺样式不统一恢复 Prefab 原视觉。

DebugPanelBackgroundBehaviour 取得共享 Style 后直接改 NormalColor.a，随后切 style key，修改的是共享对象而非副本，可能影响其他消费者或造成 Editor 资产状态变化。需要隔离样式时应由实际调用方明确复制；隐藏面板不恢复这次改色。资源与脚本绑定见 [DebuggerAssets.md](DebuggerAssets.md)。

源码：[DebuggerTabController.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/UI/DebuggerTabController.cs)、[VirtualVerticalLayoutGroup.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/Scripts/UI/Layout/VirtualVerticalLayoutGroup.cs)、[StyleSheet.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/Scripts/UI/StyleSheet.cs)。
