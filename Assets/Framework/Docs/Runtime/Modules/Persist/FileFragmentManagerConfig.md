# FileFragmentManagerConfig

`FileFragmentManagerConfig` 是 `FileFragmentManager` 的初始化配置 DTO。

和 `PlayerPrefsManagerConfig` 一样，它当前没有新增字段，实际生效参数全部来自 `PersistManagerConfigBase`：

- `UseAESEncrypt`
- `AutoSaveInterval`

## 什么时候先看这页

- 你要核对 FileFragment 后端初始化时到底吃哪些配置。
- 你在判断“分片文件根目录”是不是从这里传入的。
- 你要为 FileFragment 增加专属配置项。

## 配置语义

### 1. 当前只承载公共持久化参数

- `UseAESEncrypt` 决定 `.dat` 文件内容是否以 AES 处理
- `AutoSaveInterval` 决定自动保存轮询节奏

### 2. 根目录不是由这个配置决定

文件根目录来自实现内部的：

- `Path.Persist.FileFragment.FolderFullPath`

所以这不是一个“你给什么路径它就写哪里”的自由配置对象。

## 设计边界

- 公共字段留在 `PersistManagerConfigBase`
- 只有 FileFragment 专属参数才应放回这个类
- 路径策略属于运行时框架约定，不属于这个 DTO 当前职责

## 风险点 / 易错点

- 不要把“空类”误认为冗余；它承担的是后端类型边界。
- 如果未来增加路径、序列化策略之类字段，需要确认是否真的应该开放给组件层，而不是继续保持框架内置约束。
- 误把文件根目录当成外部可配项，会导致文档与源码认知错位。

## 继续阅读

关键源码：

- [FileFragmentManagerConfig.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentManagerConfig.cs)
- [PersistManagerConfigBase.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PersistManagerConfigBase.cs)

相关文档：

- [FileFragmentManager.md](FileFragmentManager.md)
- [PersistComponent.md](PersistComponent.md)
- [PersistManagerConfigBase.md](PersistManagerConfigBase.md)

