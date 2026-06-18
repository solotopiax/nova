# EditorUtil.HybridCLR

**类签名**：`public static partial class EditorUtil.HybridCLR`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.HybridCLR`

HybridCLR 原子操作合集：提供 link.xml 校验/补全、对齐 HybridCLR/Generate 子菜单的细粒度入口（`GenerateAll()` 一键及 5 个单项）、仅编译热更 DLL 的独立入口、AOT 元数据拷贝、业务 DLL 拷贝等独立方法。框架不再提供全流程封装，流水线编排统一交给 `EditorUtil.Pipify` 按需组装。DLL 列表配置通过 `EditorUtil.Asset.Operator.Find<ConfigMasterSO>()` 读取。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.HybridCLR.cs` | `partial EditorUtil.HybridCLR` | 公有接口：`ValidateLinkXml()` / `CopyAotDlls()` / `CopyGameDlls()` 三个主流程操作；仅编译热更 DLL 的独立入口 `CompileDllActiveBuildTarget()`；对齐 HybridCLR/Generate 子菜单的细粒度入口：`GenerateAll()` / `GenerateLinkXml()` / `GenerateMethodBridgeAndReversePInvokeWrapper()` / `GenerateAotGenericReference()` / `GenerateIl2CppDef()` / `GenerateAotDlls()` |
| `EditorUtil.HybridCLR.Visitors.cs` | `partial EditorUtil.HybridCLR` | 常量：`c_LinkXmlPath` |
| `EditorUtil.HybridCLR.Methods.cs` | `partial EditorUtil.HybridCLR` | 私有方法：`ValidateAndPatchLinkXml`、`CopyDllEntries`、`StripDllSuffix` |

---

## §3 继承关系

```
NovaFramework.Editor.EditorUtil (public static partial class)
  └── HybridCLR (public static partial class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_LinkXmlPath` | `const string` | `"Assets/link.xml"` | link.xml 在 AssetDatabase 中的固定路径；Step 1 中通过 `SettingsUtil.ProjectDir` 拼接为绝对路径 |

---

## §5 完整公开 API

```csharp
/// 校验 Assets/link.xml，缺失则补全 ConfigMasterSO.AotMetadataDlls 每项的 preserve 记录。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException。
public static void ValidateLinkXml()

/// 拷贝 AOT 元数据 DLL 到 ConfigMasterSO 中各条目配置的目标位置，完成后调用 AssetDatabase.Refresh()。
/// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取（项目根相对路径，所见即所得，不追加 .bytes）。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException；源文件不存在或配置缺失时抛 FileNotFoundException。
public static void CopyAotDlls()

/// 拷贝业务层热更 DLL 到 ConfigMasterSO 中各条目配置的目标位置，完成后调用 AssetDatabase.Refresh()。
/// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取（项目根相对路径，所见即所得，不追加 .bytes）。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException；源文件不存在或配置缺失时抛 FileNotFoundException。
public static void CopyGameDlls()

// —— 以下 6 个接口对齐 HybridCLR/Generate 子菜单，为 Pipify 细粒度 Step 暴露 ——

/// HybridCLR/Generate/All：按 HybridCLR 预设顺序执行编译热更 DLL + 全部 Generate 产物（桥接 / AOT 泛型引用 / Il2CppDef / AOT 裁剪 DLL / LinkXml）。
/// 对应 HybridCLR 菜单中的一键入口，等价于依次手动点击 Generate 子菜单的全部项。
/// 内部转发到 HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll()。
public static void GenerateAll()

/// HybridCLR/Generate/LinkXml：编译 ActiveBuildTarget 热更 DLL 并基于热更代码引用生成 link.xml。
/// 内部转发到 HybridCLR.Editor.Commands.LinkGeneratorCommand.GenerateLinkXml()。
public static void GenerateLinkXml()

/// HybridCLR/Generate/MethodBridgeAndReversePInvokeWrapper：基于 ActiveBuildTarget 生成方法桥接与反向 PInvoke 包装。
/// 需要先执行 Generate/AOTDlls（或 Generate/All）产出 AOT 裁剪 DLL。
/// 内部转发到 HybridCLR.Editor.Commands.MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper()。
public static void GenerateMethodBridgeAndReversePInvokeWrapper()

/// HybridCLR/Generate/AOTGenericReference：编译 ActiveBuildTarget 热更 DLL 并生成 AOT 泛型引用 cs 文件。
/// 内部转发到 HybridCLR.Editor.Commands.AOTReferenceGeneratorCommand.CompileAndGenerateAOTGenericReference()。
public static void GenerateAotGenericReference()

/// HybridCLR/Generate/Il2CppDef：生成 Il2Cpp 宏定义头文件与 AssemblyManifest.cpp。
/// 内部转发到 HybridCLR.Editor.Commands.Il2CppDefGeneratorCommand.GenerateIl2CppDef()。
public static void GenerateIl2CppDef()

/// HybridCLR/Generate/AOTDlls：在 ActiveBuildTarget 下执行 AOT DLL 裁剪，产出 AssembliesPostIl2CppStrip 目录。
/// 内部转发到 HybridCLR.Editor.Commands.StripAOTDllCommand.GenerateStripedAOTDlls()。
public static void GenerateAotDlls()

/// HybridCLR/CompileDll/ActiveBuildTarget：针对当前 activeBuildTarget 编译热更业务 DLL，产出到 HotUpdateDllsOutputDir。
/// 仅编译不做 AOT 裁剪、不生成桥接等产物，适合只需要更新热更 DLL 的场景。
/// 内部转发到 HybridCLR.Editor.Commands.CompileDllCommand.CompileDllActiveBuildTarget()。
public static void CompileDllActiveBuildTarget()
```

