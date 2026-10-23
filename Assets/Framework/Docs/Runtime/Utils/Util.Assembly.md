# Util.Assembly

**类签名**：`public static partial class Util.Assembly`（嵌套于 `public static partial class Util`）
**命名空间**：`NovaFramework.Runtime`

纯反射工具：跨所有已加载程序集按名称查找程序集（`GetAssembly`）、按类型全名搜索 `Type`、收集子类名称列表、刷新程序集缓存。不承担任何 IO 职责。

---

## §2 文件表

| 文件 | 说明 |
|------|------|
| `Runtime/Core/Util/Util.Assembly/Util.Assembly.cs` | 全部实现：程序集查找、类型缓存、子类名称收集、缓存刷新 |

---

## §4 关键字段表

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_Assemblies` | `volatile Assembly[]` | 当前 AppDomain 已加载的程序集快照，静态构造器中调用 `RefreshAssemblies()` 初始化 |
| `s_CachedTypes` | `ConcurrentDictionary<string, Type>` | 类型全名到 Type 的查找缓存（Ordinal 比较），命中后跳过遍历 |
| `s_CachedTypeNames` | `ConcurrentDictionary<string, string>` | 保留字段（当前未使用） |

---

## §5 完整公开 API

### 程序集管理

```csharp
/// 刷新程序集缓存（热更新加载新程序集后调用）。
public static void RefreshAssemblies();

/// 获取已加载的程序集快照。
public static System.Reflection.Assembly[] GetAssemblies();

/// 按程序集名在 AppDomain.CurrentDomain 中查找已加载的 Assembly。
/// 未找到时返回 null。
public static System.Reflection.Assembly GetAssembly(string assemblyName);
```

### 类型收集

```csharp
/// 清空并写入调用方提供的 List；程序集反射仍可分配。
public static void GetTypes(List<Type> results);
```

### 类型查找

```csharp
/// 按类型全名查找 Type，支持可选程序集名称缩小查找范围。
/// 内部使用 s_CachedTypes 缓存命中结果。
public static Type GetType(string typeName, string assemblyName = null);
```

### 子类名称收集

```csharp
/// 获取指定基类的所有非抽象子类的 FullName 数组（按 Ordinal 排序）。
public static string[] GetTypeNames(Type typeBase, string assemblyName = null);

/// 限定单个程序集的子类名称收集，避免跨程序集越界扫描。
public static string[] GetTypeNames(Type baseType, System.Reflection.Assembly targetAssembly);

/// 复用列表重载：清空并写入子类名称（按 Ordinal 排序）。
public static void GetTypeNames(Type typeBase, List<string> results, string assemblyName = null);
```

---

## 查询边界

`GetAssembly` 匹配已缓存程序集的简单名称（区分大小写），空名称抛异常，未命中返回 `null`。静态构造和 `RefreshAssemblies` 只重新取得当前 AppDomain 快照，不加载 DLL，也不清空已命中的类型缓存。`GetAssemblies` 直接返回当前数组引用，调用方应避免修改元素。

`GetType` 的缓存键只有 `typeName`；缓存命中和首先执行的 `Type.GetType(typeName)` 都先于 `assemblyName` 过滤。指定程序集的后备分支实际查询 `assemblyName + "." + typeName`，因此不能把第二个参数当成对完整类型名的强制程序集校验。无程序集参数时按快照逐个查询，未找到返回 `null`，失败结果不缓存。

`GetTypeNames` 只筛选 `IsClass && !IsAbstract && baseType.IsAssignableFrom(type)`，可包含基类本身及开放泛型，不证明可构造、已裁剪保留或可热更新执行。结果按 Ordinal 排序；仅 `ReflectionTypeLoadException` 会恢复其中非空的已加载类型，其他异常传播。`GetTypes(List<Type>)` 没有该恢复或排序，异常前列表可能已有部分结果。所有列表重载都会先清空调用方列表，且没有统一并发快照事务。

---

## §10 常见误区

| 误区 | 说明 |
|------|------|
| `GetType` 内部的字符串拼接 | 内部使用 `string.Concat` 而非 `Txt.Format`，因为此处仅做简单拼接无格式化需求，`string.Concat` 性能更优 |
| 热更新后未刷新缓存 | HybridCLR 加载新程序集后必须调用 `RefreshAssemblies()`；`Util.HybridCLR.LoadGameAssemblyAsync` IL2CPP 分支内部已自动调用，手动加载 DLL 需自行调 `RefreshAssemblies()` |
| `GetAssembly` 与 `GetType` 的区别 | `GetAssembly` 按程序集名在 AppDomain 查找 `Assembly` 对象；`GetType` 按类型全名查找 `Type` 对象 |
| 用 `GetTypeNames(baseType, assemblyName=null)` 扫 Framework Procedure | 应改用 `GetTypeNames(typeof(ProcedureBase), typeof(ProcedureBase).Assembly)` 精确限定，避免误扫到尚未加载的业务程序集 |

---

## §11 使用示例

```csharp
// 按程序集名查找已加载的 Assembly
System.Reflection.Assembly asm = Util.Assembly.GetAssembly("Game.Runtime");

// 按全名查找类型
Type t = Util.Assembly.GetType("NovaFramework.Runtime.UIManager");

// 指定程序集缩小查找范围
Type t2 = Util.Assembly.GetType("XxxManager", "NovaFramework.Runtime");

// 复用列表收集所有类型
List<Type> allTypes = new List<Type>();
Util.Assembly.GetTypes(allTypes);

// 限定程序集扫描子类（推荐方式，避免跨程序集越界）
string[] names = Util.Assembly.GetTypeNames(typeof(ProcedureBase), typeof(ProcedureBase).Assembly);

// 全程序集子类名称
string[] allNames = Util.Assembly.GetTypeNames(typeof(FrameworkManager));

// 热更新加载新程序集后刷新缓存
Util.Assembly.RefreshAssemblies();
```

`Util.TypeCreator` 内部依赖 `GetType` 实现反射创建实例。

---

## §13 关联文档

- [Util.TypeCreator.md](Util.TypeCreator.md)
- [Util.HybridCLR.md](Util.HybridCLR.md)（业务 DLL 加载唯一 Facade；加载完成后调用 RefreshAssemblies）
- [ProcedureLoadDll.md](../Modules/Procedure/Procedures/ProcedureLoadDll.md)（主要调用方：GetAssembly + RefreshAssemblies）
