# 文档同步审计报告：ABPath+AssetName → AssetLocation 合并

**日期**：2026-05-17
**范围**：`Assets/Framework/Docs/` 全树（L0/L1/L2 三层）
**对应代码变更**：Batch 1-10 代码合并（ABPath+AssetName 双字段寻址 → AssetLocation 单字段）

---

## 总览

| 指标 | 结果 |
|------|------|
| 修改文档数量 | 50+ 个 .md 文件 |
| 最终 grep 命中（旧字段名） | 3 条，均为合理豁免 |
| 代码与文档不一致项 | 0（已全部修复） |

---

## 豁免说明（最终 grep 剩余 3 条）

| 文件 | 内容 | 豁免原因 |
|------|------|--------|
| `Runtime/Core/Path/Path.AB.md:14` | `AB 路径相关静态类` | 描述的是 AssetBundle 路径工具类 `Path.AB`，与 ABPath 字段无关 |
| `Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md:132` | `旧版 DllAssetEntry 有独立的 ABPath...` | §10 常见误区的历史对比说明，属于故意保留的迁移记录 |
| `Editor/Inspectors/DebugComponentInspector/DebugComponentInspector.md:84` | `aabPath` 参数名 | Android App Bundle (AAB) 路径参数，与本次合并的 ABPath 字段无关 |

---

## 修改文件清单

### L0/L1 层

| 文件 | 修改内容 |
|------|---------|
| `ARCHITECTURE.md` | ConfigManager.Initialize 参数：`ABPath, AssetName` → `AssetLocation`；LoadAsync 流程中 ABPath/AssetName 引用 |
| `INDEX.md` | ConfigManagerConfig、DllAssetEntry、VibrateUnitSetting、SoundUnitSetting 四条条目描述 |

### L2 层 — Runtime

| 文件 | 修改内容 |
|------|---------|
| `Runtime/Core/Table/IDataReceiver.md` | 3 个方法签名从双参数 → 单 `assetLocation` 参数；删除不在接口中的方法 |
| `Runtime/Core/Table/IDataTableUnitSetting.md` | `ABPath + AssetName` 属性 → `AssetLocation` |
| `Runtime/Core/Table/DataTableUnitSettingBase.md` | §4 字段表，§5 API |
| `Runtime/Core/Table/DataReceiver.md` | 委托签名，构造器（2 个独立 2-param ctor），所有方法参数，算法流程 |
| `Runtime/Core/Table/LubanDataReceiver.md` | §4 字段（`m_Cache: LubanDataCache`），§5 两个 4-param 构造器，§9 算法，§11/§12 |
| `Runtime/Core/Path/Path.GMTool.md` | 删除 ABPath+AssetName 两常量，保留单 `AssetLocation` 常量 |
| `Runtime/Utils/Util.HybridCLR.md` | 两个方法签名单参数化，§9 算法（LoadRawAsync），§10 误区，§11 示例 |
| `Runtime/Modules/Procedure/Procedures/DllAssetEntry.md` | §5 字段：ABPath+AssetName → AssetLocation |
| `Runtime/Modules/Procedure/ProcedureComponent.md` | §2 文件表 DllAssetEntry 注释 |
| `Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md` | 步骤 3/4 算法，Editor 行为注释 |
| `Runtime/Modules/UI/UIViewManager/Definitions/IUIView.md` | 属性表，OnInit 签名 |
| `Runtime/Modules/UI/UIViewManager/Definitions/UIView.md` | 字段表，OnInit 签名 |
| `Runtime/Modules/UI/UIViewManager/UIViewManager.md` | 接口块，OnInit，UIView 字段注释 |
| `Runtime/Modules/UI/UIGroupHelper/Definitions/IUIGroup.md` | Has/Get/GetAll 方法签名 |
| `Runtime/Modules/UI/UIManager/Interfaces/IUIManager.md` | 全部 Open/Get/Has/IsLoading 签名 |
| `Runtime/Modules/UI/UIManager/Definitions/IUIViewRow.md` | 接口字段，示例 |
| `Runtime/Modules/UI/UIComponent.md` | §4 描述，Start() 时序，全部 Open/Get 签名 |
| `Runtime/Modules/UI/UIManager/UIManager.md` | §4 字段，§5 Open，§8 时序，§9 算法 |
| `Runtime/Modules/UI/UIManager/Implements/UIManagerBase.md` | 便捷 override 描述 |
| `Runtime/Modules/UI/UIManager/Definitions/UISettings.md` | §11 示例 |
| `Runtime/Modules/Config/Definitions/ConfigManagerConfig.md` | §1 描述，§5 字段，§11 示例 |
| `Runtime/Modules/Config/ConfigComponent.md` | §2 Visitors 注释，§4 字段，§8 时序 |
| `Runtime/Modules/Config/ConfigManager.md` | §2 文件表，§4 字段，§8/§9 时序，§12 注意事项 |
| `Runtime/Modules/Network/Definitions/NetworkSettings.md` | HostKeyUnitSetting 字段表，显式接口实现 |
| `Runtime/Modules/Network/WebSocketManager/Definitions/WebSocketManagerConfig.md` | 字段表，API 块 |
| `Runtime/Modules/Network/NetworkComponent.md` | 初始化时序 |
| `Runtime/Modules/Network/NetworkManager/NetworkManager.md` | LubanDataReceiver 构造算法 |
| `Runtime/Modules/Sound/ISoundRow.md` | 接口，示例 |
| `Runtime/Modules/Sound/SoundUnitSetting.md` | §1 描述，§4 字段，§5 API，§11 示例 |
| `Runtime/Modules/Sound/SoundComponent.md` | LoadSoundDataAsync 描述，PlaySound，§12 误区 |
| `Runtime/Modules/Sound/ISoundManager.md` | Initialize 描述，PlaySound |
| `Runtime/Modules/Sound/SoundManager.md` | §4 字段描述，§5 描述，§9 算法 |
| `Runtime/Modules/Sound/SoundManagerConfig.md` | 注释 |
| `Runtime/Modules/Sound/PlaySoundInfo.md` | 示例 |
| `Runtime/Modules/Localization/LocalizationManagerConfig.md` | SupportedLanguagesABPath+AssetName → AssetLocation |
| `Runtime/Modules/Localization/ILocalizationFontRow.md` | 接口，示例 |
| `Runtime/Modules/Localization/LocalizationFontData.md` | §5，§11 |
| `Runtime/Modules/Localization/LocalizationManager.md` | 算法流程 |
| `Runtime/Modules/Localization/TextLocalizing.md` | 算法流程（字体加载/材质加载） |
| `Runtime/Modules/Table/Definitions/TableSettings.md` | §4 字段，§5 TableUnitSetting |
| `Runtime/Modules/Table/TableManager.md` | §9 算法 3 处 |
| `Runtime/Modules/Vibrate/VibrateUnitSetting.md` | §5 描述，§11 示例 |
| `Runtime/Modules/Vibrate/IVibrateManager.md` | Initialize 描述 |

