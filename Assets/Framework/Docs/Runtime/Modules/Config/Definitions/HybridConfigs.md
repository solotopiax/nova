# HybridConfigs

Runtime HybridCLR 配置，只包含运行时加载需要的数据：

```csharp
[Serializable]
public sealed class HybridConfigs
{
    public string GameEntranceProcedureName;
    public List<DllAssetEntry> AotMetadataDlls = new();
    public List<DllAssetEntry> StartupGameDlls = new();
}
```

`StartupGameDlls` 由 `ProcedureLoadDll` 在启动阶段自动加载。`RunningGameDlls`、`link.xml` 和 DLL 构建源/目标路径都属于 Editor，不进入 Runtime 配置；运行时按需 DLL 由业务持有地址并调用 `Util.HybridCLR.LoadGameAssemblyAsync`。
