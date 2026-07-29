# TableManagerConfig

`TableManagerConfig` 是 `TableComponent.Start()` 下发给 `ITableManager.Initialize` 的运行时配置：

```csharp
public sealed class TableManagerConfig
{
    public IReadOnlyList<TableLoadDescriptionSetting> LoadDescriptions;
}
```

每条加载描述内部保存生成 Binding 类型，以及 `output_data_file -> Asset 地址` 的完整映射。表清单与 Codec 解码方式仍由对应 `ILubanTableBinding` 提供，Manager 不根据文件扩展名猜测格式。

## 关联文档

- [TableSettings.md](TableSettings.md)
- [TableManager.md](../TableManager.md)
