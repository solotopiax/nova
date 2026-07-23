# VibrateComponentInspector

**类签名**：`[CustomEditor(typeof(VibrateComponent))] internal sealed partial class VibrateComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.VibrateComponent`

振动组件 Inspector，提供 Emphasis 和 Custom 两个独立的 Luban 管线导出 UI 和运行时振动调试面板。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `VibrateComponentInspector.cs` | `VibrateComponentInspector` | 主体：`OnEnable`（绑定属性 + 收集管理器类型名列表）、`OnInspectorGUI`（依次调用 DrawConfigs → DrawEmphasisVibrateExport → DrawCustomVibrateExport → DrawRuntimeInfos → FinalRefreshInspectorGUI） |
| `VibrateComponentInspector.Visitors.cs` | `VibrateComponentInspector` | 静态颜色/宽度常量、模板文件名常量、`SerializedProperty` 字段（6 个）、`m_ManagerTypeNames`、2 个静态 `GUIStyle`、`m_IsLubanConfigExists`、8 个运行时调试字段 |
| `VibrateComponentInspector.Methods.cs` | `VibrateComponentInspector` | 全部私有方法（见 §5） |

---

## §3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector
        └── VibrateComponentInspector (sealed partial)
```

---

## §4 关键字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_EmphasisTemplateFileName` | `const string` | `"VibrateEmphasisTemplate.xlsx"` | Emphasis 模板文件名 |
| `c_CustomTemplateFileName` | `const string` | `"VibrateCustomTemplate.xlsx"` | Custom 模板文件名 |
| `m_CurManagerTypeName` | `SerializedProperty` | — | 当前管理器类型名称 |
| `m_Settings` | `SerializedProperty` | — | 振动设置整体 |
| `m_EmphasisSourceDirPath` | `SerializedProperty` | — | Emphasis 数据源目录路径 |
| `m_CustomSourceDirPath` | `SerializedProperty` | — | Custom 数据源目录路径 |
| `m_EmphasisUnitsSettings` | `SerializedProperty` | — | Emphasis 振动单元设置列表 |
| `m_CustomUnitsSettings` | `SerializedProperty` | — | Custom 振动单元设置列表 |
| `m_ManagerTypeNames` | `List<string>` | — | `IVibrateManager` 所有实现类名称 |
| `s_ContentStyle` | `static GUIStyle` | `null` | 导出区域内容行标签样式（延迟初始化） |
| `s_ContentFieldStyle` | `static GUIStyle` | `null` | 导出区域输入框样式（延迟初始化） |
| `m_IsLubanConfigExists` | `bool` | `false` | Luban `_configs/` 目录是否存在 |
| `m_DebugVibrateType` | `VibrateType` | `VibrateType.Selection` | 运行时调试：预设振动类型 |
| `m_DebugIntensity` | `float` | `0.5f` | 运行时调试：Custom 振动强度 |
| `m_DebugSharpness` | `float` | `0.5f` | 运行时调试：Custom 振动锐度 |
| `m_DebugDuration` | `float` | `0.3f` | 运行时调试：Custom 振动持续时间 |
| `m_DebugAmplitude` | `float` | `0.5f` | 运行时调试：Emphasis 振幅 |
| `m_DebugFrequency` | `float` | `0.5f` | 运行时调试：Emphasis 频率 |
| `m_DebugPreDuration` | `float` | `0f` | 运行时调试：振动前置延迟 |
| `m_DebugInterval` | `float` | `0.1f` | 运行时调试：Emphasis 间隔 |

---

## §5 完整公开 API

**继承自 `BaseComponentInspector`：**

| 签名 | 说明 |
|------|------|
| `protected override void OnEnable()` | 绑定 SerializedProperty（含 `m_EmphasisUnitSetting`/`m_CustomUnitSetting` 列表）、收集 `IVibrateManager` 实现类名、检查 Luban 配置目录是否存在 |
| `public override void OnInspectorGUI()` | 依次调用 `DrawConfigs → DrawEmphasisVibrateExport → DrawCustomVibrateExport → DrawRuntimeInfos → FinalRefreshInspectorGUI` |

**私有方法（Methods.cs）：**

