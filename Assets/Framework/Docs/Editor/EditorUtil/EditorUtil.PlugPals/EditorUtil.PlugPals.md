# EditorUtil.PlugPals

**类签名**：`public static partial class EditorUtil.PlugPals`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.PlugPals`

私有 Verdaccio 仓库 UPM 包管理工具层，提供远程包列表拉取、本地版本读取、manifest 读写、包安装/卸载、CHANGELOG 拉取、安装前依赖预检与缺失库引导功能。

当前边界：

- `EditorUtil.PlugPals` 负责**消费侧**能力：registry 查询、manifest/scoped registry 维护、安装卸载、CHANGELOG 拉取。
- Nova 的**正式发布侧**已切换为“`UPM CLI` 先签名 tarball，再上传 Verdaccio”的链路；不再以包目录直发作为正式入口。
- 因此，本类不承担签名或发布动作，发布逻辑仍由 `.agents/skills/nova-publish/scripts/publish_packages.py` 统一处理。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil.PlugPals.cs` | `partial EditorUtil.PlugPals` | 公有接口：所有 public static 方法 |
| `EditorUtil.PlugPals.Visitors.cs` | `partial EditorUtil.PlugPals` | 常量声明：`s_HttpClient`、`c_ManifestRelativePath`、`c_ChangelogCacheRelDir`、`c_TarballCacheRelDir`、`c_ChangelogTarEntry` |
| `EditorUtil.PlugPals.Methods.cs` | `partial EditorUtil.PlugPals` | 私有方法：`ResolveEntryVersion`、`GetCachedPackageVersion`、`IsNonRegistryReference`、`RunTarExtract`、`ExtractScope`、`EnsureScopedRegistry`、`CleanupScopedRegistry` |
| `EditorUtil.PlugPals.Definitions.cs` | `partial EditorUtil.PlugPals` | 嵌套类型：`PackageCategory`、`PackageStatus`、`VerdaccioPackageInfo`、`PackageDisplayEntry`、`PackageJsonData`、`NovaPackageMetadata`、`RequiredLibraryGuide`、`MissingRequiredLibraryInfo`、`ManifestData`、`ScopedRegistry`、`LocalPackageJson`、`PackagesLockData`、`PackagesLockEntry` |
| `EditorUtil.PlugPals.RequiredLibraries.cs` | `partial EditorUtil.PlugPals` | 安装前依赖预检查（`CheckDependencies`）：遍历目标包 `dependencies`，判定 registry 命中与缺失库，缺失时打开 `PlugPalsMissingRequiredLibrariesWindow` 引导并中止安装 |

---

## §3 继承关系

```
NovaFramework.Editor.EditorUtil (public static partial class)
  └── PlugPals (public static partial class)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `s_HttpClient` | `static readonly HttpClient` | `Timeout = 15s` | 全局复用的 HttpClient 实例，避免 socket 耗尽 |
| `c_ManifestRelativePath` | `const string` | `"Packages/manifest.json"` | manifest.json 相对工程根目录路径（private） |
| `c_ChangelogCacheRelDir` | `internal const string` | `"Library/Nova/Changelog"` | 更新日志本地缓存目录，相对工程根目录（永久缓存） |
| `c_TarballCacheRelDir` | `internal const string` | `"Library/Nova/Tarballs"` | tarball 临时下载目录，相对工程根目录（解压后删除） |
| `c_ChangelogTarEntry` | `internal const string` | `"package/CHANGELOG.md"` | tarball 内更新日志的固定路径 |

---

## §5 完整公开 API

### 远程包数据

```csharp
/// <summary>
/// 异步从 Verdaccio 仓库拉取远程包信息列表。
/// </summary>
public static async Task<VerdaccioPackageInfo[]> FetchRemotePackagesAsync(string registryUrl, string apiPath, CancellationToken token)

/// <summary>
/// 根据远程包信息和本地已安装版本构建显示条目列表（按 DisplayName 排序）。
/// </summary>
public static List<PackageDisplayEntry> BuildDisplayEntries(VerdaccioPackageInfo[] remotePackages, string registryUrl)
```

### 本地版本读取

```csharp
/// <summary>
/// 从 packages-lock.json 读取所有已安装包的实际版本号（含直接和传递依赖）。
/// </summary>
/// <returns>包名到版本号的字典，读取失败返回 null。</returns>
public static Dictionary<string, string> ReadInstalledVersions()
```

### manifest 操作

```csharp
/// <summary>
/// 读取 manifest.json 并反序列化（仅解析 scopedRegistries 和 dependencies）。
/// </summary>
public static ManifestData ReadManifest(string manifestPath)

