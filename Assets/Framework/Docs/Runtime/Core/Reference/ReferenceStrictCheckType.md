# ReferenceStrictCheckType

**类签名**：`public enum ReferenceStrictCheckType : byte`
**命名空间**：`NovaFramework.Runtime`

引用强制检查类型枚举，控制引用池在获取和归还引用时是否进行严格的类型与重复回收检查。可根据构建环境灵活配置检查策略，在开发阶段启用严格检查以尽早发现问题，在正式包中关闭以提升性能。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `ReferenceStrictCheckType.cs` | 枚举定义 |

## 枚举值

| 值 | 说明 |
|------|------|
| `AlwaysEnable` | 总是启用严格检查 |
| `OnlyEnableWhenDevelopment` | 仅在开发模式时启用 |
| `OnlyEnableInEditor` | 仅在编辑器中启用 |
| `AlwaysDisable` | 总是禁用严格检查 |

## 关联文档

- [ReferencePool](ReferencePool.md)
- [IReferenceHelper](Interfaces/IReferenceHelper.md)
