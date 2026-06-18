# CheckUpdateWindow

**类签名**：`public sealed partial class CheckUpdateWindow : EditorWindow`
**命名空间**：`NovaFramework.Editor`
**菜单入口**：`Nova/Open CheckUpdate`

Nova 包版本更新提示窗口，以表格形式展示所有已安装包中存在新版本的条目，支持启动自动弹出和手动打开两种入口。当前窗口会同时展示外网仓库与内部云仓库的更新项。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `CheckUpdateWindow.cs` | `partial CheckUpdateWindow` | 生命周期：`OnEnable`、`OnDisable`、`OnGUI`、`OnDestroy` |
| `CheckUpdateWindow.Visitors.cs` | `partial CheckUpdateWindow` | 常量、颜色常量、单例及全部实例字段 |
| `CheckUpdateWindow.Methods.cs` | `partial CheckUpdateWindow` | 公有入口 `Open()`、`Open(items)`、`Open(externalItems, internalItems)`；私有方法：`StartCheckAsync`、`EnsureStyles`、`DrawHeader`、`DrawTableHeader`、`DrawRow`、`DrawFooter`、`DrawHorizontalLine` |
| `CheckUpdateWindow.Definitions.cs` | `partial CheckUpdateWindow` | 嵌套类型定义（当前为空） |

---

## §3 继承关系

```
UnityEditor.EditorWindow
  └── CheckUpdateWindow (public sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_WindowTitle` | `const string` | `"Nova · CheckUpdate"` | 窗口标题 |
| `c_MinWidth` | `const float` | `520f` | 窗口最小宽度 |
| `c_MinHeight` | `const float` | `320f` | 窗口最小高度 |
| `c_RowSpacing` | `const float` | `4f` | 行间距（singleLineHeight + c_RowSpacing = 行高） |
| `c_ColPackageWidth` | `const float` | `240f` | Package 列宽 |
| `c_ColVersionWidth` | `const float` | `110f` | Current / Latest 列宽（共用） |
| `s_RowEvenColor` | `static readonly Color` | `new Color(0.22f, 0.22f, 0.22f, 0.4f)` | 斑马纹偶数行背景色（浅灰） |
| `s_LatestColor` | `static readonly Color` | `new Color(0.6f, 0.9f, 0.4f)` | Latest 列高亮色（绿色） |
| `s_InternalPackageNameColor` | `static readonly Color` | `new Color(1f, 0.82f, 0.62f)` | 内部云仓库包名色（浅橙色） |
| `s_Instance` | `static CheckUpdateWindow` | `null` | 窗口单例引用 |
| `m_Items` | `List<EditorUtil.CheckUpdate.UpdateInfo>` | `null` | 当前展示的更新列表 |
| `m_ExternalItems` | `List<EditorUtil.CheckUpdate.UpdateInfo>` | `null` | 外网仓库更新列表 |
| `m_InternalItems` | `List<EditorUtil.CheckUpdate.UpdateInfo>` | `null` | 内部云仓库更新列表 |
| `m_IsChecking` | `bool` | `false` | 是否正在异步拉取中 |
| `m_Scroll` | `Vector2` | `default` | 滚动位置 |
| `m_DontShowAgain` | `bool` | `false` | 是否勾选"启动时不再提示这些版本" |
| `m_HeaderStyle` | `GUIStyle` | `null` | 标题样式（懒初始化，`EnsureStyles` 标志） |
| `m_LatestStyle` | `GUIStyle` | `null` | Latest 版本列文本样式（绿色） |
| `m_EmptyStyle` | `GUIStyle` | `null` | 空态提示文本样式（居中灰色） |

---

## §5 完整公开 API

```csharp
/// <summary>
/// 手动打开：窗口自行触发一次 CheckAsync，展示拉取中状态。
/// </summary>
public static void Open()

/// <summary>
/// 带参数打开：启动钩子传入已知更新列表，直接展示无需再次拉取。
/// </summary>
/// <param name="items">待展示的更新列表。</param>
public static void Open(List<EditorUtil.CheckUpdate.UpdateInfo> items)

public static void Open(
    List<EditorUtil.CheckUpdate.UpdateInfo> externalItems,
    List<EditorUtil.CheckUpdate.UpdateInfo> internalItems)
```

---

## §6 生命周期状态机

