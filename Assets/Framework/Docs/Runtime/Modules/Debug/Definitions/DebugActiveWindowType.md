# DebugActiveWindowType（旧枚举名说明）

`DebugActiveWindowType` 已不在当前源码中。

当前生效的枚举是 [DebuggerActiveType.md](DebuggerActiveType.md)，用于决定 `DebugComponent` 是否初始化 `RuntimeDebugger`。

旧名与当前名的对应关系：

| 旧名 | 当前名 |
|---|---|
| `AlwaysOpen` | `AlwaysEnable` |
| `OnlyOpenWhenDevelopment` | `OnlyEnableWhenDevelopment` |
| `OnlyOpenInEditor` | `OnlyEnableInEditor` |
| `AlwaysClose` | `AlwaysDisable` |

如果你在旧文档或旧场景说明里看到 `DebugActiveWindowType`，应按 `DebuggerActiveType` 理解。

## 当前应查看

- [DebuggerActiveType.md](DebuggerActiveType.md)
- [../DebugComponent.md](../DebugComponent.md)
