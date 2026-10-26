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
| `EditorUtil.PlugPals.Definitions.cs` | `partial EditorUtil.PlugPals` | 包、registry、manifest 与 packages-lock 数据模型 |
| `EditorUtil.PlugPals.AgentOperations.cs` | `partial EditorUtil.PlugPals` | 消费项目 Agent 的单包 `Plan -> Confirm -> Execute -> Verify` Adapter |
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
| `s_HttpClient` | `static readonly HttpClient` | `Timeout = 10s` | 全局复用的 HttpClient 实例，避免 socket 耗尽 |
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

/// <summary>按需补充各版本发布时间；原三参数入口不增加额外查询。</summary>
public static Task<VerdaccioPackageInfo[]> FetchRemotePackagesAsync(string registryUrl, string apiPath, CancellationToken token, bool includeReleaseTimes)

/// <summary>按编辑器本地时区生成独立展示的 24 小时制发布时间。</summary>
public static string FormatReleaseTime(string releaseTime)

/// <summary>
/// 根据远程包信息和本地已安装版本构建显示条目列表（按 DisplayName 排序）。
/// </summary>
public static List<PackageDisplayEntry> BuildDisplayEntries(VerdaccioPackageInfo[] remotePackages, string registryUrl)
```

详情时间统一存为包内 `nova.releaseTime`（UTC ISO 8601，精确到秒，例如 `2026-10-26T08:09:10Z`）。正式发布器在公网、内网签名前记录；消费侧只读，不修改已发布包或用文件修改时间回填。

- `VerdaccioPackageInfo.VersionReleaseTimes`：按精确版本记录有效时间；包内字段优先，旧包回退仓库完整元数据的 `time[版本号]`。不使用 `created`、`modified` 或其他版本时间代替。
- `PackageDisplayEntry.LatestReleaseTime`：远端最新版本的时间，供详情独立的 `Release Time` 行显示；本地版本不展示时间。
- 四参数拉取入口用于窗口详情的时间补充，单仓库最多并发 4 个元数据请求；调用方取消令牌照常传播，单包失败只使该包时间未知。现有三参数入口继续只查列表，自动更新和 Agent 包操作不额外请求全部版本元数据。
- `FormatReleaseTime` 输出 `yyyy/MM/dd HH:mm:ss`，转换为编辑器本地时区；非法或缺失时间显示 `未知`。原始版本字段不包含时间，不改变比较、安装、缓存或依赖规则。

### 本地版本读取

```csharp
/// <summary>
/// 从 Unity 当前已注册包及其 resolvedPath/package.json 读取实际安装版本。
/// </summary>
/// <returns>包名到版本号的字典，读取失败返回 null。</returns>
public static Dictionary<string, string> ReadInstalledVersions()
```

`packages-lock.json` 只表示 UPM 期望解析图，不再作为“已安装”证据。PlugPals 会联合核对 `manifest.json`、lock、Unity Registered Packages、`resolvedPath` 与实际 `package.json`：声明或锁定版本未形成有效注册包、注册包带解析错误、路径或元数据缺失、实际版本与 lock 不一致时，条目标记为 `ResolutionFailed`，不会显示成“已安装”。开发仓通过 manifest 直接声明的 `file:` 包是明确例外：只有引用目录真实存在且 `package.json` 包名、版本有效时才视为已安装，以兼容 `file:../Assets/Framework` 这类源码开发形态。

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
public static bool InstallPackage(string manifestPath, string registryUrl, string registryName, PackageDisplayEntry entry, IReadOnlyDictionary<string, RegistrySource> knownRegistryPackages)

/// <summary>
/// 从 manifest.json 中卸载指定包并触发 UPM 解析。
/// </summary>
public static void UninstallPackage(string manifestPath, string registryUrl, PackageDisplayEntry entry, ISet<string> registryUrlsNeededByOthers)

/// <summary>
/// 延迟触发 UPM 包解析，优先使用 Client.Resolve（Unity 2020.1+），不可用时回退 AssetDatabase.Refresh。
/// </summary>
public static void ResolvePackages()
```

### Agent 单包 Adapter

安装提交前，`InstallPackage` 会把显式包名、原版本和目标版本写入本地重要声明批次；已提交仍不等于实际成功。UPM 稳定后由 [ImportantUpdates](../EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md) 从实际包内 CHANGELOG 提取并强制展示。单包只显示当前包，批量 GUI 使用一个工具层批次合并；这不会替代原有依赖预检、Resolve 或 Agent Verify，也不会把普通更新的“跳过版本”当作重要声明已读。

消费端 Skill 的稳定入口是 C# Project Action `nova.project.upm.manage-latest`，由 `EditorUtil.AgentActions` 提供统一 Registry、一次性计划、确认令牌、资源锁与结果协议；该 Handler 复用以下 PlugPals 领域实现，不复制 UPM 业务逻辑。

```csharp
public static Task<ProjectPackageOperationPlan> PlanProjectPackageOperationAsync(
    ProjectPackageOperationRequest request,
    CancellationToken token)

public static Task<ProjectPackageOperationResult> ExecutePlannedProjectPackageOperationAsync(
    string planId,
    bool confirmed,
    CancellationToken token)

public static ProjectPackageOperationResult VerifyProjectPackageOperation(
    ProjectPackageOperationReceipt receipt)
```

