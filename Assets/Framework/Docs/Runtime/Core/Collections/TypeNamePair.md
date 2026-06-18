# TypeNamePair

**类签名**：`internal struct TypeNamePair : IEquatable<TypeNamePair>`
**命名空间**：`NovaFramework.Runtime`

类型与名称的组合值结构体，用于在框架内部将 `Type` 与可选的字符串名称组合为一个可哈希、可比较的键。常用于引用池等需要按类型+名称区分实例的场景。标记为 `internal`，仅供框架内部使用。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `TypeNamePair.cs` | 结构体定义 |

## 关键字段/属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Type` | `Type` | 类型 |
| `Name` | `string` | 名称（默认为空字符串） |

## 公开 API

```csharp
// 构造
TypeNamePair(Type type);
TypeNamePair(Type type, string name);

// 比较
bool Equals(TypeNamePair value);
override bool Equals(object obj);
override int GetHashCode();
static bool operator ==(TypeNamePair a, TypeNamePair b);
static bool operator !=(TypeNamePair a, TypeNamePair b);

// 字符串表示
override string ToString();
```

## 关联文档

- [Structures](Structures.md)