---

## §9 关键算法

### ConfigMasterSO 自治查找

`ValidateLinkXml` / `CopyAotDlls` / `CopyGameDlls` 各自内部调用 `Asset.Operator.Find<ConfigMasterSO>()` 查找主配置，未找到时抛 `InvalidOperationException`。`AssetDatabase.Refresh()` 在 `CopyAotDlls` 与 `CopyGameDlls` 各自内部调用，框架不再做跨步骤编排。

### link.xml 校验与补全（ValidateAndPatchLinkXml）

1. 将 `c_LinkXmlPath` 与 `SettingsUtil.ProjectDir` 拼接为绝对路径。
2. 若文件**不存在**，自动创建空骨架（`<?xml?>` + 根节点 `<linker>`）并记录日志；若存在则用 `XmlDocument` 加载，校验根节点为 `<linker>`，否则抛 `InvalidOperationException`。
3. 收集所有已存在 `<assembly fullname="...">` 的 fullname 到 `HashSet<string>`。
4. 遍历 `aotEntries`（`IReadOnlyList<DllMasterAssetEntry>`），对每个 `entry.AssetLocation` 调用 `StripDllSuffix` 剥离 `.dll` 后缀得到逻辑名（link.xml 规范不允许带扩展名）；若不在 Set 中则追加 `<assembly fullname="{logicalName}" preserve="all"/>` 并记录 `patched = true`。
5. `patched == true` 或 `justCreated == true` 时用 `XmlWriter`（Indent=true、IndentChars="  "）回写文件。

### DLL 拷贝（CopyDllEntries）

`CopyAotDlls` / `CopyGameDlls` 共用同一私有方法，签名：`CopyDllEntries(IReadOnlyList<DllMasterAssetEntry> entries, string tag)`。

逻辑：
1. **预检**：遍历所有条目，若 `SourceLocation` 或 `TargetLocation` 为空字符串，标记为配置缺失；若源文件不存在（`{projectRoot}/{entry.SourceLocation}`），标记为文件缺失；有任何缺失则整批抛 `FileNotFoundException`，不执行部分拷贝。
2. **拷贝**：确认全部存在后，对每条目创建目标父目录，将源文件拷贝到 `{projectRoot}/{entry.TargetLocation}`（`overwrite: true`）。目标路径所见即所得，**不追加** `.bytes` 或任何后缀。

源目录不再来自 `SettingsUtil`（旧设计），改为由每条 `DllMasterAssetEntry.SourceLocation` 自描述（项目根相对路径）。

### StripDllSuffix

```csharp
private static string StripDllSuffix(string assetLocation)
```

剥离 `assetLocation` 末尾的 `.dll` 后缀（大小写不敏感）。用于 Step 1 生成 link.xml 的 `fullname` 属性——Unity link.xml 规范要求 assembly 逻辑名不带扩展名，而面板填写的 `AssetLocation` 可能带 `.dll`（与磁盘文件名一致）。

---

## §10 常见误区

**误区 1：跳过编译步骤直接执行 `CopyAotDlls` / `CopyGameDlls`**

`CopyDllEntries` 的预检步骤在源文件缺失时整批失败。`CopyAotDlls` / `CopyGameDlls` 依赖前置编译 + AOT 裁剪步骤（如 Pipify 中 `hybridclr.compile_dll_active_build_target` + `hybridclr.generate_aot_dlls`）产出的 DLL 文件，次序错误会抛 `FileNotFoundException`。依赖次序由调用方（Pipify Batch 等）自行保证，本类不做跨方法编排。

