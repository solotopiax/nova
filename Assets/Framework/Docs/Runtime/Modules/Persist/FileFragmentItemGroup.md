# FileFragmentItemGroup

`NovaFramework.Runtime.FileFragmentItemGroup` 是 `internal sealed class`，负责 `FileFragmentManager` 底下单个分类文件的内存容器。业务程序集不能直接创建或调用它，应使用公开的 `IFileFragmentManager`；Editor 维护服务通过框架内部可见性复用同一编解码。

可以把它理解成：

- 一个 `.dat` 文件对应一个 `FileFragmentItemGroup`
- 一个 `FileFragmentItemGroup` 内部维护 `item -> string` 映射

它本身不处理分类维度，分类维度由 `FileFragmentManager` 在外层维护。

## 什么时候先看这页

- 你要理解 FileFragment 存储实现到底把什么写进文件。
- 你在排查单个 `.dat` 文件反序列化失败后的行为。
- 你要确认基础类型在文件分片模式下是如何编码的。

## 角色定位

- 它是单文件数据容器，不是完整 Manager
- 它只关心一个分类内部的条目映射
- 它负责二进制序列化 / 反序列化，可选 AES

## 核心语义

### 1. 内部真实存储是字符串字典

它用 `SortedDictionary<string, string>` 存数据，所以：

- `bool / int / float` 只是读写时做字符串转换
- 不存在独立的强类型底层布局

### 2. 文件格式是简单二进制，不是 JSON

旧版单文件落盘格式是：

- 条目数
- 一组 `key/value` 字符串

当前格式在这段二进制负载后追加总计 44 字节的 `NFF1` 标记、格式版本、负载长度和 SHA-256 完整性校验；启用 AES 时再整体加密。旧格式仍可读取，并在下一次成功保存时自动升级；旧版客户端也能继续读取新版文件的前置负载。

解析先识别版本尾段：无标记时按旧负载处理；识别到的未知版本、长度或校验不符会失败。负载读取还拒绝过大条目数、重复键、截断及多余尾部数据。校验只用于内容完整性，既不加密，也不认证写入者。

### 3. 内容校验失败返回 `false`，文件访问错误继续向上传播

防御性失败场景包括：

- 文件为空
- AES 解密失败
- 条目数为负
- 流损坏
- 长度或完整性校验失败

这些内容错误返回 `false`，已有内存不会被半途替换。读取文件的 `IOException`、`UnauthorizedAccessException` 继续抛给调用方，不能当作空存档。

`Deserialize` 本身不恢复文件。`LoadWithRecovery` 是另一条内部入口：正式文件失败时尝试 `.bak`，成功可能重建正式文件；两者不可用时可能隔离坏文件。因此要求保持现场的只读检查不能调用 `LoadWithRecovery`。

### 4. 写入采用崩溃安全替换

保存时不会直接覆盖正式文件，而是：

1. 在同目录写入 `.tmp` 并刷新文件流
2. 重新读取临时文件完成解密、格式和完整性校验
3. 将原正式文件移动为 `.bak`
4. 将已校验临时文件移动为正式 `.dat`

任何一步抛出时都不会把该分类标记为保存成功；若正式文件已经转为备份但新文件尚未提升，会立即尝试恢复原文件。

非 WebGL 文件流使用 `Flush(true)`，WebGL 使用普通 Flush 后请求浏览器同步；这不是跨分类事务。finally 会清理临时文件，恢复或清理本身的 IO 异常仍可能传播。`.tmp`/`.bak` 路径仅为原路径追加后缀；坏文件隔离追加 UTC 时间，重名时增加后缀，不会静默删除原坏档。

### 5. 成功反序列化后才会整体替换内存

源码会先把文件完整读完并构造临时字典，成功后再替换 `m_Items`。这能避免坏文件把已有内存状态半途污染。

`Items` 返回原字典的 `IReadOnlyDictionary` 视图，并非快照；`Count` 和条目枚举只访问当前内存。直接调用容器的 Set/Remove 不会通知 Manager 标脏或写盘，业务应始终走 `IFileFragmentManager`。

## 风险点 / 易错点

- 它是内部类型，业务调用必须走 `IFileFragmentManager`，不得通过临时反射绕过。
- `SetString(null)` 会被转成空字符串，不会保留 null。
- `float` 使用 `InvariantCulture` 处理，避免不同地区小数点格式带来的歧义。
- SHA-256 在这里用于检测意外损坏，不替代加密认证；当前 AES-CBC 仍主要承担本地数据混淆。

## 继续阅读

关键源码：

- [FileFragmentItemGroup.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentItemGroup.cs)

相关文档：

- [FileFragmentManager.md](FileFragmentManager.md)
- [IFileFragmentManager.md](IFileFragmentManager.md)
