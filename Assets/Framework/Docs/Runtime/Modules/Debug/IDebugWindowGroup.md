# IDebugWindowGroup（已移除）

`IDebugWindowGroup` 已不在当前源码中。

它原本属于旧版 Runtime 调试窗口树，用来组织多个 `IDebugWindow`。当前 `DebugManager` 已不再维护这套窗口结构，因此：

- 不存在当前可实现或可依赖的 `IDebugWindowGroup`
- 不存在当前生效的 `DebugWindowGroup`
- 旧窗口组相关页面都只能视为退役说明

## 当前应查看

- [DebugManager.md](DebugManager.md)
- [DebugComponent.md](DebugComponent.md)
- [Managers/IDebugManager.md](Managers/IDebugManager.md)
