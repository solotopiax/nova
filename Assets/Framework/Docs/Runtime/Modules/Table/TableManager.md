# TableManager

`TableManager` 是 Table 模块的运行时加载与查询适配器。它不维护表清单、格式枚举或解码分支，这些信息由 Luban 生成代码对应的 `ILubanTableBinding` 提供。

## 加载流程

`TableRuntimeSettings.Bindings` 可以配置任意数量的 Binding：

1. 反射创建 `BindingTypeName` 指定的 `ILubanTableBinding`。
2. 读取 Binding 的 `DataFiles`，按 `DataAssetLocationPrefix/output_data_file` 加载每个 `TextAsset`。
3. 在释放资源 Handle 前复制 `TextAsset.bytes`。
4. 把原始字节 Loader 传给 `ILubanTableBinding.Create`。
5. 由 Binding 调用对应 Luban `Tables` 构造函数并完成 Codec 解码。
6. 调用 `ResolveRef()`，把所有 `ITable` 按运行时类型写入查询字典。

异步入口会并行加载同一 Binding 内的数据文件；全部 Binding 构造成功后才替换当前查询缓存。

## 直接注册

业务也可以完全使用 Luban 原生构造方式，然后调用：

```csharp
Nova.Table.RegisterTables(tables);
```

这条路径不要求配置 Runtime Binding，也不经过 Nova 资源加载。

## 对外语义

- `LoadTablesAsync()` / `LoadTablesSync()`：加载全部已配置 Binding。
- `RegisterTables(ILubanTables)`：注册业务自行构造的 Tables 容器。
- `HasTable<T>()` / `GetTable<T>()`：按生成表类型查询。
- `Count`：当前已注册的表类型数，不是数据行数。

## 关联文档

- [TableComponent.md](TableComponent.md)
- [TableSettings.md](Definitions/TableSettings.md)
- [TableManagerConfig.md](Definitions/TableManagerConfig.md)
- [ILubanTableBinding.md](../../Core/Table/ILubanTableBinding.md)
