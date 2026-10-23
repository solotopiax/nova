# RuntimeDebugger Console

`StandardConsoleService` 订阅 Unity 的 threaded 日志回调，保存 `ConsoleEntry` 并通知 UI；`ConsoleLogControl` 负责显示筛选，ConsoleTabController 负责计数、详情、复制、上传和警告面板，DockConsoleController 是固定区的三类日志视图。

## 采集、折叠与清空

构造时读取容量、折叠配置和当前 Unity logHandler；日志在写锁中更新计数、与上一条比较，再折叠或加入环形缓冲。相同 Message/StackTrace/Unity LogType/NovaLevel 才折叠，不比较 Timestamp；Count 累加，首次时间保留。Nova 级别以整段正文 Contains 判断，引用同样标记的普通正文也可能被识别。

| 成员 | 当前含义 |
|---|---|
| Entries | 清空前为历史缓冲；清空后为本次清空后的显示缓冲，均是原缓冲的 IReadOnlyList 视图 |
| AllEntries | 有限容量历史，包含 Clear 前的日志，报告从这里取数据 |
| Clear | 归零显示计数并清显示缓冲游标，保留历史；不会擦除数组中的旧引用 |
| LoggingEnabled | 读写 `UnityEngine.Debug.unityLogger.logEnabled`，影响全局 Unity logger，不是本 Console 的独立收集开关 |
| LogHandlerIsOverriden | 比较当前 handler 与构造快照的引用，不重装 handler，也不证明所有日志都已丢失 |
| Dispose | 解除 threaded 回调并清缓冲游标，不清数组全部引用、计数、事件或服务登记 |

回调中的 Error/Updated 可从工作线程同步触发，未统一派发到主线程。Updated 捕获整个 multicast 调用的异常，首个订阅者失败仍会阻断后续订阅者；Error 没有相同隔离。计数在日志存储前更新，异常可留下已计数却未入缓冲的状态。折叠项淘汰只减 1，未按该项 Count 扣除；淘汰错误项时同样会触发 Error 事件。计数不构成稳定快照。

## CircularBuffer 的实际边界

`CircularBuffer<T>` 固定容量，PushBack/PushFront 在满时覆盖相反端；Pop 清当前槽，Clear 只归零计数/游标，ArraySegment、索引器和枚举均引用原数组，不提供并发快照。构造拒绝容量小于 1、null 初始数组及超容量项，初始项数组结构会复制，元素对象不深拷贝。

当前分段枚举在满状态 start==end 时有缺口：例如容量 3、连续加入三项后，Count=3 但 start=end=0，GetEnumerator 没有元素，ToArray 返回三项默认值。其他满游标位置也可能漏段；报告的 ToList 因此不能视为完整日志证明。Back 在 end=0 的部分状态也不等于逻辑最后一项，例如满后 PopFront 的剩余序列可能读错尾项。索引器仅检查空和上界，负索引没有完整拒绝，可能访问逻辑范围外的槽。需要可靠快照的消费者不能只依赖只读接口名。

## 预览与详情

ConsoleEntry 保存完整 Message/StackTrace，复制构造共享这些字符串，不复制预览缓存。MessagePreview/StackTracePreview 分别按 180/120 的可见 UTF-16 字符数生成首行；列表行按实际 Text 宽度二分估算 48～900 字符，宽度变化超过约 1 像素才重算。最小预览长度不保证窄屏一定装得下。

富文本扫描支持 b/i/size/color/material/quad，保留标记并在截断时补闭合标签；长度不含识别到的标签，但不是 Unicode 字素计数，可能拆代理对。标签配对是宽松匹配，不是严格嵌套验证，不识别的标签按文字处理。多行只显示首行，必要时追加省略号。原始 Message/StackTrace 不被改写。

预览缓存按长度键保存，公开字段原地修改不会使缓存失效；行复用同一 ConsoleEntry 且宽度未变时也未必刷新文字。详情显示完整原文/堆栈，UI 文本上限为 5000 个 UTF-16 字符后提示截断；复制回调仍接收原 ConsoleEntry。RichTextInConsole 在控件初始化时读取，不自动对全部既有行实时重配。