/// <summary>
/// 将 ManifestData 变更合并回原始 manifest.json（保留未建模字段，JObject 局部更新）。
/// </summary>
public static void SaveManifest(string manifestPath, ManifestData manifest)
```

### 包安装/卸载

```csharp
/// <summary>
/// 安装指定包到 manifest.json 并触发 UPM 解析；安装前执行 CheckDependencies 做依赖预检（零额外联网），缺失库时中止并弹引导窗口。
/// </summary>
public static bool InstallPackage(string manifestPath, string registryUrl, string registryName, PackageDisplayEntry entry)

/// <summary>
/// 从 manifest.json 中卸载指定包并触发 UPM 解析。
/// </summary>
public static void UninstallPackage(string manifestPath, string registryUrl, PackageDisplayEntry entry)

/// <summary>
/// 延迟触发 UPM 包解析，优先使用 Client.Resolve（Unity 2020.1+），不可用时回退 AssetDatabase.Refresh。
/// </summary>
public static void ResolvePackages()
```

### 版本比较

```csharp
/// <summary>
/// 比较本地版本与远程版本，确定包安装状态。
/// </summary>
public static PackageStatus CompareVersions(string local, string remote, bool isNonRegistry)

/// <summary>
/// 根据包名前缀判定包分类（SDK / Kit / Framework / Other）。
/// </summary>
public static PackageCategory CategorizePackage(string packageName)
```

### Samples

```csharp
/// <summary>
/// 读取已安装包的 Samples 列表（基于 UPM 标准 Sample.FindByPackage）。
/// </summary>
/// <returns>Samples 列表，无声明时返回空数组（不返回 null）。</returns>
public static IReadOnlyList<Sample> GetPackageSamples(string packageName, string version)
```

### CHANGELOG 拉取

```csharp
/// <summary>
/// 异步获取指定包版本的更新日志，优先命中本地缓存（永久缓存）；
/// 缓存缺失时从 Verdaccio 下载 tarball 解出 CHANGELOG.md 并缓存。
/// </summary>
/// <param name="registryUrl">Verdaccio 仓库地址（如 https://upm.solotopiax.com）。</param>
/// <param name="packageName">UPM 包名。</param>
/// <param name="version">目标版本号。</param>
/// <param name="token">取消令牌。</param>
/// <returns>缓存后的 CHANGELOG.md 绝对路径；失败返回 null。</returns>
public static async Task<string> FetchChangelogAsync(string registryUrl, string packageName, string version, CancellationToken token)
```

缓存路径：`Library/Nova/Changelog/<packageName>/<version>.md`（相对工程根目录，永久缓存）。

缓存命中直接返回绝对路径；未命中时流程：下载 tarball → 调用系统 `tar` 解压 `package/CHANGELOG.md` → 移动到缓存目录，tarball 和临时解压目录在 `finally` 块中清理。

失败情形（HTTP 错误 / 包内无 `CHANGELOG.md` / tar 解压失败 / 操作取消）均返回 `null`，调用方负责向用户给出提示。

---

## §9 关键算法

### 安装前依赖预检与缺失库引导（CheckDependencies）

触发时机：点击某包的「安装/更新」按钮时，`CheckDependencies` 立即执行，使用 `PlugPalsWindow` 打开时已 fetch 到内存的外网（upm.solotopiax.com）与内部云（4874）包列表做判定，**零额外联网**。

判定数据源：目标包的 `dependencies` 字段（必须完整声明，不遗漏）。`nova.requiredLibraries` 仅作展示/提醒元数据（`displayName`、`purchaseUrl`），不参与命中判定。

每个依赖按以下顺序短路判定，命中任一即跳过（不进入缺失集）：

1. **本地已安装**：`ReadInstalledVersions()` 中包含该依赖名。
2. **`com.solotopia.` 前缀**（`IsSolotopiaPackageName`）：主包本身及同源包，安装时 scope 已配，UPM 可自动解析。
3. **`com.unity.` 前缀**（`IsUnityPackageName`）：Unity 官方默认 registry 兜底，无需额外配置。
4. **命中内存 registry 包列表**：依赖名存在于已 fetch 的外网或内部云包列表中 → 记为「待自动配 scope 安装」（进 `ToAutoScope`），并记录来源（`RegistrySource`：外网/内部云）。

以上均不命中 → 判为**缺失库**（进 `Missing`）。

**有缺失库时**：打开 `PlugPalsMissingRequiredLibrariesWindow`，展示各缺失库的 `displayName`（取自 `nova.requiredLibraries`）与 `purchaseUrl`（购买地址），并附「Solotopia 成员请到内部云仓库自行安装」提示。**本次安装中止，不写 manifest**。

**无缺失库时**：对每个 `ToAutoScope` 依赖按其 `RegistrySource`（外网/内部云）调用 `EnsureScopedRegistry` 自动配 scope；再为主包配 scope、写 `manifest.dependencies`、触发 UPM Resolve。命中的依赖随主包由 UPM 一并解析安装。

宏机制说明：PlugPals **不再注入或管理任何宏**。旧的 `requiredLibraries.defineSymbols` 注入、`PlugPalsInjectedDefines.json` 账本、后台审计弹窗、会话级抑制、以及「scope 已注册」判据均已移除。可选库的宏改由各 asmdef 的 `versionDefines` / `defineConstraints`（Unity 原生「某包存在→自动定义某宏」机制）自行处理。

BestHTTP 后端的依赖约定：

1. BestHTTP 后端由独立包 `com.solotopia.nova.framework.besthttp` 提供，主框架包不再携带 BestHTTP 适配程序集。
2. `NovaFramework.BestHTTP.Runtime` 不再使用宏开关；安装适配包且原厂程序集存在时直接编译。
3. `com.solotopia.nova.framework.besthttp` 的 `nova.requiredLibraries` 声明 `com.tivadar.best.http` 与 `com.tivadar.best.tlssecurity`，用于消费端缺库提示与内部云仓库引导。
4. TLS 包依赖 `com.tivadar.best.http`；`requiredLibraries` 同时列出两者，确保缺少任一原厂包时都能弹出引导。

### CHANGELOG 缓存与下载策略（FetchChangelogAsync）

1. 计算缓存路径 `Library/Nova/Changelog/<packageName>/<version>.md`，命中直接返回绝对路径（永久缓存，不校验内容）。
2. 构造 tarball URL：`<registryUrl>/<packageName>/-/<pkgBaseName>-<version>.tgz`。
3. 下载 tarball 字节写入 `Library/Nova/Tarballs/<pkgBaseName>-<version>.tgz`。
4. 调用系统 `tar -xzf <tarball> -C <extractDir> package/CHANGELOG.md` 仅解压单文件（`RunTarExtract`，超时 15 秒）。
5. 将解压出的文件移动到缓存目录，`finally` 块删除 tarball 和临时解压目录。

### 包状态判定（CompareVersions）

1. `local == null` 时：仓库包记为 `NotInstalled`，非仓库引用记为 `NonRegistry`。
2. `isNonRegistry == true` 时：只要本地已存在，一律返回 `NonRegistry`，不因为远端版本变化转成 `Upgradeable`。
3. 普通仓库包仅在 `remote > local` 时返回 `Upgradeable`。
4. `local == remote` 与 `local > remote` 都返回 `Installed`。

### manifest.json 局部更新（SaveManifest）

使用 `JObject` 局部合并：读取原始 JSON → 仅覆盖 `scopedRegistries` 和 `dependencies` 两个字段 → 保留 `testables`、`enableLockFile` 等未建模字段。`scopedRegistries` 为空时从根对象删除该字段（避免空数组残留）。

---

## §11 使用示例

### 拉取远程包列表并构建显示条目

```csharp
// CheckUpdate 内部用法示例
VerdaccioPackageInfo[] remotePackages = await EditorUtil.PlugPals.FetchRemotePackagesAsync(
    "https://upm.solotopiax.com",
    "/-/verdaccio/data/packages",
    CancellationToken.None);

List<EditorUtil.PlugPals.PackageDisplayEntry> entries =
    EditorUtil.PlugPals.BuildDisplayEntries(remotePackages, "https://upm.solotopiax.com");
```

### 拉取 CHANGELOG

```csharp
string changelogPath = await EditorUtil.PlugPals.FetchChangelogAsync(
    "https://upm.solotopiax.com",
    "com.solotopia.nova.framework",
    "1.2.0",
    cancellationToken);

if (changelogPath != null)
{
    // 读取文件内容展示到窗口
    string content = Util.SysIO.File.ReadAllTextSync(changelogPath);
}
else
{
    Log.Warning("未能获取 CHANGELOG，请检查网络或确认包内含 CHANGELOG.md。");
}
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [EditorUtil.CheckUpdate.md](../EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) — 版本检查工具（依赖本类 `FetchRemotePackagesAsync` / `ReadInstalledVersions`）
- [PlugPalsWindow.md](../../Windows/PlugPalsWindow.md) — UPM 包管理窗口（依赖本类所有公有接口）
- [Editor.md](../../Editor.md) — Editor 层级总览
