# HybridConfigs

Runtime HybridCLR 配置，只包含运行时加载需要的数据：

```csharp
[Serializable]
public sealed class HybridConfigs
{
    public string GameEntranceProcedureName;
    public List<DllAssetEntry> AotMetadataDlls = new();
    public List<DllAssetEntry> GameDlls = new();
}
```

`link.xml` 和 DLL 构建源/目标路径属于 Editor，不在此类型中。