## 筛选与标签树

IConsoleFilterState 的类型开关只影响显示。Unity Error/Assert/Exception 归并到错误组，Nova Debug/Fatal 独立，Info/Warning/Error 桥接对应 Unity 类型。初始各级别为 true，TextFilter 为空，TagFilter 为空表示不按标签过滤。

ConsoleLogControl 刷新时清行并重新遍历 Entries；级别先判断 Debug/Fatal，文本使用 OrdinalIgnoreCase，标签是 Ordinal 的 OR 匹配。过滤的 Message 为 null 时可能失败。GetFilteredEntries 采用同样条件，提供当前过滤结果，不脱敏或深拷贝；EnableSelection 只改布局选择许可，不控制采集。

TextFilter setter 只在字符串改变时通知；TagFilter setter 先清再枚举输入，忽略 null/空标签并去重，每次发同一个 HashSet。不要直接传入当前 TagFilter 本身，会先把输入清空；只读视图也不是不可变快照。订阅者异常时状态已写，未回滚。

TagFilterPanelControl 从注入类型的 public static literal string 常量按方括号片段建排序树，描述交解析器；缺标签类型会得到空树。初始化先设完成标记，途中失败不自动重试；资源或解析器失败可留部分行。

- 全选和计数包括父节点与子节点，不只是叶子；选择父节点会加入自身及所有后代。取消子节点不会移除祖先标签，OR 匹配下父标签仍可能覆盖该子项日志。
- 搜索只检查 Label、忽略大小写，不查描述、不自动显示命中子节点的祖先；分类/搜索只改变显隐，不限制全选范围。重新 Open 清搜索文字但未统一重置旧分类/行显隐。
- 标签计数约每秒遍历保留日志，不应用其他级别/文本筛选，不乘 ConsoleEntry.Count；一条带多个兄弟标签的日志可能累计到多个分支。
- Confirm 传同一个 pending 集合，ConsoleLogControl 直接保存它，FilterState 则复制；之后重开、取消或清空 pending 可能提前改变 LogControl 的有效筛选。需要稳定提交语义的调用方须复制集合；不能把当前界面内部对象视为不可变提交结果。
- Toggle/行 Setup 会增加监听，重复 Setup 没有去除旧监听；描述和正文没有额外富文本转义。

## 主面板、Dock 与上传

ConsoleTab 订阅五级计数、文本变化并同步 UI，操作中的 Toggle 回写由服务相同值早退限制。选中项、复制动画和面板 Canvas 各有生命周期；OnDestroy 并未解除所有面板/UnityEvent 回调，隐藏面板不等于结束采集。日志关闭和 handler 更换提示只提供说明/重新启用动作，不修复外部 handler。

Dock 只展示 Unity 三类开关与计数，不完整同步 Debug/Fatal/Text/Tag；其 LogControl 的 Debug/Fatal 默认开启可能与三开关不同。QuickView 计数显示为最多 `999+`，每次更新读取活计数，非原子快照。Dock 指针/拖拽只调透明度，Dropdown 与 Visible 分开控制。

上传拼接当前过滤日志的 Unity LogType、Message 和 StackTrace 后交 UploadLogCallback；没有时间戳、自动脱敏或长度限制。复制同样输出完整原文，回调异常不回滚 UI 状态。报告、剪贴板和上传的保留边界不同，Clear 不能作为删除所有历史数据的动作。

其他内存/布局与资源边界见 [RuntimeDebugger.UI.md](RuntimeDebugger.UI.md)、[DebuggerAssets.md](DebuggerAssets.md)。

源码：[StandardConsoleService.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Services/Implementation/StandardConsoleService.cs)、[IConsoleService.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/Services/IConsoleService.cs)、[TagFilterPanelControl.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Debugger/UI/Controls/TagFilterPanelControl.cs)。
