# CommonConfig

**类签名**：`[Serializable] public sealed class CommonConfig`
**命名空间**：`NovaFramework.Runtime`

全局公共配置数据结构，持有 AppID 和 AES 密钥/向量，由 ConfigMasterSO 按 DevelopMode 选取后填入。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/CommonConfig.cs` | `CommonConfig` | 数据结构定义 |

---

## §5 完整公开 API

```csharp
[Serializable]
public sealed class CommonConfig
{
    public string AppID;      // 应用标识符
    public string AppAesKey;  // AES 加密密钥
    public string AppAesIV;   // AES 初始化向量
}
```

---

## §11 使用示例

```csharp
// 运行时读取（DevelopMode 已在导出时选值）
CommonConfig common = Nova.Config.Common;
string appId = common.AppID;
```

---

## §13 关联文档

- [ConfigRuntimeSO.md](ConfigRuntimeSO.md)
- [ConfigMasterSO.md](ConfigMasterSO.md)
