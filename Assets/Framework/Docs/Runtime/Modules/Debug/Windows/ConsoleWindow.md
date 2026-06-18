# ConsoleWindow（已移除）

`ConsoleWindow` 已不在当前源码中。

它属于旧版 Runtime 自绘控制台窗口。当前实现已经不再维护：

- 自绘日志列表
- `LogNode` 节点池化
- 标签过滤与搜索面板
- 触摸拖拽与详情弹窗

当前 `DebugComponent` 只会按 `DebuggerActiveType` 决定是否初始化 `RuntimeDebugger`，`m_MaximumConsoleEntries` 只参与 `RuntimeDebugger.Init(...)`。

## 当前应查看

- [../DebugComponent.md](../DebugComponent.md)
- [../DebugManager.md](../DebugManager.md)
