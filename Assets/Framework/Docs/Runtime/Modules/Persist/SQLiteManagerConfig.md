# SQLiteManagerConfig

`SQLiteManagerConfig` 是 SQLite 后端初始化时使用的配置 DTO。

它在公共持久化配置之外，额外新增了一个 SQLite 专属字段：

- `CipherPassword`

## 什么时候先看这页

- 你要确认 SQLite 后端到底有哪些专属初始化参数。
- 你在区分“值级 AES 加密”和“数据库级 Cipher 加密”。
- 你打算继续给 SQLite 后端增加专属能力。

## 配置语义

### 1. 公共部分仍来自基类

- `UseAESEncrypt`
- `AutoSaveInterval`

这两项对三套后端都成立。

### 2. SQLite 专属部分是数据库级密码

`CipherPassword` 的语义是：

- 传给数据库连接层
- 为空时不启用数据库级加密

它和 `UseAESEncrypt` 并行存在，不是二选一关系。

## 设计边界

- 组件层只负责把 Inspector 里的 `m_SQLiteCipherPassword` 搬进这个 DTO
- 真正如何打开连接、何时启用 WAL、如何应用密码，属于 `SQLiteManager` 内部职责
- 只有 SQLite 专属参数才应该进入这个类

## 风险点 / 易错点

- `CipherPassword` 不是给单条 value 做加密的，它作用在数据库层。
- 如果同时开了 `UseAESEncrypt` 和 `CipherPassword`，含义是“值级 + 库级”双层保护，不是重复字段。
- 不要把 WebGL 可用性、表名策略之类运行时行为误写成这个 DTO 的字段职责。

## 继续阅读

关键源码：

- [SQLiteManagerConfig.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/SQLite/SQLiteManagerConfig.cs)
- [PersistManagerConfigBase.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PersistManagerConfigBase.cs)

相关文档：

- [SQLiteManager.md](SQLiteManager.md)
- [PersistComponent.md](PersistComponent.md)
- [PersistManagerConfigBase.md](PersistManagerConfigBase.md)
