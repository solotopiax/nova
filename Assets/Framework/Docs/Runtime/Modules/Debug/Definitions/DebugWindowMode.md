# DebugWindowMode（已移除）

`DebugWindowMode` 已不在当前源码中。

它属于旧版 Runtime 自绘调试窗口模式枚举。当前 `Debug` 模块不再维护最小窗 / 中窗 / 全屏窗 / 日志详情窗这套 UI 状态。

当前调试能力只有两层：

- `DebugComponent`：决定是否启用 `RuntimeDebugger`
- `DebugManager`：负责磁盘检测与事件发布

## 当前应查看

- [../DebugComponent.md](../DebugComponent.md)
- [../DebugManager.md](../DebugManager.md)
