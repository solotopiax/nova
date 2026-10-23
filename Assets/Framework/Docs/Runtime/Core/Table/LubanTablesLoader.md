# LubanTablesLoader

**命名空间**：`NovaFramework.Runtime`

**文件位置**：`Runtime/Core/Table/LubanTablesLoader.cs`

Luban Tables 反射加载器。通过反射构造 `*Tables` 实例（如 `ConfigTables`），并提取其中的所有 `ITable` 实现到字典中。Config、Network、Sound 等专用模块继续共用此实现；通用 Table 模块使用 `ILubanTableBinding`。

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

这里的 null 主要覆盖类型未找到和构造失败：ResolveType 本身、构造后的 ResolveRef/GetAllTables、空表元素或反射 getter/indexer 异常仍可能向外传播，并非所有失败都转 null。相同具体表类型由后项覆盖；反射 fallback 无表会记录错误但仍返回空字典。命名空间只是查找前缀，未命中仍回退原类型名，不是隔离边界，也不加载 DLL。

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

### LoadBinary 与缓存适配

`LoadBinary(string tablesClassName, string namespace_, Func<string,byte[]> loader)` 拒绝 null loader，再查 `tablesClassName + "Binding"` 且必须实现 ILubanTableBinding；Type 查找仍在 try 之外。Binding 创建、Create、ResolveRef 与收集表位于同一个 try，异常记录 Error 并返回 null。这和 JSON Load 只包住反射构造的异常边界不同，也不证明反射类型在 AOT/裁剪后可用。

专用 Unit 链的 LubanRuntimeData.LoadTables 在缓存缺项时记录 Warning，并分别返回空 byte[] 或新空 JArray 给生成构造器；缺数据可能得到空表，不必然失败。它不预检 cache，也不替生成容器校验完整性。ILubanTables/ITable 的只读接口不强制非空、唯一、排序或深不可变；按 Type 查询的是表类型，不是行类型，Map 的业务 key 与 DataList 的零基行索引也应区分。Get 缺键抛、GetOrDefault 缺键给 default 等契约仍由实际生成表实现兑现。

通用 Binding 的 `TableDataStore` 按 Ordinal 的 output_data_file 名保存 byte[] 原引用，Add 拒绝空白键、null 字节与重复键，不复制。GetBytes/GetBinary 共享原字节，GetText 用默认 UTF-8，缺键抛 KeyNotFoundException；GetJson 解析数组，GetMsgPackJson 也要求根为数组，Codec 错误直接传播。

MsgPack 解码器支持基础整数、浮点、nil/bool、字符串、数组、Map 与 bin（转 Base64 文本），整数按大端字节读取；拒绝尾随、截断、不支持标记与深度>128，不支持 ext/timestamp。长度只限制 int 范围，没有额外元素/内存预算；UTF-8 使用替代策略，非有限浮点可进入 JValue。Map 非字符串键转 ToString，转换碰撞会抛错，不能保留原异型键身份。Reader 以 ReadOnlySpan 读取原数组，解码期间不可并发修改；读取失败可能已推进部分偏移，不回滚。

---

## 算法说明

### Load 流程

1. 通过 `ResolveType()` 在 `namespace_` 下搜索 `tablesClassName` 对应的 `Type`
2. 若未找到，记录错误日志，返回 `null`
3. 通过 `Activator.CreateInstance(tablesType, loader)` 反射构造实例
4. 若构造异常，记录错误日志，返回 `null`
5. 检查实例是否实现 `ILubanTables`：
   - **是**：先调用 `ResolveRef()`，再调用 `GetAllTables()` 获取 `IReadOnlyList<ITable>`，遍历放入字典
   - **否**：使用反射遍历属性，找出所有 `ITable` 实现放入字典（Log.Warning 降级提示）
6. 返回结果字典

### ResolveType 流程

1. 若 `namespace_` 非空，调用 `Util.Assembly.GetType(namespace_ + "." + typeName)` 查找
2. 命中时立即返回
3. 未命中，调用 `Util.Assembly.GetType(typeName)` 尝试全局搜索
4. 返回结果（可能为 `null`）

---

## 使用示例

### 通用 Table 模块

```csharp
// TableManager 读取 ILubanTableBinding.DataFiles，加载原始字节后调用：
ILubanTables tables = binding.Create(loader);
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

**问题**：调用 `Load()` 时 Loader 中缺少生成 Tables 构造函数请求的 `output_data_file`

**解决**：先准备完整原始单表数据，再调用 `Load()`；通用 Table 模块可由 `ILubanTableBinding.DataFiles` 完成预加载

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
