# ConfigRuntimeSO

`ConfigRuntimeSO` 是 `NovaFramework.Runtime` 下的运行时配置快照，由 Editor 导出链覆盖写入。

```csharp
public sealed class ConfigRuntimeSO : ScriptableObject
{
    public PlatformType Platform;
    public ChannelType Channel;
    public DevelopMode DevelopMode;
    public AppConfigs AppConfigs;
    public string Namespace;
    public HybridConfigs HybridConfigs;
    public CustomConfigs CustomConfigs;

    [SerializeReference] public List<ISDKPluginConfig> EnabledSDKConfigs = new();
    [SerializeReference] public List<IKitConfig> EnabledKitConfigs = new();
}
```

## 分层边界

- `HybridConfigs` 只保存运行时需要的入口 Procedure 名和 DLL Asset 地址。
- `link.xml`、DLL 源/目标路径归 `HybridEditorConfigs`，不进入本 SO。
- YooAsset 工程资产路径归 `YooAssetEditorConfigs`，不进入本 SO。
- CDN 部署凭据与路径归 `CDNEditorConfigs`，不进入本 SO。
- `CustomConfigs` 当前为空类，作为业务自定义 Runtime 配置扩展点。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)、[EditorUtil.Config.Exporter.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md)。
