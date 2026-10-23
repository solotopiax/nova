# RuntimeDebugger 服务与生命周期

调试器通过 `IDebugService` 暴露业务入口，通过 `DebugServiceRegistry` 按需取得 Console、Profiler、面板、触发器、系统信息、选项和固定 UI 服务。接口只声明调用面；下列行为属于当前内置实现，外部实现不能仅因实现接口就视为具有同样清理和失败策略。

## 创建、关闭与面板

RuntimeDebuggerService 构造时依次取得依赖、订阅事件、配置触发器/快捷键、复制 PIN、设置持久根节点，最后接收 InternalOptionsRegistry 的预注册容器。此链不是事务，某个依赖、资源、PIN 或回调失败时不会撤销先前创建和订阅。Service 静态门面缓存服务引用，SubsystemRegistration 清门面缓存，不负责 Dispose 旧实例。

| 入口 | 当前行为与边界 |
|---|---|
| ShowDebugPanel 两个重载 | 默认 requireEntryCode=true；需要且未授权时显示 PIN，否则打开面板；带 Tab 重载保存等待中的目标页。返回 void，不表示指定页一定存在。 |
| HideDebugPanel | 隐藏 Canvas、恢复此前光标状态，不卸载资源、不停止 Console、不清 PIN 授权。 |
| DestroyDebugPanel | 先隐藏再 Unload，保留服务、日志和上次 Tab；隐藏回调失败会阻止卸载。 |
| IDebugPanelService.OpenTab | 必要时直接打开面板后切页，不经过 IDebugService 的 PIN 判断；不存在或禁用 Tab 的下层 false 不向外返回。 |
| EnableWorldSpaceMode | 直接显示面板，移除 Retina/CanvasScaler，改为 WorldSpace、1024×768，缓存 RectTransform；UseDebugCamera=true 时拒绝。此入口不经过 PIN 判断。 |
| DebugPanelServiceImpl.Dispose | 直接 DestroyImmediate 面板对象，不等于 Hide/Unload 的光标恢复及退订流程。 |

面板延迟 Resources 实例化，Load 可能只记录缺失资源后返回，上层随后仍访问必需组件。显示字段、Canvas、光标和事件之间没有统一回滚；IsLoaded 只证明根对象存在，IsVisible 也不证明全部 Tab 的 Start 已完成。外部直接销毁根对象可能使内部显示字段与实际对象不一致。

面板关闭按钮按 Settings.UnloadOnClose 决定 Hide 或 Destroy；CloseAndDestroy 总是卸载。DebugPanelRoot 会在 Canvas 下补透明全屏 RaycastBlocker；已有同名对象只调整 sibling，不重新校验其 Image/锚点/图层。

## PIN 与触发入口

`EntryCode` 是四位只读结构，每位 0～9；索引限 0～3，默认结构是四个 0。枚举会创建临时四元素列表。SetEntryCode 重置授权并保存服务内 PIN，不保存 Settings，也不关闭已打开面板；DisableEntryCode 只移除服务要求，不关闭已显示键盘。

PinEntryServiceImpl 只允许一个显示中的输入，重复提示抛 InvalidOperationException；错误 PIN 清空输入并播放 Invalid 动画，取消或成功后先隐藏再回调。回调/PIN 引用没有在结束时主动清空。PinEntryControl 的 Complete 参数是原数字列表的只读包装，监听者清空输入会改变其他持有者看到的数据；需要保留时自行复制。CanCancel 影响按钮文字，公开 Cancel 方法并不以它作为拒绝门。

输入按钮按数组下标生成数字，公开 PushNumber 不自行限制 0～9；键盘文本采用 char.IsNumber/GetNumericValue，输入系统分支直接使用 Keyboard.current。没有物理键盘或键盘实例变化时，相关订阅/解绑并无统一空值和设备切换处理。Hide 只切 CanvasGroup，不清输入、不禁用组件。

触发器按 TripleTap、TapAndHold 或 DoubleTap 创建按钮；点击有错误提示时切 Console，否则打开默认页，均使用默认 PIN 入口。关闭触发器只 SetActive，不取消错误订阅。错误回调仅置队列，Animator 在 Update 更新；多条错误合并并延长约 6 秒提示。FPS 每约 0.5 秒读取平均帧时间更新。

