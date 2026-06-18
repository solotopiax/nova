# PersistManagerConfigBase

**类签名**：`public class PersistManagerConfigBase`
**命名空间**：`NovaFramework.Runtime`

持久化管理器配置公共基类，提取三套配置 DTO 的共用属性。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/PersistManagerConfigBase.cs` | `PersistManagerConfigBase` | 配置基类定义 |

---

## §5 完整公开 API

```csharp
public class PersistManagerConfigBase
{
    public bool  UseAESEncrypt    { get; set; }  // 是否启用 AES 加密存储值
    public float AutoSaveInterval { get; set; }  // 自动保存间隔（秒）；0 或负数禁用自动保存
}
```

---

## §11 使用示例

```csharp
// 子类通过继承获得公共字段，PersistComponent.Start() 统一构造
var config = new PlayerPrefsManagerConfig
{
    UseAESEncrypt    = true,
    AutoSaveInterval = 60f,
};
```

---

## §13 关联文档

- [PlayerPrefsManagerConfig.md](PlayerPrefsManagerConfig.md)
- [FileFragmentManagerConfig.md](FileFragmentManagerConfig.md)
- [SQLiteManagerConfig.md](SQLiteManagerConfig.md)
