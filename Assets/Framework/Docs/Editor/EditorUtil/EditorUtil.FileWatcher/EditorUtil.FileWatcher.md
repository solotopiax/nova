# EditorUtil.FileWatcher

**类签名**：`[InitializeOnLoad] public static class EditorUtil.FileWatcher`
**命名空间**：`NovaFramework.Editor`

文件变动监控器，全局单例管理 `FileSystemWatcher` 实例，监控指定目录变更并在主线程触发回调。

> 本类由 `EditorUtil.Luban.FileWatcher` 迁移提升为顶级命名空间，功能完全一致，新代码统一使用 `EditorUtil.FileWatcher`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.FileWatcher.cs` | `EditorUtil.FileWatcher` | 目录监控注册/注销、跨线程变更通知转主线程回调 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── FileWatcher (public static class, [InitializeOnLoad])
```

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `s_Watchers` | `Dictionary<string, FileSystemWatcher>` | `private static readonly` | 规范化目录路径 → FileSystemWatcher 实例 |
| `s_Callbacks` | `Dictionary<string, List<CallbackEntry>>` | `private static readonly` | 规范化目录路径 → 回调条目列表（含 ID 去重） |
| `s_HasPendingChange` | `volatile bool` | `private static` | 是否有待处理的变更通知（volatile 保证线程可见性） |
| `s_PendingChangeDirs` | `HashSet<string>` | `private static readonly` | 待处理变更的目录路径集合 |
| `s_Lock` | `object` | `private static readonly` | 同步锁，保护 `s_PendingChangeDirs` 和 `s_HasPendingChange` |

### CallbackEntry 结构体字段（private）

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `string` | 回调唯一标识（用于去重） |
| `Callback` | `Action` | 回调委托 |

---

## §5 完整公开 API

```csharp
/// <summary>
/// 注册对指定目录的监控。
/// 若 callbackId 为 null，使用 onChange 的引用做去重（要求传入具名方法）。
/// 若 callbackId 非空，使用 ID 字符串做去重（允许传入 lambda）。
/// </summary>
/// <param name="dirPath">目录完整路径。</param>
/// <param name="onChange">文件变更时的回调（在主线程执行）。</param>
/// <param name="callbackId">回调唯一标识（可选），用于 lambda 场景的去重。</param>
public static void Watch(string dirPath, Action onChange, string callbackId = null)

/// <summary>
/// 取消对指定目录的监控。
/// </summary>
/// <param name="dirPath">目录完整路径。</param>
/// <param name="onChange">要移除的回调。</param>
/// <param name="callbackId">注册时使用的回调唯一标识（可选）。</param>
public static void Unwatch(string dirPath, Action onChange, string callbackId = null)
```

---

## §6 生命周期状态机

```
[InitializeOnLoad] 静态构造
  ├── 注册 EditorApplication.update += OnEditorUpdate
  └── 注册 AssemblyReloadEvents.beforeAssemblyReload += DisposeAll

Watch(dirPath, onChange, callbackId)
  ├── 目录不存在 → 静默跳过
  ├── 规范化路径
  ├── 添加回调条目到 s_Callbacks（ID 去重）
  └── 首次注册时创建 FileSystemWatcher（Changed/Created/Deleted → OnFileChanged）

文件变更发生
  ├── 线程池：OnFileChanged → lock(s_Lock) → s_PendingChangeDirs.Add + s_HasPendingChange = true
  └── 主线程：OnEditorUpdate → lock → 复制集合 → 遍历调用 s_Callbacks 快照

Unwatch(dirPath, onChange, callbackId)
  ├── 移除匹配 ID 的回调条目
  └── 回调列表为空时 DisposeWatcher（停止并释放 FileSystemWatcher）

AssemblyReload → DisposeAll（释放全部 FileSystemWatcher + 清空状态）
```

---

## §7 线程模型

- `FileSystemWatcher` 的 Changed/Created/Deleted 事件在**线程池线程**触发
- `OnFileChanged` 通过 `lock(s_Lock)` 将变更目录写入 `s_PendingChangeDirs`，设置 `s_HasPendingChange = true`
- `OnEditorUpdate` 在**主线程**（`EditorApplication.update`）中检查 `s_HasPendingChange`，加锁复制待处理集合后释放锁
- 回调 `Action onChange` 始终在**主线程**执行，可安全调用 Unity API
- 回调执行前对 `s_Callbacks` 做快照（`new List<CallbackEntry>(callbacks)`），防止回调中修改集合导致迭代异常

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 传入 lambda 不指定 `callbackId` 导致重复注册 | lambda 每次创建新引用，ID 生成结果不同；传 lambda 时必须指定 `callbackId` 参数 |
| 组件销毁时忘记调用 `Unwatch` | 会导致 FileSystemWatcher 持续持有，Assembly Reload 时由 `DisposeAll` 统一释放，但 Inspector 销毁后回调仍会触发 |

---

## §11 使用示例

```csharp
// Inspector 中监控 Excel 数据源目录变更（使用具名方法，无需 callbackId）
private void OnEnable()
{
    EditorUtil.FileWatcher.Watch(m_ExcelDirPath, OnExcelDirChanged);
}

private void OnDisable()
{
    EditorUtil.FileWatcher.Unwatch(m_ExcelDirPath, OnExcelDirChanged);
}

private void OnExcelDirChanged()
{
    Log.Debug(LogTag.Editor, "数据源目录发生变更，准备刷新...");
    Repaint();
}

// 使用 lambda + callbackId（推荐用于 lambda 场景）
private const string c_WatcherId = "MyInspector.ExcelDir";

EditorUtil.FileWatcher.Watch(m_ExcelDirPath, () => Repaint(), callbackId: c_WatcherId);
EditorUtil.FileWatcher.Unwatch(m_ExcelDirPath, null, callbackId: c_WatcherId);
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
