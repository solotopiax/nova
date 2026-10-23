# RuntimeDebugger

**命名空间**：`NovaFramework.Runtime`

`RuntimeDebugger` 是 Debug 模块内置调试器门面，替代拆分前的旧插件入口。它负责初始化调试器服务、提供 `IDebugService` 访问入口，并注入日志标签、日志条数和上传回调等启动参数。

## 入口

```csharp
RuntimeDebugger.Init(new RuntimeDebugger.InitOptions
{
    LogTagType = typeof(LogTag),
    LogTagDescriptionResolver = LogTag.GetDescription,
    MaximumConsoleEntries = maxEntries,
    TextFont = runtimeDebuggerFont
});
```

## 边界

- `DebugComponent` 统一触发初始化。
- 不在其他模块散落调用初始化。
- 不保留旧插件兼容门面。
- `TextFont` 非空时，`DebugInstantiate` 会把它应用到每个动态创建对象下的全部 `UnityEngine.UI.Text`；为空时保留 Prefab 原字体。
- WebGL / 微信小游戏不能依赖 Editor 的系统字体回退。中文日志必须使用实际包含中文字形、且会进入 Player 构建的 `Font`。

## 初始化与服务访问

`Init(null)` 使用已有静态值与 Settings；有 options 时赋标签类型、描述解析器和字体，仅正的 MaximumConsoleEntries 覆盖 Settings，仅非 null 的上传委托替换已有委托。它先置 IsInitialized=true，再注册程序集并取得 Console/Debug 服务；没有重复初始化或失败回滚守卫，因此标记为 true 不等于全部服务成功创建。

`Instance` 调用 `DebugServiceRegistry.GetService<IDebugService>()`，可能构造服务与依赖，并非纯字段读取；不以 IsInitialized 或 Settings.IsEnabled 为前置门。Editor 非 Play 时返回 null，构造/反射异常可能传播。不要用访问 Instance 作为无副作用的就绪探测。

`AutoInitialize.OnLoadBeforeScene()` 在 SubsystemRegistration 注册服务程序集（仅 `UNITY_2018` 分支改用 BeforeSceneLoad），并在 Settings.IsEnabled 时提前建立 Console；AfterSceneLoad 再按同一开关 Init。这是与 `DebugComponent.DebuggerActiveType` 独立的自动入口，均受 `NOVA_DISABLE_RUNTIME_DEBUGGER` 编译条件约束。配置缺失时 Settings 会创建默认启用的内存对象，不能仅凭组件的关闭策略推断另一路不会初始化。

`SetMaximumConsoleEntries(count)` 仅赋 Settings，不验证正数、不重建已有日志缓冲；无效容量可能在后续创建缓冲时失败。复制回调默认只在 Android/iOS/Editor/Standalone 编译分支提供，把完整日志和堆栈写入剪贴板；其他平台默认 null。上传回调接收完整过滤结果文本，不自动脱敏。

## 主题导航

- [Settings.md](Settings.md)：源码默认值、运行时覆盖与配置变化。
- [RuntimeDebugger.Services.md](RuntimeDebugger.Services.md)：服务创建、面板、PIN、固定 UI、报告和系统信息。
- [RuntimeDebugger.Console.md](RuntimeDebugger.Console.md)：日志收集、缓冲、筛选、标签与复制上传。
- [DebugOptions.md](DebugOptions.md)：反射选项、属性绑定、控件和固定操作。
- [RuntimeDebugger.Profiler.md](RuntimeDebugger.Profiler.md)：CPU 时间采样、内存统计与图表。
- [RuntimeDebugger.UI.md](RuntimeDebugger.UI.md)：Tab、布局、虚拟行、交互和样式。
- [RuntimeDebugger.Foundation.md](RuntimeDebugger.Foundation.md)：服务注册基础、集合、反射、数学与内部工具。
- [DebuggerAssets.md](DebuggerAssets.md)：Resources、Prefab、样式和裁剪资源。

## Console 预览规则

Console 列表行显示的是 `ConsoleEntry` 的预览文本，不直接把完整日志塞进列表行：

- 完整日志内容和堆栈仍保留在 `ConsoleEntry.Message` / `ConsoleEntry.StackTrace` 中，详情面板和复制逻辑读取原始内容。
- 列表预览保留 Unity rich text 结构，例如 `<color=#6D6D6D>...</color><color=#FDFF00>...</color>`，以保证日志前缀和正文仍按日志级别动态着色。
- 预览截断按“可见字符数”计算，不把 rich text 标签计入长度。
- 多行日志只展示首行预览；如果首行打开了 `<color>` 等标签但闭合标签在后续行，预览会追加 `...` 并补齐闭合标签，避免 UI 中露出残缺的 `<color=` 文本。
- 列表行会根据当前 `Text` 宽度动态估算可显示字符数，竖屏和横屏不共用固定长度。
- `Message.supportRichText` 仍由 `Settings.Instance.RichTextInConsole` 控制，不能绕过 Settings 写死开关。

这条规则只影响列表预览，不改变日志采集、过滤、折叠、详情展示和复制的原始数据。

## 结构同步要求

凡是改动 RuntimeDebugger 的初始化入口、Console 显示语义、服务接口、资源路径、程序集归属或旧插件迁移状态，必须同步更新：

- [DebugComponent.md](../DebugComponent.md)
- [DebuggerAssets.md](DebuggerAssets.md)
- 本页 RuntimeDebugger 行为说明
- [../../../../INDEX.md](../../../../INDEX.md) 中 Debug 入口描述
