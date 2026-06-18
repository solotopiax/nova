# ABPath/AssetName → AssetLocation 合并改造静态审查报告

> 审查时间：2026-05-17
> 审查人：code-reviewer (L2 静态关卡)
> 基准分支：develop

---

## 范围

- git diff 文件数（.cs）：138 个 Framework + 4 个 Game/DataTypes = 共 142 个 .cs 文件
- 变更行数：+3223 / -3003（含 Game 测试文件）
- 模块覆盖：Batch 1-10（详见下表）

| Batch | 模块 | 核心改动 |
|-------|------|---------|
| 1 Core | Path.GMTool / IDataTableUnitSetting / DataTableUnitSettingBase / IDataReceiver / DataReceiver / LubanDataReceiver | 接口统一字段名 |
| 2 Config | ConfigComponent / ConfigManager / ConfigManagerConfig / Inspector 三件套 | SP + 字段 + Manager 同步 |
| 3 Localization | LocalizationComponent / LocalizationManager / ILocalizationFontRow / LocalizationFontData / TextLocalizing / Inspector + Exporter | 多处字段名 + SP |
| 4 Network | NetworkSettings (HostKeyUnitSetting/NetCmdUnitSetting) / WebSocketSettings / NetworkManager / WebSocketManagerConfig / Inspector | 显式接口实现 + WS 字段 |
| 5 Procedure | DllAssetEntry / ProcedureLoadDll.Methods / Util.HybridCLR / ConfigWindow.RightPanel.HybridCLR / EditorUtil.HybridCLR.Methods | DLL 条目字段名 |
| 6 Sound | ISoundRow / ISoundManager / SoundManagerBase / SoundManager 三件套 / SoundManagerConfig / SoundSettings / SoundComponent / Inspector | 接口 + 调用链 |
| 7 Table | TableManager / TableManager.Methods / TableSettings / TableComponentInspector | 显式接口实现 |
| 8 UI | IUIView / UIView / IUIViewRow / IUIManager（4 组重载）/ UIManager 四件套 / UIManagerBase / UIComponent / Inspector | assetLocation 参数全链路 |
| 9 Vibrate | VibrateSettings / VibrateManager.Methods / Inspector | 字段名 + DrawAssetLocationRow |
| 10 EditorUtil | EditorUtil.Draw.FieldUnitList / EditorUtil.Draw.SourceFileTree（旧方法删除）/ Inspector caller 收尾 + 跨 batch 编译追踪修复 | 旧方法删除、新方法统一 |

---

## 审查结论

**[CHECK-PASS]** 静态审查通过，可交付 qa-reviewer 运行时验证。

---

## 问题清单

### 已直接修复（本次审查已 Edit）

| # | 严重度 | 维度 | 文件:行号 | 描述 | 状态 |
|---|--------|------|---------|------|------|
| 1 | P4 | 规范/注释 | LocalizationComponentInspector.Methods.cs:194 | 注释残留 "AB / Asset 行" 应为 "Asset 地址行" | 已修复 |
| 2 | P4 | 规范/注释 | NetworkComponentInspector.Methods.cs:216 | 注释残留 "AB / Asset 行" 应为 "Asset 地址行" | 已修复 |

> 修复 diff 摘要：两处 `/// 绘制单个数据源文件的所有行：文件名行、数据导出行、类型导出行、AB / Asset 行。` → `/// 绘制单个数据源文件的所有行：文件名行、数据导出行、类型导出行、Asset 地址行。`

### 无 P0 / P1 问题

---

## 残留 grep 命中

