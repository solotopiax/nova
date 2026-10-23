# Debugger Assets

内置调试器资源随 `Assets/Framework` 主包交付，不再来自独立 UPM 包。

| 类型 | 路径 |
|---|---|
| Runtime Resources | `Assets/Framework/Resources/Debug`（`Settings.asset` 与 `Prefabs/**` 位于该目录下） |
| Prefab / UI | `Assets/Framework/Prefabs/Debug` |
| Animation | `Assets/Framework/Animations/Debug` |
| Runtime Texture / Sprite | `Assets/Framework/Textures/Runtime/Debug` |
| 示例 Scene | `Assets/Framework/Scenes/Debug` |

移动或重命名资源时必须保留 `.meta`，否则 Prefab 脚本绑定和资源引用会丢失。

## 当前边界

- `Resources/Debug/Settings.asset` 是运行时调试器设置资产，不再依赖自定义 Editor 绘制。
- 运行时 UI Prefab 直接位于 `Resources/Debug/Prefabs/**`，不再额外挂一层 `UI` 或 `RuntimeDebugger` 目录。
- UI Prefab 仍保留内置 Arial 作为默认字体；`DebugComponent.m_RuntimeDebuggerFont` 可在实例化时统一覆盖，供 WebGL / 微信小游戏绑定包含中文字形的项目字体，Framework 不反向引用 Sample 字体资产。
- 调试器运行时图片、Sprite、Logo 位于 `Textures/Runtime/Debug/**`。
- 当前不再保留调试器专属 Editor 代码和 Editor 图标依赖；若未来重新引入 Editor 可视化工具，必须重新同步本页与 Editor 文档。

## 输入资源与消费者

Resources 路径是去掉 `Assets/Framework/Resources/` 和扩展名后的逻辑加载名，不是磁盘绝对路径；各 Prefab 内的字段、UnityEvent、嵌套 Prefab、脚本 GUID 和图层共同形成 UI 绑定。单凭文件存在不能证明交互已配置完成。

| 资源 | 主要绑定与消费 |
|---|---|
| Resources/Debug/Settings.asset | Settings.Instance 的固定 `Debug/Settings` 入口；资产配置可覆盖源码默认，组件初始化还能注入日志容量/字体 |
| Resources/Debug/Prefabs/DebugPanel.prefab | DebugPanelServiceImpl 克隆，DebugPanelRoot、Tab 控制器、Canvas 配置、移动菜单、安全区及背景样式 |
| Resources/Debug/Prefabs/PinnedUI.prefab | PinnedUIServiceImpl 延迟载入；包含真实 DockConsoleController、Profiler、固定选项布局和拖拽手柄 |
| Resources/Debug/Prefabs/DockConsole.prefab | 只有 inactive GameObject/Transform 的遗留资源；虽然路径常量仍存在，当前 DockConsole 从 PinnedUIRoot 取控制器，不把该空 Prefab 当作有效控制台 |
| Resources/Debug/Prefabs/Tabs/{Console,Options,Profiler,System}.prefab | DebuggerTabController 扫描 Tab，分别绑定 Console、Options、Profiler、Info 控制器；是否启用还受 DisabledTabs/IEnableTab 约束 |
| Resources/Debug/Prefabs/Options/{Action,Bool,Enum,Number,ReadOnly,String}Option.prefab | OptionControlFactory 的动作/数据控件来源，具体字段类型决定可绑定性；数字控件还依赖 Spinner/按钮与布局字段 |
| Resources/Debug/Prefabs/Trigger.prefab | 触发器按钮、ErrorNotifier、FPS、Canvas 配置、图层与字体 |
| Resources/Debug/Prefabs/{TagFilterPanel,TagCategoryRowPrefab,TagDetailRowPrefab,TagLeafRowPrefab}.prefab | 标签树、分类和选择行；事件/Toggle 与引用字段必须完整，过滤行为见 Console 主题页 |
| Prefabs/Debug/Console/Item.prefab | ConsoleEntryView、DebugText、StyleComponent 的虚拟日志行 |
| Prefabs/Debug/{InfoBlock,MemorySummaryBlock}.prefab | 系统/Profiler 文本块、内存虚拟行、滚动及采样按钮 |
| Prefabs/Debug/ModalPopup.prefab | 弹层布局与样式输入，是否有有效报告控制器须以实际实例绑定核对 |
| Prefabs/Debug/Options/Category.prefab | Options 分类选择、流式布局与样式 |
| Prefabs/Debug/SideBar/{Tab,ConsoleTabButtonContent}.prefab | 页按钮与三类日志计数快览 |
| Styles/{Default,DefaultConsoleAltRow,DefaultConsoleSelectedRow,DefaultTabActive}.asset | StyleSheet 的键/样式配对与 Parent 继承，消费时返回共享 Style；修改引用/键/父链会影响绑定 |

Settings 发行资产的自动初始化开关为关闭，容量为 1500、图层 5、UI 缩放 1、UnloadOnClose 关闭；源码 fallback 自动初始化默认开启，DebugComponent 源码容量默认 3000。它们是不同配置层，项目中最终生效值还受实际序列化资产与启动入口影响。

自有 MonoBehaviour GUID 可从 Debug 源码 `.cs.meta` 解析；Prefab 中还包含 Unity UI 等包脚本，未在 Debug 目录解析到的 GUID 不能直接当作 Missing Script。资源图的静态存在性也不能代替实际 UGUI 绑定、布局、字体字符与平台输入验证。

`Core/Options/link.xml` 的保留声明与当前类型/程序集不一致，具体边界见 [DebugOptions.md](DebugOptions.md#构建边界)。本页仅描述当前输入和消费关系，不改写序列化值、Prefab、样式或裁剪配置。

## 变更同步规则

移动 `Resources/Debug`、`Prefabs/Debug`、`Animations/Debug` 或 `Textures/Runtime/Debug` 任一路径时，属于 Debug 资源契约变化，必须同轮更新：

- 本页资源表。
- Prefab / Settings 依赖说明。
- 全局 `Docs/INDEX.md` 的 Debug 入口描述。
- 如涉及脚本目录或程序集归属，同时触发 `nova-prelookup` 并复核 `Minds` 中 Debug 图谱与程序集边界。
