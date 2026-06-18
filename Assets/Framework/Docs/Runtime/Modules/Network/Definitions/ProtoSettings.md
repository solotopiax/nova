# ProtoSettings

**类签名**：`[Serializable] public class ProtoSettings`
**命名空间**：`NovaFramework.Runtime`

Protobuf 编辑器设置，存储 .proto 文件根目录与各文件的 C# 导出路径。仅用于 Editor 工具链，运行时打包后相关字段不存在。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Definitions/ProtoSettings.cs` | `ProtoSettings` | Protobuf 编辑器设置主类 |
| `Definitions/ProtoSettings.cs` | `ProtoUnitSetting` | 单个 .proto 文件的导出设置（同文件定义） |

---

## §5 完整公开 API

```csharp
// ProtoSettings（纯数据，无方法）
[Serializable]
public class ProtoSettings
{
#if UNITY_EDITOR
    public string ProtoSourceDirPath;                             // .proto 文件根目录路径
#endif
    public List<ProtoUnitSetting> ProtoUnits = new List<ProtoUnitSetting>();
}

// ProtoUnitSetting（纯数据，无方法）
[Serializable]
public class ProtoUnitSetting
{
#if UNITY_EDITOR
    public string SourcePath;                                     // 相对 Proto 根目录的 .proto 文件路径
    public string CSharpExportPath;                               // protoc 生成 C# 代码的输出目录
#endif
}
```

---

## §11 使用示例

```csharp
// NetworkComponentInspector 读取 m_ProtoSettings 驱动 protoc 编译
#if UNITY_EDITOR
foreach (var unit in m_ProtoSettings.ProtoUnits)
{
    // unit.SourcePath        — 相对根目录的 .proto 文件路径
    // unit.CSharpExportPath  — protoc C# 输出目录
}
#endif
```

---

## §13 关联文档

- [../NetworkComponent.md](../NetworkComponent.md)
- [../../../../Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md](../../../../Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md)
