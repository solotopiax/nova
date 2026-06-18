# PlatformType

**类签名**：`[Serializable] public enum PlatformType : byte`
**命名空间**：`NovaFramework.Runtime`

运行平台类型枚举，标识应用发布的目标平台，供 ConfigMasterSO 矩阵行索引与 ConfigWindow 下拉选择使用。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Definitions/PlatformType.cs` | `PlatformType` | 枚举定义 |

---

## §5 完整公开 API

```csharp
[Serializable]
public enum PlatformType : byte
{
    None    = 0,  // 无效平台，兜底默认值
    Android = 1,  // Android 平台
    iOS     = 2,  // iOS 平台
    PC      = 3,  // PC 平台（Windows / macOS / Linux）
    WebGL   = 4,  // WebGL 平台
    Mini    = 5,  // 小程序平台（微信 / 抖音等）
}
```

---

## §11 使用示例

```csharp
// ConfigWindow 顶栏 Platform 下拉
PlatformType platform = (PlatformType)EditorGUILayout.EnumPopup(m_Master.CurrentPlatform);

// 按平台查找矩阵行
if (master.TryGetEntry(PlatformType.Android, ChannelType.Google, out var entry))
{
    // 使用 entry.SDKConfigs
}
```

---

## §13 关联文档

- [ChannelType.md](ChannelType.md)
- [ConfigMasterSO.md](../../Modules/Config/ConfigMasterSO.md)
