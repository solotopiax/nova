# DllAssetEntry

**类签名**：`[Serializable] public struct DllAssetEntry`
**命名空间**：`NovaFramework.Runtime`

DLL 运行期寻址条目（单字段），供 `ConfigRuntimeSO` 持有。仅承载运行期 Asset 地址，不含源/目标路径语义；编辑期三字段视图由 `DllMasterAssetEntry`（在 `ConfigMasterSO`）负责。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Runtime/Modules/Config/Definitions/DllAssetEntry.cs` | `DllAssetEntry` | 结构体定义：一个 `[SerializeField]` 私有字段 + 只读属性 |

---

## §5 完整公开 API

```csharp
// 只读属性（对应 [SerializeField] private string m_AssetLocation 字段）
public string AssetLocation  // 原始运行期 Asset 地址，由 ProcedureLoadDll 传给 HybridCLR 加载工具

// 构造器：由 EditorUtil.Config.Exporter 从 DllMasterAssetEntry 提取 AssetLocation 构造运行期条目
public DllAssetEntry(string assetLocation)
```

构造器原样保存字符串，不 Trim、不校验存在性，也不加载 DLL。Asset 地址、程序集名称和实际加载的 Assembly 是不同对象，不能仅凭这个条目证明三者等价或加载成功；加载句柄与释放由实际消费者负责。

---

## §11 使用示例

```csharp
// 由 ConfigRuntimeSO.HybridConfigs 的 AotMetadataDlls / StartupGameDlls 持有
// 编辑期配置入口见 DllMasterAssetEntry + ConfigMasterSO

// 遍历 AOT metadata 条目（由 ProcedureLoadDll 内部调用）
IReadOnlyList<DllAssetEntry> aotList = configManager.HybridConfigs.AotMetadataDlls;
foreach (DllAssetEntry entry in aotList)
{
    await Util.HybridCLR.LoadAotMetadataAsync(entry.AssetLocation);
}

// 遍历业务 DLL 条目
IReadOnlyList<DllAssetEntry> dllList = configManager.HybridConfigs.StartupGameDlls;
foreach (DllAssetEntry entry in dllList)
{
    await Util.HybridCLR.LoadGameAssemblyAsync(entry.AssetLocation);
}
```

---

## §13 关联文档

- [ProcedureLoadDll.md](../../Procedure/Procedures/ProcedureLoadDll.md)
- [DllMasterAssetEntry.md](../../../../Editor/Config/Definitions/DllMasterAssetEntry.md)（编辑期三字段视图，供 ConfigMasterSO 使用）
- [ConfigRuntimeSO.md](../ConfigRuntimeSO.md)（AotMetadataDlls / StartupGameDlls 运行时来源）
- [Util.HybridCLR.md](../../../Utils/Util.HybridCLR.md)
- [EditorUtil.HybridCLR.md](../../../../Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md)
