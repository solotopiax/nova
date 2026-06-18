# Util.MD5

**类签名**：`public static class MD5`（嵌套于 `public static partial class Util`）
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Util.MD5`

MD5 哈希工具，提供从字节数组或文件路径计算小写 MD5 十六进制字符串的静态方法。

---

## 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Utils/Util.MD5/Util.MD5.cs` | `static class MD5`（嵌套于 `partial Util`） | MD5 哈希方法实现 |

---

## 完整公开 API

```csharp
// --- Util.MD5 ---
string Util.MD5.GetHashFromBytes(byte[] bytes)
string Util.MD5.GetHashFromFile(string filePath)
```

| 方法 | 说明 | 返回值（空输入时） |
|------|------|-----------------|
| `GetHashFromBytes(byte[] bytes)` | 从字节数组计算 MD5 | `string.Empty` |
| `GetHashFromFile(string filePath)` | 从文件路径计算 MD5 | `string.Empty`（文件不存在时） |

---

## 使用示例

```csharp
// 校验下载文件完整性
string serverMD5 = hotfixVersion.ABInfos[fileName].MD5;
string localMD5  = Util.MD5.GetHashFromBytes(downloadedBytes);
if (serverMD5 == localMD5)
{
    // 校验通过，写入本地
}

// 校验本地持久化文件
string fileMD5 = Util.MD5.GetHashFromFile(Path.AB.PersistentTmp.GetFileFullPath(abName));
```

---

## 注意事项

| 场景 | 说明 |
|------|------|
| 类名与 BCL 的 `System.Security.Cryptography.MD5` 重名 | 内部实现使用 `using SystemMD5 = System.Security.Cryptography.MD5` 别名规避；调用方无感知 |
| 仅用于完整性校验 | MD5 不具备安全加密强度，不应用于账号、通信等安全敏感场景 |

---

## 关联文档

- [Util.Encrypt.md](Util.Encrypt.md)
- [Utils.md](Utils.md)
