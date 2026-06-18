# PlatformChannelEntry

**类签名**：`[Serializable] public sealed class PlatformChannelEntry`
**命名空间**：`NovaFramework.Runtime`

Platform × Channel 矩阵的一行；SDK 配置与 Kit 配置均按 DevelopMode 分组存储。由 `ConfigMasterSO` 通过 `[SerializeField] List<PlatformChannelEntry>` 持久化。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/PlatformChannelEntry.cs` | `PlatformChannelEntry` | 数据结构定义；内嵌 `DevelopModeCommonEntry`、`DevelopModeSDKEntry`、`DevelopModeKitEntry` |

---

## §5 完整公开 API

```csharp
[Serializable]
public sealed class PlatformChannelEntry
{
    public PlatformType Platform;
    public ChannelType Channel;
    public List<DevelopModeCommonEntry> CommonByMode = new();
    public List<DevelopModeSDKEntry> SDKConfigsByMode = new();
    public List<DevelopModeKitEntry> KitConfigsByMode = new();

    // 无参构造器；自动预置 Debug 与 Publish 两份空条目（CommonByMode / SDKConfigsByMode / KitConfigsByMode）
    public PlatformChannelEntry();

    // 按指定 DevelopMode 获取 CommonConfig；不存在时自动追加，永不返回 null
    public CommonConfig GetCommon(DevelopMode mode);

    // 按指定 DevelopMode 获取 SDK Plugin 配置列表；不存在时自动追加，永不返回 null
    public List<ISDKPluginConfig> GetSDKConfigs(DevelopMode mode);

    // 按指定 DevelopMode 获取 Kit 配置列表；不存在时自动追加，永不返回 null
    public List<IKitConfig> GetKitConfigs(DevelopMode mode);
}

[Serializable]
public sealed class DevelopModeSDKEntry
{
    public DevelopMode Mode = DevelopMode.Debug;

    [SerializeReference]
    public List<ISDKPluginConfig> SDKConfigs = new();
}

[Serializable]
public sealed class DevelopModeKitEntry
{
    public DevelopMode Mode = DevelopMode.Debug;

    [SerializeReference]
    public List<IKitConfig> KitConfigs = new();
}
```

---

## §11 使用示例

```csharp
// ConfigMasterSO 通过 TryGetEntry 按平台渠道查询，再按 DevelopMode 拿 SDK / Kit 配置
if (master.TryGetEntry(PlatformType.Android, ChannelType.Google, out PlatformChannelEntry entry))
{
    // SDK 配置列表
    List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(DevelopMode.Debug);
    // Kit 配置列表
    List<IKitConfig> kitConfigs = entry.GetKitConfigs(DevelopMode.Debug);
}

// 勾选 Kit 时遍历 allEntries × allModes 给每格补实例（对称 SDK 写法）
IReadOnlyList<PlatformChannelEntry> all = master.GetAllEntries();
DevelopMode[] allModes = (DevelopMode[])System.Enum.GetValues(typeof(DevelopMode));
for (int i = 0; i < all.Count; i++)
{
    for (int m = 0; m < allModes.Length; m++)
    {
        EditorUtil.Config.KitConfigScanner.EnsureInstance(all[i], allModes[m], kitType);
    }
}
```

---

## §13 关联文档

- [ConfigMasterSO.md](ConfigMasterSO.md)（持有 `List<PlatformChannelEntry>` 的主 SO）
- [DevelopMode.md](../../Core/Definitions/DevelopMode.md)
- [PlatformType.md](../../Core/Definitions/PlatformType.md)
- [ChannelType.md](../../Core/Definitions/ChannelType.md)
- [EditorUtil.Config.StructureGuard.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md)
- [EditorUtil.Config.SDKPluginScanner.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md)
