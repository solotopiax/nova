# TextLocalizingValidator

**类签名**：`internal static class TextLocalizingValidator`
**命名空间**：`NovaFramework.Editor`
**入口**：`LocalizationComponent` Inspector 面板 → "修复预制体缺失 TextLocalizing" 按钮

全工程预制体扫描并补挂缺失 TextLocalizing 的 Inspector 按钮工具。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Editor/Inspectors/CustomInspectors/TextLocalizingInspector/TextLocalizingValidator.cs` | `internal static class TextLocalizingValidator` | Inspector 按钮工具（不挂 `[MenuItem]`），扫描 Assets/ 下所有 Prefab（含 inactive 节点），为缺失 `TextLocalizing` 的 `TextMeshProUGUI` 节点补挂并保存 |

---

## 继承关系

```
(无继承 -- 静态工具类)
```

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_DefaultFontMark` | `const string` | `"Main"` | 补挂时设置的默认字体标记 |

---

## 完整公开 API

本类为 `internal static`，无对外公开 API。以下为内部方法：

```csharp
// 扫描工程内所有预制体，为缺失 TextLocalizing 的 TMP 节点补挂并保存。
// 使用 AssetDatabase.FindAssets("t:Prefab") 枚举全量 Prefab，
// PrefabUtility.LoadPrefabContents 打开可写编辑上下文，
// 为缺失 TextLocalizing 的 TMP 节点 AddComponent 并设置 m_LocalizingFontMark = "Main"，
// 有改动时 SaveAsPrefabAsset，最终统一 AssetDatabase.SaveAssets/Refresh。
// 不走 Undo（Prefab 资产侧操作不适合 Undo）。
// 由 LocalizationComponent Inspector 面板"修复预制体缺失 TextLocalizing"按钮调用。
internal static void FixMissingInPrefabs()
```

---

## 关键算法

```
FixMissingInPrefabs()
  ├─ AssetDatabase.FindAssets("t:Prefab") → 全量 Prefab GUID 数组
  ├─ 遍历每个 GUID（带进度条）：
  │     ├─ GUIDToAssetPath → path
  │     ├─ try { PrefabUtility.LoadPrefabContents(path) → root }
  │     ├─ root.GetComponentsInChildren<TextMeshProUGUI>(true)
  │     ├─ 对每个 TMP：
  │     │     ├─ 已有 TextLocalizing → continue
  │     │     ├─ AddComponent<TextLocalizing>（不走 Undo）
  │     │     ├─ SerializedObject → FindProperty("m_LocalizingFontMark")
  │     │     │     └─ 为空时设置为 "Main" → ApplyModifiedPropertiesWithoutUndo()
  │     │     ├─ prefabDirty = true; fixedNodeCount++
  │     │     └─ (如有 prefabDirty) PrefabUtility.SaveAsPrefabAsset; fixedPrefabCount++
  │     ├─ catch (Exception ex) → Log.Warning(path, ex.Message)，继续下一个
  │     └─ finally → PrefabUtility.UnloadPrefabContents(root)
  ├─ ClearProgressBar
  ├─ AssetDatabase.SaveAssets() + AssetDatabase.Refresh()
  ├─ fixedNodeCount > 0：
  │     ├─ Log.Debug 输出修复预制体数 + 节点数
  │     └─ EditorUtility.DisplayDialog("修复完成", ...)
  └─ fixedNodeCount == 0：
        └─ EditorUtility.DisplayDialog("验证通过", ...)
```

---

## 使用示例

通过 LocalizationComponent Inspector 面板执行：

```
在 Hierarchy 中选中挂载 LocalizationComponent 的 GameObject
→ Inspector 面板 → 字体自动适配 下方
→ 点击"修复预制体缺失 TextLocalizing"按钮
```

适用场景：
- 项目中引入 TextLocalizing 自动挂载机制之前，已存在大量 TMP Prefab 节点
- 从外部导入的 Prefab 中 TMP 未挂载 TextLocalizing
- 批量验证工程内所有预制体的本地化配置完整性

---

## 注意事项

| 事项 | 说明 |
|------|------|
| 作用于全工程预制体 | 扫描 Assets/ 下所有 Prefab，不限场景、不限目录 |
| 包含未激活对象 | `GetComponentsInChildren(true)` 确保隐藏的节点也会被扫描 |
| 不走 Undo | Prefab 资产侧 AddComponent 不经过 Undo 系统，操作不可通过 Ctrl+Z 撤销 |
| 逐 Prefab 异常隔离 | 单个 Prefab 失败时 Log.Warning 后继续，不中断整个流程 |
| LoadPrefabContents / UnloadPrefabContents 配对 | try/finally 保护，保证上下文始终释放 |
| 不覆盖已有配置 | 仅在 `m_LocalizingFontMark` 为空时设置默认值，已有 TextLocalizing 的节点不做任何修改 |

---

## 关联文档

- [TextLocalizing.md](../../../Runtime/Modules/Localization/TextLocalizing.md)
- [TextLocalizingAutoMount.md](TextLocalizingAutoMount.md)
- [TextLocalizingInspector.md](TextLocalizingInspector.md)
