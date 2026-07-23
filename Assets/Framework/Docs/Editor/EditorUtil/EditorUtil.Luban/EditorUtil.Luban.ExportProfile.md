# EditorUtil.Luban.ExportProfile

**类签名**：`internal sealed class LubanExportProfile` + `internal static class LubanExportProfiles`  
**命名空间**：`NovaFramework.Editor`

Luban 导出 Profile 是 Nova Editor 导出链中固定身份的单一真相源。它不参与 Unity 序列化，也不属于项目业务配置。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Luban.ExportProfile.cs` | `LubanExportProfile` | 保存 Id、target、manager 和模板键 |
| `EditorUtil.Luban.ExportProfile.cs` | `LubanExportProfiles` | 集中提供九个 Nova 内置导出目标 |

---

## §4 Profile 字段

| 字段 | 说明 |
|---|---|
| `Id` | Nova 内部稳定标识，当前与 target 相同 |
| `TargetName` | `luban.conf` target 名称和 CLI `-t` 参数 |
| `ManagerName` | Luban 生成的 Tables 管理类名称 |
| `TemplateKey` | `Templates/Luban/<key>` 模块模板目录键 |

---

## §5 内置目录

| Profile | target | manager | template key |
|---|---|---|---|
| `Table` | `table` | `TableTables` | `table` |
| `Sound` | `sound` | `SoundTables` | `sound` |
| `UI` | `ui` | `UITables` | `ui` |
| `NetworkCmd` | `network-cmd` | `NetworkTables` | `network-cmd` |
| `NetworkHostKey` | `network-hostkey` | `HostKeyTables` | `network-hostkey` |
| `LocalizationText` | `localization-text` | `LocalizationTextTables` | `localization-text` |
| `LocalizationFont` | `localization-font` | `LocalizationFontTables` | `localization-font` |
| `VibrateEmphasis` | `vibrate-emphasis` | `VibrateEmphasisTables` | `vibrate-emphasis` |
| `VibrateCustom` | `vibrate-custom` | `VibrateCustomTables` | `vibrate-custom` |

---

## §9 使用规则

- Framework 内部 Exporter、Inspector 和 Pipify 不得手写上述 target、manager 或模板键。
- Profile 只保存所有项目都相同的框架事实；源路径、输出路径、地域和目标 Unit 继续放在单次导出上下文中。
- Framework 内部只通过 Profile 构建上下文，不提供由调用方拼接 `targetName` / `managerName` 的 public 入口。
- Profile 不改变 Luban 生成内容、Runtime Settings、Prefab 序列化或模块运行时 API。

---

## §13 关联文档

- [EditorUtil.Luban.ExportHelper.md](EditorUtil.Luban.ExportHelper.md)
- [EditorUtil.Luban.Pipeline.md](EditorUtil.Luban.Pipeline.md)
- [EditorUtil.Luban.ConfigSyncer.md](EditorUtil.Luban.ConfigSyncer.md)
