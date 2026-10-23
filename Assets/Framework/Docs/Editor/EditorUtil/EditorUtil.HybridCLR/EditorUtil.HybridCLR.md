# EditorUtil.HybridCLR

**类签名**：`public static partial class EditorUtil.HybridCLR`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.HybridCLR`

HybridCLR 原子操作合集：提供 link.xml 校验/补全、对齐 HybridCLR/Generate 子菜单的细粒度入口（`GenerateAll()` 一键及 5 个单项）、仅编译热更 DLL 的独立入口、AOT 元数据拷贝、业务 DLL 拷贝等独立方法。框架不再提供全流程封装，流水线编排统一交给 `EditorUtil.Pipify` 按需组装。配置先通过 `EditorUtil.Config.WorkspaceActive.Get()` 锚定当前激活 `ConfigMasterSO`，再按该 Master 当前 `Platform / Channel / DevelopMode` 三维坐标解析最终生效的 `HybridEditorConfigs`；其中 ConfigMaster Platform 实时映射 Unity Active BuildTarget。

---

## 当前上游版本与执行边界

Nova Framework 仅支持 Unity `6000.4.x`，最低版本为 `6000.4.2f1`；更高的 Unity 次版本存在 HybridCLR 兼容问题，不属于当前支持范围。

项目封装包 `com.solotopia.hybridclr` 的 `Core/` 对应官方 `hybridclr_unity v8.15.0`。包根版本号与上游版本分别管理，编辑器命名空间为 `HybridCLR.Editor`、`HybridCLR.Editor.Commands`、`HybridCLR.Editor.Installer`，程序集名为 `HybridCLR.Editor` 与 `HybridCLR.Runtime`。

本地工具链位于 `HybridCLRData/`，生成物使用 `MethodBridge.cpp`、`UnityVersion.h`、`AssemblyManifest.cpp`。热更 DLL、裁剪 AOT DLL、link.xml 与 AOT 泛型引用文件的输出路径以 `HybridCLRSettings` 中的实际设置为准。包替换和编辑器编译通过不代表工具链已安装、产物已重新生成或 Player 构建通过；受控 Generate/Player 继续按实际源码与回调边界核验。

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
/// WebGL 当前指向缺失的 PROJECT 模板时，裁剪构建期间临时使用 APPLICATION:Default，结束后恢复原模板。
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
/// WebGL 当前指向缺失的 PROJECT 模板时，裁剪构建期间临时使用 APPLICATION:Default，结束后恢复原模板。
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
1. **预检**：遍历所有条目，若 `SourceLocation` 或 `TargetLocation` 为空字符串，标记为配置缺失；源/目标路径先解析 `{ActiveBuildTarget}`，再相对 `SettingsUtil.ProjectDir` 取完整路径。若任一源产物不存在则整批抛 `FileNotFoundException`，并明确说明 Config 的 DLL 列表只声明复制与加载映射，不会生成 DLL；不执行部分拷贝。
2. **拷贝**：确认全部存在后，对每条目创建目标父目录，将源文件拷贝到解析后的目标路径（`overwrite: true`）。目标路径所见即所得，**不追加** `.bytes` 或任何后缀。
3. **导入**：将目标路径分隔符统一为 `/`；只有以 `Assets/` 开头的目标才逐文件调用 `AssetDatabase.ImportAsset(..., ForceSynchronousImport)`，不执行全局 Refresh。

源目录不再来自 `SettingsUtil`（旧设计），改为由每条 `DllMasterAssetEntry.SourceLocation` 自描述（项目根相对路径）。

### GenerateAll 的临时 script-only Player

Android 的专用 Action 已建立独立计划、工具链、provider、Native/SDK 阶段观察与证据存储，但全部回调及 AF 导入尾链尚未闭合时仍 blocked。准确实现与剩余门统一见 [Android Generate 接线](../EditorUtil.AgentActions/EditorUtil.AgentActions.md#android-generate-的独立接线与未完成边界)。已有 Pipify 组合需独立冻结并审查实际 Step 和副作用；局部测试不代表本次裁剪报告、最终 Player 或设备通过。

`GenerateAll()` 转发到当前 HybridCLR 包的 `PrebuildCommand.GenerateAll()`。该入口依次编译当前 `activeBuildTarget` 的热更 DLL、生成 Il2CppDef 与 link.xml、调用 `StripAOTDllCommand.GenerateStripedAOTDlls(target)` 生成裁剪后的 AOT DLL，之后再生成 MethodBridge 与 AOT 泛型引用。

`GenerateStripedAOTDlls` 会把 `EditorUserBuildSettings.buildScriptsOnly` 临时设为 `true`，以 Build Settings 中启用的场景调用 `BuildPipeline.BuildPlayer`，输出到 `HybridCLRData/StrippedAOTDllsTempProj/{target}`，并在 `finally` 恢复原构建位置和平台设置。这个 temporary script-only BuildPipeline.BuildPlayer 只为生成 stripped AOT DLL 服务：即使它成功，也绝不是最终 Player 安装包、平台工程、运行时或真机构建成功证据。

消费态 `nova.project.hotfix.generate-artifacts` 在 Plan 中核对已安装平台模块、IL2CPP、实际场景资产、两种 link.xml 的合法项目路径、无持久化脏资产和无旧 YooAsset staging marker；不会自动改 PlayerSettings 或清他人的残留。它冻结 PlayerSettings、启用场景、Master/坐标、HybridCLR 设置及 Player 回调的编译身份，并显示缺少写入契约的具体回调后返回 `blocked`。Nova SDK processor 构造与方法、项目回调、HybridCLR/SDK 后处理都不能因 script-only 而默认视作无副作用。

可执行计划除正式生成路径外还声明 Unity 共享构建缓存和 Android Manifest/Proguard 输入。YooAsset staging 对 `/StrippedAOTDllsTempProj/` 的正常内部构建已有跳过条件，不纳入新的临时 Resources 副本；旧 marker 会在 Plan 阻断，避免 post cleanup 误清原残留。未知回调尚未建立契约时仍不执行，不扩大 `Assets/**` 掩盖缺口。

Execute 固定一次 `GenerateAll -> ValidateLinkXml`，异常后保留非 finalized Receipt 与可能写入范围，禁止重放；Android 与其它同步分支由 Action finally 恢复进入时的 script-only、Android 导出选项和构建位置。WebGL 由原同步 StripAOT finally 恢复，并核对真实 typed 后态，不在跨 await 的外层 finally 写回用户后续设置。HybridCLR 设置的 `outputLinkFile` 负责生成热更引用保留表，Master 的 `LinkXmlTargetPath` 负责补全 AOT metadata 条目；两者允许使用不同路径，Action 不修改任一设置。Plan 同时冻结并声明两条路径，Receipt 分别捕获、核对两份 XML；同一路径仅计一次。成功执行后的 Receipt 冻结上下文及精确八项（同路径）或九项（不同路径）产物；Verify 只读核对该集合、Hash、MethodBridge DEVELOPMENT、生成 XML 的 linker 结构、Master XML 的 AOT 条目与上下文恢复。旧或未完成 Receipt 为 `partial`；Unity 后续编译、最终 Player 与设备证据仍需分别采集。

Android 另冻结官方实际显式 BuildOptions、subtarget、调试/Profiler/deepProfiling、导出/安装选项与环境变量摘要；全局 scripts-only 不等于显式 BuildScriptsOnly。官方 UNITY_IL2CPP_PATH 与 _CL_ 正常转换按冻结后态验证，不盲目恢复。自有 StripAOT 观察租约只覆盖本次 GenerateAll，要求自然发生一次 pre/Gradle/post；Gradle 路径必须等于本次临时工程下现存且无链接的 unityLibrary，在 HybridCLR 的整个 Library fallback 前拒绝缺失或越界路径。该 guard 不覆盖其他 SDK 写集，真实原生路径仍需实际执行观察，未知回调继续阻断。

生成后的编译复用 `GeneratedExportCompilationState`，用单次令牌和完整 Receipt 载荷 Hash 绑定实际程序集编译与脚本重载；未完成、失败、被后续生成替换或 Editor 重启丢失的证据都使 Verify 为 `partial`。Hash 或旧 Console 无错误不能代替这次编译。该跟踪不会重放 GenerateAll。

当前真实 WebGL 的受控 Generate 复用既有 Player 的准确回调捕获、七组 typed before/after、正常 reset 与五组真实报告材料，保持全部源码/DLL/自然分支门；不复用普通 Player 的 BuildMode、输出根或环境回滚。StripAOT 报告必须是本次新 GUID、WebGL、准确 `HybridCLRData/StrippedAOTDllsTempProj/WebGL`、官方显式 options、零错误和 Succeeded。GenerateAll 仅调用一次；全链错误日志、旧报告、未知回调或后验缺失均保持未完成，不私调回调补证据。Android 仍使用原合同，不因 WebGL adapter 放行未知类型。

源输入只排除当前设置推导的准确热更/AOT/CPP/临时工程目录、双 link.xml、AOTGenericReference 源码与伴随 meta，以及该源码唯一所属程序集的准确 Library 派生 DLL/PDB。祖先目录继续逐文件保护兄弟源、场景、meta、GUID、配置、工具和未受影响的导入摘要；明确递归包含生成源码的节点另绑完整保留依赖、导入器与现存有效对象。已有 owned 树的全部深层链接（含悬空链接）在排除和执行前都拒绝；Assets 输出父目录及 meta 必须既存并保持。外部热更解析目录、非唯一源码所属程序集或其它编译源落入输出域均具体 blocked，不改设置过门。

执行先核真实官方设置/环境 before，完成全部 typed after 与原 ProjectAuditor 自然复制（无 pending 时可以同步完成），再提交编译前证书及 finalized Receipt，并由原 `GeneratedExportCompilationState` 绑定单次 token、完整载荷、真实编译和重载。AOT 泛型/引用表的官方 Refresh 正常执行；旧操作或异常早重载中，原 Operation 只能恢复未 finalized 计划为 partial，保留准确报告备份，不重放或手泵 update。Verify 先复核已绑定旧域历史后验，再核新域源码、持久输入和精确输出；只允许逐字段列明的域瞬态重建。新域 Shader/UGUI 和两个空池要求自然空态，Adaptive metadata 要求准确四字段默认 struct；TMP 三表/弱槽保存完整实际行：只有原 before 已加载且明确 marker 字体的源预测首次登记可通过独立逐行证明；原 winner、外部行及活 weak 槽保持，Verify 当前完整 map/weak 与已记录 after 相等。其它资源缓存、外部委托全链、字体 native ID/正文/源和全部字符缓存仍保持严格比较。ProBuilder 默认双仓库必须已自然初始化，有效字典、原序列化行和磁盘完全一致；只对 Nova 自有无回调 DTO 预测原 Save 的准确 pretty 字节与 Hash，证明重载保存不会改源。未知、未保存或未初始化仓库具体 blocked，不调用 Init/Save。

`UNITY_IL2CPP_PATH` 与 `_CL_` 保留原厂正常转换，成功按唯一后态验证；失败/取消只接受稳定设置恢复和原值/官方阶段值，保留实际阶段证据，不盲目回滚。MethodBridge DEVELOPMENT 与当前 ABI、双 XML 和八/九项正式产物仍由原 Action 验证；存在旧 CPP、单次 DLL 刷新、日志无错或候选测试通过都不能代替这次完整链证据。

当 ActiveBuildTarget 为 WebGL 且 `PlayerSettings.WebGL.template` 指向不存在的 `PROJECT:` 目录时，`GenerateAll()` 与 `GenerateAotDlls()` 会在上述临时 Player 构建期间使用 `APPLICATION:Default`，并在成功或异常后恢复进入前的模板值。网页模板不参与 AOT 裁剪 DLL 内容；该隔离只避免无关的模板前置校验阻断 HybridCLR 产物生成，不修改正式 Player 的模板选择。

### StripDllSuffix

```csharp
private static string StripDllSuffix(string assetLocation)
```

剥离 `assetLocation` 末尾的 `.dll` 后缀（大小写不敏感）。用于 Step 1 生成 link.xml 的 `fullname` 属性——Unity link.xml 规范要求 assembly 逻辑名不带扩展名，而面板填写的 `AssetLocation` 可能带 `.dll`（与磁盘文件名一致）。

---

## §10 常见误区

**误区 1：跳过编译步骤直接执行 `CopyAotDlls` / `CopyGameDlls`**

`CopyDllEntries` 的预检步骤在源产物缺失时整批失败。Config 中显示的“启动时 DLL 列表”或“运行时 DLL 列表”只代表复制与加载映射，不代表对应平台的 DLL 已生成。`CopyGameDlls` 依赖当前 Target 的 HybridCLR 热更新 DLL 编译产物，典型最窄顺序是 `hybridclr.compile_dll_active_build_target` -> `hybridclr.copy_game_dll`；`CopyAotDlls` 则依赖另行生成的 AOT 裁剪产物。二者不能因共用拷贝方法而混成同一前置链，依赖次序由调用方（Pipify Batch 等）自行保证。

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

Generate Action 正式声明 `ExternalWrite` 以覆盖已核原 Player 回调可能发送的统计数据；`callbacks.knownEffects` 展示条件与字段，未知下游仍保持阻断。它不自动关闭 Analytics 或 vendor telemetry，也不把生成成功视为外部送达证据。


当前真实 WebGL Generate 使用与 Player 相同的准确回调捕获、typed 执行后验与真实 BuildReport，输出只接受已审默认 `HybridCLRGenerate/AOTGenericReferences.cs` 和 `HybridCLRGenerate/link.xml`，Master AOT XML 仍按独立冻结路径处理。父目录与其 meta 必须既存；拥有目录内全部现存后代均拒绝链接，包括悬空链接。Android 未闭合的回调合同继续阻断，不能因 WebGL 分支通过而放行。

必要诊断结束后，先等待正常编译和域重载完成并进入稳定域，再建立需要重载验证的正式 Generate Plan。当前已审 `execute_code` 通过 `Assembly.Load(byte[])` 载入名为 `MCPDynamic` 的普通内存 PE 程序集，名称含 Dynamic 但 `IsDynamic=false`，正常域重载后不自动重放；不能把该诊断程序集带入正式计划的程序集基线。READY → Execute → Verify 完成期间，不再额外运行 `execute_code` 或 Native probe。保留真实捕获与严格比较，不预热、不删除已捕获的程序集行、不重插程序集，也不在事后回填或升级旧证据。

编译记录的未登记 `firstReloadDomain` 经 Unity `JsonUtility` 往返后可为空字符串；登记首次真实域时仅将 null 与空串视为同一未登记状态。已重载、非空标记、旧协议、缺少有效 nonce、错误令牌或过期记录仍被拒绝；不补签历史操作。

完整写前/写后 typed 材料分别保存在 `Library/Nova/AgentActions/HybridCLRGenerate/<本次 N-GUID>/before.json` 和 `final.json`。Plan/WriteBefore 保持原完整 before 严格 UTF-8 JSON 原文 8MiB 硬限，即使 files 大量重复、可以压小也不得接纳。final 完整 DTO 原文不超过 8MiB 时继续使用原裸 typed 字节；只有更大的实际 final 才可使用下述固定双槽无损行表 v3，最终未压缩 wire 仍最多 8MiB，完整展开 DTO 另外严格限制 16MiB。Plan 只展示完整计划并保存有限未写指针，不创建证据目录；Execute 要求 GUID 目录不存在，先原子写完整 before，再恰一次生成，正式收尾及全部后验通过后保存完整 final。小 Receipt 继续绑定固定文件的准确 wire SHA-256 与长度，完整 Operation 预算限 192KiB，预留 Store 后续状态空间；不截断快照或扩大 Operation Store。两个文件均列入准确 WriteSet 与正式 Artifacts，须保留供 Live Verify 读取；`report-backup` 子目录与正式证据分离，Auditor Commit 不删除这两个文件。

final 的 `nova-hybridclr-generate-file-rows-v3` 只复用 `before.inputs.files` 和 `afterInputs.files` 两个准确 `FileState[]` 槽；根级字段恰新增 `fileRowCodec`、`expandedLength`、`expandedSha256`、`fileRows`，原有序数组暂以整数索引表示。行表保存完整 path/kind/sha256 或 null，最多 32768 个唯一行，两个槽合计最多 65536 个引用；重复、null、顺序及缺失/空数组完整还原，解码后不共享可变行对象。只接受规范 v3 JSON、准确已知版本/字段/类型、规范首次使用次序、无重复/未用表行及有界整数引用；先按完整行 UTF8 计算整体展开预算，再复制行，完整展开长度与 SHA、全部原 DTO/SVS 严格校验和规范字节必须一致。原证书和回调摘要继续针对原完整 DTO 计算，Reference 版本/独立 before/两个文件长度与 SHA 绑定不变；旧裸 typed 证据继续可读，8MiB 内写入字节不变。此 codec 不使用外层 gzip、不丢 before/after 快照、不引用另一个文件或较晚捕获；非共享巨大字段仍受原 wire 上限约束，缺失 final 的旧 partial 无法借此回填或升级。

编译 payload 绑定上述准确文件摘要和编译前证书。重载后先核原编译令牌、载荷及 requireReload，再读原文件重核历史自洽及当前持久源/配置/输出；不把旧 managed 对象当作当前对象。官方 Refresh 正常执行，当前WebGL执行由本次公开租约保护至Dispatcher持久化尝试后；旧未 finalized 的早重载 Operation 仍保持 partial，已有 before/产物保留，不重放。WebGL 不在跨 await 的外层 finally 写回用户设置，恢复证据来自原同步 StripAOT finally 和真实 typed 后态；尚未尝试final时，失败/取消/收尾错误清除完成标记和证书并只首存一次；final已尝试后的编译前历史不改写或重试，外层失败Receipt保持未finalized。外部 telemetry 仅按原回调自然条件发生，产物或本地证书不证明外部送达。

Plan 的 `webglGeneratePlan` 保持完整可审阅，运输表示使用专用 `nova-hybridclr-generate-plan-gzip-v1` 封装：字段恰为 `codec`、`rawLength`、`rawSha256`、`gzipBase64`。固定本地 serializer 保留所有 null 和默认字段，严格 UTF-8 原文最多 8MiB，单 member gzip 最多 1MiB；规范 Base64 无空白，原文长度与 SHA-256 均绑定。`TryDecodePlanPreview` 只输出审阅 JSON，核严格 JSON/已知 DTO 字段、CRC/ISIZE、精确 DEFLATE 终点，拒绝截断、尾随/串联、超额解压、未知/重复字段、类型元数据和嵌套 codec。解码成功不代表业务 Plan 有效、没有漂移或获准执行。

Plan DataJson 同时保留原小 Receipt 与完整压缩预览，最多 1536KiB，为现有 2MiB 正式 Plan 响应保留外层空间；完整 Operation 仍限 192KiB（Store 256KiB 不变）。预览编码只操作本次内存 DTO，不写 Plan 文件、不截断数组、不更改原 State/Runner 绑定，也不参与 Execute、恢复或正式证书。最终 before/final 文件及其编译载荷绑定继续使用原完整证据协议，超预算明确 blocked。

当前真实 WebGL Generate 的 Execute 在输入重新核对后、写 before/生成前取得本状态唯一的公开 `EditorApplication.LockReloadAssemblies` 租约。正常 Refresh、编译及官方报告自然等待继续；不等待域重载自身，不新增 isCompiling/isUpdating 的 idle 门。完整 typed 后验、final 原文、原编译 Schedule 与 Receipt 返回后，由既有 Dispatcher 在 `TryCompleteExecution` 持久化尝试后的 State.Dispose 主线程配对一次 Unlock。持久化返回 false 时仍释放且保持 partial/warning，不宣称持久化成功。Plan、未执行/过期清理不获取原生锁；普通 Player、Android、Pipify 不变。

30分钟是 linked cancellation token 的协作期限，计时线程只取消，不释放锁；同步原生 Build 不能强杀，只能返回后再观察。取消、异常或超时保存可用实际材料后同样经 Dispatcher 清理自己的锁，绝不循环清除他人计数。仅尚未尝试 final 保存时可降级并首次保存失败材料；已保存的 final 是不可变编译前历史，之后取消/调度失败不改写或重试，partial Receipt 的 finalized 保持 false。最终正常编译与 requireReload 仍由原令牌/完整载荷协议核验；旧早重载操作保持 partial，不重放。不扩大AutoSync输出拥有域或复制授权；后续真实漂移照旧阻断，正式Copy按独立合同消费。

WebGL Generate 的 TMP 检查在同域收尾/Verify 使用冻结观察，不为取得后验再次 Load 未清理字体。原 Prepare 分类行作为明确历史输入绑定，逐路径文件/meta/依赖与当前现存对象仍重新读取；缺席仅接纳原 clean、无原/后资源强行或活 weak、真实 Scope after 和当前均缺席的原候选。`tmpFontAfter` 始终来自真正 Scope.Dispose；较晚回调捕获只能复核，不能补造 after。正常编译重载后的 TMP 规则需独立实证，这项同域修复不放宽新域或旧 receipt。

WebGL Generate 的场景可见性使用独立合同：Plan 保存唯一既存 Native `SceneVisibilityState`/manager、完整三表原文、原 dirty、Main Stage、全部主场景、既存 Preview、准确 source-owner 与全部无有效场景对象，以及准确 `Library/SceneVisibilityState.asset` 文件。三表和完整观察分别使用有界无损 gzip，保留原文字节长度与原文/规范 SHA，不用摘要代替正文；Preview 与无场景对象保留全部组件的原文、ID、GlobalObjectId、hideFlags 和源码/DLL 身份。仅支持全部持久标记数组为空且全部对象四项隐藏/禁选标记为 false 的已证分支；未知对象、角色、组件、源或 DLL 指纹一律拒绝，不创建、关闭或删除它们，不替用户 ShowAll。既存 SceneView 通过准确 backing 字段绑定两相机与三光源、Preview 及 window GUID；既存 DeformationManager 通过已审 backing 绑定唯一 helper。MAX executor 与 InternalIdentityTransform 只作为原 Native 实例保护，不调用 SDK。

公开四项 flag 查询并非 Native 零变化：本机完整前后材料证明查询可仅在 Visibility 表物化准确 owner 的当前 Preview 空行。因此每次 Capture 独立保存真正 query-before 的完整三表、dirty、Library 状态及查询后态；只允许该 Visibility 空行、dirty 与 Library 完全不变，原行/key/value/顺序全部保留。同域写前和较晚 Verify 必须先核 query-before 原文字节 SHA 等于上一份 post-query，再核完整后态相同，防止删行后查询重建掩盖变化。正式同步 Generate 的三表跨度继续要求原行保留，新增仅严格零 GUID、非零 Int64 handle、两个空数组；原有自然 dirty true→false 与准确单 Library 文件写合同不变。Scope 的真实原材料不可用后来 Capture 补造。输入行仅投影为明确历史 `GenerateSVSProtectedBaseline`，当前实际材料独立保留并绑定原证书。

Generate 的真实 before→scopeAfter→原同域 Observed 另有有限对象转换校验。已保存、加载且 clean 的非 Preview 场景按准确64位 handle、场景 GUID 与完整 GlobalObjectId 唯一双射；GlobalObjectId 自身的场景 GUID 必须相符，所有非 ID 的 ObjectRow 字段逐项保留。每个场景只能全组原 ID 保持或全组 disjoint 重建，拒绝部分替换、旧 ID 跨场景交换、新增/缺失/重复与任何四 flag、名称、hideFlags 变化；scopeAfter→Observed 的新组 ID 须稳定。既存三 Preview 光源、全部 owner、四个核心角色及固定无场景对象仍严格保护。普通场景 ObjectRow 不包含组件正文，不把该双射冒称业务组件验证。

无场景对象唯一临时变化限于同一准确 SceneView owner 的主 Camera：同一 Native/component/globalID、类型、hideFlags、源 DLL 指纹，完整固定 Camera JSON 形状内的 m_HDR 布尔 false→true→false。scopeAfter 原文只能恰一次该布尔替换，所有其它字节不变，原 Observed 必须恢复 before 的完整无场景对象原文；gizmo、未知字段、重复键、其它属性或未恢复均拒绝。不归一化或覆写任何三态 DTO，不用较晚新域观察补造原恢复证据。

正式 Verify 唯一使用不同名 Observed 入口：先验证原证书绑定的完整三态，再以原稳定 Observed 与本次真实 Capture 比较；本次须为当前域且四个活弱见证准确。新域沿用原真实 token/payload、首次域证明及既有重载门，比较基线为已验证的原 Observed；原 scopeAfter 始终保留为 witness key 和 scopeAfterSnapshotSha256，原 final/payload 继续绑定原 Observed。旧无独立 Observed 的编译入口保持原严格 Native 门，不能借较晚 current 获得新例外；全部写前 BeforeEquals/NativeIdentityEquals 与冻结比较保持原文。历史 partial 不升级，必须使用新 Plan、新 Execute 的完整实际证据验收。

跨脚本域默认 blocked；只有原正式编译 token/payload hash 对应本轮 Schedule 的新协议 stamp、随机 nonce、准确起源域，编译实际成功且当前恰为首次不同域时，才允许已核源 owner 的两相机、三光源、Deform helper 的 Native 子 ID 与 Preview handle 自然重建。从原稳定 Observed 到首次重载观察，主场景、owner ID/GlobalObjectId/window GUID、全部原文及非 ID 字段、四项实际 flag、固定 MAX/Internal Native 身份、dirty 与 Library 文件均须精确保持。首次 reload 到 query-before 仅准 Picking 表新增准确新 Preview 空行，随后的公开查询仅准 Visibility 表新增同 handle 空行；不许可 isolated 表或未知句柄。任何后续 reload，或正式周期结束后又开始新编译，都不可补签/复用新许可；旧记录无新协议不补造，但其它导出的旧 `TryVerify` 语义保持不变。旧 schema、缺少完整原材料、未知变化仍为 partial。合同不反写 JSON、不 SetDirty/清 dirty、不保存，也不扩大普通 Player/Bundle 门。单快照96KiB、完整 JSON2MiB/gzip32KiB及完整证据8MiB门保持不变，超量明确拒绝，不截断材料；候选纯测试/离线编译不代表正式 Generate 或 Player 成功。

首次新域完整后验通过后，本域仅以原 token/payload hash、原 Scope 完整指纹及 Schedule nonce 绑定首个实际 post-query 的完整自有 DTO 字符串；后续 Verify 先要求此次 query-before 等于该首 post，再核 post 完全不变，不能再次泛用 P/V 空行许可。见证不强持 Native，也不跨后续域。

真实 WebGL Verify 保留原 `DataJson` artifact 数组，在既有 `Evidence` 中返回 `nova-generate-svs-postcompile-v1:` 前缀与严格 `nova-generate-svs-postcompile-gzip-v1` 信封：完整首次/当前 Snapshot、实际编译记录和域、原 before/final 长度/SHA 指针、Scope 完整 SHA、本次 adapter 结果及新后验证书均无损绑定。ASCII gzip/Base64 信封防止 Gateway 路径脱敏改动原正文；单正文160KiB、gzip32KiB、外信封64KiB，仍不改原8MiB Store。解码只供只读核验，不能产生执行许可。本次验收须核对实际工具完整返回及其响应 SHA；Operation journal 只保存状态/消息，不能代替完整后验材料。临时响应随本次任务清理，不另建长期验收档案；未完成事务仍按恢复生命周期保留必要状态。Verify 不写新 sidecar、不改旧 final 或 Receipt。真实后验不足或返回越界仍 partial，不能把候选纯测试当实际 Generate/Player 通过。

建立全新 Plan 的首个 SVS 输入许可使用零参数 `CaptureInitial()`：内部恰一次真实 Capture，并完整校验其真实 query-before→post 有限 Visibility 暖化、dirty/Library 不变、当前域及同一活 Native 弱见证；同一个完整 Snapshot 原样作为 baseline/observed 保存。不把首个观察误套“前一 post 必须等于本次 query-before”的两次观察比较，不二次 Capture 预热或丢弃真实前态，也不接受任意外部 DTO 初次审批。随后的准备、输入捕获末尾、Execute 与重复 Verify 继续使用原严格 `BeforeEquals`/已有 witness 门。

完整计划预览与 before/final 存储只在四个准确 typed 位置识别 SVS：`GeneratePlan.sceneVisibilityBefore`，以及 `GenerateEvidence.before.sceneVisibilityBefore`、`sceneVisibilityAfter`、`sceneVisibilityObserved`。每个非空 Snapshot 必须先通过固定 wire 字段/类型、完整 body/observation 解码、原文/规范 SHA、已审来源及全部原有预算校验；只有该原树直系 body/observation 的 `gzipBase64` 得到存储表示许可。存储器原有省略 null 引用字段的表示保持，所有值字段须显式存在，不能用 DTO 默认值补造证据。其它位置、Receipt、未知 codec/嵌套计划封装与任何 `$type`/`$id`/`$ref` 仍拒绝；纯存储校验不读取 Native、不核当前域弱见证、不授予输入投影、执行或恢复许可。单快照96KiB、完整正文2MiB/gzip32KiB、预览及8MiB证据总门均不变，原 gzip 正文不被重编码或摘要替代。

正式Generate Plan若同次prepared输入捕获成功而原`PreparedInputsPreserveOriginal`拒绝，会在原blocked说明后附加纯DTO差异诊断；原比较器、准入及原错误含义保持。诊断仅克隆这同一次original/prepared，用原sha清空及精确TMP dirty行投影规范化，不重捕获、预热或改Native/磁盘。保留两份实际input SHA、规范化成功/失败阶段、完整规范化输入SHA及各变化字段的完整值SHA/类型/数量；一般字段不返回配置正文。effective行按完整文本多重计数返回真实变化行，另保留索引差异总数、序列SHA及纯顺序变化，重复不能被集合差分漏掉。

诊断含中文前缀最多15KiB，给原失败和Dispatcher前缀预留1KiB。单行前插造成大量索引移位时先整体省略index明细，仍保留紧凑完整行计数差异；后者仍超预算才整体省略，并明确`detailsComplete`/`effectiveRowDiffComplete`等false、真实总数与完整`rawDiffSha256`/长度，不截字符串冒充全量。规范化异常只返回阶段、类型和异常消息SHA，保持blocked。`rawDiffSha256`绑定加入自身SHA/长度字段前的脱敏前完整诊断树；Gateway会继续脱敏项目路径和URL，不能从脱敏后的JSON直接重算该原SHA，如需核对传输后的响应SHA，应对本次实际工具完整返回计算，临时材料随本次任务清理。本诊断不写sidecar、不创建Operation、不授予执行/恢复许可；旧blocked返回不能据事后捕获补造原因。

正式Generate准备失败的字体缓存诊断只使用**同一次guard判定**的读取记录：两次输入捕获分别返回局部out记录，不给Snapshot增加字段，不进入inputSHA、Store、Preview或准入比较。来源/importer/唯一owner/直接ID及完整零JSON条件保持。guard先执行唯一一次Texture ToJson，再按序读取6项元数据与第二hideFlags；序列化可能改变Native缓存，因此严格条件核对的是序列化后的同阶段状态：width/height=0、Alpha8、不可读、mip1、Tex2D及准确标志须全部成立。6项元数据保持&&短路，各getter调用点至多执行一次；诊断不增加ToJson、重捕获、字体加载、清dirty或预热，也不按尺寸、任意TTF或hash白名单放行。

`fontCacheDecisions.countScope=collected-dirty-Texture2D-only`表示数量只覆盖原持久化dirty筛选中的Texture2D候选，不能称全部guard调用。记录仅保存原读取已返回的纯值；未执行点、已尝试但无返回、实际异常类型分别明确，最后尝试点不推断为异常原因。guard=false分支只绑定原Select已算出的完整dirty行；guard=true分支只有原已读path，最多作唯一path关联，并明确没有GUID/localID归属证明；缺匹配、多匹配或未请求采集不冒充通过/完整。变化行保留同一次actual输入SHA与原比较器拒绝，trace仅用于定位，不能授权排除脏资产。

完整记录以数量、UTF8长度和脱敏前SHA绑定；展示按阶段/首个拒绝门分组，提供实际整份样本并明确省略/完整性。分组展示受4KiB内部预算且与失败说明共用15KiB总门；总量紧张时先整体省略trace样本/分组，优先保留完整变化行身份，所有省略标非完整。非纹理配置正文、Font完整JSON和凭据不进入诊断；Gateway仍可能脱敏路径/URL，传输响应SHA与脱敏前诊断SHA须分别核对。事后只读检查不能回填旧Plan失败原因；本次同调用trace仍需真实Unity回归及新的冷域正式Plan验证，纯测试不能代替该验收。

真实回归须在独立重新导入并加载自有字体后、任何fixture序列化或前置helper之前，直接验证guard结果及同次trace，核对序列化后的严格条件、Source/meta/dirty归属及Material/Font inventory；离线编译和纯DTO测试不能代替本次Unity与冷域正式Plan。异常分组仅凭实际非空异常类型展示最后尝试点，不能把值为null的字符串token误报为异常，也不能从最后尝试点推断异常原因。

Execute 已成功取得同次 current、原冻结比较返回 false 且没有具体 error 时，会返回至多 15KiB 的同调用纯 DTO 差异诊断。诊断只列外层及 Generate inputs/callbacks/SVS 字段指纹、长度/数量和 diff keys；dirty/TMP 对象行只显示受限类型、身份/路径 SHA、正文尾 SHA 与完整行 SHA，未知前缀仅摘要，不输出配置正文。重复项和顺序变化分别保留；超限整项省略并标记 incomplete、真实数量和完整差异指纹，不截字符串冒全量。恢复随机目录与未参与的 Request/lease 不被当作原比较判据。

这些是诊断 DTO 的事后纯比较，各原判据统一 notReevaluated；不会重新调用 SVS/TMP/Facebook/其他 BeforeEquals、弱 Target、Native getter、回调、捕获或重写已有材料。首查询压缩 observation 的差异不自动代表 SVS 门失败；DTO 全同仍可能是非序列化 managed/weak witness 门，保持 blocked/inconclusive。原 FrozenStatesEqual/GeneratePlansEqual 与成功路径不变；捕获失败或已有 error 原文保持，诊断不能作为许可或用于绕过匿名脏对象门。新的正式同次失败材料才能解释此次原因，分离快照不能回填为同次拒绝现场。

Generate 回调初始完整性门失败时，原拒绝条件、短路次序、typed after 校验及总 callback 合同摘要均保持。仅在该门已经实际失败后，使用该次调用已有的 before/after/current 回调 DTO 与 TMP 后态/错误存在标志追加 callbackDiagnostic；不重捕、不读 Native/getter/WeakReference.Target、不修改 DTO 或放宽合同。诊断先给出材料/chainReturned/unsupported/TMP 存在标志，只有存储摘要不一致时才按原摘要的十五个参与部分给出完整 SHA、计数与相邻阶段差异：八个有序数组部分（含 unsupported 与原 normal-reset 规范化）和七个既有 typed InvariantHash。行样本跨所有部分最多四条，只含索引、存在标记及完整行 SHA；原行、路径、配置、命令和密钥不输出。总错误含原 generic 前缀最多8KiB UTF8；超限整段省略并保完整诊断长度/SHA与明确 partial，单部分失败只标不完整，诊断异常不覆盖原失败原因。该消息只是失败定位，不是完整 final 或构建通过材料；旧 partial 不回填，需要全新 Plan/Execute 才能验收。

final 完整证据保存失败时，Execute 会把同一次 TryWriteFinal 返回的实际保存错误，同时保留于既有 Warnings 与原 Operation Message 的附加诊断；原生成/回调错误前缀不覆盖。附加段至多4KiB UTF8，保留原错误完整UTF8长度和SHA；超限只整份省略正文并明确标记，null或不可编码材料明确缺失/不完整。该诊断不重试 final、不重新序列化执行材料，也不改变8MiB Store、Operation预算、typed字段或编码。没有完整 final 的旧操作保持 partial，不能通过后来捕获、原 before 或现有产物补造丢失后态；必须使用全新操作取得完整验收材料。

WebGL Generate 另绑定当前 Unity 安装根下准确 `Resources/Scripting/Managed/BeeBuildProgramCommon.Data.dll` 的完整文件 SHA/MVID/长度、实际同名加载数量与原回调域。完整 IL 仅有 DTO/编译器属性构造；正常 Bee 构建配置会自然使用它。写前完整状态必须精确保持；只有正式 Generate 后才允许原未加载变为唯一准确加载，或原状态全等。原 Capture 不删行、原十五部分总哈希算法不变，独立预计 DTO 只增加这一准确行，其余行和 typed after 逐项保护；同域 after→current 完整相等。首个正式编译重载域已由原 token/payload 与 SVS 入口批准后，当前域可以自然尚未加载或已经加载这同一准确 DTO，独立比较数组只为持久义务补足原已认证行，不改原 after/current。不同安装根、DLL、MVID、同名重复、额外程序集或缺少真实重载证明仍拒绝。旧 partial 不升级、不重放，也不预加载该程序集。

## 原始工具的身份与恢复限制

DLL 身份去重依据裁剪 .dll 的地址文本和规范化目标路径，不读取程序集元数据；只检查 Startup/Running 跨列表冲突，不完整检查各列表内部重复。源存在预检之后的复制、目录创建和 ImportAsset 仍可部分失败，无文件事务。导入条件依据解析后的 TargetLocation 文本是否以 Assets/ 开头，工程内绝对路径不会因物理位置相同自动导入。路径工具不提供项目边界或符号链接授权。

Sample 自动同步只依缺失/mtime 判断，不比正文 Hash，同时间损坏目标无法发现；NeedsCopy 和配置解析可在复制 catch 外抛，不能笼统认为所有失败仅告警。MethodBridge DEVELOPMENT 解析要求精确行格式，不校多个矛盾标记或 ABI；Validate 不生成桥。

缺失 WebGL PROJECT 模板的临时 scope 只查目录存在，不核 index.html 或路径安全；正常 using 顺序恢复原值，重复 Dispose 会再次写原值，不核用户中途修改。所有 Generate/Copy 原语没有 Action 的计划、权限或收据门，包工具的异常与已生成物保留。
