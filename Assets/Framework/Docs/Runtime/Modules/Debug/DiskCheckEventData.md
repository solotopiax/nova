# DiskCheckEventData

**类签名**：`public sealed class DiskCheckEventData : EventData`
**命名空间**：`NovaFramework.Runtime`

`DiskCheckEventData` 是当前 `Debug` 模块仍在使用的事件数据类型，由 `DebugManager` 在磁盘检测循环命中档位后发布。

## 当前字段

| 属性 | 类型 | 说明 |
|---|---|---|
| `AvailableSpace` | `int` | 当前可用磁盘空间，单位 MB |
| `AvailableSpaceLevel` | `int` | 本次命中的空间档位阈值，单位 MB |

## 当前 API

```csharp
public static DiskCheckEventData Create(int availableSpace, int availableSpaceLevel);
public override void Clear();
```

## 当前发布链路

`DebugManager.RunDiskCheckLoopAsync(...)` 中会：

1. 调用私有磁盘空间查询方法获取当前可用空间
2. 在 `DiskCheckingConfig.AvailableSpaces` 中命中当前档位
3. 通过 `m_EventManager.Fire(this, DiskCheckEventData.Create(...))` 发布事件

## 使用注意

- 该对象来自 `ReferencePool`，订阅方不要跨回调长期持有
- 发布方是 `DebugManager`，不是 `DebugComponent`
- Create 只从池取对象并保存两个整数，不发布、不检查 -1 等无效空间；Clear 将两值归零。必须在事件有效回调范围内复制所需数值，不能跨 await 持有该事件对象。

## 关联文档

- [DebugManager.md](DebugManager.md)
- [Windows/DiskCheckingConfig.md](Windows/DiskCheckingConfig.md)
- [../Event/EventData.md](../Event/Definitions/EventData.md)
