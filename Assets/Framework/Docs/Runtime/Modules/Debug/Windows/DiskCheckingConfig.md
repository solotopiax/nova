# DiskCheckingConfig

**类签名**：`[Serializable] public class DiskCheckingConfig`
**命名空间**：`NovaFramework.Runtime`

`DiskCheckingConfig` 是当前 `Debug` 模块仍然在用的配置 DTO。虽然这页文档历史上放在 `Windows/` 目录下，但当前真实源码位于 `Assets/Framework/Scripts/Runtime/Modules/Debug/Definitions/DiskCheckingConfig.cs`。

它由 `DebugComponent` 序列化持有，在 `Start()` 时通过 `DebugManagerConfig` 传给 `DebugManager`。`DebugManager` 会按平台选出当前配置，并按空间档位驱动磁盘检测循环。

## 当前字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `Enabled` | `bool` | 是否开启当前平台的磁盘检测 |
| `PlatformName` | `string` | 平台名称 |
| `AvailableSpaces` | `List<int>` | 剩余空间阈值列表，单位 MB |
| `AvailableSpacesIntervals` | `List<float>` | 每个阈值命中后的下一次检测间隔，单位秒 |

## 当前行为

- 构造函数会初始化 `AvailableSpaces` 与 `AvailableSpacesIntervals`
- `DebugManager.Initialize(...)` 会校验两个列表长度是否一致
- 初始化阶段会把最后一档阈值回填为当前磁盘总空间，作为兜底档位

新对象默认 Enabled=false、两列表为空，PlatformName 仅保存传入文本；实际平台按列表位置选择，不按名称查找。DTO 不验证平台名、阈值顺序或间隔范围，也不启动检测；Manager 持有原引用并会修改末阈值，业务不能把传入配置视为不可变快照。

## 当前应查看

- [../DebugComponent.md](../DebugComponent.md)
- [../DebugManager.md](../DebugManager.md)
- [../DiskCheckEventData.md](../DiskCheckEventData.md)
