# IFileFragmentManager

`IFileFragmentManager` 是 `FileFragment` 后端的标记契约。

和 `IPlayerPrefsManager`、`ISQLiteManager` 一样，它不扩展 `IPersistManager` 的成员面，作用是把“文件分片存储实现”从统一持久化契约里显式分出来，便于：

- `PersistComponent.FileFragment` 暴露强语义入口
- 运行时按接口反射创建具体实现

## 什么时候先看这页

- 你要替换或扩展 FileFragment 后端实现。
- 你在确认 `Nova.Persist.FileFragment` 到底承诺了什么调用面。
- 你想判断某个能力应不应该进入所有持久化后端的公共接口。

## 契约语义

- 上层只能依赖 `IPersistManager` 这一组统一读写能力。
- 这个接口的职责是“声明后端身份”，不是“增加文件系统专属能力”。
- 因为契约面保持一致，业务代码可以在不改调用面的前提下切换后端。

## 与实现的关系

- 当前默认实现是 `FileFragmentManager`
- 真实差异点在于：按 `classify` 切文件、懒加载、脏片段写回、删除待处理
- 这些都属于实现语义，不在接口层暴露

## 风险点 / 易错点

- 如果把文件路径、序列化格式之类的实现细节塞进接口，会直接抬高上层耦合。
- 仅实现 `IPersistManager` 而不实现 `IFileFragmentManager`，无法作为 `PersistComponent` 的 FileFragment 后端被创建。
- 想做跨后端共享逻辑时，优先依赖 `IPersistManager`，不要依赖这个标记接口。

## 继续阅读

关键源码：

- [IFileFragmentManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/IFileFragmentManager.cs)
- [IPersistManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/IPersistManager.cs)

相关文档：

- [FileFragmentManager.md](FileFragmentManager.md)
- [FileFragmentManagerConfig.md](FileFragmentManagerConfig.md)
- [PersistComponent.md](PersistComponent.md)

