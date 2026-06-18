# PipifyWindow

## §1 文件头

```csharp
internal sealed partial class PipifyWindow : EditorWindow
// namespace: NovaFramework.Editor
// 菜单路径: Nova/Open Pipify
```

Pipify 流水线配置与执行窗口，用于管理 Batch 列表、配置步骤参数并触发一键构建流水线。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `PipifyWindow.cs` | `PipifyWindow` | 主文件：`[MenuItem]` 入口 `Open()` |
| `PipifyWindow.Visitors.cs` | `PipifyWindow` | 字段声明：常量（含 TopBar 布局常量 / `c_LeftPanelWidth` / `c_LeftRowHeight`）、实例字段（`m_Settings` / `m_SettingsSO` / `m_IsDirty` / `m_SelectedBatchIndex` / `m_Filter` / `m_LeftScroll` / 样式字段） |
| `PipifyWindow.Methods.cs` | `PipifyWindow` | 总调度 + Batch 辅助：`OnEnable` / `TryAutoBindSettings` / `OnGUI` / `DrawBody` / `DrawMainTitle` / `EnsureStyles` / `RebindSettings` / `OnClickCreate` / `OnClickSave` / `OnClickRevealInFinder` / `ConfirmDiscardDirty` / `MarkDirty` / `OnClickNewBatch` / `OnRenameBatch` / `OnDuplicateBatch` / `OnDeleteBatch` / `ShowBatchContextMenu` / `IsBatchNameDuplicate` / `GenerateUniqueBatchName` |
| `PipifyWindow.TopBar.cs` | `PipifyWindow` | 顶部工具栏：编辑器存档文件 ObjectField + 创建/选择/保存/打开文件夹按钮；`DrawTopBar()` / `OnClickPick()` |
| `PipifyWindow.LeftList.cs` | `PipifyWindow` | 左侧 Batch 列表：搜索框 / ScrollView / 行点击事件 / 右键菜单 / 新建按钮；`DrawLeftList()` |
| `PipifyWindow.RightPanel.cs` | `PipifyWindow` | 右侧详情面板：`DrawRightPanel()` / `DrawBatchHeader()` / `DrawItemsList()` / `DrawItemElement()` / `DrawItemParams()` / `DrawParamField()` |
| `PipifyWindow.RightPanel.Execute.cs` | `PipifyWindow` | 底部执行区：`DrawExecute()` / `OnClickRunBatch(batch)` |
| `PipifyInputDialog.cs` | `PipifyInputDialog` | 模态单行文本输入弹窗；`Show(title, label, initialValue)` 返回用户输入或 null |

---

## §3 继承关系

```
EditorWindow
  └── PipifyWindow (internal sealed partial)
```

---

## §5 公开 API

```csharp
// 菜单入口，路径 Nova/Open Pipify
[MenuItem("Nova/Open Pipify")]
public static void Open()
```

---

## §5·1 TopBar 行为说明

| 控件 / 方法 | 行为 |
|------------|------|
| ObjectField (`PipifySettingsSO`) | 显示"编辑器存档文件："标签；值变化时调用 `RebindSettings(newSettings)`；有脏数据则先弹 `ConfirmDiscardDirty` |
| 创建按钮 (`OnClickCreate`) | 弹 `SaveFilePanelInProject` → `EditorUtil.Asset.Operator.CreateAt<PipifySettingsSO>` → 选中资产 → `RebindSettings` |
| 选择按钮 (`OnClickPick`) | 弹 `OpenFilePanel` → `FileUtil.GetProjectRelativePath` → `EditorUtil.Asset.Operator.LoadAt<PipifySettingsSO>` → `RebindSettings`；路径不在项目或类型不匹配时弹错提示 |
| 保存按钮 (`OnClickSave`) | `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` + `m_IsDirty = false`；无脏数据时按钮禁用 |
| 打开文件夹 (`OnClickRevealInFinder`) | `EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(m_Settings))`；未绑定时静默返回 |

