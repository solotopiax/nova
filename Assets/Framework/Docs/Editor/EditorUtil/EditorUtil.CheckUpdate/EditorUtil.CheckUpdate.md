# EditorUtil.CheckUpdate

**类签名**：`public static partial class EditorUtil.CheckUpdate`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.CheckUpdate`

UPM 包版本检查工具，提供启动自动检查与手动触发两种入口，支持按版本跳过提示的持久化。当前检查流程同时覆盖外网仓库与内部云仓库，并在窗口层混合展示结果。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.CheckUpdate.cs` | `partial EditorUtil.CheckUpdate` | 公有接口：`[InitializeOnLoad]` 启动钩子 `Initializer` 嵌套类 |
| `EditorUtil.CheckUpdate.Visitors.cs` | `partial EditorUtil.CheckUpdate` | 常量声明：`c_ConfigPath`、`c_MenuPath`、`c_RegistryUrl`、`c_InternalRegistryUrl`、`c_ApiPath`；菜单入口 `OpenWindow` |
| `EditorUtil.CheckUpdate.Methods.cs` | `partial EditorUtil.CheckUpdate` | 全部方法实现：`CheckAsync`、`CheckOnStartupAsync`、`MarkSkip`、`ClearSkip`、`CheckExternalAsync`、`CheckInternalAsync`、`CompareSemVer` 及私有辅助方法 |
| `EditorUtil.CheckUpdate.Definitions.cs` | `partial EditorUtil.CheckUpdate` | 嵌套类型：`UpdateInfo`（public）、`SkipConfig`（private） |

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
| `c_MenuPath` | `const string` | `"Nova/Open CheckUpdate"` | Unity 菜单路径 |

> registry 地址不再硬编码：运行时由 `EditorUtil.PlugPals.LoadRegistries()` 读取 `ProjectSettings/Nova/PlugPalsRegistries.json`（公网默认占位、内网默认空，该文件 `.gitignore` 不入库）；包列表 API 路径用 `EditorUtil.PlugPals.c_RegistryApiPath`。内网地址为空时跳过内部云检查。

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
/// 将指定包的当前最新版本标记为"跳过"，写入持久化配置。
/// </summary>
/// <param name="items">要跳过的更新列表。</param>
public static void MarkSkip(IEnumerable<UpdateInfo> items)

/// <summary>
/// 清空所有跳过记录。
/// </summary>
public static void ClearSkip()
```

内部还提供仓库感知重载：

```csharp
internal static void MarkSkip(IEnumerable<UpdateInfo> items, bool isInternal)
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
      │  下一帧 Editor 就绪
      ▼
OnDelayedStart()
      │  fire-and-forget
      ▼
CheckOnStartupAsync()
      ├─ CheckExternalAsync() + CheckInternalAsync() → 分别拉取两侧仓库并比对本地版本
      ├─ LoadConfig() → 读取 Library/Nova/CheckUpdate.json
      ├─ 按仓库维度过滤 SkipVersions 中记录的已跳过版本
      └─ 任一侧 filtered.Count > 0 → CheckUpdateWindow.Open(filteredExternal, filteredInternal)

[CheckUpdateWindow 关闭，m_DontShowAgain == true]
      └─ EditorUtil.CheckUpdate.MarkSkip(items) → SaveConfig()
```

---

## §7 线程模型

- `CheckAsync` 和 `CheckOnStartupAsync` 均为 `async Task`，在 Unity Editor 主线程的 async 上下文中执行。
- 内部通过 `PlugPals.FetchRemotePackagesAsync` 发起网络请求（`HttpClient` + `await`）；返回后回到主线程。
- `CheckOnStartupAsync` 以 fire-and-forget（`_ = CheckOnStartupAsync()`）方式调用，异常全部在方法内部 `catch` 静默处理，不向外传播。
- `LoadConfig` 和 `SaveConfig` 为同步文件操作，调用时必须在主线程。

---

## §8 初始化时序

```
[InitializeOnLoad] Initializer 静态构造器（Editor 加载时）
  └─ EditorApplication.delayCall += OnDelayedStart（注册延迟回调）

