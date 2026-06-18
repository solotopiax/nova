# TextLocalizingInspector

**类签名**：`[CustomEditor(typeof(TextLocalizing))] [CanEditMultipleObjects] internal sealed class TextLocalizingInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.TextLocalizing`

TextLocalizing 组件的 Inspector 面板定制。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Editor/Inspectors/CustomInspectors/TextLocalizingInspector/TextLocalizingInspector.cs` | `internal sealed class TextLocalizingInspector` | 主体：`OnEnable` 绑定属性并加载预览数据和字体标记选项，`OnInspectorGUI` 绘制配置字段和预览区 |
| `Editor/Inspectors/CustomInspectors/TextLocalizingInspector/TextLocalizingInspector.Visitors.cs` | 同上（partial） | 字段与 SerializedProperty 缓存 |
| `Editor/Inspectors/CustomInspectors/TextLocalizingInspector/TextLocalizingInspector.Methods.cs` | 同上（partial） | 具体绘制逻辑与数据加载方法 |

---

## 继承关系

```
UnityEditor.Editor
  +-- BaseComponentInspector (abstract, NovaFramework.Editor)
        +-- TextLocalizingInspector (sealed)
```

---

## 关键字段表

### SerializedProperty（与 TextLocalizing 字段对应）

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_LocalizingKeyName` | `SerializedProperty` | 本地化键名属性 |
| `m_LocalizingFontMark` | `SerializedProperty` | 字体标记属性 |

### 编辑器预览缓存

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_PreviewLanguageName` | `string` | `""` | 编辑器模式下缓存的当前语言名称（用于预览标题） |
| `m_PreviewTranslation` | `string` | `""` | 编辑器模式下缓存的译文（用于预览显示） |
| `m_LastPreviewKey` | `string` | `""` | 上次预览刷新时的 Key 值（用于检测变化以避免重复查询） |
| `m_CachedLanguageTexts` | `Dictionary<string, string>` | `null` | 编辑器模式下缓存的语言文本数据（从已导出的 JSON 文件反序列化） |

### 字体标记下拉选项

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_FontMarkOptions` | `string[]` | `null` | 从字体 JSON 中解析出的唯一 Mark 选项列表（排序后） |
| `m_HasFontMarkData` | `bool` | `false` | 是否成功加载了字体 Mark 数据，决定字体标记字段使用下拉还是纯文本 |

---

## 完整公开 API

```csharp
// 基类接口
protected override void OnEnable()       // 绑定 SerializedProperty + 加载预览数据 + 加载字体标记选项
public override void OnInspectorGUI()    // 绘制键名字段 + 字体标记字段（Popup/纯文本）+ 预览区 + FinalRefreshInspectorGUI()
```

---

## Inspector 绘制结构

`OnInspectorGUI()` 的绘制顺序：

```
1. base.OnInspectorGUI()（编译检测 + serializedObject.Update）
2. 本地化 Key（Property 字段，标签宽度 175）
3. 字体标记（DrawFontMarkField）
   ├─ m_HasFontMarkData == true → Popup 下拉选择
   │     └─ 当前值不在选项中 → 自动修正为第一个选项
   └─ m_HasFontMarkData == false → 降级为纯文本 Property 字段
4. 分割线
5. 预览区（DrawPreview）
   ├─ 运行时（isPlaying）→ DrawRuntimePreview
   │     └─ 从 Nova.Localization 实时获取译文
   └─ 编辑器模式 → 从 m_CachedLanguageTexts 查找译文
        ├─ Key 变化时触发 RefreshPreviewTranslation
        └─ Foldout "预览 (当前语言: xxx)"
             ├─ Key 为空 → "(未设置 Key)"
             ├─ Key 未找到 → "(未找到 Key: xxx)"
             └─ 找到 → 显示 "译文: \"xxx\""
6. FinalRefreshInspectorGUI()
```

---

## 初始化时序

```
OnEnable()
  ├─ base.OnEnable()
  ├─ serializedObject.FindProperty("m_LocalizingKeyName")
  ├─ serializedObject.FindProperty("m_LocalizingFontMark")
  ├─ 重置预览缓存字段
  ├─ TryLoadPreviewData()
  │     ├─ isPlaying → return
  │     ├─ FindFirstObjectByType<LocalizationComponent>()
  │     ├─ 读取 m_TextJsonExportFolderPath 和 m_FallbackLanguage
  │     ├─ 构造 JSON 文件路径（支持 {0} 占位符和纯文件夹两种格式）
  │     └─ JsonConvert.DeserializeObject<Dictionary<string, string>>
  └─ TryLoadFontMarkOptions()
        ├─ isPlaying → return
        ├─ FindFirstObjectByType<LocalizationComponent>()
        ├─ 读取 m_FontJsonExportPath
        ├─ JsonConvert.DeserializeObject<List<LocalizationFontData>>
        ├─ 遍历收集唯一 Mark 到 HashSet
        └─ Array.Sort → m_FontMarkOptions, m_HasFontMarkData = true
```

---

## 使用示例

Inspector 面板在选中挂载了 `TextLocalizing` 的 GameObject 时自动显示。

**编辑器模式预览**：填写 Key 后，面板自动从已导出的 JSON 文件中查找译文并显示预览。需要场景中存在 `LocalizationComponent` 且已导出文本 JSON。

**字体标记下拉**：当字体 JSON 已导出且包含有效数据时，字体标记字段显示为 Popup 下拉选择，而非纯文本输入。

```
绘制效果示意：
+-------------------------------+
| 本地化 Key    [ui_start_btn ] |
| 字体标记      [Main      v  ] |  ← Popup（有字体数据时）
|-------------------------------|
| > 预览 (当前语言: English)    |
|   译文: "Start"               |
+-------------------------------+
```

---

## 注意事项

| 事项 | 说明 |
|------|------|
| CanEditMultipleObjects | 支持多选编辑，但预览区仅展示第一个目标的数据 |
| 预览数据依赖 LocalizationComponent | 场景中需存在 `LocalizationComponent` 且已导出 JSON，否则预览区无数据 |
| 字体标记自动修正 | 当当前值不在选项列表中时（如手动改为不存在的值），自动修正为排序后的第一个选项 |
| 运行时预览 | `isPlaying` 时直接从 `Nova.Localization` 获取实时数据，不依赖 JSON 文件 |
| EditorGUILayout.Popup | `DrawFontMarkField` 中使用了 `EditorGUILayout.Popup` 而非 `EditorUtil.Draw`，因为 `EditorUtil.Draw` 未封装 Popup 控件 |

---

## 关联文档

- [TextLocalizing.md](../../../Runtime/Modules/Localization/TextLocalizing.md)
- [TextLocalizingAutoMount.md](TextLocalizingAutoMount.md)
- [TextLocalizingValidator.md](TextLocalizingValidator.md)
- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [LocalizationComponent.md](../../../Runtime/Modules/Localization/LocalizationComponent.md)
- [LocalizationFontData.md](../../../Runtime/Modules/Localization/LocalizationFontData.md)
- [EditorUtil.Draw.md](../../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md)