**存档绑定恢复：** `OnEnable` 调 `TryAutoBindSettings`，优先按当前 active scene 所属 sample 逐级向上推断配对的 `Editor/PipifySettings.asset`；命中则直接绑定该 sample 自身配置。仅当当前 scene 不在 sample 下或未找到配对文件时，才回退到 `AssetDatabase.FindAssets("t:PipifySettingsSO")` 扫全项目兜底。窗口同时监听 `sceneOpened`，`Single` 模式切 scene 后会自动重绑到新 sample 对应的配置；若存在未保存改动，则先走 `ConfirmDiscardDirty`。绑定状态完全由项目内资产自身承载，不依赖 EditorPrefs，UPM 全新安装/换项目场景仍可即开即用。

> **铁律：** 框架范围（`Assets/Framework/Scripts`）禁用 `EditorPrefs.*`。EditorPrefs 是 user-level 持久化，会跨项目串味且无法随包分发；存档/选择类状态一律通过项目内资产承载，临时态保留在内存即可。

---

## §5·3 RightPanel 行为说明

| 控件 / 方法 | 行为 |
|------------|------|
| `DrawBatchHeader(batch)` | Name TextField（实时唯一校验：重复则恢复原值并弹提示）+ Description TextField |
| `DrawItemsList()` | 调用 `m_ItemsList.DoLayoutList()`，懒初始化由 `EnsureItemsListForSelectedBatch()` 保证 |
| `EnsureItemsListForSelectedBatch()` | Batch 索引与 `m_ItemsListBoundBatchIndex` 不符时重建 ReorderableList，同时清空 `m_ExpandedItemIndices` 和 `m_ParamsCache` |
| ReorderableList Header | 显示 `"Steps (N)"`（N = 当前 Items 数量） |
| ReorderableList 行内容 | 序号标签 + `[Category] DisplayName` + 折叠箭头（有参时显示） + 自定义删除按钮（×） |
| 折叠箭头 | 切换 `m_ExpandedItemIndices` 集合中对应索引的存在性，展开后通过 `DrawItemParams` 绘制参数字段 |
| 删除按钮 | 通过 `EditorApplication.delayCall` 延迟移除，防止 Layout/Repaint 阶段直接修改集合导致崩溃；同时清空 `m_ExpandedItemIndices` / `m_ParamsCache` 并设 `m_ItemsListBoundBatchIndex = -1` 强制重建 ReorderableList，避免 key 移位错位 |
| `onAddDropdownCallback` | 按 Category 分组弹 GenericMenu，选中后调用 `AddBatchItemFromStep(info)` |
| `onReorderCallback` | 拖拽排序后清空 `m_ExpandedItemIndices` 和 `m_ParamsCache` 并 `MarkDirty()` |
| 参数内联 Drawer | 反射遍历 `ParamsType.GetFields(Public | Instance)` 按类型分发：`string`→TextField；`bool`→Toggle；`int`→IntField；`float`→FloatField；`Enum`→EnumPopup；其他复杂类型→Log.Warning 跳过 |
| 参数持久化 | `EditorGUI.BeginChangeCheck/EndChangeCheck` 包住字段组；有变化则 `Util.Json.Serialize(paramsInstance)` 写回 `item.ParamsJson` + `MarkDirty()` |
| `DrawExecute()` | 顶部 `EditorUtil.Draw.Line()` 分割线 + 右对齐 `SuccessButton("▶ 运行")`；禁用条件：`batch.Items.Count == 0 \|\| m_IsDirty`；点击后 fire-and-forget 调用 `EditorUtil.Pipify.RunBatchAsync(batch, this)`，Runner 内部通过 WindowReporter 以模态进度条呈现执行进度，结束后通过宿主窗口 `ShowNotification` 弹右下角结果浮窗 |

---

## §5·2 LeftList 行为说明

