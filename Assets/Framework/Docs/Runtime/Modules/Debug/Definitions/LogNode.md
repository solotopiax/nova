# LogNode（已移除）

`LogNode` 已不在当前源码中。

它原本是旧版自绘控制台窗口的数据节点类型，服务于日志虚拟列表与标签过滤系统。当前 `Debug` 模块不再维护这套运行时控制台实现，因此：

- 不存在当前 `LogNode` 类型
- 不存在当前 `ConsoleWindow` 日志节点池化链路
- `MaximumConsoleEntries` 现在只用于 `RuntimeDebugger.Init(...)`

## 当前应查看

- [../DebugComponent.md](../DebugComponent.md)
- [../DebugManager.md](../DebugManager.md)