### L2 层 — Editor

| 文件 | 修改内容 |
|------|---------|
| `Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md` | 步骤 4 中 `entry.AssetName`/`entry.ABPath` → `entry.AssetLocation`；CopyDllEntries 算法；StripDllSuffix 参数名；§10 误区 2 重写；§13 DllAssetEntry 链接描述 |
| `Editor/EditorUtil/EditorUtil.Draw/EditorUtil.Draw.SourceFileTree.md` | §4 字段名（`s_` 前缀改为公开，默认值修正）；`c_FieldLabelWidth` 描述；`DrawABPathRow`/`DrawAssetNameRow` → `DrawAssetLocationRow`；`DrawDefaultSourceFileRow` 描述；§11 示例 |
| `Editor/Inspectors/ConfigComponentInspector/ConfigComponentInspector.md` | §1 描述，§2 Visitors 字段，§4 字段表，§5 OnEnable 注释，§9 DrawConfigs 流程，§11 Inspector 示意图 |
| `Editor/Inspectors/LocalizationComponentInspector/LocalizationComponentInspector.md` | §4 字段：ABPath+AssetName → AssetLocation；§9 文件行描述 |
| `Editor/Inspectors/SoundComponentInspector/SoundComponentInspector.md` | §9 文件行表，§11 Inspector 示意图 |
| `Editor/Inspectors/VibrateComponentInspector/VibrateComponentInspector.md` | §4 删除旧布局常量；`DrawVibrateUnitExport` → `DrawVibrateSourceDataOperations`；§11 示例 |
| `Editor/Inspectors/UIComponentInspector/UIComponentInspector.md` | §4 删除旧颜色/布局常量；§9 文件行描述，`AssetName` → `AssetLocation`；§11 示意图 |
| `Editor/Inspectors/TableComponentInspector/TableComponentInspector.md` | §9 文件行描述；§11 示意图 |
| `Editor/Windows/ConfigWindow.md` | HybridCLR 面板布局：双行 ABPath+AssetName → 单行 Asset 地址 |

---

## 关键代码变更摘要（与文档对应的代码真相）

| 变更点 | 旧代码 | 新代码 |
|--------|--------|--------|
| `DllAssetEntry` 字段 | `m_ABPath + m_AssetName` | `m_AssetLocation` |
| `DataReceiver` 委托 | `LoadAssetAsyncFunc(string abPath, string assetName)` | `LoadAssetAsyncFunc(string assetLocation)` |
| `DataReceiver` 构造器 | 1个 3-param ctor（混合 async/sync） | 2个独立 2-param ctor（async only / sync only） |
| `LubanDataReceiver` 构造器 | 8/9 param 含独立 abPath, assetName | 4-param `(LubanDataCache, IDataTableUnitSetting, LoadAsset*Func, ReleaseAssetAction)` |
| `EditorUtil.Draw.SourceFileTree` | `DrawABPathRow` + `DrawAssetNameRow` | `DrawAssetLocationRow` |
| `EditorUtil.HybridCLR.StripDllSuffix` | `string assetName` | `string assetLocation` |
| `ConfigWindow` HybridCLR 条目绘制 | 双行 ABPath+AssetName | 单行 Asset 地址（labelWidth=80f） |

---

## 结论

[DOCS-PASS] 全树扫描完成，旧字段名 grep 命中为 0（豁免 3 条均已确认合理）。所有文档内容均已从对应 `.cs` 源文件提取验证，无凭印象编写内容。