**误区 2：以为目标路径会自动追加 `.bytes`**

旧版行为：目标路径 = `{projectRoot}/{entry.AssetLocation}.bytes`（隐式追加后缀）。
当前版本：目标路径 = `{projectRoot}/{entry.TargetLocation}`（所见即所得）。若需要 `.bytes` 文件，在 ConfigWindow 的"目标位置"字段直接填写 `XXX/YYY.dll.bytes`。

**误区 3：以为 DLL 列表配置来自 ProcedureComponent**

HybridCLR 的 DLL 条目来源已迁移到 `ConfigMasterSO`（`AotMetadataDlls` / `GameDlls`，类型 `List<DllMasterAssetEntry>`），由各需要读取配置的方法内部各自调用 `Asset.Operator.Find<ConfigMasterSO>()` 读取，不再依赖 `ProcedureComponent`。

**误区 4：混淆 `DllMasterAssetEntry`（Master 三字段）与 `DllAssetEntry`（Runtime 单字段）**

`ConfigMasterSO.AotMetadataDlls / GameDlls` 是 `List<DllMasterAssetEntry>`（编辑期视图，含 SourceLocation / TargetLocation / AssetLocation）。导出到 `ConfigRuntimeSO` 后只保留 `DllAssetEntry`（单字段 AssetLocation）。`CopyDllEntries` 接收 Master 视图；运行期 `ProcedureLoadDll` 接收 Runtime 视图。

---

## §11 使用示例

每个方法内部自行完成 ConfigMasterSO 查找与空值检查，独立可调用。流水线编排由 Pipify Batch 负责，本类不再提供整体封装。

```csharp
// 仅校验/补全 link.xml
EditorUtil.HybridCLR.ValidateLinkXml();

// 编译热更业务 DLL（仅编译，不做 AOT 裁剪/桥接等）
EditorUtil.HybridCLR.CompileDllActiveBuildTarget();

// 一键 Generate All（等价于所有 Generate 子菜单按序执行）
EditorUtil.HybridCLR.GenerateAll();

// 细粒度 Generate（可按需单独调用；次序须由调用方保证）
EditorUtil.HybridCLR.GenerateAotDlls();                            // 先裁剪 AOT DLL
EditorUtil.HybridCLR.GenerateMethodBridgeAndReversePInvokeWrapper(); // 再生成桥接
EditorUtil.HybridCLR.GenerateAotGenericReference();
EditorUtil.HybridCLR.GenerateIl2CppDef();
EditorUtil.HybridCLR.GenerateLinkXml();

// 仅拷贝 AOT 元数据 DLL（内含 AssetDatabase.Refresh）
EditorUtil.HybridCLR.CopyAotDlls();

// 仅拷贝业务 DLL（内含 AssetDatabase.Refresh）
EditorUtil.HybridCLR.CopyGameDlls();
```

注意：`CopyAotDlls` / `CopyGameDlls` 依赖前置编译 + AOT 裁剪步骤（`CompileDllActiveBuildTarget` + `GenerateAotDlls` 或 `GenerateAll`）产出的 DLL 文件，在 Pipify Batch 中按此顺序安排，次序错误预检阶段将抛 `FileNotFoundException`。

---

## §12 注意事项

- **前提条件**：工程中须存在 `ConfigMasterSO`（通过 `Nova/Config Window` 创建）；link.xml 不存在时 `ValidateLinkXml` 会自动创建空骨架。
- **路径填写**：`SourceLocation` / `TargetLocation` 均为项目根相对路径（如 `HybridCLRData/AssembliesPostIl2CppStrip/StandaloneOSX/mscorlib.dll`）；目标路径所见即所得，不会自动追加 `.bytes`，如需 YooAsset 原始字节格式需手动在目标位置填写 `.dll.bytes` 后缀。
- **幂等性**：`ValidateLinkXml` 的补全是幂等的（已存在条目不重复添加）；`CopyAotDlls` / `CopyGameDlls` 使用 `overwrite: true`，重复执行安全。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [DllAssetEntry.md](../../../Runtime/Modules/Config/Definitions/DllAssetEntry.md) — DLL 资产寻址条目（AssetLocation 单字段）
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md) — AotMetadataDlls / GameDlls 配置来源
- [EditorUtil.Asset.Operator.md](../EditorUtil.Asset/EditorUtil.Asset.Operator.md) — ConfigMasterSO 查找入口（`Find<ConfigMasterSO>()`）
- [Editor.md](../../Editor.md) — Editor 层级总览