| 关键字 | 命中位置 | 处置 |
|--------|---------|------|
| `ABPath` / `AssetName`（旧字段名） | Framework/Scripts/ 内 **0 命中**（DebugComponentInspector 中的 `AABPath` 是 Android App Bundle 缩写，与 AssetBundle ABPath 无关，不属本次清理范围） | 无需处理 |
| `DrawABPathRow` / `DrawAssetNameRow` | **0 命中** | 已清除 |
| `FindProperty("ABPath")` / `FindProperty("AssetName")` | **0 命中** | 已清除 |
| `FindPropertyRelative("ABPath")` / `FindPropertyRelative("AssetName")` | **0 命中** | 已清除 |
| `"AB路径"` / `"Asset名称"` | **0 命中** | 已清除 |
| `.ABPath` / `.AssetName`（属性访问） | Framework/Scripts/ 内 **0 命中** | 已清除 |
| `ABPath` / `AssetName` (Game/DataTypes 旧字段) | `SoundTest.cs` / `UITest.cs` / `LocalizationFonts.cs` / `TbUITest.cs` | **待 Batch 11 luban 重导覆盖**（见下节） |

---

## 越界回顾确认

### Batch 4 Network 越界 5 文件

越界文件：`TableSettings.cs` / `TableManager.cs` / `TableManager.Methods.cs`（原 Batch 7 范围）/ `EditorUtil.Localization.TextExporter.cs` / `LocalizationTextExporter.cs`（原 Batch 3 范围）

**方向 OK。** 改动内容与对应批次目标（ABPath/AssetName → AssetLocation 字段替换）完全一致，Batch 3/7 实际完成后 grep 确认 0 重复冲突，无竞争写入。

### Batch 10 续作 luban 改动：是否仍在 git diff

**仍在 git diff 终态中。** `SoundTest.cs` / `UITest.cs` / `LocalizationFonts.cs` / `TbUITest.cs` 这四个 luban 自动生成文件中的手动改动依然存在于工作区：

- 改动内容：`ABPath`/`AssetName` → `AssetLocation` 字段合并（正确方向）
- 风险评估：这些改动会被 Batch 11 luban 重导**全量覆盖**，属临时编译桥接，不影响最终交付正确性
- 当前状态：编译通过，不会引发 P0/P1，保留至 Batch 11 替换

### Batch 10 跨 batch 编译追踪修复

涉及文件：`UIManagerBase.cs` / `IUIGroup.cs` / `UIComponent.cs` / `UIManager.cs` / `VibrateManager.cs` / `ConfigComponentInspector.Methods.cs`

**方向 OK。** 这些均是批次边界与编译图边界不重合导致的必要联动修复，内容方向与各对应批次目标一致，无架构变动，无红线触碰。

---

## Batch 11 接力清单

以下文件中的 `ABPath` / `AssetName` → `AssetLocation` 更改，由手工改动临时占位，**需 Batch 11 Excel 改头 + luban 重导彻底替换**：

| 文件 | 改动位置 | 说明 |
|------|---------|------|
| `Assets/Game/Scripts/Runtime/DataTypes/Sounds/SoundTest.cs` | ABPath/AssetName → AssetLocation 字段与构造 | luban 生成，重导后自动覆盖 |
| `Assets/Game/Scripts/Runtime/DataTypes/UIs/UITest.cs` | ABPath/AssetName → AssetLocation 字段与构造 | luban 生成，重导后自动覆盖 |
| `Assets/Game/Scripts/Runtime/DataTypes/Localizations/Fonts/LocalizationFonts.cs` | ABPath/AssetName → AssetLocation 字段与构造 | luban 生成，重导后自动覆盖 |
| `Assets/Game/Scripts/Runtime/DataTypes/UIs/TbUITest.cs` | 删除 AUTO-GENERATED MAP PROPERTIES 尾块 | luban 生成，重导后自动覆盖 |

---

## 接口契约一致性核查结论

