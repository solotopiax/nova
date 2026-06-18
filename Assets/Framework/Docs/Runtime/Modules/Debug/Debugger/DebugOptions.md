# DebugOptions

**命名空间**：`NovaFramework.Runtime`

`DebugOptions` 是运行时调试选项容器，替代拆分前的旧选项容器。业务侧如需注册自定义选项，应通过 `RuntimeDebugger.Instance.AddOptionContainer(...)` 注册独立容器，而不是修改框架内置文件。

## 迁移口径

- 旧选项容器已删除，不提供 `[Obsolete]` shim。
- 选项变更事件委托已改为 `DebugOptionsPropertyChanged`。
- Options 属性和 Action 的反射逻辑保持原实现语义。

## 同步规则

`DebugOptions` 属于 RuntimeDebugger 的公开扩展面。以下改动必须同轮同步本页和 [RuntimeDebugger.md](RuntimeDebugger.md)：

- 属性 / Action 反射规则变化。
- 注册入口或事件委托命名变化。
- 选项容器目录、命名空间或程序集归属变化。
- 删除旧兼容入口或新增公开 shim。
