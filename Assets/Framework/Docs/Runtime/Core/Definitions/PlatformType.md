# PlatformType

**类签名**：`[Serializable] public enum PlatformType : byte`
**命名空间**：`NovaFramework.Runtime`

运行平台类型枚举，标识应用发布的目标平台，供 ConfigMasterSO 矩阵行索引使用。ConfigWindow 仅只读展示由 Unity Active BuildTarget 映射的当前值；平台切换必须通过 Unity Build Settings 完成。

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
    WebGL   = 3,  // WebGL 平台
}
```

---

## §11 使用示例

```csharp
// Editor 配置入口：实时取得 Unity 当前 Active BuildTarget 对应的平台
PlatformType platform = EditorUtil.Config.ActivePlatform.Current;
if (platform == PlatformType.None)
{
    throw new InvalidOperationException("请先切换到 Android、iOS 或 WebGL BuildTarget。");
}

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