OnDelayedStart()（下一帧 Editor 就绪后）
  ├─ 注销自身：delayCall -= OnDelayedStart
  └─ _ = CheckOnStartupAsync()（fire-and-forget）

CheckOnStartupAsync()
  ├─ CheckExternalAsync()
  │    ├─ PlugPals.FetchRemotePackagesAsync(c_RegistryUrl, c_ApiPath, CancellationToken.None)
  │    └─ PlugPals.ReadInstalledVersions()
  ├─ CheckInternalAsync()
  │    ├─ PlugPals.FetchRemotePackagesAsync(c_InternalRegistryUrl, c_ApiPath, CancellationToken.None)
  │    └─ PlugPals.ReadInstalledVersions()
  ├─ LoadConfig()：读取 Library/Nova/CheckUpdate.json → SkipConfig
  ├─ 过滤 SkipVersions（key 含仓库前缀）
  └─ CheckUpdateWindow.Open(filteredExternal, filteredInternal)（仅当任一列表非空）
```

---

## §9 关键算法

### SemVer 比较（CompareSemVer）

`internal static int CompareSemVer(string a, string b)`

比较两个语义版本号，返回正数（a > b）、0（相等）、负数（a < b）：

1. 空字符串处理：两者均空返回 0；a 为空返回 -1；b 为空返回 1。
2. 剥离 pre-release 后缀（取 `-` 前核心版本号），记录 `hasPreA` / `hasPreB`。
3. 将核心版本号按 `.` 分割为整数数组，逐段比较。
4. 若某一方段数不足，以 0 补齐（如 `1.2` vs `1.2.0` 视为相等）。
5. 核心版本号相同时：有 pre-release 后缀（`-rc1`）的版本 < 无后缀版本。
6. 整数解析失败时兜底：`string.Compare(a, b, StringComparison.Ordinal)`。

### 跳过版本语义

`SkipConfig.SkipVersions` 存储 `{ 带仓库前缀的包 key: 被跳过的 latestVersion }`，例如：

- `external:com.solotopia.nova.framework`
- `internal:com.solotopia.nova.framework`

判断规则：仅当 `skippedVersion == info.LatestVersion`（字符串相等）时跳过——即新版本出现后仍会弹窗提示，不会被旧的跳过记录屏蔽。

---

## §10 常见误区

**误区 1：`ClearSkip` 不是按包清除**

`ClearSkip` 写入全新空 `SkipConfig`，清除**所有**包的跳过记录，而非某一个包。如需按包清除，应在调用方手动修改 `SkipConfig.SkipVersions` 后调用 `SaveConfig`（当前无公开单包清除 API）。

**误区 2：`CheckAsync` 不过滤跳过版本**

`CheckAsync` 返回**所有** `latestVersion > currentVersion` 的包，不应用跳过过滤；跳过过滤仅在 `CheckOnStartupAsync` 中执行。手动打开 CheckUpdateWindow 时由窗口分别调用双仓库检查逻辑，因此会展示所有可用更新（包括已跳过的版本）。

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
// 跳过所有当前检查到的更新（启动时不再弹窗，直到有更新的版本出现）
EditorUtil.CheckUpdate.MarkSkip(updates);

// 清除所有跳过记录（下次启动会重新检查弹窗）
EditorUtil.CheckUpdate.ClearSkip();
```

---

## §12 注意事项

- `c_RegistryUrl` 与 `c_InternalRegistryUrl` 现已拆分为两个固定地址；后续如仓库迁移，只需替换 Visitors 常量。
- 双仓库检查采用“单边失败不阻断另一边成功结果”的策略；日志中会分别记录失败仓库地址。
- `Library/Nova/CheckUpdate.json` 不受版本控制，多人协作时跳过配置不同步。
- `[InitializeOnLoad]` 仅在 Editor 启动（域重载）时触发；Play Mode 进出不会再次触发。

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [CheckUpdateWindow.md](../../Windows/CheckUpdateWindow.md) — 版本更新提示窗口
- [PlugPalsWindow.md](../../Windows/PlugPalsWindow.md) — UPM 包管理窗口（依赖其 `FetchRemotePackagesAsync` / `ReadInstalledVersions`）
- [Editor.md](../../Editor.md) — Editor 层级总览
