# RuntimeDebugger

**命名空间**：`NovaFramework.Runtime`

`RuntimeDebugger` 是 Debug 模块内置调试器门面，替代拆分前的旧插件入口。它负责初始化调试器服务、提供 `IDebugService` 访问入口，并注入日志标签、日志条数和上传回调等启动参数。

## 入口

```csharp
RuntimeDebugger.Init(new RuntimeDebugger.InitOptions
{
    LogTagType = typeof(LogTag),
    LogTagDescriptionResolver = LogTag.GetDescription,
    MaximumConsoleEntries = maxEntries
});
```

## 边界

- `DebugComponent` 统一触发初始化。
- 不在其他模块散落调用初始化。
- 不保留旧插件兼容门面。

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
