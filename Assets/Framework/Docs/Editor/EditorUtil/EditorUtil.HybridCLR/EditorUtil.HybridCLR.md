# EditorUtil.HybridCLR

**类签名**：`public static partial class EditorUtil.HybridCLR`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.HybridCLR`

HybridCLR 原子操作合集：提供 link.xml 校验/补全、对齐 HybridCLR/Generate 子菜单的细粒度入口（`GenerateAll()` 一键及 5 个单项）、仅编译热更 DLL 的独立入口、AOT 元数据拷贝、业务 DLL 拷贝等独立方法。框架不再提供全流程封装，流水线编排统一交给 `EditorUtil.Pipify` 按需组装。配置先通过 `EditorUtil.Config.WorkspaceActive.Get()` 锚定当前激活 `ConfigMasterSO`，再按该 Master 当前 `Platform / Channel / DevelopMode` 三维坐标解析最终生效的 `HybridEditorConfigs`；其中 ConfigMaster Platform 实时映射 Unity Active BuildTarget。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.HybridCLR.cs` | `partial EditorUtil.HybridCLR` | 公有接口：`ValidateLinkXml()` / `CopyAotDlls()` / `CopyGameDlls()` 三个主流程操作；仅编译热更 DLL 的独立入口 `CompileDllActiveBuildTarget()`；对齐 HybridCLR/Generate 子菜单的细粒度入口：`GenerateAll()` / `GenerateLinkXml()` / `GenerateMethodBridgeAndReversePInvokeWrapper()` / `GenerateAotGenericReference()` / `GenerateIl2CppDef()` / `GenerateAotDlls()` |
| `EditorUtil.HybridCLR.Visitors.cs` | `partial EditorUtil.HybridCLR` | 当前为空的 partial 占位文件 |
| `EditorUtil.HybridCLR.Methods.cs` | `partial EditorUtil.HybridCLR` | 私有方法：激活 Master/当前坐标解析、`ValidateAndPatchLinkXml`、`CopyDllEntries`、`StripDllSuffix` |

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
| `ResolveLinkXmlPath()` | private method | 当前坐标 `LinkXmlTargetPath`，空时回退 `"Assets/link.xml"` | 返回项目根相对的 link.xml 路径；当前实现没有 `c_LinkXmlPath` 常量 |

---

## §5 完整公开 API

