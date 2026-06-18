# AssetDecryptorType

**类签名**：`public enum AssetDecryptorType : byte`
**命名空间**：`NovaFramework.Runtime`

YooAsset 解密器类型枚举，由 AssetManagerConfig 持有，决定 AssetManager 注入哪种解密实现。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Definitions/AssetDecryptorType.cs` | `AssetDecryptorType` | 枚举定义 |

---

## 完整公开 API

| 值 | 底层值 | 说明 |
|----|--------|------|
| `None` | `0` | 不启用解密 |
| `OffsetBundleDecryptor` | `2` | 偏移量解密器（IBundleOffsetDecryptor） |

---

## 使用示例

```csharp
// 由 AssetManagerConfig 持有，BootstrapAsync 内部根据此值工厂化解密器
// Inspector 中通过 AssetComponent.m_DecryptorType 字段配置
// AssetComponent.Start() 现场构造 AssetManagerConfig 传入
await Nova.Asset.BootstrapAsync(ct);
```

---

## 关联文档

- [AssetManagerConfig.md](../AssetManager/Definitions/AssetManagerConfig.md)
- [OffsetBundleDecryptor.md](Decryptors/OffsetBundleDecryptor.md)