快捷键在 Awake 浅复制配置，Input System 分支缓存当前键盘 Key；Escape 与匹配到的首条快捷键触发动作，不过滤 InputField 焦点，也不禁止额外修饰键。初始化对新输入键盘的访问可能先于运行时 UIInputMode 分支。Dock/Profiler 的开关不经过面板 PIN。

## 固定 UI 与 DockConsole

PinnedUI 延迟载入固定区 Prefab；即使将 IsProfilerPinned 设为 false，也可能先创建 UI。Pin 按 OptionDefinition 对象登记，重复 Pin 不调序；创建控件、加入字典/列表后才发送事件，事件异常不回滚。Unpin 销毁该控件对象；UnpinAll 清所有固定选项，不隐藏 Profiler/Dock，不逐项广播状态变化。

固定属性可在关闭面板时排到下一帧 Refresh，读取真实业务 getter；首个异常会阻断本轮后续刷新。选项被移除时以当前 Options 列表检查并取消对应固定项。服务对面板和选项的事件订阅没有独立 OnDestroy 退订实现，不能把隐藏 UI 当作完整释放服务。

DockConsoleServiceImpl 从具体 PinnedUIServiceImpl.DockConsoleController 取得控制器，替换 IPinnedUIService 实现时存在具体类型依赖。Visible/Expanded 字段与根对象分开保存，部分赋值先改字段再加载；根未创建时直接将初始 Expanded=true 改成 false 可能访问空根。加载失败也没有自动重试事务。显示 Dock 时会暂停同边触发器，消除冲突后恢复；该恢复可能覆盖期间外部手动关闭触发器的意图。

## Bug Report 与系统信息

SetBugReporterHandler 只登记 handler，不提交报告；null 清除，IsUsable 每次调用外部 getter。BugReport Tab 在创建时判断可用性，之后换 handler 不自动重建已有 Tab。

SendBugReport 要求可用 handler、非 null report/completion；NotReachable 直接失败，其余交给 `IBugReporterHandler.Submit`。接口用进度/完成回调，没有超时、取消、强制主线程或单次完成约束；handler 抛异常、返回 null 结果或重复/迟到完成均不被服务统一隔离。成功/错误结果只是保存文本，错误文本未自动转义或脱敏。

报告 UI 先取消选中、禁用表单和取消按钮；非空邮箱先写明文 PlayerPrefs 键 `RUNTIME_DEBUGGER_BUG_REPORT_LAST_EMAIL` 并 Save，再启动截图和提交。ClearForm 会读回这个邮箱；不代表删除历史邮箱。截图等待帧末后读取全屏 RGB24、转 PNG 并销毁纹理，字节保存在共享静态字段；没有并发截图隔离，编码失败也无 finally 保证纹理清理。

报告包含完整 AllEntries、系统报告与可选 PNG；日志对象和值只浅复制，没有自动脱敏。截图/反射/报告构造/handler 异常可在表单禁用后中断，没有统一 finally 恢复；失败提交停留表单供重试，成功或用户取消才关闭 Popover。重叠 ShowBugReportSheet 可直接返回而不调用本次完成回调。

系统信息按分类与 Title 去重，返回的分类键及信息列表是可变活视图；外部改 Title/列表可破坏去重。InfoEntry.Value 每次执行动态 getter，异常转为 Error 文本而不缓存值。IsPrivate 仅控制默认 CreateReport 排除，Info Tab 仍显示；自定义项默认非 private，调用方自行标记。Cloud Build manifest 的字段作为 Build 信息读取，不自动识别敏感字段。报告字典构建仍可能因 null/重复标题或并发变化失败。

UI 绑定、图层及输入资源见 [RuntimeDebugger.UI.md](RuntimeDebugger.UI.md) 与 [DebuggerAssets.md](DebuggerAssets.md)。

源码：[RuntimeDebuggerService.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Services/Implementation/RuntimeDebuggerService.cs)、[BugReportService.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Services/Implementation/BugReportService.cs)、[BugReportSheetController.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/UI/Other/BugReportSheetController.cs)。
