# ConfigRuntimeSO

`ConfigRuntimeSO` 是 `NovaFramework.Runtime` 下的运行时配置快照，由 Editor 导出链覆盖写入。

`Platform` 与 `Channel` 必须是已定义的非 `None` 值；Exporter 不生成 None 坐标，ConfigManager 也会拒绝加载遗留或手工写入的 None 快照。

```csharp
public sealed class ConfigRuntimeSO : ScriptableObject
{
    public PlatformType Platform;
    public ChannelType Channel;
    public DevelopMode DevelopMode;
    public AppConfigs AppConfigs;
    public PrivacyConfigs PrivacyConfigs;
    public string Namespace;
    public HybridConfigs HybridConfigs;
    public CustomConfigData Custom;

    [SerializeReference] public List<ISDKPluginConfig> EnabledSDKConfigs = new();
    [SerializeReference] public List<IKitConfig> EnabledKitConfigs = new();
}
```

## 分层边界

- `HybridConfigs` 只保存运行时需要的入口 Procedure 名和 DLL Asset 地址。
- `link.xml`、DLL 源/目标路径归 `HybridEditorConfigs`，不进入本 SO。
- YooAsset 工程资产路径归 `YooAssetEditorConfigs`，不进入本 SO。
- CDN 部署凭据与路径归 `CDNEditorConfigs`，不进入本 SO。
- `Custom` 保存 ConfigMaster 导出的本地 JSONPath/string 默认值。运行时不会修改本 SO；ConfigManager 另行维护不受本地路径限制的完整远端 JSON 快照。
- `PrivacyConfigs` 仅保存 `Util.Encrypt.AES` 默认 Key/IV；不替代、不迁移 `AppConfigs.AppAesKey / AppAesIV`。
- SDK / Kit 配置使用 `[SerializeReference]` 保存；承载这些配置 DTO 的程序集必须在所有支持平台保持可解析。原生能力不支持 WebGL 时应在实现层禁用，不得连同配置 DTO 一起排除，否则加载资产时会产生 Missing types 警告。

关联文档：[IConfigManager.md](Interfaces/IConfigManager.md)、[ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)、[EditorUtil.Config.Exporter.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md)。
