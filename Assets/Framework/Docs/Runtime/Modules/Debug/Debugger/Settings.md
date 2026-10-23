# RuntimeDebugger Settings

`NovaFramework.Runtime.Settings` 是调试器的 ScriptableObject 配置。`Instance` 缓存 `Resources.Load<Settings>("Debug/Settings")` 的结果；资源缺失时记录日志并创建默认实例，不自动保存资产。Getter 可能创建 Unity 对象，已有资源加载时还可能迁移旧快捷键。

## 默认值与生效点

下表是源码初始值；发行资源 `Assets/Framework/Resources/Debug/Settings.asset` 可以覆盖它们，`DebugComponent` 初始化又可覆盖日志容量和字体。现有项目的序列化值不能由源码默认值推断。

| 属性 | 源码默认值 | 主要消费 |
|---|---|---|
| IsEnabled | true | AutoInitialize 的自动初始化开关 |
| MaximumConsoleEntries | 1500 | 创建 Console 缓冲时读取；组件源码默认注入 3000 |
| CollapseDuplicateLogEntries | true | Console 构造时保存，相邻相同日志折叠 |
| RichTextInConsole | true | Console 行和详情的 UGUI rich text |
| DefaultTab / DisabledTabs | SystemInformation / 空数组 | Tab 配置；当前 Tab 启动实际优先队列或 Console，再取可用首项，未消费 DefaultTab |
| TriggerBehaviour / EnableTrigger | TripleTap / Enabled | 触发按钮创建策略 |
| TriggerPosition / ProfilerAlignment / OptionsAlignment | TopLeft / BottomLeft / BottomRight | 触发器、Profiler 与固定选项位置 |
| ConsoleAlignment | Top | DockConsole 对齐 |
| UIInputMode | NewInputSystem | 输入模块/快捷键分支，仍受编译宏和设备存在性约束 |
| EnableEventSystemGeneration | true | 没有 EventSystem 时允许创建 |
| EnableKeyboardShortcuts / KeyboardEscapeClose | true / true | 快捷键监听与 Escape 关闭 |
| RequireCode / RequireEntryCodeEveryTime | false / false | 服务构造时的 PIN 与成功授权策略 |
| EnableBugReportScreenshot | true | 报告提交默认截图 |
| ErrorNotification | true | 触发器新错误提示 |
| AutomaticallyShowCursor / UnloadOnClose | true / false | 面板显示时的光标处理；关闭时隐藏或卸载 |
| UseDebugCamera / DebugCameraDepth / DebugLayer | false / 100 / 5 | 专用相机及 UI 图层 |
| UIScale | 1 | CanvasScaler 附加缩放 |
| EnableBackgroundTransparency / BackgroundTransparency | true / 0.9 | 背景样式 alpha |

普通 setter 直接保存字段，不 SetDirty、不保存资产、不发事件，也不实时重配已创建服务。Range 等 Inspector 标注不构成运行时输入校验；容量、图层、超时类数值、枚举与浮点有限性由具体消费者承担。

## 集合、PIN 与位置联动

- DisabledTabs 和 KeyboardShortcuts 的 setter 使用 ToArray 复制容器，getter 暴露原数组的 IList 视图；元素对象仍共享，不是深拷贝。null 集合会失败。默认快捷键为 Ctrl+Shift+F1～F4，分别指向 Info、Console、Options、Profiler；额外修饰键不会阻止匹配。
- EntryCode 的 getter 每次建立包住原数组的 ReadOnlyCollection；setter 要求四位、每位 0～9 后复制。改 Settings 不更新服务已复制的 PIN，运行中变更应使用 IDebugService.SetEntryCode；它只是本地 UI 入口控制。
- TriggerPosition 与固定区位置 setter 会交换冲突位置；Profiler 不接受中心边位置，Options 不接受左右居中，按可用角落回退。未知枚举未统一拒绝，也不通知现存布局。
- UIScale 是发 PropertyChanged 的配置例外：值变化后先赋值再广播，ConfigureCanvasFromSettings 按已有原始比例更新 CanvasScaler。订阅者异常不会回滚字段；0、负值、NaN 未被 setter 拒绝。

`UpgradeKeyboardShortcuts()` 将旧键数组及修饰键复制成新配置，清空旧数组，Editor 只标脏而不自动保存。`ClearCache()` 卸载 Settings 资产并清静态引用，不 Dispose 既有服务；旧服务的构造快照和之后新读配置可能不同。

服务与输入边界见 [RuntimeDebugger.Services.md](RuntimeDebugger.Services.md)，实际资源位置见 [DebuggerAssets.md](DebuggerAssets.md)。

源码：[Settings.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Settings.cs)。
