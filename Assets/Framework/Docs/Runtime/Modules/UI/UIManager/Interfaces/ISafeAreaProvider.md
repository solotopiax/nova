# ISafeAreaProvider

**类签名**：`public interface ISafeAreaProvider`
**命名空间**：`NovaFramework.Runtime`

设备安全区域数据提供者接口，抽象平台差异，使 UIManager 只依赖接口。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Interfaces/ISafeAreaProvider.cs` | `interface ISafeAreaProvider` | 接口定义 |
| `Definitions/SafeAreaData.cs` | `struct SafeAreaData` | 安全区域原始数据（Left/Right/Top/Bottom/PixelRatio/ScreenHeight） |
| `Definitions/DefaultSafeAreaProvider.cs` | `sealed class DefaultSafeAreaProvider : ISafeAreaProvider` | 基于 `Screen.safeArea` 的默认实现 |

---

## §5 完整公开 API

```csharp
public interface ISafeAreaProvider
{
    SafeAreaData GetSafeArea();
}

public sealed class DefaultSafeAreaProvider : ISafeAreaProvider
{
    public SafeAreaData GetSafeArea(); // 基于 Screen.safeArea，PixelRatio 固定为 1.0f
}
```

---

## §11 使用示例

```csharp
// 业务层注入自定义提供者（如 WebGL 微信平台）
public class WXSafeAreaProvider : ISafeAreaProvider
{
    public SafeAreaData GetSafeArea() =>
        new SafeAreaData { Left = 0f, Right = 750f, Top = 44f, Bottom = 34f, PixelRatio = 2f, ScreenHeight = 844f };
}

// UIManagerConfig 中注入，null 时自动回退为 DefaultSafeAreaProvider
config.SafeAreaProvider = new WXSafeAreaProvider();
```

---

## §13 关联文档

- [UIManager.md](../UIManager.md)
- [UIManagerConfig.md](../Definitions/UIManagerConfig.md)
