# DevelopMode

**类签名**：`[Serializable] public enum DevelopMode`
**命名空间**：`NovaFramework.Runtime`

开发模式枚举，与 `PlatformType`、`ChannelType` 共同构成配置三维索引。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Core/Definitions/DevelopMode.cs` | `DevelopMode` | 枚举定义 |

---

## §5 完整公开 API

```csharp
[Serializable]
public enum DevelopMode
{
    Debug,    // 开发/调试环境
    Release,  // 正式发布环境
}
```

---

## §11 使用示例

```csharp
await Nova.Config.LoadAsync();
DevelopMode runtimeMode = Nova.Config.DevelopMode;

// 示例导出路径，按项目自己的 Runtime SO 落盘位置调整。
ConfigRuntimeSO runtime = EditorUtil.Config.Exporter.Export(
    master, PlatformType.Android, ChannelType.Google, DevelopMode.Debug,
    "Assets/Config/Runtime_Android_Google_Debug.asset");
```

---

## §13 关联文档

- [ConfigRuntimeSO.md](../../Modules/Config/ConfigRuntimeSO.md)
- [IConfigManager.md](../../Modules/Config/Interfaces/IConfigManager.md)
