# Util.TypeCreator

**类签名**：`public static partial class Util { public static class TypeCreator { ... } }`
**命名空间**：`NovaFramework.Runtime`

框架 DI 核心：按类型全名字符串反射创建实例。Manager、Helper 可通过此方法注入，实现运行时可替换实现（Inspector 修改类型名字符串即可切换）。

## 文件

`Util.TypeCreator/Util.TypeCreator.cs`

## API

```csharp
// 按完整类名字符串创建对象，返回接口类型
IXxxManager mgr = Util.TypeCreator.Create<IXxxManager>("NovaFramework.Runtime.XxxManager");
```

> **注意**：需要完整限定类名（含命名空间）。内部依赖 `Util.Assembly.GetType` 跨程序集搜索。

## 创建与失败边界

空类型名、找不到类型或无法赋给 `T` 时抛 `ArgumentException`。类型解析沿用 `Util.Assembly.GetType` 的缓存与程序集查询边界。普通 C# 类型通过 `Activator.CreateInstance(type, args)` 创建；`MonoBehaviour` 子类则新建以类型名命名的 `GameObject` 并调用 `AddComponent`，不使用构造参数，也不自动挂入既有场景层级。

创建阶段的异常包装为保留内部异常的 `InvalidOperationException`；结果为 null 也抛异常。失败时没有销毁已创建 GameObject 的回滚，成功返回后的对象生命周期由调用方负责。此工具不证明类型具备合适构造器、AOT 代码或裁剪保留，也不切换到 Unity 主线程。

## 关联

- `Util.Assembly`：[Util.Assembly.md](Util.Assembly.md)
- 架构说明：[ARCHITECTURE.md](../../ARCHITECTURE.md)