```
[窗口未打开]
      │  Open() — 手动打开
      ▼
[m_IsChecking = true，展示 "Checking for updates..."]
      │  StartCheckAsync()：await EditorUtil.CheckUpdate.CheckExternalAsync() + CheckInternalAsync()
      ├─ 成功 → m_ExternalItems / m_InternalItems -> MergeItems -> m_Items，m_IsChecking = false → [展示列表]
      └─ 失败 → m_Items = 空列表，m_IsChecking = false → [展示"无更新"空态]

[窗口未打开]
      │  Open(items) — 启动钩子传参打开
      ▼
[m_Items = items，m_IsChecking = false → 直接展示列表]

[OnEnable]
      └─ m_DontShowAgain = false（重置勾选状态）

[OnDisable]
      └─ m_DontShowAgain == true
           ├─ 外网列表 → EditorUtil.CheckUpdate.MarkSkip(externalItems, false)
           └─ 内部列表 → EditorUtil.CheckUpdate.MarkSkip(internalItems, true)

[OnDestroy]
      └─ s_Instance = null
```

状态转换超过 3 条，触发此章节。

---

## §7 线程模型

- `StartCheckAsync` 为 `async void`，在主线程 async 上下文中执行，`await` 后回到主线程。
- `await` 完成后通过 `s_Instance` 空检查防止窗口已关闭时的写操作；写完调用 `Repaint()` 触发重绘。
- `OnGUI` 及全部字段操作均在 Unity 主线程。

---

## §8 初始化时序

```
Open() — 手动入口
  ├─ GetWindow<CheckUpdateWindow>(utility: true, ...)
  ├─ 若 m_IsChecking == true → 直接 Focus 返回
  ├─ m_Items = null，m_IsChecking = true，Repaint()
  └─ StartCheckAsync()

Open(items) — 启动钩子入口
  ├─ GetWindow<CheckUpdateWindow>(utility: true, ...)
  ├─ m_ExternalItems / m_InternalItems 赋值
  ├─ MergeItems(...) → m_Items
  └─ m_DontShowAgain = false，Repaint()
```

---

## §11 使用示例

### 启动钩子自动弹出（框架内部）

```csharp
// EditorUtil.CheckUpdate 内部调用（无需外部显式调用）
CheckUpdateWindow.Open(filteredExternalUpdates, filteredInternalUpdates);
```

### 手动打开（菜单或代码）

```csharp
// 通过 Unity 菜单 Nova/Open CheckUpdate 触发
// 或在编辑器代码中直接调用
CheckUpdateWindow.Open();
```

---

## §12 注意事项

| 场景 | 说明 |
|------|------|
| `m_DontShowAgain` 生效时机 | 勾选后需**关闭窗口**才触发 `OnDisable` 写入 `MarkSkip`；直接点击 Close 调用 `Close()` 会触发 `OnDisable` |
| 重复打开防抖 | `Open()` 检查 `m_IsChecking`，若正在拉取则仅 Focus 不重新发起请求 |
| Latest 列颜色含义 | 始终以绿色（`s_LatestColor`）显示最新版本号，无论与当前版本的差异大小 |
| 包名排序规则 | 外网与内部云仓库条目合并后统一按**包名**字母升序展示，不按仓库分组顺序绘制 |
| 内部云仓库条目 | 包名显示为浅橙色；点击"日志"时按内部云仓库地址拉取 CHANGELOG |
| `ScrollView` 直接使用 `EditorGUILayout` | `EditorUtil.Draw` 尚未封装 `BeginScrollView`，此处直接使用，不视为违规 |
| GUIStyle 懒初始化 | `EnsureStyles()` 以 `m_HeaderStyle != null` 为已初始化标志，在 `OnGUI` 首帧调用，避免 `OnEnable` 时 `EditorStyles` 未就绪 |

---

## §13 关联文档

- [EditorUtil.CheckUpdate.md](../EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) — 版本检查工具（数据来源、跳过配置持久化）
- [ConfigWindow.md](ConfigWindow.md) — 同目录其他 EditorWindow 参考
- [PlugPalsWindow.md](PlugPalsWindow.md) — UPM 包管理窗口（同类窗口参考）
- [EditorUtil.Draw.md](../EditorUtil/EditorUtil.Draw/EditorUtil.Draw.md) — GUI 绘制工具集
- [Editor.md](../Editor.md) — Editor 层级总览
