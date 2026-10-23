# TableManager

`TableManager` 是 Table 模块的运行时加载与查询适配器。它不维护表清单、格式枚举或解码分支，这些信息由 Luban 生成的 `ILubanTableBinding` 提供。

## 加载流程

`TableRuntimeSettings.LoadDescriptions` 可以配置任意数量的生成结果：

1. 使用加载描述内部解析出的类型创建 `ILubanTableBinding`。
2. 读取 Binding 的 `DataFiles`，逐项查找显式配置的 `Asset 地址`。
3. 通过 `IAssetManager` 加载每个 `TextAsset`，并在释放 Handle 前复制字节。
4. 把按 `output_data_file` 查询的字节 Loader 传给 `ILubanTableBinding.Create`。
5. Binding 调用对应 Luban `Tables` 构造函数，使用生成代码匹配的 JSON、Binary、Protobuf 或 MsgPack Codec 解码。
6. 全部 Binding 的 `Create` 都返回非空容器后，先清空旧查询字典，再依次调用各容器的 `ResolveRef()` / `GetAllTables()`，把 `ITable` 按运行时类型写入字典；同类型的后注册实例覆盖前一个。

任一 Binding、逻辑文件或 Asset 地址缺失都会明确失败。资源加载或任一 `Create` 失败发生在清空字典之前，旧查询缓存保留；清空之后的引用解析、取表或逐表注册失败会留下空缓存或部分新表，**没有原子替换或回滚**。这里只描述查询字典，不保证 Binding 自身没有副作用。

`ReplaceTables` 不检查每次 `RegisterTables` 的布尔返回值：某容器不提供表时会记日志并继续；只要最终字典非空，整个加载仍返回 `true`。因此返回 `true` 表示至少存在一张已注册表，不是每个加载描述都成功提供了表。

## 直接注册

业务也可以使用 Luban 原生构造方式，然后调用：

```csharp
bool registered = Nova.Table.RegisterTables(tables);
```

这条路径不需要加载描述，也不经过 Nova 资源加载。参数 `tables` 是业务已经构造的 `ILubanTables`，没有可省略的默认值。注册先调用 `ResolveRef()`，再调用 `GetAllTables()`，最后逐表写入查询字典；它不会先清空已注册表，同类型表会被覆盖。

- `tables == null`，或 `GetAllTables()` 返回 `null` / 空列表：记错误日志并返回 `false`。
- `ResolveRef()` / `GetAllTables()` 抛异常：向调用方传播；引用解析已经产生的副作用不回滚。
- 列表中出现空表：抛 `InvalidOperationException`；该列表中此前已经写入的表保留。
- 返回 `true`：该容器提供的非空列表已逐项注册完成，至少注册了一张表；不是新增表类型数。

## 对外语义

- `LoadTablesAsync()` / `LoadTablesSync()`：加载全部配置的加载描述；没有加载描述时只返回当前字典是否非空。缺少 `IAssetManager` 时记日志并返回 `false`，其他加载/注册异常在 Manager 层传播。`TableComponent.LoadAsync()` / `LoadSync()` 捕获异常并返回 `false`，但不回滚字典。
- `RegisterTables(ILubanTables)`：注册业务自行构造的 Tables。
- `HasTable<T>()` / `GetTable<T>()`：按生成表类型查询。
- `Count`：当前已注册的表类型数，不是数据行数。

## 参数、资源和退出边界

Initialize 取得 IAssetManager，清旧表与旧描述，只浅复制非 null 加载描述引用；null config/列表允许为空，不在此时校验依赖或加载。原描述对象仍可变。Base Priority=14 用于较早 Update、较晚 Shutdown，Update 当前为空；声明没有加载执行体，外部实现须另核。

DataFiles 必须非空且各项非空白、Ordinal 唯一；地址映射要求每条非 null、逻辑键/AssetAddress 非空白、键唯一并覆盖所有必需文件。DataFile 是 Luban output_data_file，不是 AssetPath/地址；运行时不使用 AssetPath 猜地址，也不按扩展名选格式。错误消息使用的 bindingTypeName 只用于诊断。

异步加载按描述顺序处理，每个描述内并行加载 DataFiles；同步加载不合并在途异步。每个成功取得的 TextAsset handle 在 finally Release，先 Buffer.BlockCopy 到独立数组再建 Store；默认值结构及内部构造器不另验 null，getter 返回引用，不深复制或持有原 handle。资源加载/Release 异常仍传播，没有统一取消或线程锁。

Has/Get 按精确 System.Type 查询；null Type 返回 false/null，缺表 null，返回原表引用而非副本。旧表托管引用在 Initialize/Replace/Shutdown 后仍可能被调用方持有，但不代表还在当前注册字典。Shutdown 只清字典、描述与依赖，不 Dispose 生成表，也不取消或等待在途异步加载；调用方须管理加载与退出时序。

## 关联文档

- [TableComponent.md](TableComponent.md)
- [TableSettings.md](Definitions/TableSettings.md)
- [ILubanTableBinding.md](../../Core/Table/ILubanTableBinding.md)
