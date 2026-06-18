# DebugManagerConfig

**类签名**：`public class DebugManagerConfig`
**命名空间**：`NovaFramework.Runtime`

调试管理器配置类，作为 `IDebugManager.Initialize` 的参数传入。当前用于承载磁盘检测配置集合。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Runtime/Modules/Debug/Managers/DebugManagerConfig.cs` | 配置类定义 |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `DiskCheckingConfigs` | `List<DiskCheckingConfig>` | 磁盘监控配置集合，由 `DebugComponent.Start()` 组装传入 |

## 关联文档

- [IDebugManager](IDebugManager.md)
- [DebugManagerBase](DebugManagerBase.md)