```csharp
/// 校验当前激活 ConfigMaster 当前三维坐标的 LinkXmlTargetPath；配置为空时回退 Assets/link.xml，缺失则补全由 ConfigMasterSO.HybridEditorConfigs 与 HybridEditorConfigsOverrides 解析出的 AotMetadataDlls 每项 preserve 记录。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException。
public static void ValidateLinkXml()

/// 拷贝 AOT 元数据 DLL 到当前激活 ConfigMaster 当前三维坐标中各条目配置的目标位置。
/// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取（项目根相对路径，所见即所得，不追加 .bytes）。
/// 仅当目标位于 Assets/ 时逐文件调用 AssetDatabase.ImportAsset(..., ForceSynchronousImport)。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException；源文件不存在或配置缺失时抛 FileNotFoundException。
public static void CopyAotDlls()

/// 拷贝业务层热更 DLL 到当前激活 ConfigMaster 当前三维坐标中各条目配置的目标位置。
/// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取（项目根相对路径，所见即所得，不追加 .bytes）。
/// 仅当目标位于 Assets/ 时逐文件调用 AssetDatabase.ImportAsset(..., ForceSynchronousImport)。
/// 未找到 ConfigMasterSO 时抛 InvalidOperationException；源文件不存在或配置缺失时抛 FileNotFoundException。
public static void CopyGameDlls()

// —— 以下 6 个接口对齐 HybridCLR/Generate 子菜单，为 Pipify 细粒度 Step 暴露 ——

/// HybridCLR/Generate/All：按 HybridCLR 预设顺序执行编译热更 DLL + 全部 Generate 产物（桥接 / AOT 泛型引用 / Il2CppDef / AOT 裁剪 DLL / LinkXml）。
/// 对应 HybridCLR 菜单中的一键入口，等价于依次手动点击 Generate 子菜单的全部项。
/// 内部转发到 HybridCLR.Editor.Commands.PrebuildCommand.GenerateAll()。
/// 其中 AOT 裁剪会临时启用 script-only 并调用 BuildPipeline.BuildPlayer；该临时产物不是最终 Player 构建成功证据。
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

### 激活 ConfigMaster 与当前三维坐标

`ValidateLinkXml` / `CopyAotDlls` / `CopyGameDlls` 先通过 `Config.WorkspaceActive.Get()` 读取当前激活 `ConfigMasterSO`；未绑定时由 `ResolveActiveMasterOrThrow()` 抛 `InvalidOperationException`。随后 `ResolveHybridCLRForCurrentCoord()` 使用 `master.CurrentPlatform / CurrentChannel / CurrentDevelopMode` 调用 `Config.DimensionalResolver.ResolveHybridCLR(...)`，因此 AOT/Game DLL 列表和 link.xml 路径都来自当前激活 Master 的当前三维坐标，而不是全工程任意查找。这里的 `CurrentPlatform` 与 Unity Active BuildTarget 实时对齐；`CompileDllActiveBuildTarget`、Generate 和 `{ActiveBuildTarget}` 路径解析本来就直接以 Unity BuildTarget 为真相，不改为读取 ConfigMaster。

`CopyAotDlls` / `CopyGameDlls` 不调用全局 `AssetDatabase.Refresh()`。每个文件复制完成后，只有解析后的目标路径位于 `Assets/` 时才调用 `AssetDatabase.ImportAsset(assetRelative, ImportAssetOptions.ForceSynchronousImport)`；其他项目内目标只做文件复制。

### link.xml 校验与补全（ValidateAndPatchLinkXml）

1. 通过当前激活 Master 的当前三维坐标解析 `LinkXmlTargetPath`；为空时回退 `Assets/link.xml`，再与 `SettingsUtil.ProjectDir` 拼接为绝对路径。
2. 若文件**不存在**，自动创建空骨架（`<?xml?>` + 根节点 `<linker>`）并记录日志；若存在则用 `XmlDocument` 加载，校验根节点为 `<linker>`，否则抛 `InvalidOperationException`。
3. 收集所有已存在 `<assembly fullname="...">` 的 fullname 到 `HashSet<string>`。
4. 遍历 `aotEntries`（`IReadOnlyList<DllMasterAssetEntry>`），对每个 `entry.AssetLocation` 调用 `StripDllSuffix` 剥离 `.dll` 后缀得到逻辑名（link.xml 规范不允许带扩展名）；若不在 Set 中则追加 `<assembly fullname="{logicalName}" preserve="all"/>` 并记录 `patched = true`。
5. `patched == true` 或 `justCreated == true` 时用 `XmlWriter`（Indent=true、IndentChars="  "）回写文件。

### DLL 拷贝（CopyDllEntries）

`CopyAotDlls` / `CopyGameDlls` 共用同一私有方法，签名：`CopyDllEntries(IReadOnlyList<DllMasterAssetEntry> entries, string tag)`。

逻辑：
1. **预检**：遍历所有条目，若 `SourceLocation` 或 `TargetLocation` 为空字符串，标记为配置缺失；源/目标路径先解析 `{ActiveBuildTarget}`，再相对 `SettingsUtil.ProjectDir` 取完整路径。若任一源文件不存在则整批抛 `FileNotFoundException`，不执行部分拷贝。
2. **拷贝**：确认全部存在后，对每条目创建目标父目录，将源文件拷贝到解析后的目标路径（`overwrite: true`）。目标路径所见即所得，**不追加** `.bytes` 或任何后缀。
3. **导入**：将目标路径分隔符统一为 `/`；只有以 `Assets/` 开头的目标才逐文件调用 `AssetDatabase.ImportAsset(..., ForceSynchronousImport)`，不执行全局 Refresh。

源目录不再来自 `SettingsUtil`（旧设计），改为由每条 `DllMasterAssetEntry.SourceLocation` 自描述（项目根相对路径）。

### GenerateAll 的临时 script-only Player

`GenerateAll()` 转发到当前 HybridCLR 包的 `PrebuildCommand.GenerateAll()`。该入口依次编译当前 `activeBuildTarget` 的热更 DLL、生成 Il2CppDef 与 link.xml、调用 `StripAOTDllCommand.GenerateStripedAOTDlls(target)` 生成裁剪后的 AOT DLL，之后再生成 MethodBridge 与 AOT 泛型引用。

`GenerateStripedAOTDlls` 会把 `EditorUserBuildSettings.buildScriptsOnly` 临时设为 `true`，以 Build Settings 中启用的场景调用 `BuildPipeline.BuildPlayer`，输出到 `HybridCLRData/StrippedAOTDllsTempProj/{target}`，并在 `finally` 恢复原构建位置和平台设置。这个 temporary script-only BuildPipeline.BuildPlayer 只为生成 stripped AOT DLL 服务：即使它成功，也绝不是最终 Player 安装包、平台工程、运行时或真机构建成功证据。

### StripDllSuffix

```csharp
private static string StripDllSuffix(string assetLocation)
```

剥离 `assetLocation` 末尾的 `.dll` 后缀（大小写不敏感）。用于 Step 1 生成 link.xml 的 `fullname` 属性——Unity link.xml 规范要求 assembly 逻辑名不带扩展名，而面板填写的 `AssetLocation` 可能带 `.dll`（与磁盘文件名一致）。

---

## §10 常见误区

**误区 1：跳过编译步骤直接执行 `CopyAotDlls` / `CopyGameDlls`**

`CopyDllEntries` 的预检步骤在源文件缺失时整批失败。`CopyGameDlls` 依赖当前 Target 的业务热更 DLL 编译产物，典型最窄顺序是 `hybridclr.compile_dll_active_build_target` -> `hybridclr.copy_game_dll`；`CopyAotDlls` 则依赖另行生成的 AOT 裁剪产物。二者不能因共用拷贝方法而混成同一前置链，依赖次序由调用方（Pipify Batch 等）自行保证。

**误区 2：以为目标路径会自动追加 `.bytes`**

旧版行为：目标路径 = `{projectRoot}/{entry.AssetLocation}.bytes`（隐式追加后缀）。
当前版本：目标路径 = `{projectRoot}/{entry.TargetLocation}`（所见即所得）。若需要 `.bytes` 文件，在 ConfigWindow 的"目标位置"字段直接填写 `XXX/YYY.dll.bytes`。

**误区 3：以为 DLL 列表配置来自 ProcedureComponent**

HybridCLR 的 DLL 条目来源是 `ConfigMasterSO.HybridEditorConfigs` 与 `HybridEditorConfigsOverrides`；各入口通过 `Config.WorkspaceActive.Get()` 锚定激活 Master，再按当前三维坐标解析 `AotMetadataDlls` / `StartupGameDlls` / `RunningGameDlls`。它不依赖 `ProcedureComponent`，也不会扫描并任取一个 ConfigMaster。

**误区 4：混淆 `DllMasterAssetEntry`（Master 三字段）与 `DllAssetEntry`（Runtime 单字段）**

三个列表都是 `List<DllMasterAssetEntry>`（含 SourceLocation / TargetLocation / AssetLocation），`CopyGameDlls` 会校验并复制 Startup 与 Running 两份业务列表。导出到 `ConfigRuntimeSO` 时仅保留 AOT 与 Startup 的 `DllAssetEntry`；Running 不导出，也不会被 `ProcedureLoadDll` 启动加载。

---

## §11 使用示例

每个方法内部自行完成激活 ConfigMasterSO 与当前三维坐标解析及空值检查，独立可调用。流水线编排由 Pipify Batch 负责，本类不再提供整体封装。

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

// 仅拷贝 AOT 元数据 DLL（Assets/ 目标逐文件同步 ImportAsset）
EditorUtil.HybridCLR.CopyAotDlls();

// 仅拷贝业务 DLL（Assets/ 目标逐文件同步 ImportAsset）
EditorUtil.HybridCLR.CopyGameDlls();
```

