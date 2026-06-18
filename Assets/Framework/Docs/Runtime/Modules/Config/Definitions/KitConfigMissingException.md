# KitConfigMissingException

**类签名**：`public sealed class KitConfigMissingException : Exception`
**命名空间**：`NovaFramework.Runtime`
**程序集**：`NovaFramework.Runtime`

Kit 配置缺失异常；Kit Service 通过 `Nova.Config.GetKitConfig<T>()` 取不到配置时抛出，属开发期部署错误，fail-fast 暴露配置漏填。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/KitConfigMissingException.cs` | `KitConfigMissingException` | 异常类定义 |

---

## §5 完整公开 API

```csharp
public sealed class KitConfigMissingException : Exception
{
    public KitConfigMissingException(string configTypeName);
}
```

**构造参数**：`configTypeName` — 缺失的 Kit 配置类型全名（通常传 `typeof(T).FullName`）。

---

## §11 使用示例

```csharp
// ConfigManager.GetKitConfig<T>() 内部抛出示例
if (config == null)
    throw new KitConfigMissingException(typeof(T).FullName);

// 业务侧不应 catch 此异常；属开发期配置错误，应修复 ConfigWindow 配置再重跑
```

---

## §13 关联文档

- [IKitConfig.md](IKitConfig.md)
- [ConfigRuntimeSO.md](../ConfigRuntimeSO.md)
