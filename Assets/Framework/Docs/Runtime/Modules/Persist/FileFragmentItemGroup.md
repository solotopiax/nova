# FileFragmentItemGroup

`FileFragmentItemGroup` 是 `FileFragmentManager` 底下单个分类文件的内存容器。

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

当前格式在这段二进制负载后追加 `NFF1` 标记、格式版本、负载长度和 SHA-256 完整性校验；启用 AES 时再整体加密。旧格式仍可读取，并在下一次成功保存时自动升级；旧版客户端也能继续读取新版文件的前置负载。

### 3. 反序列化失败会返回 `false`，不抛异常

防御性失败场景包括：

- 文件为空
- AES 解密失败
- 条目数为负
- 流损坏
- 长度或完整性校验失败

这时调用方需要自己决定如何兜底。

### 4. 写入采用崩溃安全替换

保存时不会直接覆盖正式文件，而是：

1. 在同目录写入 `.tmp` 并刷新文件流
2. 重新读取临时文件完成解密、格式和完整性校验
3. 将原正式文件移动为 `.bak`
4. 将已校验临时文件移动为正式 `.dat`

任何一步抛出时都不会把该分类标记为保存成功；若正式文件已经转为备份但新文件尚未提升，会立即尝试恢复原文件。

### 5. 成功反序列化后才会整体替换内存

源码会先把文件完整读完并构造临时字典，成功后再替换 `m_Items`。这能避免坏文件把已有内存状态半途污染。

## 风险点 / 易错点

- 它不是业务层直接操作的推荐入口，常规调用仍应走 `IFileFragmentManager`。
- `SetString(null)` 会被转成空字符串，不会保留 null。
- `float` 使用 `InvariantCulture` 处理，避免不同地区小数点格式带来的歧义。
- SHA-256 在这里用于检测意外损坏，不替代加密认证；当前 AES-CBC 仍主要承担本地数据混淆。

## 继续阅读

关键源码：

- [FileFragmentItemGroup.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentItemGroup.cs)

相关文档：

- [FileFragmentManager.md](FileFragmentManager.md)
- [IFileFragmentManager.md](IFileFragmentManager.md)