注意：仅刷新业务 DLL 时使用 `CompileDllActiveBuildTarget` -> `CopyGameDlls`；AOT 元数据复制另行依赖 AOT 裁剪产物。不要为了业务 DLL 本地刷新调用 `GenerateAll`，也不要把 `GenerateAll` 内部用于生成 stripped AOT 的临时 script-only Player 当作最终 Player 证据。

---

## §12 注意事项

- **前提条件**：工程中须存在 `ConfigMasterSO`（通过 `Nova/Config Window` 创建）；link.xml 不存在时 `ValidateLinkXml` 会自动创建空骨架。
- **路径填写**：`SourceLocation` / `TargetLocation` 均为项目根相对路径（如 `HybridCLRData/AssembliesPostIl2CppStrip/StandaloneOSX/mscorlib.dll`）；目标路径所见即所得，不会自动追加 `.bytes`，如需 YooAsset 原始字节格式需手动在目标位置填写 `.dll.bytes` 后缀。
- **幂等性**：`ValidateLinkXml` 的补全是幂等的（已存在条目不重复添加）；`CopyAotDlls` / `CopyGameDlls` 使用 `overwrite: true`，重复执行安全。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [DllAssetEntry.md](../../../Runtime/Modules/Config/Definitions/DllAssetEntry.md) — DLL 资产寻址条目（AssetLocation 单字段）
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md) — HybridEditorConfigs / HybridEditorConfigsOverrides 配置来源
- [EditorUtil.Config.WorkspaceActive.md](../EditorUtil.Config/EditorUtil.Config.WorkspaceActive.md) — 当前激活 ConfigMasterSO 锚点
- [EditorUtil.Config.DimensionalResolver.md](../EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md) — 当前三维坐标的 HybridCLR 配置解析
- [Editor.md](../../Editor.md) — Editor 层级总览
