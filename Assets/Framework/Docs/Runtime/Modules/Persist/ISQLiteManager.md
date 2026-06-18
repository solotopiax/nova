# ISQLiteManager

`ISQLiteManager` 是 SQLite 持久化后端的标记契约。

它延续 `IPersistManager` 的统一读写面，不额外暴露 SQL 级接口。这样做的重点不是“隐藏能力”，而是防止上层业务直接与 SQLite 细节耦合。

## 什么时候先看这页

- 你要替换 SQLite 后端实现。
- 你在确认 `PersistComponent.SQLite` 对外到底承诺了哪些能力。
- 你在思考某个需求是否应该以“新公共契约”而不是“SQLite 特有方法”进入系统。

## 契约语义

- 上层只拿到统一的 `IPersistManager` 行为面。
- 这个接口存在的主要意义是：
  - 类型过滤
  - 属性命名语义
  - 后端身份隔离

## 为什么它不暴露 SQL API

- 业务层一旦拿到原生 SQL 能力，就会绕开 `classify -> item -> value` 的统一持久化模型。
- 统一契约可以保留“换后端不换调用面”的空间。
- 真正的 SQLite 特性，例如表名规则、WebGL 不可用、事务提交，应该留在实现语义中说明，而不是上推到公共接口。

## 风险点 / 易错点

- 不要把它理解为“SQLite 全能力接口”；它只是统一持久化模型在 SQLite 后端下的承载体。
- 如果某能力只有 SQLite 用得上，先判断那是不是框架真的想公开给业务的稳定契约。
- 仅实现 `IPersistManager` 但不实现 `ISQLiteManager` 的类型，不能作为组件侧 SQLite 实现被正常创建。

## 继续阅读

关键源码：

- [ISQLiteManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/SQLite/ISQLiteManager.cs)
- [IPersistManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/IPersistManager.cs)

相关文档：

- [SQLiteManager.md](SQLiteManager.md)
- [SQLiteManagerConfig.md](SQLiteManagerConfig.md)
- [PersistComponent.md](PersistComponent.md)

