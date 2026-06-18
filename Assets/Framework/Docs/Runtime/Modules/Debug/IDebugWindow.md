# IDebugWindow（已移除）

`IDebugWindow` 已不在当前源码中。

它属于旧版 Runtime 自绘调试窗口体系的一部分。当前 `Debug` 模块已经收敛为：

- `DebugComponent`：决定是否初始化 `RuntimeDebugger`
- `DebugManager`：磁盘检测循环与事件发布
- `DebuggerActiveType`：调试器激活策略

这页仅为历史链接兼容保留，不能再当作当前接口契约。

## 当前应查看

- [DebugComponent.md](DebugComponent.md)
- [DebugManager.md](DebugManager.md)
- [Definitions/DebuggerActiveType.md](Definitions/DebuggerActiveType.md)
- [Windows/DiskCheckingConfig.md](Windows/DiskCheckingConfig.md)
