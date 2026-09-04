# AssetPlayMode

**类签名**：`public enum AssetPlayMode : byte`
**命名空间**：`NovaFramework.Runtime`

Nova 资源策略枚举。资源策略与运行平台解耦，底层文件系统由 `AssetManager` 根据当前平台选择。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Definitions/AssetPlayMode.cs` | `AssetPlayMode` | 枚举定义 |

---

## 完整公开 API

| 值 | 底层值 | 说明 |
|----|--------|------|
| `EditorSimulateMode` | `0` | 编辑器模拟模式 |
| `OfflinePlayMode` | `1` | 离线运行模式 |
| `HostPlayMode` | `2` | 联机运行模式（需远端 URL） |

WebGL 不是独立资源策略：`OfflinePlayMode` 使用 WebServer 文件系统，`HostPlayMode` 使用 WebServer + WebNetwork 文件系统。

---

## 使用示例

```csharp
// AssetManagerConfig 持有，Inspector 配置；BuildPlayModeOptions 内部按运行环境二选一
// Application.isEditor  → m_Config.EditorPlayMode  决定资源策略
// !Application.isEditor → m_Config.RuntimePlayMode 决定资源策略
// UNITY_WEBGL            → AssetManager 为该策略选择 Web 文件系统
```

---

## 关联文档

- [AssetManagerConfig.md](../AssetManager/Definitions/AssetManagerConfig.md)
- [AssetComponentInspector.md](../../../../Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.md)