| 控件 / 方法 | 行为 |
|------------|------|
| 搜索框 (`m_Filter`) | 实时过滤列表，大小写不敏感；过滤不影响真实索引（删除/重命名仍以原始 index 操作） |
| 单击行 | `m_SelectedBatchIndex = index`，`Repaint()` |
| 双击行 | 触发 `OnRenameBatch(index)` |
| 右键行 | 先选中该行，再调 `ShowBatchContextMenu(index)`（GenericMenu） |
| "＋ 新建 Batch" | 调 `OnClickNewBatch()`：弹 `PipifyInputDialog` → 校验非空唯一 → 追加并选中 |
| `OnRenameBatch(index)` | 弹 `PipifyInputDialog` → 校验非空唯一 → 写入 `batch.Name` → `MarkDirty()` |
| `OnDuplicateBatch(index)` | JSON 深拷贝 → 名称自动追加 " (Copy)" 直到唯一 → 插入末尾并选中 |
| `OnDeleteBatch(index)` | `EditorUtility.DisplayDialog` 确认 → 删除 → 调整 `m_SelectedBatchIndex` → `MarkDirty()` |

---

## §6 布局结构

```
DrawBody()
  DrawMainTitle()       <- 居中大标题 + 分隔线
  DrawTopBar()          <- 存档文件行
  Layout.Horizontal
    DrawLeftList()      <- 固定 260f 宽，帮助框 / 搜索框 / ScrollView / 新建按钮
    DrawRightPanel()    <- 弹性宽，Batch 详情（T16）
```

---

## §7 状态字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Settings` | `PipifySettingsSO` | 当前绑定的存档资产 |
| `m_SettingsSO` | `SerializedObject` | 绑定 m_Settings 的 SO 封装 |
| `m_IsDirty` | `bool` | 是否有未保存改动 |
| `m_SelectedBatchIndex` | `int` | 左列表当前选中项索引；-1 表示未选中 |
| `m_Filter` | `string` | 左列表搜索关键词 |
| `m_LeftScroll` | `Vector2` | 左列表 ScrollView 滚动位置 |
| `m_RightScroll` | `Vector2` | 右侧面板 ScrollView 滚动位置 |
| `m_ItemsList` | `ReorderableList` | 绑定当前选中 Batch.m_Items 的 ReorderableList；Batch 切换时重建 |
| `m_ItemsListBoundBatchIndex` | `int` | 当前 m_ItemsList 绑定的 Batch 索引；-1 表示未绑定 |
| `m_ExpandedItemIndices` | `HashSet<int>` | 已展开参数区的 Item 索引集合 |
| `m_ParamsCache` | `Dictionary<int, object>` | Item 参数对象缓存；key = 索引，value = 反序列化后的参数实例 |

---

## §8 初始化时序

`Open()` → `GetWindow` → 窗口首次绘制 `OnGUI` → `EnsureStyles` 初始化 GUIStyle → `EnsureLeftListStyles` 初始化列表样式 → 绑定存档后 `m_SettingsSO = new SerializedObject(m_Settings)`

---

## §9 关键算法

### 参数内联 Drawer（反射分发）

`DrawItemParams` 调用 `BuildParamsInstance` 取参数对象（优先从 `m_ParamsCache` 读取），再通过反射枚举 `paramsType.GetFields(Public | Instance)` 遍历字段，按 `FieldInfo.FieldType` 分发到 Unity 原生 `EditorGUI.*` 裸 API（ReorderableList 内需要精确 Rect 控制，EditorUtil.Draw 的 Layout 系列无法使用 `EditorGUILayout` 版本），覆盖类型：

| 类型 | 控件 |
|------|------|
| `string` | `EditorGUI.TextField` |
| `bool` | `EditorGUI.Toggle` |
| `int` | `EditorGUI.IntField` |
| `float` | `EditorGUI.FloatField` |
| `Enum` 子类 | `EditorGUI.EnumPopup` |
| 其他 | 只读占位 + `Log.Warning` 跳过 |

整个字段组用 `EditorGUI.BeginChangeCheck / EndChangeCheck` 包裹，有变化则回写 `item.ParamsJson = Util.Json.Serialize(paramsInstance)`。

---

## §10 常见误区

