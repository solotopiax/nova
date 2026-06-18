# IDiskUtilProvider（已移除）

`IDiskUtilProvider` 已不在当前源码中。

当前磁盘空间检测直接收敛在 `DebugManager.Methods.cs` 内，通过条件编译调用 `SimpleDiskUtils`，不再额外抽一层 provider 接口。

这意味着：

- `DebugComponent` 不再持有 `m_DiskUtilProvider`
- 可用空间 / 已占用空间 / 总空间查询都属于 `DebugManager` 私有实现细节
- 业务层不应该再寻找或依赖 `IDiskUtilProvider`

## 当前应查看

- [DebugManager.md](DebugManager.md)
- [Windows/DiskCheckingConfig.md](Windows/DiskCheckingConfig.md)
- [DiskCheckEventData.md](DiskCheckEventData.md)
