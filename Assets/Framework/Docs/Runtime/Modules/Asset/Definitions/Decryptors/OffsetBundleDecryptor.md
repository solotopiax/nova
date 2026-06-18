# OffsetBundleDecryptor

**类签名**：`public sealed class OffsetBundleDecryptor : IBundleOffsetDecryptor`
**命名空间**：`NovaFramework.Runtime`

YooAsset 偏移量解密器（占位骨架），待加密管线对接后填充偏移量逻辑。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Definitions/Decryptors/OffsetBundleDecryptor.cs` | `OffsetBundleDecryptor` | 占位骨架实现 |

---

## 完整公开 API

```csharp
public long GetFileOffset(BundleDecryptArgs args)
// 占位实现返回 0，Wave 5 业务接入后补全偏移量逻辑
```

---

## 使用示例

```csharp
// AssetManager 工厂化（AssetDecryptorType.OffsetBundleDecryptor 时实例化）
// 不直接调用
```

---

## 关联文档

- [AssetDecryptorType.md](../AssetDecryptorType.md)
- [BundleEncryptConfig.md](../BundleEncryptConfig.md)
