# TypeNamePair

**类签名**：`internal struct TypeNamePair : IEquatable<TypeNamePair>`
**命名空间**：`NovaFramework.Runtime`

类型与名称的组合值结构体，用于在框架内部将 `Type` 与可选的字符串名称组合为一个可哈希、可比较的键。用于 ObjectPoolManager 的类型+名称池键；ReferencePool 按 Type 单独分池。标记为 `internal`，仅供框架内部使用。

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

type 必须非空，单参构造使用空名称，双参 name=null 也归为空字符串。Equals 与运算符比较 Type 身份和名称字符串，default 值可比较且属性为 null；但 default 的 GetHashCode/ToString 会解引用空 Type，不可作为正常池键。哈希在 unchecked 中用 Type 哈希乘 397 再异或 Name 哈希；非空名 ToString 还依赖 Txt 格式化器，不构造或查询任何池。

## 关联文档

- [Structures](Structures.md)
