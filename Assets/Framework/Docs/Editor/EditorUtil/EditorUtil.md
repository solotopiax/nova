# EditorUtil

**类签名**：`public static partial class EditorUtil`
**命名空间**：`NovaFramework.Editor`

编辑器工具集聚合入口，通过 `static partial class` 拆分为多个子工具类，统一由 `EditorUtil.*` 命名空间访问。

---

## 文件

| 文件 | 子类 | 说明 |
|------|------|------|
| `EditorUtil.cs` | `EditorUtil.Initializer` | `[InitializeOnLoad]`：注入 TxtHelper + LogHelper |
| [`EditorUtil.Draw/`](EditorUtil.Draw/EditorUtil.Draw.md) | `EditorUtil.Draw.*` | Inspector GUI 绘制工具集（Label/Field/Button/Toggle/Slider/Foldout/HelpBox/Toolbar/Layout） |
| [`EditorUtil.FileSystem/`](EditorUtil.FileSystem/EditorUtil.FileSystem.md) | `EditorUtil.FileSystem` | 文件系统封装（路径转换、目录创建、AssetDatabase 刷新） |
| [`EditorUtil.Serializer/`](EditorUtil.Serializer/EditorUtil.Serializer.md) | `EditorUtil.Serializer` | 反射读写 Component 私有字段（RuntimeDrawer 专用） |
| [`EditorUtil.ScriptingDefineSymbols/`](EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md) | `EditorUtil.ScriptingDefineSymbols` | 脚本宏定义增删查工具（跨平台批量操作，`NamedBuildTarget` API） |
| [`EditorUtil.TypeCache/`](EditorUtil.TypeCache/EditorUtil.TypeCache.md) | `EditorUtil.TypeCache` | 编辑器类型缓存（反射收集实现类名称，编译后自动刷新） |
| [`EditorUtil.PlugPals/`](EditorUtil.PlugPals/EditorUtil.PlugPals.md) | `EditorUtil.PlugPals` | 私有 Verdaccio 仓库 UPM 包管理工具层（远程包拉取、manifest 读写、包安装/卸载、CHANGELOG 拉取） |
| [`EditorUtil.CheckUpdate/`](EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) | `EditorUtil.CheckUpdate` | UPM 包版本检查工具（启动自动检查 + MarkSkip 持久化，复用 PlugPals 网络层） |
| [`EditorUtil.AndroidResolver/`](EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md) | `EditorUtil.AndroidResolver` | Android 依赖解析工具（反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Assets/GeneratedLocalRepo/**） |

| [`EditorUtil.FileWatcher/`](EditorUtil.FileWatcher/EditorUtil.FileWatcher.md) | `EditorUtil.FileWatcher` | 编辑器侧目录监控工具，供各 Inspector 监听 `_configs/` 或源目录变更 |
| [`EditorUtil.CsvExporter/`](EditorUtil.CsvExporter/EditorUtil.CsvExporter.md) | `EditorUtil.CsvExporter` | 将结构化数据导出为 CSV 文件 |
| [`EditorUtil.Asmdef/`](EditorUtil.Asmdef/EditorUtil.Asmdef.md) | `EditorUtil.Asmdef` | Assembly Definition 命名空间解析（向上查找 .asmdef，提取 rootNamespace/name） |
| [`EditorUtil.Excel/`](EditorUtil.Excel/EditorUtil.Excel.md) | `EditorUtil.Excel` | Excel 读写工具（ExcelDataReader 读取 + EPPlus 写入） |
| [`EditorUtil.Luban/`](EditorUtil.Luban/EditorUtil.Luban.Pipeline.md) | `EditorUtil.Luban.*` | Luban 导出工具集（CliRunner/ConfigSyncer/JsonMerger/MapPropGen/Pipeline） |
| [`EditorUtil.Config/`](EditorUtil.Config/EditorUtil.Config.Exporter.md) | `EditorUtil.Config.*` | Config 导出工具集（Exporter/Validator/SDKPluginScanner/KitConfigScanner/StructureGuard/RuntimeProvider） |
| [`EditorUtil.Table/`](EditorUtil.Table/EditorUtil.Table.Exporter.md) | `EditorUtil.Table.*` | Table 模块 Luban 导出入口（ExportAll/ExportCode/ExportData） |
| [`EditorUtil.UI/`](EditorUtil.UI/EditorUtil.UI.Exporter.md) | `EditorUtil.UI.*` | UI 模块 Luban 导出入口（ExportAll/ExportCode/ExportData/ExportCodeForFile/ExportDataForFile） |
| [`EditorUtil.Localization/`](EditorUtil.Localization/EditorUtil.Localization.TextExporter.md) | `EditorUtil.Localization.*` | 本地化导出工具集（TextExporter 文本三阶段 + FontExporter 字体标准 Pipeline） |
| [`EditorUtil.Network/`](EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md) | `EditorUtil.Network.*` | Network 导出工具集（HostKeyExporter / NetCmdExporter / ProtoExporter） |
| [`EditorUtil.Sound/`](EditorUtil.Sound/EditorUtil.Sound.Exporter.md) | `EditorUtil.Sound.*` | Sound 模块 Luban 导出流水线薄封装（ExportAll/ExportData/ExportCode） |
| [`EditorUtil.Vibrate/`](EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md) | `EditorUtil.Vibrate.*` | Vibrate 模块 Luban 导出工具（Emphasis + Custom 双轨，各提供 Data/Code/All） |
| [`EditorUtil.BundleBuilder/`](EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) | `EditorUtil.BundleBuilder` | YooAsset ScriptableBuildPipeline 资源包构建薄封装（11 项参数对齐 Bundle Builder 窗口） |

---

## Initializer 初始化

```csharp
[InitializeOnLoad]
public static class Initializer
{
    static Initializer()
    {
        // 注入 TxtHelper → Txt.Format 在编辑器模式下可用
        var txtHelper = Util.TypeCreator.Create<ITxtHelper>(typeName);
        txtHelper.Initialize();
        Txt.SetHelper(txtHelper);

        // 注入 LogHelper → Log.* 在编辑器模式下可用
        var logHelper = Util.TypeCreator.Create<ILogHelper>(typeName);
        logHelper.Initialize();
        Log.SetHelper(logHelper);
    }
}
```

> 如果 Inspector 中出现 `Txt.Format` NullReferenceException，检查 `EditorUtil.Initializer` 是否正常加载（确认程序集引用正确）。

---

## 关联文档

- [Editor.md](../Editor.md)
- [BaseComponentInspector.md](../Inspectors/BaseComponentInspector.md)
- [EditorUtil.BundleBuilder.md](EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)
