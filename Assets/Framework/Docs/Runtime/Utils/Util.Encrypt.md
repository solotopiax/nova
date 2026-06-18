# Util.Encrypt

**类签名**：`public static partial class Util` > `public static partial class Encrypt`  
**命名空间**：`NovaFramework.Runtime`

Nova 的轻量加解密工具集合。当前实现只有两组能力：`AES` 与 `XOR`。它们都是独立静态类，不依赖任何 Hotfix 运行时入口。

## 文件组成

| 文件 | 作用 |
|---|---|
| `Util.Encrypt.cs` | `Util.Encrypt` 根定义 |
| `Util.Encrypt.AES.cs` | AES 工具 |
| `Util.Encrypt.XOR.cs` | XOR 工具 |

## AES

### 公开 API

```csharp
void   Util.Encrypt.AES.Configure(string key, string iv)   // 注入默认 Key/IV，必须 16 字节
string Util.Encrypt.AES.EncryptString(string content, string key = null, string iv = null)
string Util.Encrypt.AES.DecryptString(string content, string key = null, string iv = null)

byte[] Util.Encrypt.AES.EncryptBytes(byte[] content, string key = null, string iv = null)
byte[] Util.Encrypt.AES.DecryptBytes(byte[] content, string key = null, string iv = null)
```

### 行为

- 使用 `Aes.Create()`
- 模式固定为 `CBC`
- 填充固定为 `PKCS7`
- 框架**不内置任何默认 Key/IV**；`key` / `iv` 为空时取 `Configure(key, iv)` 注入的默认值
- 默认 Key/IV 未注入又未显式传入时：打印错误日志（提示先调用 `Configure`）并返回空结果，不抛异常
- 显式传入或注入的 `key` / `iv` 必须是 16 字节 UTF-8 字符串，否则抛 `ArgumentException`

### 设计意图

- 密钥由使用方（游戏）通过 `Configure` 在启动时注入，框架源码不携带任何密钥（开源安全要求）
- 默认 Key / IV 仅适用于本地数据混淆，不应用于高安全场景
- 每次调用都创建独立 AES 实例，没有静态共享状态

## XOR

### 公开 API

```csharp
string Util.Encrypt.XOR.EncryptString(string content, byte[] code)
string Util.Encrypt.XOR.DecryptString(string content, byte[] code)

byte[] Util.Encrypt.XOR.EncryptBytes(byte[] content, byte[] code)
byte[] Util.Encrypt.XOR.DecryptBytes(byte[] content, byte[] code)
```

### 行为

- XOR 的加解密核心是同一套 `XorBytes`
- `code` 为空时直接返回空结果
- 适合轻量级混淆，不适合安全敏感数据

## 注意事项

- 当前实现是独立的运行时工具，不依赖额外模块入口。
- AES 解密和 XOR 解密在失败时会记录错误日志，并返回空字符串。
- 这套工具是运行时通用工具，不等同于资源系统的解密器实现。

## 关联文档
