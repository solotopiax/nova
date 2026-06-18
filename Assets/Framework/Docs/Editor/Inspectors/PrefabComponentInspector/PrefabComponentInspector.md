# PrefabComponentInspector

**类签名**：`[CustomEditor(typeof(PrefabComponent))] internal sealed partial class PrefabComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.PrefabComponent`

负责 PrefabManager 类型选择 + 运行时实例列表展示（Play Mode 专用折叠组）。

---

## 文件

| 文件 | 说明 |
|------|------|
| `PrefabComponentInspector.cs` | 主 Inspector：OnEnable 绑定属性 + OnInspectorGUI 调用 DrawConfigs + RequiresConstantRepaint |
| `PrefabComponentInspector.Visitors.cs` | 属性与字段声明 |
| `PrefabComponentInspector.Methods.cs` | DrawConfigs 私有方法 |

---

## §4 序列化属性（Visitors.cs）

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_CurPrefabManagerTypeName` | `SerializedProperty` | 绑定 `PrefabComponent.m_CurPrefabManagerTypeName` |
| `m_PrefabManagerTypeNames` | `List<string>` | 程序集扫描得到的 `IPrefabManager` 实现类全名列表 |

---

## §5 DrawConfigs 行为说明

`DrawConfigs()` 按以下布局顺序绘制：

1. **Prefab 管理器**（TypesSelector）— 枚举 `IPrefabManager` 全部实现类，配合 HelpBox 说明可自定义扩展。
2. **运行时数据 Foldout**（仅 `Application.isPlaying` 时出现，key `"PrefabRuntimeGroup"`，默认展开；编辑模式下整组不绘制，分隔线亦不出现）：
   - **当前实例计数**：取自 `comp.RecordedInstanceCount`，以 `Label(label, value)` 双列展示。
   - **实例列表**：逐行展示 `comp.RecordedInstances`；每行 `ObjectField(只读) + SelectableLabel(Location)`；列表为空时展示 HelpBox 提示。
   - 所有内容统一 `Space(16f)` 缩进。
3. `RequiresConstantRepaint()` 返回 `Application.isPlaying`，确保计数实时刷新。

运行时实例列表的 `ObjectField` 使用裸 `EditorGUILayout.ObjectField`（`EditorUtil.Draw` 无只读 ObjectField 封装）。

---

## §10 常见误区

| 误区 | 说明 |
|------|------|
| 在 Inspector 中持有 IPrefabManager | 违反架构规范，Inspector 只通过 `PrefabComponent` 公开接口获取数据 |
| 直接调用 `GUILayout / EditorGUILayout` 绘制配置区控件 | 配置区必须使用 `EditorUtil.Draw.*`；仅运行时 ObjectField 列表例外 |

---

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [PrefabComponent.md](../../../Runtime/Modules/Prefab/PrefabComponent.md)
- [IPrefabManager.md](../../../Runtime/Modules/Prefab/PrefabManager/IPrefabManager.md)
- [PrefabManagerConfig.md](../../../Runtime/Modules/Prefab/PrefabManager/PrefabManagerConfig.md)
- [PrefabInstanceTag.md](../../../Runtime/Modules/Prefab/Definitions/PrefabInstanceTag.md)
