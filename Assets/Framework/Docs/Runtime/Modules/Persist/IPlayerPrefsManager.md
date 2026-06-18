# IPlayerPrefsManager

`IPlayerPrefsManager` 是 `Persist` 模块里最轻的一层契约。

它本身不扩展新成员，只是把 `IPersistManager` 这套通用持久化读写契约标记为 “PlayerPrefs 后端”，供两处使用：

- `PersistComponent` 通过 `PlayerPrefs` 属性暴露这个后端
- `TypeCreator.Create<IPlayerPrefsManager>(...)` 用它做类型过滤和实例创建

## 什么时候先看这页

- 你要确认 `Nova.Persist.PlayerPrefs` 暴露的到底是什么契约。
- 你要替换 PlayerPrefs 后端实现，但不想改上层调用代码。
- 你在排查 Inspector 里 `CurPlayerPrefsManagerTypeName` 为什么会创建失败。

## 契约语义

- 上层只知道它是一个 `IPersistManager`，也就是统一的 `Load / Save / HasItem / GetXxx / SetXxx / Remove` 契约。
- 这个接口的意义不是“定义 PlayerPrefs 特有 API”，而是“把某个实现归类为 PlayerPrefs 后端”。
- 因为没有附加成员，所以跨后端调用面天然一致；真正的差异在具体实现行为，而不在接口签名。

## 与其他后端的关系

- `IPlayerPrefsManager`、`IFileFragmentManager`、`ISQLiteManager` 三者都是 `IPersistManager` 的后端别名。
- 三者可以被 `PersistComponent` 并存持有，但各自面向不同存储模型与平台约束。
- 如果你要写跨后端通用逻辑，优先看 `IPersistManager` 语义，不要把这个接口当成扩展点。

## 风险点 / 易错点

- 这个接口不是能力扩展点；给它加成员会直接破坏三后端统一调用面。
- Inspector 里填写的类型必须真正实现 `IPlayerPrefsManager`，仅继承 `IPersistManager` 但没挂这个标记接口也不会被正确创建。
- 如果你期待它暴露 PlayerPrefs 特有能力，那就是设计方向错了；特有差异应该留在实现细节或新增独立契约中。

## 继续阅读

关键源码：

- [IPlayerPrefsManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/PlayerPrefs/IPlayerPrefsManager.cs)
- [IPersistManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/IPersistManager.cs)

相关文档：

- [PersistComponent.md](PersistComponent.md)
- [PlayerPrefsManager.md](PlayerPrefsManager.md)
- [PlayerPrefsManagerConfig.md](PlayerPrefsManagerConfig.md)