- **右键菜单 lambda 闭包陷阱**：`GenericMenu.AddItem` 的回调是延迟执行的；若直接捕获循环变量 `i`，回调执行时 `i` 已变化。`ShowBatchContextMenu` 内每次调用均重新声明 `int capturedIndex = index` 并在 lambda 内引用 `capturedIndex` 来规避。
- **过滤与真实索引**：`m_Filter` 过滤只影响显示，`DrawLeftBatchRow(index, batch)` 传入的 `index` 始终是 `m_Settings.Batches` 中的真实索引，右键菜单 / 重命名 / 删除均基于该真实索引操作。
- **ShowModal 与 ExitGUI**：`PipifyInputDialog.Show` 使用 `ShowModal()`，此时调用栈内禁止调用 `GUIUtility.ExitGUI()`；因此 `DrawBody` 内的按钮回调走 `Button(label, disableOnPlaying, exitGUI=false, onClick)` 重载。
- **切换 Batch 必须重建 ReorderableList**：`m_ItemsList` 保存了 `SerializedProperty` 引用，Batch 切换后旧引用失效。`m_ItemsListBoundBatchIndex` 守卫确保每次 `EnsureItemsListForSelectedBatch()` 调用时检测到索引变化则重建，同时清空 `m_ExpandedItemIndices` 和 `m_ParamsCache`。
- **参数类只绘 public fields**：反射使用 `BindingFlags.Public | BindingFlags.Instance`，private 字段和 properties 均不绘制（参数类设计就是用 public 字段）。
- **复杂类型不支持**：非基础类型（嵌套类 / List / 数组等）第一版不支持内联编辑，`DrawParamField` 内绘制只读占位并 `Log.Warning`，不抛异常。

---

## §12 注意事项

- **修改参数字段后务必 MarkDirty + 回写 ParamsJson**：参数编辑走纯 C# 对象，不走 SerializedProperty，Unity 不会自动检测 dirty。`DrawItemParams` 内 `EndChangeCheck` 为 true 时必须同时调用 `item.ParamsJson = Util.Json.Serialize(paramsInstance)` 和 `MarkDirty()`。
- **行内删除走 delayCall**：`DrawItemElement` 的删除按钮通过 `EditorApplication.delayCall` 延迟执行列表修改，避免在 ReorderableList 绘制阶段（Layout/Repaint）直接修改集合引发 IndexOutOfRange。
- **删除 Item 后必须整表清空展开态与参数缓存**：`m_ExpandedItemIndices` 和 `m_ParamsCache` 以索引为 key；删除 index=k 后所有 `> k` 的 key 均移位。单独 Remove(k) 不够，必须调 `m_ExpandedItemIndices.Clear()` + `InvalidateParamsCache(-1)` + 设 `m_ItemsListBoundBatchIndex = -1` 强制重建 ReorderableList。

---

- **Dirty 状态下按钮禁用（需先保存再运行）**：`DrawExecute` 的禁用条件包含 `m_IsDirty`，即存在未保存改动时「▶ 运行」按钮不可点击。需先点击 TopBar 的「保存」按钮将改动持久化到 SO，再执行流水线，防止以脏数据跑流水线产生不一致结果。

---

## §12·1 更多注意事项

- **Dirty 状态下切换或关闭**：`RebindSettings` 入口调用 `ConfirmDiscardDirty`；用户选择"取消"时切换被中止，选择"丢弃"时 `m_SettingsSO.Update()` 回滚未持久化的改动。
- **保存按钮状态**：通过 `EditorUtil.Draw.DisabledGroup(!m_IsDirty, ...)` 包裹，只有 `m_IsDirty == true` 时才可点击；业务代码修改数据后必须同步设置 `m_IsDirty = true` 并 `Repaint()`。
- **删除触发 `m_SelectedBatchIndex` 调整**：删除索引 `k` 后，若选中的是 `k` 则重置为 -1；若选中项索引 `> k` 则减 1（整体前移）；若选中项索引 `< k` 则不变。

---

## §11 使用示例

通过菜单打开窗口：

```
Unity 菜单栏 → Nova → Open Pipify
```

也可在代码中调用：

```csharp
PipifyWindow.Open();
```

---

## §13 关联文档

- [Editor.md](../Editor.md)
- [PipifySettingsSO.md](../EditorUtil/EditorUtil.Pipify/PipifySettingsSO.md)