该入口只支持 `install-latest`、`upgrade-latest` 与 `uninstall`，不接受指定版本、降级、任意 registry、来源切换或 Framework 自身。Plan 必须完整读取全部已配置 registry，任一来源失败都会阻断，避免在来源或依赖信息不完整时写项目；Execute 仅消费一次已确认且未漂移的计划，写入 manifest 并排队 Resolve 后返回 `partial`；只有 Verify 在 Unity 包更新与 domain reload 稳定后核对 manifest、packages-lock 的精确版本与 registry URL，才返回 `success`。卸载前会遍历 packages-lock 的依赖图阻止仍有消费者的目标，并使用当前已安装包元数据判断共享 registry。

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

1. **`com.solotopia.` 前缀**（`IsSolotopiaPackageName`）：主包本身及同源包，安装时 scope 已配，UPM 可自动解析。
2. **`com.unity.` 前缀**（`IsUnityPackageName`）：Unity 官方默认 registry 兜底，无需额外配置。
3. **被包自带 `nova.scopedRegistries` scope 前缀覆盖**（`IsCoveredByDeclaredRegistries`）：依赖由包声明的私有仓库提供（如 MAX 包声明的 AppLovin 仓库），安装时写入该私有仓库的 scoped registry，但依赖本身保持为主包 `package.json` 的传递依赖，不展开写入项目顶层 `manifest.dependencies`。
4. **被项目 `manifest.json` 已配 `scopedRegistries` scope 前缀覆盖**（`IsCoveredByProjectScopedRegistries`）：用户已为该依赖手工配置好私有仓库（典型场景：OpenUPM 提供 `com.google.external-dependency-manager`），不再误判缺库、也不再尝试自动配 scope，交由 UPM 解析拉取。
5. **本地已安装**：Unity Registered Packages 中存在该包，且 `resolvedPath/package.json` 可读取、包名与版本有效、注册信息不含解析错误。
6. **命中内存 registry 包列表**：依赖名存在于已 fetch 的外网或内部云包列表中 → 记为「待自动配 scope 安装」（进 `ToAutoScope`），并记录来源（`RegistrySource`：外网/内部云）。

以上均不命中 → 判为**缺失库**（进 `Missing`）。

**有缺失库时**：打开 `PlugPalsMissingRequiredLibrariesWindow`，展示各缺失库的 `displayName`（取自 `nova.requiredLibraries`）与 `purchaseUrl`（购买地址），并附「Solotopia 成员请到内部云仓库自行安装」提示。**本次安装中止，不写 manifest**。

**无缺失库时**：对每个 `ToAutoScope` 依赖按其 `RegistrySource`（外网/内部云）调用 `EnsureScopedRegistry` 自动配 scope；再为主包配 scope、写主包到 `manifest.dependencies`、触发 UPM Resolve。被包自带 `nova.scopedRegistries` 覆盖的依赖不写顶层，随主包 package.json dependencies 由 UPM 作为传递依赖解析；旧版 PlugPals 曾写入顶层的同类依赖仍会在卸载主包时兼容清理。

宏机制说明：PlugPals **不再注入或管理任何宏**。旧的 `requiredLibraries.defineSymbols` 注入、`PlugPalsInjectedDefines.json` 账本、后台审计弹窗、会话级抑制、以及「scope 已注册」判据均已移除。可选库的宏改由各 asmdef 的 `versionDefines` / `defineConstraints`（Unity 原生「某包存在→自动定义某宏」机制）自行处理。

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

## 来源、解析状态与后验的实际范围

显示链 ReadInstalledVersions 会核真实注册/error/metadata，但领域 ResolveInstalledVersion 对 registry 仍主要返回 lock.version，不能因名字就当真实安装版本。Agent Verify 的安装分支核 manifest、lock版本/来源 URL，尚未重新证明 registered/errors/resolvedPath；卸载分支若读取失败被底层投影 null，也可能与“双不存在”混同，必须先独立确认 manifest/lock 可读有效。领域 success 不应扩大成编译、包 API 或安装已就绪的保证。

计划读取远端期间未反复冻结本地模型，Store 的 fingerprint 可能晚于模型读取；Execute 消费内存计划后重查 latest，但远端 await 后未再核本地 Hash，依赖/nova/scopes 也不只靠版本号保证相同。fingerprint 只含 manifest/lock，缺失与空等同，不含实际包、registry存档或 Editor状态。确认/锁/Play门由 Action 组合，底层方法不独立提供完整保护。

依赖预检只按名字/声明 scope，未核依赖版本约束或完整递归图；manifest 已声明未 Resolve 也被当存在。自动 scope 取包名前两段，声明同 URL 可整条覆盖用户 scopes，卸载主 scope 清理和 legacy 顶层依赖删除不是可靠所有权账本；GUI legacy 卸载可删来源不明 direct 项，领域 Action 的消费者保护不能反推 GUI 也有。提交后 true 只表示已写 manifest/排队 Resolve，异常可发生在 entry Pending 状态更新前，无回滚。

SaveManifest 读全文保留未知字段后直接重写 dependencies/scopedRegistries，非原子且有并发覆盖窗口。registry存档失败可能只告警，窗口仍继续同步 manifest。远端版本比较剥预发布、按数字段或文本回退，不是完整 SemVer；git缓存取首个目录也不绑定准确 revision，不能据缓存证明当前加载。

CHANGELOG 缓存按包名/版本，不含 registry，跨源同名同版本可能复用旧日志。tar 先同步 ReadToEnd 再进入15秒等待，stdout未排干，不能承诺总耗时15秒；token不终止 tar。下载/解压错误通常 null，但 finally 清理仍可外抛、并发同名临时路径可冲突。Sample Import 覆盖旧导入，不提供整批事务。