| 接口 | 确认结论 |
|------|---------|
| `IDataTableUnitSetting.AssetLocation` | Interface / DataTableUnitSettingBase / HostKeyUnitSetting / NetCmdUnitSetting / TableUnitSetting / DllAssetEntry 全部一致 ✓ |
| `IDataReceiver` 三方法（ReadDataAsset / ReadDataAssetAsync / ReadDataAssetSync）参数均为 `string assetLocation` | Interface / DataReceiver / LubanDataReceiver 全部一致 ✓ |
| `Util.HybridCLR.LoadAotMetadataAsync(string location)` / `LoadGameAssemblyAsync(string location)` | 唯一实现在 Util.HybridCLR.cs，ProcedureLoadDll.Methods.cs 通过 `entry.AssetLocation` 调用 ✓ |
| `IUIManager` 全部 OpenUIViewSync/Async/GetUIView/GetUIViews/HasUIView/IsLoadingUIView 重载 | Interface / UIManagerBase / UIManager 三层签名完全一致，`assetLocation` 参数贯穿 ✓ |
| `ISoundManager.PlaySound(string soundGroupName, string assetLocation, PlaySoundParams)` | Interface / SoundManagerBase / SoundManager 全部一致 ✓ |
| `ISoundRow.AssetLocation` | Interface 已定义，Luban 测试生成类 SoundTest.cs 已临时对齐（Batch 11 接管） ✓ |
| `IUIViewRow.AssetLocation` | Interface 已定义，UITest.cs 已临时对齐（Batch 11 接管） ✓ |
| `ILocalizationFontRow.AssetLocation` | Interface 已定义，LocalizationFonts.cs 已临时对齐（Batch 11 接管） ✓ |
| `IUIView.AssetLocation` | Interface + UIView.cs OnInit 参数 + Visitors m_AssetLocation 全部一致 ✓ |
| `WebSocketSettings.AutoReconnectFailedUIAssetLocation` | 旧 `AutoReconnectFailedUIABPath` + `AutoReconnectFailedUIAssetName` 合并为单字段，Inspector FindProperty 同步 ✓ |

---

## Component+Manager 架构合规核查结论

| 检查项 | 结论 |
|--------|------|
| UIManagerBase 修饰符 | `internal abstract class UIManagerBase : FrameworkManager, IUIManager` ✓ |
| SoundManagerBase 修饰符 | `internal abstract class SoundManagerBase : FrameworkManager, ISoundManager` ✓ |
| PrefabManagerBase 修饰符 | `internal abstract class PrefabManagerBase : FrameworkManager, IPrefabManager` ✓ |
| ConfigManagerConfig 无 [Serializable] / [SerializeField] | 纯 DTO，仅含公有字段 `string AssetLocation` ✓ |
| PrefabManagerConfig [Serializable] 标注 | 空扩展类，`[Serializable]` 是合法的 C# 序列化标记，不违反"ManagerConfig 禁 [SerializeField]"红线 ✓ |
| ManagerConfig 纯 DTO | 所有已检查的 ManagerConfig 无 Inspector 序列化字段 ✓ |

---

## HybridCLR 链路核查结论

| 检查项 | 结论 |
|--------|------|
| `Assembly.Load(bytes)` 唯一调用点 | 仅在 `Util.HybridCLR.LoadGameAssemblyAsync` 内（Util.HybridCLR.cs:120） ✓ |
| `RuntimeApi.LoadMetadataForAOTAssembly` 唯一调用点 | 仅在 `Util.HybridCLR.LoadAotMetadataAsync` 内（Util.HybridCLR.cs:70） ✓ |
| ProcedureLoadDll 改造后调用链 | `entry.AssetLocation` → `Util.HybridCLR.LoadAotMetadataAsync(entry.AssetLocation)` / `LoadGameAssemblyAsync(entry.AssetLocation)` 链路完整 ✓ |
| UniTask.Yield() 脱栈防 FSM 守卫冲突 | ProcedureLoadDll.Methods.cs RunLoadAsync 首行 `await UniTask.Yield()` 保留 ✓ |

---

## Inspector 三文件联动核查结论

