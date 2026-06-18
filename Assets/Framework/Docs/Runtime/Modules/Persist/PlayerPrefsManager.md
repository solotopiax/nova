# PlayerPrefsManager

`PlayerPrefsManager` 是三套持久化后端里最偏“轻量键值缓存”的实现。

它把数据组织成 `classify -> item -> string` 的逻辑层，再映射到 Unity `PlayerPrefs`。写入时先改内存并标脏，真正落盘由 `Save()`、自动保存或 `Shutdown()` 触发。

## 什么时候先看这页

- 你要存用户偏好、开关、少量配置这类轻量键值数据。
- 你在排查“为什么 `SetXxx()` 之后不是立刻写盘”。
- 你想知道它和 `FileFragment`、`SQLite` 的真实差异。

## 依赖与边界

### 它依赖什么

- `PersistManagerBase<PlayerPrefsManagerConfig>`
- Unity `PlayerPrefs`
- `PersistComponent` 注入的 `UseAESEncrypt` 与 `AutoSaveInterval`

### 它对外负责什么

- 维护分类索引与条目索引
- 提供完整 `GetXxx / SetXxx / Remove / Save` 键值接口
- 在 Update 中驱动自动保存

### 它不负责什么

- 不做复杂查询
- 不做真正的分类级物理隔离，底层仍然是统一 PlayerPrefs key 空间
- 不保证每次 `SetXxx()` 立刻持久化

## 核心行为

### 1. Initialize 会先接入公共配置，再全量加载索引

初始化顺序很直接：

1. `InitializeBase(config)`
2. `Load()`

这里加载的是分类和条目索引，不是“每次读取前再懒加载”。

### 2. Set 系列默认是写内存 + 标脏

这个后端的关键设计不是立即刷盘，而是：

- 写值
- 标记分类为 dirty
- 等待 `Save()` / 自动保存 / `Shutdown()`

所以它更像“带缓冲的 PlayerPrefs”，不是“每次写立即存盘”。

### 3. Save(classify) 逻辑上按分类，物理上仍是全局 Save

旧直觉容易误判这里有真正的分类局部提交，但源码里已经明确说明：

- 它会刷新指定分类相关索引
- 然后调用底层 `_Save()`

由于 Unity `PlayerPrefs` 不支持分类级独立提交，所以本质仍是全局保存。

### 4. RemoveAll(classify) 会立刻删索引并执行一次保存

这和普通 `RemoveItem()` 不同：

- `RemoveItem()` 偏向脏标记延迟保存
- `RemoveAll(classify)` 会同步清索引并立刻 `_Save()`

## 什么时候适合它

- 小规模、频繁读写的用户本地设置
- 不需要复杂结构和独立文件管理的简单数据
- 希望沿用统一 `IPersistManager` 调用面，但底层仍用 Unity 原生存储

## 风险点 / 易错点

- `Save(classify)` 不是“只提交这一类”的真正分区保存。
- 如果自动保存被关掉，`SetXxx()` 后必须自己调用 `Save()`，否则依赖 `Shutdown()` 才会落盘。
- 加密开关只决定值的 AES 处理，不改变 PlayerPrefs 自身的键组织方式。
- 不要把它当高容量存档方案；它的强项是轻量键值，不是大对象和大批量数据。

## 继续阅读

关键源码：

- [PlayerPrefsManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PlayerPrefs/PlayerPrefsManager.cs)
- [PersistManagerBase.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PersistManagerBase.cs)

相关文档：

- [PersistComponent.md](PersistComponent.md)
- [IPlayerPrefsManager.md](IPlayerPrefsManager.md)
- [PlayerPrefsManagerConfig.md](PlayerPrefsManagerConfig.md)