| 签名 | 说明 |
|------|------|
| `private void DrawConfigs()` | 绘制管理器类型选择器 |
| `private void DrawEmphasisVibrateExport()` | 绘制 Emphasis 振动表格导出区域（调用 `DrawVibrateUnitExport`） |
| `private void DrawCustomVibrateExport()` | 绘制 Custom 振动表格导出区域（调用 `DrawVibrateUnitExport`） |
| `private void DrawVibrateSourceDataOperations(string sectionTitle, string foldoutKey, string templateFileName, SerializedProperty sourceDirPathProp, SerializedProperty unitsSettingsProp, Dictionary<string, bool> foldoutState)` | 绘制单个振动区域的完整导出区域：Foldout、模板路径提示、表格目录位置、数据源文件树、全局导出按钮 |
| `private static void EnsureStylesInitialized()` | 延迟初始化两个静态 GUIStyle |
| `private void DrawVibrateSourceFileRow(...)` | 组合文件名、数据导出、类型导出和 Asset 地址四行 |
| `private void OnExportDataForFile(...)` | 按 Emphasis / Custom 调用对应 `EditorUtil.Vibrate` 数据导出入口 |
| `private void OnExportClassForFile(...)` | 按 Emphasis / Custom 调用对应 `EditorUtil.Vibrate` 代码导出入口 |
| `private void DoExportAllDataAndTypes(...)` | 应用 Inspector 改动后调用对应 `EditorUtil.Vibrate.Export*All` |
| `private VibrateSettings GetVibrateSettings()` | 反射获取 `VibrateComponent.m_Settings` 字段值 |
| `private void DrawRuntimeInfos()` | 仅运行态：绘制 Enable/IsSupported 标签、预设振动按钮、Custom/Emphasis 调试区、StopAll 按钮 |
| `private void DrawPresetVibrateButtons(VibrateComponent component)` | 遍历 `VibrateType` 枚举绘制预设振动测试按钮 |
| `private void DrawCustomVibrateSection(VibrateComponent component)` | Custom 振动调试区（Intensity/Sharpness/PreDuration/Duration 输入框 + Play 按钮） |
| `private void DrawEmphasisVibrateSection(VibrateComponent component)` | Emphasis 振动调试区（Amplitude/Frequency/PreDuration/Interval 输入框 + Play 按钮） |

---

## §11 使用示例

```csharp
// 在 Unity Inspector 中，VibrateComponent 挂载后自动显示此面板
// 编辑态操作：
// 1. 配置区：选择 Manager 类型
// 2. Emphasis 振动表格导出区：
//    - 选择表格目录（源 Excel 所在文件夹）
//    - 配置数据导出路径、类型导出路径、Asset 地址
//    - 点击"导出数据"/"导出类型"/"导出全部"触发对应 Luban 管线
// 3. Custom 振动表格导出区（同上，独立配置）
// 4. 运行态调试区（Play Mode 可见）：
//    - 查看 Enable / IsSupported 状态
//    - 点击预设振动按钮（Selection / Success / MediumImpact 等）
//    - 调节参数后点击 Play Custom / Play Emphasis
//    - Stop All 立即停止所有振动
```

---

## §12 注意事项

- `GUIStyle` 在 `OnEnable` 时 `EditorStyles` 尚未就绪，必须在 `EnsureStylesInitialized()` 中延迟初始化
- Inspector 不执行 Sheet 名刷新；`EditorUtil.Vibrate` 导出器进入 Pipeline 后扫描 Excel 并构建本次 schema manifest
- 单文件和全量入口都先生成到各区域的 `_temp/_publish`，验证后通过 `FileSystem.OutputApplier` 发布，Inspector 不直接修改正式产物
- Emphasis 与 Custom 使用各自固定的 Luban Profile、暂存目录和发布事务，后续 ConfigSyncer、文件筛选和 Map 属性生成共享各自快照

---

## §13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [VibrateComponent.md](../../../Runtime/Modules/Vibrate/VibrateComponent.md)
- [VibrateSettings.md](../../../Runtime/Modules/Vibrate/VibrateSettings.md)
- [EditorUtil.Luban.Pipeline.md](../../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
