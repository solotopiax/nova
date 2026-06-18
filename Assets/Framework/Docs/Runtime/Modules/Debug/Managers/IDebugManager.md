# IDebugManager

**类签名**：`public interface IDebugManager`
**命名空间**：`NovaFramework.Runtime`

调试管理器接口。当前对 `DebugComponent` 只暴露最小生命周期契约：初始化与关闭。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Runtime/Modules/Debug/Managers/Interfaces/IDebugManager.cs` | 接口定义 |

## 公开 API

```csharp
// 初始化
void Initialize(DebugManagerConfig config);

// 关闭并清理调试器管理器
void Shutdown();
```

## 关联文档

- [DebugManagerBase](DebugManagerBase.md)
- [DebugManagerConfig](DebugManagerConfig.md)
- [DebugManager](../DebugManager.md)
