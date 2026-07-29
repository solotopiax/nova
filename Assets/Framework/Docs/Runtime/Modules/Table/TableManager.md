# TableManager

`TableManager` 是 Table 模块的运行时加载与查询适配器。它不维护表清单、格式枚举或解码分支，这些信息由 Luban 生成的 `ILubanTableBinding` 提供。

## 加载流程

`TableRuntimeSettings.LoadDescriptions` 可以配置任意数量的生成结果：

1. 使用加载描述内部解析出的类型创建 `ILubanTableBinding`。
2. 读取 Binding 的 `DataFiles`，逐项查找显式配置的 `Asset 地址`。
3. 通过 `IAssetManager` 加载每个 `TextAsset`，并在释放 Handle 前复制字节。
4. 把按 `output_data_file` 查询的字节 Loader 传给 `ILubanTableBinding.Create`。
5. Binding 调用对应 Luban `Tables` 构造函数，使用生成代码匹配的 JSON、Binary、Protobuf 或 MsgPack Codec 解码。
6. 调用 `ResolveRef()`，把全部 `ITable` 按运行时类型写入查询字典。

任一 Binding、逻辑文件或 Asset 地址缺失都会明确失败；全部 Tables 构造成功后才原子替换当前查询缓存。

## 直接注册

业务也可以使用 Luban 原生构造方式，然后调用：

```csharp
Nova.Table.RegisterTables(tables);
```

这条路径不需要加载描述，也不经过 Nova 资源加载。

## 对外语义

- `LoadTablesAsync()` / `LoadTablesSync()`：加载全部配置的加载描述。
- `RegisterTables(ILubanTables)`：注册业务自行构造的 Tables。
- `HasTable<T>()` / `GetTable<T>()`：按生成表类型查询。
- `Count`：当前已注册的表类型数，不是数据行数。

## 关联文档

- [TableComponent.md](TableComponent.md)
- [TableSettings.md](Definitions/TableSettings.md)
- [ILubanTableBinding.md](../../Core/Table/ILubanTableBinding.md)
