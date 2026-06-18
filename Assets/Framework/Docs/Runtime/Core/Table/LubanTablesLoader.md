# LubanTablesLoader

**命名空间**：`NovaFramework.Runtime`

**文件位置**：`Runtime/Core/Table/LubanTablesLoader.cs`

Luban Tables 反射加载器。通过反射构造 `*Tables` 实例（如 `TableTables`、`ConfigTables`），并提取其中的所有 `ITable` 实现到字典中。Table / Config 等模块共用此实现，避免重复编写相同的反射加载逻辑。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Runtime/Core/Table/LubanTablesLoader.cs` | 静态加载器实现 |

---

## 继承关系

```
LubanTablesLoader (static class)
```

---

## 关键字段

无（静态工具类）

---

## 公开 API

### Load

```csharp
public static Dictionary<Type, ITable> Load(
    string tablesClassName,
    string namespace_,
    object loader)
```

**功能**：反射构造 `*Tables` 实例，提取所有 `ITable` 到字典。

**参数**：
- `tablesClassName` - `*Tables` 类的短名称（如 `"TableTables"`、`"ConfigTables"`）
- `namespace_` - 命名空间（单值字符串），在此命名空间下查找类型
- `loader` - 传递给 `*Tables` 构造函数的 loader 参数（透传，通常是 `LubanDataReceiver`）

**返回**：类型到 `ITable` 的映射字典；失败时返回 `null`

**失败情况**：
- `tablesClassName` 在指定 `namespace_` 下无法找到（且全局搜索也失败）
- 反射构造 `*Tables` 异常

### ResolveType（私有）

```csharp
private static Type ResolveType(string typeName, string namespace_)
```

**功能**：在指定命名空间下搜索类型，未命中时回退全名搜索。

**参数**：
- `typeName` - 类型短名称（如 `"TableTables"`）
- `namespace_` - 命名空间（单值字符串）

**返回**：找到的类型，未找到返回 `null`

---

## 算法说明

### Load 流程

1. 通过 `ResolveType()` 在 `namespace_` 下搜索 `tablesClassName` 对应的 `Type`
2. 若未找到，记录错误日志，返回 `null`
3. 通过 `Activator.CreateInstance(tablesType, loader)` 反射构造实例
4. 若构造异常，记录错误日志，返回 `null`
5. 检查实例是否实现 `ILubanTables`：
   - **是**：调用 `GetAllTables()` 获取 `IReadOnlyList<ITable>`，遍历放入字典
   - **否**：使用反射遍历属性，找出所有 `ITable` 实现放入字典（Log.Warning 降级提示）
6. 返回结果字典

### ResolveType 流程

1. 若 `namespace_` 非空，调用 `Util.Assembly.GetType(namespace_ + "." + typeName)` 查找
2. 命中时立即返回
3. 未命中，调用 `Util.Assembly.GetType(typeName)` 尝试全局搜索
4. 返回结果（可能为 `null`）

---

## 使用示例

### Table 模块中加载

```csharp
// 在 TableManager.Methods.cs 中（示意）
// namespace_ 由 IConfigManager 在 Load 阶段现取
string namespace_ = FrameworkManagersGroup.GetManager<IConfigManager>()?.Namespace;

Dictionary<Type, ITable> tables = LubanTablesLoader.Load(
    tablesClassName: "TableTables",
    namespace_: namespace_,
    loader: loader
);
```

### Config 模块中加载

```csharp
// 在 ConfigManager.Methods.cs 中（示意）
// ConfigManager 通过 m_Runtime?.Namespace 按需读取，不缓存独立字段
Dictionary<Type, ITable> configs = LubanTablesLoader.Load(
    tablesClassName: "ConfigTables",
    namespace_: m_Runtime?.Namespace ?? string.Empty,
    loader: loader
);
```

### Network 模块中加载

```csharp
// 在 NetworkManager.Methods.cs 中（示意）
// namespace_ 由 IConfigManager 在 Load 阶段现取
string namespace_ = FrameworkManagersGroup.GetManager<IConfigManager>()?.Namespace;

Dictionary<Type, ITable> hostKeyTables = LubanTablesLoader.Load(
    c_HostKeyTablesClassName, namespace_, loader);

Dictionary<Type, ITable> networkTables = LubanTablesLoader.Load(
    c_NetworkTablesClassName, namespace_, loader);
```

---

## 常见误区

**问题**：调用 `Load()` 时忘记先填充 `m_DataCache`

**解决**：必须先加载 AB 包获得 JSON 文件，然后通过 `LubanDataReceiver.OnParseDataAsset()` 填充缓存，再调用 `Load()`

**问题**：`*Tables` 类所在命名空间与 `namespace_` 参数不一致

**解决**：检查 `IConfigManager.Namespace` 属性值是否与 `TableTables` / `ConfigTables` 所在的命名空间一致，该属性是全框架命名空间的唯一权威

---

## 关联文档

- [LubanDataReceiver.md](LubanDataReceiver.md)（数据接收器，预填充缓存）
- [ITable.md](ITable.md)（表格数据标记接口）
- [ILubanTables.md](ILubanTables.md)（Luban 生成的 Tables 容器接口）
- [IDataTableSettings.md](IDataTableSettings.md)（数据表设置接口）
- [TableManager.md](../../Modules/Table/TableManager.md)（运行时消费者）
- [ConfigManager.md](../../Modules/Config/ConfigManager.md)（运行时消费者）
