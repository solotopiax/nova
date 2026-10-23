# PlatformChannelEntry

**类签名**：`[Serializable] public sealed class PlatformChannelEntry`
**命名空间**：`NovaFramework.Editor`

Platform × Channel 矩阵的一行；App、Privacy、SDK 与 Kit 配置均按 DevelopMode 分组存储。由 `ConfigMasterSO` 的序列化列表持久化。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/Config/Definitions/PlatformChannelEntry.cs` | `PlatformChannelEntry` | 同文件另定义 `DevelopModeAppConfigsEntry`、`DevelopModePrivacyConfigsEntry`、`DevelopModeSDKEntry`、`DevelopModeKitEntry`；它们不是 `PlatformChannelEntry` 的嵌套类型 |

---

## §5 完整公开 API

```csharp
[Serializable]
public sealed class PlatformChannelEntry
{
    public PlatformType Platform;
    public ChannelType Channel;
    public List<DevelopModeAppConfigsEntry> AppConfigsByMode = new();
    public List<DevelopModePrivacyConfigsEntry> PrivacyConfigsByMode = new();
    public List<DevelopModeSDKEntry> SDKConfigsByMode = new();
    public List<DevelopModeKitEntry> KitConfigsByMode = new();

    // 无参构造器；四类列表分别预置 Debug 与 Release 两份配置条目
    public PlatformChannelEntry();

    // 按指定 DevelopMode 获取 AppConfigs；不存在时追加默认条目
    public AppConfigs GetAppConfigs(DevelopMode mode);

    // 按指定 DevelopMode 获取 PrivacyConfigs；外层列表为 null 时先新建，缺模式时追加默认条目
    public PrivacyConfigs GetPrivacyConfigs(DevelopMode mode);

    // 按指定 DevelopMode 获取 SDK Plugin 配置列表；不存在时追加默认条目
    public List<ISDKPluginConfig> GetSDKConfigs(DevelopMode mode);

    // 按指定 DevelopMode 获取 Kit 配置列表；不存在时追加默认条目
    public List<IKitConfig> GetKitConfigs(DevelopMode mode);
}
```

`DevelopModeAppConfigsEntry.Config` 与 `DevelopModePrivacyConfigsEntry.Config` 分别保存 `AppConfigs`、`PrivacyConfigs`；SDK/Kit 包装项分别以 `[SerializeReference]` 列表保存多态配置。

这些 Getter 返回可变配置或列表，不是只读查询，也不自动保存资产。已有条目中的 `Config` / 配置列表若为 null，不会在命中时自动修复，不能把返回值一概视为非 null。模式参数也不在本类校验；实际坐标合法性与保存校验见 [ConfigMasterSO](../ConfigMasterSO.md)。

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

- [ConfigMasterSO.md](../ConfigMasterSO.md)（持有 `List<PlatformChannelEntry>` 的主 SO）
- [DevelopMode.md](../../../Runtime/Core/Definitions/DevelopMode.md)
- [PlatformType.md](../../../Runtime/Core/Definitions/PlatformType.md)
- [ChannelType.md](../../../Runtime/Core/Definitions/ChannelType.md)
- [EditorUtil.Config.StructureGuard.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.md)
- [EditorUtil.Config.SDKPluginScanner.md](../../EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.md)
