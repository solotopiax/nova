# Util.TypeCreator

**类签名**：`public static partial class Util { public static partial class TypeCreator { ... } }`
**命名空间**：`NovaFramework.Runtime`

框架 DI 核心：按类型全名字符串反射创建实例。所有 Manager、Helper 均通过此方法注入，实现运行时可替换实现（Inspector 修改类型名字符串即可切换）。

## 文件

`Util.TypeCreator/Util.TypeCreator.cs`

## API

```csharp
// 按完整类名字符串创建对象，返回接口类型
IXxxManager mgr = Util.TypeCreator.Create<IXxxManager>("NovaFramework.Runtime.XxxManager");
```

> **注意**：需要完整限定类名（含命名空间）。内部依赖 `Util.Assembly.GetType` 跨程序集搜索。

## 关联

- `Util.Assembly`：[Util.Assembly.md](Util.Assembly.md)
- 架构说明：[ARCHITECTURE.md](../../ARCHITECTURE.md)
