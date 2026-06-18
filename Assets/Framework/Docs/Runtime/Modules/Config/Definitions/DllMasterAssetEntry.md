# DllMasterAssetEntry

**类签名**：`[Serializable] public struct DllMasterAssetEntry`
**命名空间**：`NovaFramework.Runtime`

DLL 主配置条目（编辑期三字段视图），供 `ConfigMasterSO` 持有。持有源位置、目标位置与运行期 Asset 地址；源/目标均为项目根相对路径，所见即所得，不追加扩展名。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `Runtime/Modules/Config/Definitions/DllMasterAssetEntry.cs` | `DllMasterAssetEntry` | 结构体定义：三个 `[SerializeField]` 私有字段 + 对应只读属性 |

---

## §5 完整公开 API

```csharp
// 源位置：项目根相对路径，EditorUtil.HybridCLR 从该路径读取文件
public string SourceLocation

// 目标位置：项目根相对路径，文件拷贝到此处，所见即所得，不追加 .bytes
public string TargetLocation

// 运行期 Asset 地址，传入 AssetComponent.LoadAssetAsync 的 location 参数（与 HybridCLR assemblyName 等价）
public string AssetLocation
```

---

## §11 使用示例

```csharp
// 由 Inspector 序列化，通过 ConfigMasterSO 的 AotMetadataDlls / GameDlls 访问
// 在 ConfigWindow → HybridCLR 配置面板编辑条目

ConfigMasterSO master = EditorUtil.Asset.Operator.Find<ConfigMasterSO>();

// 遍历 AOT metadata 条目（编辑期，EditorUtil.HybridCLR 拷贝逻辑使用）
foreach (DllMasterAssetEntry entry in master.AotMetadataDlls)
{
    // entry.SourceLocation  → 源文件路径（项目根相对）
    // entry.TargetLocation  → 拷贝目标路径（项目根相对，所见即所得）
    // entry.AssetLocation   → 运行期 Asset 地址，导出到 ConfigRuntimeSO 后供 ProcedureLoadDll 使用
}
```

---

## §13 关联文档

- [DllAssetEntry.md](DllAssetEntry.md)（运行期单字段视图，供 ConfigRuntimeSO 使用）
- [ConfigMasterSO.md](../ConfigMasterSO.md)（AotMetadataDlls / GameDlls 编辑入口）
- [ConfigRuntimeSO.md](../ConfigRuntimeSO.md)（导出产物，AotMetadataDlls / GameDlls 为 DllAssetEntry 列表）
- [EditorUtil.HybridCLR.md](../../../../Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md)（拷贝逻辑消费方）
