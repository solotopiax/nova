# PlayerPrefsManager

`PlayerPrefsManager` 是三种持久化存储实现里最偏“轻量键值缓存”的实现。

它把数据组织成 `classify -> item -> string` 的逻辑层，再通过 `PlatformPlayerPrefs` 写入当前平台存储。Unity 默认实现的值先进入 PlayerPrefs 缓存，索引由 `Save()`、自动保存或 `Shutdown()` 刷新；微信、支付宝小游戏的底层写入使用同步 Storage API，不能把所有平台都理解为仅写内存。

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

### 2. Set 系列先调用平台写入，再标脏

这个存储实现的关键设计不是立即刷盘，而是：

- 注册内存条目索引，按配置加密值，再调用平台写入
- 标记分类为 dirty
- 等待 `Save()` / 自动保存 / `Shutdown()`

Unity 默认实现不会在每次 `SetXxx()` 后调用 `PlayerPrefs.Save()`。平台写入、索引刷新与底层耐久性是三个不同边界。

### 3. Save(classify) 逻辑上按分类，物理上仍是全局 Save

旧直觉容易误判这里有真正的分类局部提交，但源码里已经明确说明：

- 它只在该分类有脏标记时刷新该分类条目索引和总分类索引
- 然后调用底层 `_Save()`

由于 Unity `PlayerPrefs` 不支持分类级独立提交，所以本质仍是全局保存。其他分类的值可能一并提交，但它们尚未刷新的条目索引不会因此自动刷新，脏标记也会保留；需要保存当前全部变更时使用无参 `Save()`。

### 4. RemoveAll(classify) 会立刻删索引并执行一次保存

这和普通 `RemoveItem()` 不同：

- `RemoveItem()` 偏向脏标记延迟保存
- `RemoveAll(classify)` 会同步清索引并立刻 `_Save()`

只有分类已在内存索引中时才会执行这条删除链；未知分类直接返回。`RemoveItem()` 删掉最后一项时会移除内存分类，后续索引刷新不会主动删除遗留的旧条目索引键。

## 输入、索引与读取结果

| 输入或操作 | 当前行为 |
|---|---|
| `classify` | 必须非 null、非空，不能含 `::` 或逗号；纯空白未被校验拒绝。 |
| `item` | 同样非 null、非空，不能含 `::` 或逗号，且不能等于保留名 `__items__`。 |
| 数据与索引键 | 值为 `classify::item`；条目索引为 `classify::__items__`；总分类索引为 `__pp_classifies__`。索引以逗号连接，不参与值级 AES。 |
| `SetString(..., null)` | 转为空串后写入，不删除条目，索引中仍可存在。 |
| `GetString` | 底层原始字符串为 null/空时返回 `defaultValue`；否则按配置解密。未加密空串因此会读成默认值；加密空串的原始密文非空，解密后可返回空串。 |
| `GetBool / GetInt / GetFloat` | 原始值缺失/为空时返回默认值；非空布尔内容仅 `"1"` 为 true，其他内容为 false；数值解析失败返回默认值，float 使用 `InvariantCulture`。解密异常继续抛出。 |
| `HasItem / Count` | 查询内存索引，不核实平台值键实际存在；索引中有项不能证明值可读取。 |
| `GetAllItemNames` | 数组重载复制当前集合；列表重载追加、不清空，已有条目时传 null 列表会抛出。HashSet 条目顺序没有保证。 |
| `GetAllClassifyNames` | 复制当前内存分类索引，不扫描平台全部原始键。 |

`Load()` 清空并重建内存分类/条目索引，过滤索引中的空片段，但不验证名称是否合法、值键是否存在，也不重置现有脏标记。它不能恢复未登记在索引中的原始键。扩展类型、缺省值与解析失败规则见 [PersistManagerBase.md](PersistManagerBase.md#扩展类型默认实现)。

## 保存失败与平台边界

- `WriteValue()` 先注册内存索引，随后加密和写入，最后才标脏。加密或平台写入失败会向上传播，已经注册的索引不会自动回滚，也未必被标脏。
- `RemoveItem()` 先从内存集合删除，再删除平台键；平台删除失败时内存已经改变。`RemoveAll()` 逐键删除，没有事务，异常可留下部分删除状态。
- 无参 `Save()` 在平台保存成功后才清空脏集合；`Save(classify)` 在调用平台保存前就移除该分类脏标记，保存异常时不会自动恢复标记。返回 true 表示调用链正常返回，不能证明设备断电或小游戏重启后的耐久性。
- `Shutdown()` 只在整体脏标记为 true 时刷新并保存，随后清索引；刷新或保存异常会阻止后续清理，没有 finally 兜底。
- `PlatformPlayerPrefs` 的宏分支优先为 `NOVA_DYSDK`（StarkSDK）、`NOVA_WXSDK`（WX 同步 Storage）、`NOVA_ALIPAYSDK`（Alipay 同步 Storage），否则使用 Unity PlayerPrefs。Unity/抖音调用平台 Save；微信/支付宝的 Save 分支为空。底层 helper 不做名称校验、AES 或索引维护，平台异常也不会在 helper 中被吞掉。

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

## Editor 已索引值维护

现三个存档 Action 可对标准默认 Unity provider 的完整 indexed strings 进行值 AES 开关维护，精确输入与恢复见 [存档维护](../../../Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)。必须 `acknowledgeGlobalSave=true`：Save 会提交整个进程偏好缓存，恢复只还原已知键，无法撤销其它脏值随此次全局提交落盘的影响。缺失索引、非字符串或坏密文阻断；不枚举未知键，不改普通业务读写/Save 语义，设备耐久性独立验证。
