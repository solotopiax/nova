# EditorUtil.CheckUpdate

**类签名**：`public static partial class EditorUtil.CheckUpdate`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.CheckUpdate`

UPM 包版本检查工具支持启动和跨日自动检查，也支持手动检查。当天跳过的版本会在次日仍可升级时重新提醒。查询覆盖外网仓库和内部云仓库，更新项由窗口合并展示。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| [EditorUtil.CheckUpdate.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.cs) | `partial EditorUtil.CheckUpdate` | 公有接口：`[InitializeOnLoad]` 启动钩子 `Initializer` 嵌套类 |
| [EditorUtil.CheckUpdate.Visitors.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.Visitors.cs) | `partial EditorUtil.CheckUpdate` | 常量声明：`c_ConfigPath`、`c_MenuPath`、`c_RegistryUrl`、`c_InternalRegistryUrl`、`c_ApiPath`；菜单入口 `OpenWindow` |
| [EditorUtil.CheckUpdate.Methods.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.Methods.cs) | `partial EditorUtil.CheckUpdate` | 查询与跳过实现：`CheckAsync`、`CheckOnStartupAsync`、`MarkSkip`、`ClearSkip`、`CheckExternalAsync`、`CheckInternalAsync` 及私有辅助方法 |
| [EditorUtil.CheckUpdate.Definitions.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.Definitions.cs) | `partial EditorUtil.CheckUpdate` | 嵌套类型：`UpdateInfo`（public）、`SkipConfig`（private） |
| [EditorUtil.CheckUpdate.Automatic.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.Automatic.cs) | `partial EditorUtil.CheckUpdate` | 自动检查排队、跨日触发、待展示结果恢复、当前安装版本复核及等待原因 |

---

## §3 继承关系

```
NovaFramework.Editor.EditorUtil (public static partial class)
  └── CheckUpdate (public static partial class)
        ├── Initializer (private static class, [InitializeOnLoad])
        ├── UpdateInfo (public sealed class)
        └── SkipConfig (private sealed class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_ConfigPath` | `const string` | `"Library/Nova/CheckUpdate.json"` | 跳过配置文件相对工程根目录的路径 |
| `c_MenuPath` | `const string` | `"Nova/Open Check Update"` | Unity 菜单路径 |

> registry 地址由 `EditorUtil.PlugPals.LoadRegistries()` 读取 `ProjectSettings/Nova/PlugPalsRegistries.json`（该文件 `.gitignore` 不入库）：文件不存在时使用公网与内网默认地址，文件存在时保留实际 URL（包括空值）；包列表 API 路径使用 `EditorUtil.PlugPals.c_RegistryApiPath`。任一仓库地址为空时跳过对应更新检查。

---

## §5 完整公开 API

### 版本检查

```csharp
/// <summary>
/// 异步拉取所有有更新的包信息列表（latest > current）。
/// </summary>
/// <returns>有可用更新的 UpdateInfo 列表，网络失败时返回空列表。</returns>
public static async Task<List<UpdateInfo>> CheckAsync()
```

分别复用 `EditorUtil.PlugPals.FetchRemotePackagesAsync` 拉取外网仓库与内部云仓库的远端包列表，再通过 `PlugPals.ReadInstalledVersions` 读取本地已安装版本，对比后返回 `latestVersion > currentVersion` 的条目。最终结果不去重，合并后统一按**包名**字母升序排序。单边仓库失败不会清空另一边仓库的成功结果。

### 跳过配置

```csharp
/// <summary>
/// 暂停指定外网包的当前最新版本的自动提醒，有效期为本地当天。
/// </summary>
/// <param name="items">今天要跳过的外网仓库更新列表。</param>
public static void MarkSkip(IEnumerable<UpdateInfo> items)

/// <summary>
/// 清空所有跳过记录。
/// </summary>
public static void ClearSkip()
```

窗口使用内部重载一次保存两个来源，`selectedDay` 是用户勾选时的本地日期。

```csharp
internal static void MarkSkip(IEnumerable<UpdateInfo> externalItems, IEnumerable<UpdateInfo> internalItems, DateTime selectedDay)
```

### 嵌套类型

```csharp
/// <summary>
/// 单个包的版本更新信息（immutable，构造器赋值）。
/// </summary>
public sealed class UpdateInfo
{
    public string PackageName { get; }   // UPM 包名（如 com.solotopia.nova.framework）
    public string CurrentVersion { get; } // 当前已安装版本号
    public string LatestVersion { get; }  // 远端最新版本号

    public UpdateInfo(string packageName, string currentVersion, string latestVersion)
}
```

---

## §6 生命周期状态机

```
[Editor 域重载]
      │  [InitializeOnLoad] Initializer 静态构造器
      ▼
[EditorApplication.delayCall += OnDelayedStart]
      │  注册 update 调度
      ▼
QueueAutomaticStartupCheck()
      ├─ 有 SessionState 待展示列表 → 恢复，不重复联网
      └─ 无待展示列表 → 等待 Editor、当前安装及重要声明处理结束
      ▼
CheckOnStartupAsync()
      ├─ CheckExternalAsync() + CheckInternalAsync() → 分别拉取两侧仓库并比对本地版本
      └─ 保存两来源的待展示列表，重新进入 update 队列
      ▼
TryRunAutomaticStartupCheck()
      ├─ 再读当前已安装版本和当天的 SkipVersions，移除已升级、已卸载或当天已跳过的条目
      ├─ 真正展示前重新读取窗口门禁；忙碌时保留列表等待，不重新联网
      └─ 安全时才 TryOpenAutomatic(filteredExternal, filteredInternal)，实际非模态展示后清除列表；已有窗口则保留队列

[本轮处理结束]
      └─ 每分钟查看本地日期，跨日后重新进入自动检查队列

[CheckUpdateWindow 关闭，已勾选今日跳过且有更新]
      └─ EditorUtil.CheckUpdate.MarkSkip(externalItems, internalItems, selectedDay) → SaveConfig()
```

---

## §7 线程模型

- `CheckAsync` 和 `CheckOnStartupAsync` 均为 `async Task`，在 Unity Editor 主线程的 async 上下文中执行。
- 内部通过 `PlugPals.FetchRemotePackagesAsync` 发起网络请求（`HttpClient` + `await`）；返回后回到主线程。
- `CheckOnStartupAsync` 以 fire-and-forget（`_ = CheckOnStartupAsync()`）方式调用，异常在方法内部记录 Warning，不向外传播；异步完成回调只排队，不直接打开窗口。
- `LoadConfigForDay` 和 `SaveConfig` 为同步文件操作，调用时必须在主线程。

---

## §8 初始化时序

```
[InitializeOnLoad] Initializer 静态构造器（Editor 加载时）
  └─ EditorApplication.delayCall += OnDelayedStart（注册延迟回调）

OnDelayedStart()（初始化钩子注册后）
  ├─ 注销自身：delayCall -= OnDelayedStart
  └─ QueueAutomaticStartupCheck()：恢复待展示结果，或在安全时机查询

CheckOnStartupAsync()
  ├─ CheckExternalAsync()
  │    ├─ PlugPals.FetchRemotePackagesAsync(c_RegistryUrl, c_ApiPath, CancellationToken.None)
  │    └─ PlugPals.ReadInstalledVersions()
  ├─ CheckInternalAsync()
  │    ├─ PlugPals.FetchRemotePackagesAsync(c_InternalRegistryUrl, c_ApiPath, CancellationToken.None)
  │    └─ PlugPals.ReadInstalledVersions()
  └─ QueueAutomaticPresentation()：缓存结果，按 §6 的展示队列重新检查时机、当前版本和跳过记录
```

---

## §9 关键算法

### SemVer 比较（CompareSemVer）

查询及延后展示均复用 `EditorUtil.PlugPals.CompareSemVer(string a, string b)`，不另建比较实现。

比较两个语义版本号，返回正数（a > b）、0（相等）、负数（a < b）：

1. 空字符串处理：两者均空返回 0；a 为空返回 -1；b 为空返回 1。
2. 剥离 pre-release 后缀（取 `-` 前核心版本号），记录 `hasPreA` / `hasPreB`。
3. 将核心版本号按 `.` 分割为整数数组，逐段比较。
4. 若某一方段数不足，以 0 补齐（如 `1.2` vs `1.2.0` 视为相等）。
5. 核心版本号相同时：有 pre-release 后缀（`-rc1`）的版本 < 无后缀版本。
6. 整数解析失败时兜底：`string.Compare(a, b, StringComparison.Ordinal)`。

### 跳过版本语义

`SkipConfig.LocalDate` 保存勾选时的本地自然日（`yyyy-MM-dd`）。`SkipConfig.SkipVersions` 保存每个仓库的包及被跳过的最新版本。例如：

- `external:com.solotopia.nova.framework`
- `internal:com.solotopia.nova.framework`

日期为当天且 `skippedVersion == info.LatestVersion` 时才跳过。新版本在当天也会出现在后续检查中，次日会重新提醒任何仍可升级的版本。旧存档缺少日期时按已过期处理。

---

## §10 常见误区

**误区 1：`ClearSkip` 不是按包清除**

`ClearSkip` 会清除所有包的跳过记录。

**误区 2：`CheckAsync` 不过滤跳过版本**

`CheckAsync` 与手动打开 `CheckUpdateWindow` 都返回全部可用更新。当天的跳过记录仅在自动展示前应用。

**误区 3：`c_ConfigPath` 相对工程根而非 Assets**

`Library/Nova/CheckUpdate.json` 是相对于工程根目录（`Application.dataPath/../`），而非 `Assets/`，Unity 不会追踪此文件，不受版本控制。

---

## §11 使用示例

### 手动检查更新

```csharp
// 在编辑器代码中主动触发版本检查
List<EditorUtil.CheckUpdate.UpdateInfo> updates = await EditorUtil.CheckUpdate.CheckAsync();
foreach (EditorUtil.CheckUpdate.UpdateInfo info in updates)
{
    Debug.Log($"{info.PackageName}: {info.CurrentVersion} → {info.LatestVersion}");
}
```

### 标记跳过当前版本

```csharp
// externalUpdates 是调用方已确认来源为外网仓库的更新列表
EditorUtil.CheckUpdate.MarkSkip(externalUpdates);

// 清除所有跳过记录，恢复下次自动检查的提醒
EditorUtil.CheckUpdate.ClearSkip();
```

---

## §12 注意事项

- `c_RegistryUrl` 与 `c_InternalRegistryUrl` 现已拆分为两个固定地址；后续如仓库迁移，只需替换 Visitors 常量。
- 双仓库检查采用“单边失败不阻断另一边成功结果”的策略；日志中会分别记录失败仓库地址。
- `Library/Nova/CheckUpdate.json` 不受版本控制，多人协作时跳过配置不同步。
- `[InitializeOnLoad]` 在脚本域加载时注册自动检查。编辑器持续打开时，空闲的检查队列会每分钟查看一次本地日期，发现跨日后安排下一轮检查。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [CheckUpdateWindow.md](../../Windows/CheckUpdateWindow.md) — 版本更新提示窗口
- [PlugPalsWindow.md](../../Windows/PlugPalsWindow.md) — UPM 包管理窗口（依赖其 `FetchRemotePackagesAsync` / `ReadInstalledVersions`）
- [Editor.md](../../Editor.md) — Editor 层级总览

## 查询、来源和跳过存档边界

外网与内部仓依次查询，各侧异常只 Warning 并投影为空列表；空列表同时可能代表禁用、失败或无升级，不是明确的检查成功收据。UpdateInfo 只保存包名、当前和最新版本，不携 registry URL；合并不去重，同属性的两个来源仍保留。列表显示排序中的版本是文本排序，不等同 ImportantUpdates 使用的完整 SemVer。

自动展示前重新读取当天的跳过记录，按 `external` 和 `internal` 分类键过滤。分类键不绑定 URL，同日换仓后仍可能命中同名同版本的记录。两个来源一起保存，写入失败记录日志。缺日期、日期过期或 `SkipVersions` 为 `null` 时按空记录读取。

自动检查在 batchmode 不启动；编译、导入、Play 切换、当前一轮工具链安装及重要声明检查/人工确认期间延后。工具链的延时重试间隔不占用展示门，不因 `partial` 状态永久等待。已完成查询的两来源列表保存在 SessionState 的 `Nova.CheckUpdate.Automatic.PendingSnapshot.v1`，通过普通非模态 `Show` 实际展示或确定无需提示后即删除；已有更新窗口时保留队列，不覆盖其阅读或跳过选择。跨域重载复用该列表，不重复网络请求。若网络请求尚未完成就重载，下一域可重新进行这次只读查询。手动 `Open()` 与带结果的 `Open(...)` 仍立即遵循各自原有入口，不受自动队列接管。

只读 `Nova.CheckUpdate.Automatic.Phase.v1` 可看到 `queued / waiting / querying / blocked / waiting-editor / complete`，`Nova.CheckUpdate.Automatic.WaitingReason.v1` 提供中文原因。这些字段只说明自动提示阶段，不代表仓库请求全部成功、包已升级或用户已跳过。展示失败保留待展示结果，重载可恢复；不自动写入跳过记录。
