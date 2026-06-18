# AssetPlayMode

**类签名**：`public enum AssetPlayMode : byte`
**命名空间**：`NovaFramework.Runtime`

YooAsset 资源运行模式枚举，与 YooAsset 初始化参数派生类型一一对应。

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
| `WebPlayMode` | `3` | WebGL 运行模式 |

---

## 使用示例

```csharp
// AssetManagerConfig 持有，Inspector 配置；BuildPlayModeOptions 内部按运行环境二选一
// Application.isEditor  → m_Config.EditorPlayMode  决定 YooAsset 初始化参数类型
// !Application.isEditor → m_Config.RuntimePlayMode 决定 YooAsset 初始化参数类型
```

---

## 关联文档

- [AssetManagerConfig.md](../AssetManager/Definitions/AssetManagerConfig.md)
- [AssetComponentInspector.md](../../../../Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.md)
