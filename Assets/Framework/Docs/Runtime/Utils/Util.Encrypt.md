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
string Util.Encrypt.AES.EncryptString(string content, string key = null, string iv = null)
string Util.Encrypt.AES.DecryptString(string content, string key = null, string iv = null)

byte[] Util.Encrypt.AES.EncryptBytes(byte[] content, string key = null, string iv = null)
byte[] Util.Encrypt.AES.DecryptBytes(byte[] content, string key = null, string iv = null)
```

### 行为

- 使用 `Aes.Create()`
- 模式固定为 `CBC`
- 填充固定为 `PKCS7`
- 框架**不内置任何默认 Key/IV**；`key` / `iv` 为空时取 Config 隐私配置在 `Nova.Config.LoadAsync()` 内注入的默认值
- 默认 Key/IV 尚未初始化又未显式传入时：打印 Error 并返回空结果；日志会指向 `Nova/Open Config → 通用配置 → 隐私配置`，要求为当前 `Platform × Channel × DevelopMode` 配置 `AES-Key / AES-IV`（UTF-8 各 16 字节）并重新导出 `ConfigRuntimeSO`
- 显式模式的 `key` / `iv` 必须同时传入，且均为 16 字节 UTF-8 字符串；只传其中一个或长度非法都会抛 `ArgumentException`，不会与默认隐私凭据混用
- 旧 `Configure` 仅保留隐藏兼容壳，调用时记录“隐私配置已接管、禁止手动调用”，不会修改密钥

### 设计意图

- 默认密钥只在 ConfigWindow 的“隐私配置”中维护；它与 `AppConfigs.AppAesKey / AppAesIV` 完全独立
- ConfigManager 在标记加载完成前初始化 AES；FrameworkManagersGroup 的 Shutdown finally 在模块关闭后清空静态凭据，单独关闭 ConfigManager 不等于重置
- Persist 任一存储实现启用 AES 时，会在 `Nova.Persist.LoadAsync()` 的存储实现并行初始化前确认默认凭据已就绪；缺配会直接失败，标准启动顺序为 `await Nova.Config.LoadAsync()` 后再 `await Nova.Persist.LoadAsync()`
- 默认 Key / IV 仅适用于本地数据混淆，不应用于高安全场景
- 每次非空加解密创建独立 AES 实例；默认 Key/IV 仍是共享静态数组

### 参数与状态边界

所有入口先处理空 content，空输入返回空结果并跳过凭据校验。null/空 Key、IV 均视为未显式提供；默认凭据不存在时 `EnsureDefaultKeyAndIVReady` 记录并抛异常，而字节入口的解析器会将该 `InvalidOperationException` 转为空结果。显式参数错误在 `DecryptString` 中也会被捕获，故空返回值不能作为成功标志。UTF-8 解码使用默认无效字节替代行为。

Config 初始化按 Key、IV 顺序短路验证，计 UTF-8 字节数而非字符数；两者通过后才替换静态数组。失败不先清旧凭据，替换和 Reset 也不清零旧数组。默认双字段读写没有锁，不保证并发重置与加解密的原子性。密文没有 Key/IV/MAC 头，不生成随机 IV；`using` 只释放算法/转换器，不清零输入、输出或字符串。

### Editor 维护门

`PrivacyAesMaintenanceGuard` 仅在 Editor 编译。它只检查工程根固定 `Library/Nova/AgentActions/PersistenceMaintenance/privacy-aes-active.json`，不扫描旧 `.agents` 位置或自动恢复。路径从标记到文件系统根逐级拒绝链接；存在的标记即使合法也阻断启动。合法标记要求严格 UTF-8、1..2048 字节、深度不超过 4、无重复/额外字段或尾部内容，且 schemaVersion=1、owner、规范完整工程根 SHA256 与 N 格式 operationId 均匹配。该门只读并保留未知现场；工程移动会改变项目归属哈希。

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

XOR 返回与输入等长的新数组，使用 `data[i] ^ code[i % code.Length]`，不修改两输入数组；四个公开入口对空 content/code 返回空。字符串为 UTF-8 与 Base64 包装，错误 code 仍产出数据，不验证完整性；私有核心依赖公开入口的非空检查。

## 注意事项

- 当前实现是独立的运行时工具，不依赖额外模块入口。
- AES/XOR 的 `DecryptString` 捕获异常、记录日志并返回空字符串；字节入口及加密入口的异常通常向外传播。
- 这套工具是运行时通用工具，不等同于资源系统的解密器实现。

## 关联文档