| Inspector | FindProperty 名 | SP 字段名 | 绘制调用 | 结论 |
|-----------|----------------|---------|---------|------|
| ConfigComponentInspector | `"m_AssetLocation"` | `m_AssetLocationSP` | `DrawAssetLocationRow(m_AssetLocationSP)` | ✓ |
| SoundComponentInspector | `m_SoundUnitsSettings` → `"AssetLocation"` (via SourceFileTree) | `m_SoundUnitsSettings` | `DrawAssetLocationRow(detailProp)` | ✓ |
| UIComponentInspector | `m_UIUnitsSettings` → `"AssetLocation"` (via SourceFileTree) | `m_UIUnitsSettings` | `DrawAssetLocationRow(detailProp)` | ✓ |
| TableComponentInspector | `"AssetLocation"` (via SourceFileTree) | — | `DrawAssetLocationRow(detailProp)` | ✓ |
| NetworkComponentInspector | HostKeyUnits / NetCmdUnits → `"AssetLocation"` (via SourceFileTree) | — | `DrawAssetLocationRow(detailProp)` | ✓ |
| LocalizationComponentInspector | `"AssetLocation"` (via SourceFileTree) | — | `DrawAssetLocationRow(detailProp)` | ✓ |
| VibrateComponentInspector | `"AssetLocation"` (via SourceFileTree 或直接) | — | `DrawAssetLocationRow(detailProp)` | ✓ |

无任何 Inspector 仍走 `m_ConfigSP.FindPropertyRelative("ABPath")` 或 `FindPropertyRelative("AssetName")` 嵌套路径。

---

## 编译验证

- UnityMCP `read_console` error 数：**0**
- 关键 warning：未检测到与本次改造相关的 warning
- 结论：编译完全通过

---

## 已确认无问题的模块

- [x] Batch 1 Core：IDataTableUnitSetting / DataTableUnitSettingBase / IDataReceiver / DataReceiver / LubanDataReceiver
- [x] Batch 2 Config：ConfigManagerConfig / ConfigManager / ConfigComponent / Inspector 三件套
- [x] Batch 3 Localization：ILocalizationFontRow / LocalizationFontData / LocalizationManager / Inspector / Exporter
- [x] Batch 4 Network：HostKeyUnitSetting / NetCmdUnitSetting / NetworkManager / WebSocketSettings / Inspector
- [x] Batch 5 Procedure：DllAssetEntry / ProcedureLoadDll.Methods / Util.HybridCLR / ConfigWindow.RightPanel.HybridCLR
- [x] Batch 6 Sound：ISoundRow / ISoundManager / SoundManagerBase / SoundManager / Inspector
- [x] Batch 7 Table：TableUnitSetting / TableManager / Inspector
- [x] Batch 8 UI：IUIView / UIView / IUIViewRow / IUIManager / UIManagerBase / UIManager / UIComponent / Inspector
- [x] Batch 9 Vibrate：VibrateSettings / VibrateManager.Methods / Inspector
- [x] Batch 10 EditorUtil：SourceFileTree（旧方法已删、DrawAssetLocationRow 统一）/ FieldUnitList / 所有 Inspector caller

---

## 重点审查项

- **重构一致性**：已 grep 全 Framework/Scripts 下所有 .cs，旧字段名 ABPath / AssetName / m_ABPath / m_AssetName / "AB路径" / "Asset名称" / DrawABPathRow / DrawAssetNameRow / FindProperty("ABPath") 等全部 0 命中
- **接口契约**：8 组接口/实现链全部对齐，含显式接口实现（HostKeyUnitSetting / NetCmdUnitSetting / TableUnitSetting）
- **HybridCLR 唯一性**：Assembly.Load / LoadMetadataForAOTAssembly 两个调用点约束完整保持
- **架构合规**：三层继承（internal abstract ManagerBase → internal sealed Manager）未破坏，ManagerConfig 仍为纯 DTO
- **Inspector 三文件联动**：7 个 Inspector 模块全部通过 DrawAssetLocationRow 统一绘制，FindProperty 名与序列化字段名对齐
- **编译验证**：UnityMCP read_console 0 error
- **注释同步**：2 处 P4 注释残留已直接修复（"AB / Asset 行" → "Asset 地址行"）

---

## 结论

**[CHECK-PASS]** 静态审查通过，可交付 qa-reviewer 运行时验证。

**Batch 11 待处理**：`SoundTest.cs` / `UITest.cs` / `LocalizationFonts.cs` / `TbUITest.cs` 4 个 luban 自动生成文件的手动临时改动需 Excel 改头 + luban 重导彻底替换，不影响本次合入。
