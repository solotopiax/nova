# PlayerPrefsManagerConfig

`PlayerPrefsManagerConfig` 是 `PlayerPrefsManager.Initialize(...)` 使用的配置 DTO。

它本身不新增字段，实际承载的全部运行时配置都来自 `PersistManagerConfigBase`：

- `UseAESEncrypt`
- `AutoSaveInterval`

## 什么时候先看这页

- 你要确认 `PersistComponent` 给 PlayerPrefs 后端传了哪些配置。
- 你在判断某个配置该放到基类还是只属于具体后端。
- 你想核对 Inspector 中 PlayerPrefs 那几项字段最终落到了哪里。

## 配置语义

### 1. 这是“无增量字段”的具体配置类型

保留这个类型，不是因为它现在字段多，而是因为：

- `PersistManagerBase<TConfig>` 需要强类型初始化签名
- `PersistComponent` 需要按后端构造各自独立配置对象
- 后续如有 PlayerPrefs 专属参数，可以在这里安全扩展

### 2. 当前真正生效的是两个公共参数

- `UseAESEncrypt`：是否对存储值做 AES 加密
- `AutoSaveInterval`：自动保存间隔；`0` 或负数表示禁用

## 设计边界

- 这个类不负责 Inspector 展示；Inspector 字段定义在 `PersistComponent`
- 这个类不负责默认值策略；默认值由组件序列化字段给出
- 这个类不应该混入与其他后端共享的配置，那应回到 `PersistManagerConfigBase`

## 风险点 / 易错点

- “看起来是空类” 不代表它没必要，它承担的是类型边界而不是字段数量。
- 如果后续给 PlayerPrefs 增加专属配置，不要直接塞回基类，否则会污染 FileFragment / SQLite。
- 文档或工具若把它简化成“只是别名”，会低估泛型初始化链的设计作用。

## 继续阅读

关键源码：

- [PlayerPrefsManagerConfig.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PlayerPrefs/PlayerPrefsManagerConfig.cs)
- [PersistManagerConfigBase.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PersistManagerConfigBase.cs)

相关文档：

- [PlayerPrefsManager.md](PlayerPrefsManager.md)
- [PersistComponent.md](PersistComponent.md)
- [PersistManagerConfigBase.md](PersistManagerConfigBase.md)

