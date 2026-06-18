---
title: Unity YooAsset 教程
summary: YooAsset 3.0 热更与 HostPlayMode 学习整理
category: external
status: active
keywords:
  - Unity
  - YooAsset
  - 热更
  - HostPlayMode
---

# Unity_YooAsset_教程

## 使用说明

- 本文定位为 YooAsset 3.0 热更学习整理稿，重点覆盖 HostPlayMode、清单更新、CDN 部署与样例状态机拆解。
- 正文保留必要的源码级推导与样例对照，便于回查落地细节。
- 若只想快速建立心智模型，优先阅读第一部分、第三部分和第八部分。

> 本文档整合一轮**经过源码核验**的 YooAsset 3.0 热更知识，不保留已被推翻的猜测结论。
>
> 源码版本：`Assets/YooAsset/` 内 YooAsset 3.0.0-beta
> 参考环境：Unity 6000.4.2f1 + Built-in + UniTask + IL2CPP + HybridCLR（Nova Framework）

---

## 目录

- [第一部分 · Ch7 热更流程（A 层精简版）](#第一部分--ch7-热更流程a-层精简版)
- [第二部分 · CopyBuiltinPackageManifest 参数行为](#第二部分--copybuiltinpackagemanifest-参数行为)
- [第三部分 · CDN 文件夹结构](#第三部分--cdn-文件夹结构)
- [第四部分 · Bundle 文件名三种风格与覆盖风险](#第四部分--bundle-文件名三种风格与覆盖风险)
- [第五部分 · 内置资源 vs 可热更资源的 Package 划分](#第五部分--内置资源-vs-可热更资源的-package-划分)
- [第六部分 · 内置资源能否走热更（FileHash 机制）](#第六部分--内置资源能否走热更filehash-机制)
- [第七部分 · 两个文件系统的查找顺序](#第七部分--两个文件系统的查找顺序builtin-先sandbox-兜底)
- [第八部分 · 排障 Cheat Sheet](#第八部分--排障-cheat-sheet)
- [附录 C · PatchManager 状态机架构（B 层进阶参考）](#附录-c--patchmanager-状态机架构b-层进阶参考)
- [附录 D · 综合速查 / 后续专题](#附录-d--综合速查--后续专题)

---

## 第一部分 · Ch7 热更流程（A 层精简版）

### 1.1 代码分层：三层要分开看

| 层 | 谁写的 | 用户要不要照抄 |
|---|---|---|
| **A. YooAsset 框架 API**（`package.InitializePackageAsync` 这类调用） | YooAsset 官方 | ✅ 必须调，API 签名不能改 |
| **B. Space Shooter 的 PatchManager**（状态机 + 事件重试那一整套） | 样例作者 | ❌ **不是必须**，是"参考架构" |
| **C. 业务侧接入代码**（你自己的） | 你自己 | 自由发挥 |

**Space Shooter 样例里的 `PatchManager.cs` 是 B 层**——样例作者秀的一种"工程化"写法（状态机 + 事件驱动重试），**不是 YooAsset 内部实现**，**也不是用户必须这么写**。

0 基础阶段，**用户要掌握的是 A 层 API 的调用顺序**，B 层完全可以用最朴素的 `async/await` 平铺直叙。

---

### 1.2 运行模式与对应 Options 类

`InitializePackageAsync(options)` 的 `options` **必须**是下面 4 种之一：

| 运行模式 | 要 new 的 Options 类 |
|---|---|
| EditorSimulateMode（编辑器模拟） | `EditorSimulateModeOptions` |
| OfflinePlayMode（纯本地包，不热更） | `OfflinePlayModeOptions` |
| **HostPlayMode（标准联网热更，主线教程用这个）** | **`HostPlayModeOptions`** |
| WebPlayMode（WebGL / 小游戏） | `WebPlayModeOptions` |

---

### 1.3 HostPlayModeOptions 完整配置

它要你配 **2 个文件系统 + 1 个远端服务**：

```csharp
var options = new HostPlayModeOptions();

// ---- ① 内置文件系统：指向打包时塞进 APK/IPA 的 StreamingAssets/yoo ----
options.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
options.BuiltinFileSystemParameters.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);

// ---- ② 缓存文件系统：指向沙盒 persistentDataPath/yoo，下载的 Bundle 存这里 ----
IRemoteService remoteService = new MyRemoteService("http://cdn.mygame.com/Android/v1.0");
options.CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, 5);
options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxRequestPerFrame, 1);
options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, 10);
```

#### ① `BuiltinFileSystemParameters`

告诉 YooAsset 去 `StreamingAssets/yoo/` 找内置资源。

`CreateDefaultBuiltinFileSystemParameters()` 是 YooAsset 给的默认工厂，路径是 `Application.streamingAssetsPath/yoo`（来自 `YooAssetConfiguration.GetDefaultBuiltinRoot()`）。**一般不用改**。

`CopyBuiltinPackageManifest = true` 的源码级行为见第二部分。

#### ② `CacheFileSystemParameters`

告诉 YooAsset 下载来的 Bundle 存哪、怎么下。

`CreateDefaultSandboxFileSystemParameters(remoteService)` 工厂默认用 `Application.persistentDataPath/yoo` 作为缓存目录，同时把你传进去的 `remoteService` 挂上，**下载时调 `remoteService.GetRemoteUrls(文件名)` 拿到 CDN 完整 URL**。

**3 个性能参数：**

| 参数 | 含义 | 样例建议值 |
|---|---|---|
| `DownloadMaxConcurrency` | 同时下载的最大连接数 | 5~10 |
| `DownloadMaxRequestPerFrame` | 单帧最多发起多少个新请求（防卡顿） | 1 |
| `DownloadWatchdogTimeout` | 单连接无进度看门狗（秒） | 10 |

#### ③ `IRemoteService`

接口，YooAsset 不给默认实现，**你必须手写一个**。最简版：

```csharp
public class MyRemoteService : IRemoteService
{
    private readonly string _cdnRoot;
    public MyRemoteService(string cdnRoot) => _cdnRoot = cdnRoot;

    public IReadOnlyList<string> GetRemoteUrls(string fileName)
    {
        return new[] { $"{_cdnRoot}/{fileName}" };
    }
}
```

YooAsset 每次要下载一个文件时，call `GetRemoteUrls(文件名)`，你返回**一条或多条**完整 URL。多条用于 CDN 故障转移（主 CDN 挂了自动试备用 CDN）。

---

### 1.4 完整的 7 步流程代码（摊开版）

```csharp
using Cysharp.Threading.Tasks;
using YooAsset;

public static class GameLauncher
{
    public static async UniTask LaunchAsync()
    {
        string packageName = "DefaultPackage";

        // ========== Options 配置 ==========
        var options = new HostPlayModeOptions();
        options.BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        options.BuiltinFileSystemParameters.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);

        IRemoteService remoteService = new MyRemoteService("http://cdn.mygame.com/Android/v1.0");
        options.CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
        options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, 5);
        options.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, 10);

        // ========== ① 造 Package ==========
        if (!YooAssets.TryGetPackage(packageName, out var package))
            package = YooAssets.CreatePackage(packageName);

        // ========== ② 初始化：连文件系统 ==========
        var initOp = package.InitializePackageAsync(options);
        await initOp;
        if (initOp.Status != EOperationStatus.Succeeded)
        {
            Debug.LogError($"Init failed: {initOp.Error}");
            return;
        }

        // ========== ③ 问 CDN 要最新版本号 ==========
        var versionOp = package.RequestPackageVersionAsync();
        await versionOp;
        if (versionOp.Status != EOperationStatus.Succeeded) return;
        string packageVersion = versionOp.PackageVersion;   // 例 "v3_a4b5c6..."

        // ========== ④ 下载并加载清单 ==========
        var manifestOp = package.LoadPackageManifestAsync(new LoadPackageManifestOptions(packageVersion, 60));
        await manifestOp;
        if (manifestOp.Status != EOperationStatus.Succeeded) return;

        // ========== ⑤ 算差异 ==========
        var downloader = package.CreateResourceDownloader(new ResourceDownloaderOptions(
            downloadingMaxNum: 10,     // 并发
            failedTryAgain: 3));       // 单文件失败重试次数

        if (downloader.TotalDownloadCount > 0)
        {
            Debug.Log($"Found {downloader.TotalDownloadCount} files, total {downloader.TotalDownloadBytes} bytes.");

            // ========== ⑥ 真·下载 ==========
            downloader.DownloadProgressChanged += (total, curr, totalBytes, currBytes) =>
            {
                Debug.Log($"Progress: {curr}/{total}  ({currBytes}/{totalBytes} bytes)");
            };
            downloader.DownloadError += (fileName, error) =>
            {
                Debug.LogError($"Download failed: {fileName} - {error}");
            };
            downloader.StartDownload();
            await downloader;

            if (downloader.Status != EOperationStatus.Succeeded) return;
        }
        else
        {
            Debug.Log("No updates needed.");
        }

        // ========== ⑦ 清掉清单里不再引用的旧 Bundle ==========
        var clearOp = package.ClearCacheAsync(new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles));
        await clearOp;

        // ========== 热更结束，把 package 交给业务 ==========
        GameBootstrap.GamePackage = package;   // 业务侧自己存引用，YooAsset 没"默认包"概念
        // 后续业务侧调 GameBootstrap.GamePackage.LoadAssetAsync<T>("xxx")
    }
}

public static class GameBootstrap
{
    public static ResourcePackage GamePackage;
}
```

**这就是 0 基础玩家上线要写的全部代码。** 不需要状态机，不需要事件系统，朴素 `async/await` 够用。

---

### 1.5 7 个 API 逐个讲清

#### ① `CreatePackage` —— 造包

```csharp
if (!YooAssets.TryGetPackage(packageName, out var package))
    package = YooAssets.CreatePackage(packageName);
```

**为什么先 Try 再 Create？** 幂等。玩家点"重试"时再调一次 Launch 不会崩。

**packageName 是什么？** "包的名字"（如 `DefaultPackage`、`RawFilePackage`），必须和构建时 Collector 里的 Package 名**完全一致**。

#### ② `InitializePackageAsync(options)` —— 连文件系统

把上面配的 options 丢进去。内部做的事：
- 初始化 `BuiltinFileSystem`（指向 StreamingAssets/yoo）
- 初始化 `CacheFileSystem`（指向 persistentDataPath/yoo，绑定 IRemoteService）
- **不**下载任何东西，只是把文件系统挂好

#### ③ `RequestPackageVersionAsync()` —— 问版本号

从 CDN 拉 `<PackageName>.version` 这个小文件（1KB 量级）。拿到的字符串就是当前 CDN 上的最新版本号。

**为什么单独一步？** 版本号很小，先拿来判断"要不要下载几 MB 的清单"。如果本地缓存版本号 == 远端版本号，清单都不用重新下。

#### ④ `LoadPackageManifestAsync(LoadPackageManifestOptions(version, 60))` —— 下清单

按版本号下载对应的清单文件（`<pkg>_<ver>.bytes`），**加载进内存**。第二个参数 `60` 是超时秒数。

**清单里有啥？** 每个 Asset 的 Address、归属 Bundle、Hash、大小、依赖——"户口本"。此后 `LoadAssetAsync` 这类查询才有数据可查。

#### ⑤ `CreateResourceDownloader(ResourceDownloaderOptions)` —— 算差异

**两个参数：**
- `downloadingMaxNum`：下载时的并发（10 是常用值）
- `failedTryAgain`：单文件失败后的重试次数（3 够用）

**返回 `ResourceDownloaderOperation`**，关键属性：

| 属性 | 含义 |
|---|---|
| `TotalDownloadCount` | 差异文件数量 |
| `TotalDownloadBytes` | 差异总字节数 |
| `Status` | `StartDownload` 后才变化 |

这一步**不下载任何东西**，只是比对"新清单 vs 本地缓存"算出一份差异清单。

**如果 `TotalDownloadCount == 0`：** 玩家上次完整下过了，这次没更新，直接跳⑦清缓存或者直接进游戏。

#### ⑥ `downloader.StartDownload() + await downloader` —— 真·下载

挂事件回调再启动。两个事件：

```csharp
downloader.DownloadProgressChanged += (totalCount, currCount, totalBytes, currBytes) => { /* 刷进度条 */ };
downloader.DownloadError           += (fileName, error)                            => { /* 弹失败 */ };
```

`StartDownload()` 启动下载器，`await downloader` 挂起等它全部完成。完成后检查 `downloader.Status`。

#### ⑦ `ClearCacheAsync(ClearCacheOptions)` —— 清旧

```csharp
var clearOp = package.ClearCacheAsync(new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles));
await clearOp;
```

**`ClearUnusedBundleFiles`** 的意思是：**删掉"当前清单没引用到"的旧 Bundle**。

**为什么要清？** 新版本上线后，有些 Bundle 的 Hash 变了，旧文件留在沙盒里也不会被读到，纯占磁盘。

---

### 1.6 业务怎么拿到 `package` 去加载资源？

YooAsset **不提供**"全局默认 Package"这种糖（源码核实：`YooAssets` 静态类只有 `CreatePackage / GetPackage / TryGetPackage / RemovePackage / ContainsPackage` 这 5 个包管理方法，没有 `SetDefaultPackage`）。

最常见两种做法：

**方案 A：业务单例存**（Space Shooter 样例的做法）
```csharp
public static class GameBootstrap
{
    public static ResourcePackage GamePackage;
}

// 业务加载
var handle = GameBootstrap.GamePackage.LoadAssetAsync<GameObject>("player");
```

**方案 B：随时 GetPackage**
```csharp
var handle = YooAssets.GetPackage("DefaultPackage").LoadAssetAsync<GameObject>("player");
```

两者等价，`YooAssets.GetPackage(name)` 是轻查询（字典 lookup），不耗性能。**方案 B 更干净**，连单例都省了。

---

### 1.7 4 种模式 Options 对比表

| 模式 | Options | 最少配置 |
|---|---|---|
| EditorSimulateMode | `EditorSimulateModeOptions` | `EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot)` |
| OfflinePlayMode | `OfflinePlayModeOptions` | `BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()` |
| HostPlayMode | `HostPlayModeOptions` | Builtin + Cache（+ IRemoteService） |
| WebPlayMode | `WebPlayModeOptions` | `WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters()` |

**Editor 模式的 `packageRoot`** 是构建产物目录，样例用 `EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualBundle).PackageRootDirectory` 动态算，你也可以硬写 `Bundles/StandaloneOSX/DefaultPackage/` 之类的路径。

**Editor 模式可附加的模拟参数：** `EditorSimulateModeOptions` 还可通过 `AddParameter` 附加 `VirtualWebglMode`、`VirtualDownloadMode` 等模拟开关——在编辑器里模拟 WebGL 行为或模拟联网下载延时，仅在 EditorSimulate 下生效。

---

### 1.8 失败重试 / 进度条 / 等玩家确认（工程化加分项）

这几个需求 YooAsset **不管**，得自己处理。样例的做法（B 层）是套一个状态机 + 发事件驱动 UI。0 基础阶段完全可以用简化策略：

- **失败** → `Debug.LogError` + 弹 Unity 原生对话框 → 玩家点"重试" → `LaunchAsync()` 再调一次（因为①有幂等保护，没问题）
- **进度条** → `DownloadProgressChanged` 回调里直接更新 UI
- **等玩家确认下载** → 在第⑤步后 `await Dialog.ShowConfirm($"需下载 {MB}MB, 继续?")`，不同意就 return

等项目做大了需要更精致的状态管理和 UX，再去学样例的 PatchManager 那套架构。

**为什么 B 层（状态机）是"工程化加分项"——它解决了 A 层写法处理不干净的 3 件事：**

1. **玩家重试体验**——A 层平铺写法失败就 `return`"失败就停"；线上真要让玩家点"重试"回到失败那一步继续。朴素写法 `while(retry) {...}` 套 if/else，代码很快臃肿。状态机天然能"回跳到某节点"。
2. **UI 和业务解耦**——进度、失败弹窗、"发现 XX MB 更新，是否下载"这类交互不应该和下载逻辑耦合在一起。样例用"发事件 → UI 订阅"做解耦。
3. **挂起等确认**——④ 发现有差异时，**必须停下来**让玩家点确认（他可能不想花流量），状态机天然支持"卡在这个状态直到外部事件唤醒"；`async/await` 做这个就得用 `TaskCompletionSource` 或信号量，略绕。

PatchManager 状态机的详细拆解（8 节点、黑板机制、事件 → 回跳映射表）见**附录 C**。

---

## 第二部分 · CopyBuiltinPackageManifest 参数行为

> 源码出处：`BFSInitializeOperation.cs:91-120` + `CopyBuiltinPackageManifestOperation.cs` + `CopyBuiltinFileOperation.cs`
>
> **声明：** 该参数官方仅注释 "拷贝内置清单 `<bool>`"（`EFileSystemParameter.cs:90`），更高层的用途（例如是否服务于"联网失败降级"）源码未说明，本节只陈述**经源码核验的文件级行为**。
>
> **截至本次核验能给出的用途结论：它的作用是"把内置清单预存到沙盒 `ManifestFiles/` 目录，让沙盒侧也能用统一路径读到'内置版本'这份清单"——本质是"路径归一化"，不是"热更失败时降级"。** 上层业务如何消费这份沙盒里的内置清单（比如首次联网前 / CDN 请求失败时的 fallback 流程），需追踪 `SFSLoadPackageManifestOperation` 和 `SandboxFileSystem.LoadPackageManifestAsync` 完整分支才能给出权威回答，本节未穿透到底。

### 2.1 拷贝的 3 步操作

`CopyBuiltinPackageManifest = true` 时，BuiltinFileSystem 初始化期间执行 3 步（`CopyBuiltinPackageManifestOperation.cs`）：

1. **LoadBuiltinPackageVersion** — 读 StreamingAssets 里的 `.version` 文件，得到"**内置版本号**"（APK 打包那一刻的版本，不是 CDN 最新版本）
2. **CopyBuiltinPackageHash** — 把内置的 `<pkg>_<内置版本>.hash` 拷到沙盒 `persistentDataPath/yoo/<pkg>/ManifestFiles/`
3. **CopyBuiltinPackageManifest** — 把内置的 `<pkg>_<内置版本>.bytes`（清单二进制）拷到同一目录

**拷贝目标路径**（`CopyBuiltinPackageManifestOperation.cs:121-126`）：
`CopyBuiltinPackageManifestDestRoot` 若未指定，默认用 `YooAssetConfiguration.GetDefaultCacheRoot() / <pkg> / ManifestFiles`。

---

### 2.2 File.Exists 短路 → 只第一次启动真拷

`CopyBuiltinFileOperation.cs:41-54` 有短路守卫：

```csharp
if (_steps == ESteps.CheckFileExist)
{
    // 注意：只检查目标文件是否存在，不校验完整性。
    if (File.Exists(_destFilePath))
    {
        _steps = ESteps.Done;
        SetResult();   // ← 文件已存在，直接返回，不拷贝
    }
    else
    {
        _steps = ESteps.TryCopyFile;
    }
}
```

所以实际行为是：

- **第一次启动**（沙盒干净）→ 拷贝一次 `<pkg>_<内置版>.hash` + `<pkg>_<内置版>.bytes` 到 `persistentDataPath/yoo/<pkg>/ManifestFiles/`
- **以后每次启动** → `File.Exists` 命中，直接跳过，**不做任何文件操作**

**注意这条注释的含义（潜在陷阱）：只检查目标文件存在与否，不校验完整性。** 若沙盒里这份拷过去的清单后来被损坏、被玩家手动修改、或拷贝过程中断导致不完整，YooAsset **不会**重新从 StreamingAssets 覆盖修复——短路仍然命中、直接跳过。排障时如果怀疑沙盒清单出问题，得手动删沙盒 `ManifestFiles/` 下对应文件才会重拷。

---

### 2.3 文件名带版本号 → 不会覆盖 CDN 清单

- 拷贝目标路径：`<cacheRoot>/<pkg>/ManifestFiles/<pkg>_<内置版本>.bytes`（+`.hash`）
- CDN 下载下来的清单（`DownloadPackageManifestOperation.cs:36` + `SandboxFileSystem.cs:414-417`）：存到 `<cacheRoot>/<pkg>/ManifestFiles/<pkg>_<CDN版本>.bytes`

**文件名里的 version 字符串不一样**，所以两份清单是**并存**在同一目录的两个不同文件，不会互相覆盖。

举例：
```
persistentDataPath/yoo/DefaultPackage/ManifestFiles/
  ├── DefaultPackage_v1_打包那天的hash.bytes    ← 内置版（CopyBuiltin 拷过来的）
  ├── DefaultPackage_v1_打包那天的hash.hash
  ├── DefaultPackage_v3_CDN最新hash.bytes       ← CDN 拉下来的
  └── DefaultPackage_v3_CDN最新hash.hash
```

更严格地，即使**同一文件名**出现重名情况，两段代码的行为也是"安全覆盖"：
- `CopyBuiltinFileOperation.cs:45` 先 `File.Exists` 判断，**已存在则直接跳过**，不拷
- `DownloadPackageManifestOperation.cs:118-120` 则是先下到 `.tmp` 再 `File.Move` 原子替换，**且只在"同一 CDN 版本重新下载"时触发**

---

### 2.4 热更结束后 `.hash + .bytes` 会被覆盖吗？

**不会"覆盖"，是"新增"。**

- 内置那份永远保留（文件名带内置版本号，没人去动它）
- CDN 那份是**新写入**一个带 CDN 版本号的文件
- 两份并存在 `ManifestFiles/` 目录里

---

### 2.5 内置清单什么时候会消失？

**覆盖安装（App 水印变化）** 时，`BFSInitializeOperation.cs:50-86` 通过 `ApplicationFootprint` 检测。若水印变了（说明覆盖安装了新 APK），根据 `InstallCleanupMode` 参数：

| 枚举值 | 行为 |
|---|---|
| `EInstallCleanupMode.None` | 啥都不做，只 Log warning（默认） |
| `EInstallCleanupMode.ClearAllBundleFiles` | 删掉所有沙盒 Bundle，保留清单 |
| `EInstallCleanupMode.ClearAllManifestFiles` | 删所有沙盒清单 |
| `EInstallCleanupMode.ClearAllCacheFiles` | 全删（Bundle + Manifest + Temp） |

**最稳做法：** 正式版设置 `InstallCleanupMode = ClearAllCacheFiles`，覆盖安装后重新走一次完整热更流程，避开所有版本不匹配风险。

**示例调用形态**（key 名使用前请在项目中再 grep 一次 `InstallCleanupMode` 确认）：

```csharp
options.BuiltinFileSystemParameters.AddParameter(
    EFileSystemParameter.InstallCleanupMode,
    EInstallCleanupMode.ClearAllCacheFiles);
```

---

## 第三部分 · CDN 文件夹结构

### 3.1 核心事实：CDN 结构由 IRemoteService 决定

**YooAsset 自己不规定 CDN 目录层级。** CDN 上长什么样，**完全由 `IRemoteService.GetRemoteUrls(fileName)` 决定**——你把 URL 拼成什么样，CDN 上就要按什么样组织。

换句话说：**CDN 结构是你设计的，YooAsset 只问你要"单个文件的完整 URL"**。

---

### 3.2 YooAsset 要求平铺提供哪些文件

`ResourcePackage` 启动时会让 YooAsset 按**固定文件名规则**向 CDN 要文件。整个包裹的 CDN 文件清单只有这么几类（源码出处：`YooAssetConfiguration.cs`）：

| 文件 | 命名规则 | 例子 |
|---|---|---|
| 版本号 | `{packageName}.version` (L152) | `DefaultPackage.version` |
| 清单哈希 | `{packageName}_{version}.hash` (L135) | `DefaultPackage_v1_9f8a....hash` |
| 清单二进制 | `{packageName}_{version}.bytes` (L95) | `DefaultPackage_v1_9f8a....bytes` |
| Bundle 资源（N 个） | 由构建时 `FileNameStyle` 决定（`PackageManifestHelper.cs:184-208`） | 见第四部分 |

都支持 `PackageFilePrefix` 设置前缀，默认为空。

---

### 3.3 `.version` / `.hash` / `.bytes` 各自用途

| 文件 | 大小 | 内容 | 干什么用 |
|---|---|---|---|
| **`{pkg}.version`** | 几十字节 | 一个字符串：当前最新版本号（如 `v3_a4b5c6`） | **入口路标**。客户端第一步 `RequestPackageVersionAsync()` 就是下载这个文件，问"我该读哪份清单" |
| **`{pkg}_{ver}.hash`** | 32 字节左右 | 对应 `.bytes` 的 MD5/哈希值 | **清单校验**。防止 `.bytes` 下载过程中损坏——先下 `.hash` 存着，后下 `.bytes` 时算哈希比对 |
| **`{pkg}_{ver}.bytes`** | KB~MB 级 | 整个 Package 的清单二进制（所有 Asset 的 Address/BundleID/依赖关系表） | **户口本本体**。加载进内存后，`LoadAssetAsync` 这类查询才有数据可查 |

**一次热更的时序：**

```
步骤              下载文件                         用它做什么
─────────────────────────────────────────────────────────────
② RequestVersion  DefaultPackage.version          读出字符串 "v3_a4b"
③ LoadManifest    DefaultPackage_v3_a4b.hash      先下哈希（小，快）
③ LoadManifest    DefaultPackage_v3_a4b.bytes     再下清单（大）
                                                  → 算 bytes 哈希 vs .hash 内容，匹配才加载
```

**为啥要拆成 3 个而不是 1 个？** 版本号这个字符串几十字节，清单可能几 MB。如果玩家本地缓存的版本号 == CDN 的版本号，就**不用重新下清单了**——`.version` 是个廉价探针，避免每次启动都下几 MB 的清单。

---

### 3.4 样例官方目录模式（Space Shooter `GetHostServerURL()`）

源码 `FsmInitializePackage.GetHostServerURL()`（`FsmInitializePackage.cs:112-136`），官方样例拼的 URL 模式：

```
{hostServerIP}/CDN/{Platform}/{AppVersion}/{fileName}
```

CDN 上建议长这样：

```
http://cdn.mygame.com/
└── CDN/                            （固定前缀，官方样例取名为 CDN）
    ├── Android/                    （按平台分目录）
    │   └── v1.0/                   （按 App 大版本分目录）
    │       ├── DefaultPackage.version        ← 版本号入口文件
    │       ├── DefaultPackage_v3_a4b.bytes   ← v3 版本清单
    │       ├── DefaultPackage_v3_a4b.hash
    │       ├── DefaultPackage_v4_c5d.bytes   ← v4 版本清单（保留历史）
    │       ├── DefaultPackage_v4_c5d.hash
    │       ├── 9f8a7b6c.bundle               ← Bundle 文件（HashName 风格）
    │       ├── 1a2b3c4d.bundle
    │       └── ... 成百上千个 .bundle
    ├── IPhone/
    │   └── v1.0/
    ├── WebGL/
    └── PC/
```

**设计细节：**

- **`CDN` 这一层不是 YooAsset 要求的**，是样例作者加的前缀。你拼 URL 时爱怎么叫怎么叫。
- **`{Platform}/{AppVersion}` 分层也是样例约定**——让不同平台、不同 App 大版本的资源互相隔离。项目如果所有平台共用一份 Bundle，就不需要分 Platform 目录。
- **所有 Bundle 文件都平铺在 `v1.0/` 这一层**，不按 Package 子目录放——因为 `GetRemoteUrls(fileName)` 拿到的 `fileName` 就是纯文件名（`9f8a7b6c.bundle`），URL 直接拼在同一目录。
- **清单文件也混在 Bundle 同级目录**——它们在 YooAsset 眼里没区别，都是"通过文件名要的资源"。

---

### 3.5 多 Package 的两种目录方式

**方式 A：照样平铺**，靠文件名前缀自然隔离：

因为 `{packageName}_{version}.bytes` 里已经带了 packageName，`HashName` 风格下 Bundle 文件名是 hash 不会撞，所以两个 Package 的文件完全可以扔同一个 `v1.0/` 目录：

```
v1.0/
├── DefaultPackage.version
├── DefaultPackage_v3_a4b.bytes
├── RawFilePackage.version
├── RawFilePackage_v2_b5c.bytes
├── 9f8a7b6c.bundle    ← 来自 DefaultPackage
├── 1a2b3c4d.bundle    ← 来自 RawFilePackage
└── ...
```

**方式 B：分目录**（每个 Package 独立 CDN 根）：

```
v1.0/
├── DefaultPackage/
│   ├── DefaultPackage.version
│   ├── DefaultPackage_v3_a4b.bytes
│   └── *.bundle
└── RawFilePackage/
    ├── RawFilePackage.version
    ├── RawFilePackage_v2_b5c.bytes
    └── *.bundle
```

每个 Package 初始化时传各自的 `IRemoteService`：

```csharp
var svcDefault = new MyRemoteService("http://cdn.mygame.com/CDN/Android/v1.0/DefaultPackage");
var svcRawFile = new MyRemoteService("http://cdn.mygame.com/CDN/Android/v1.0/RawFilePackage");
```

两个 Package 的 Options 分别挂各自的 remoteService 即可，YooAsset 不关心。

---

### 3.6 构建产物怎么上传 CDN

构建产物路径规则：

- `BuildOutputRoot` = `<工程根>/Bundles`（`BundleBuilderHelper.cs:21`）
- `PipelineOutputDirectory` = `{BuildOutputRoot}/{BuildTarget}/{PackageName}/OutputCache`（构建过程中的中间产物）
- 最终产物落地目录：`<工程根>/Bundles/<BuildTarget>/<pkg>/<pkg_version>/`

**判定规则：这个最终产物目录里除了 `.report` / `.json`（人类可读的构建报告）外，其他文件全是 CDN 要的。** 这就是 `aws s3 sync --exclude "*.report" --exclude "*.json"` 过滤列表的来源。

推荐发布流程：

```bash
# 1. 假设你刚构建完 Android 的 DefaultPackage v3_a4b
BUILD_OUTPUT=Bundles/Android/DefaultPackage/v3_a4b/
CDN_TARGET=s3://mygame-cdn/CDN/Android/v1.0/

# 2. 上传时把非资源文件过滤掉
aws s3 sync $BUILD_OUTPUT $CDN_TARGET --exclude "*.report" --exclude "*.json"
```

**上传后务必验收 3 个文件能通过 HTTP 访问：**
1. `{CDN_ROOT}/DefaultPackage.version`
2. `{CDN_ROOT}/DefaultPackage_{version}.hash`
3. `{CDN_ROOT}/DefaultPackage_{version}.bytes`

这 3 个通，其余 bundle 就基本没问题（都是平铺关系）。

---

### 3.7 保留历史清单的意义

**CDN 上建议保留最近 N 个版本的 `.bytes` + `.hash` 清单**，不要一上传新版就删老的。原因：

- 玩家本地缓存的版本号可能是上一版 `v3`，进游戏先 `RequestPackageVersionAsync` 拿到新版本 `v4`，`LoadPackageManifestAsync(v4)` 成功——没毛病
- 但若玩家**上次热更下到一半就关游戏了**，本地 `PackageVersion` 可能卡在 `v3` 某个中间状态，再开游戏时 YooAsset 可能还要读旧清单做差异——这种情况下旧清单被删就会挂

保险起见：**新版上线后保留旧版至少 7 天**，等玩家自然更新完再清理。Bundle 文件因为是 hash 命名、天然有内容唯一性，**可以永不删**（新版不引用的老 bundle 本来也没人下载，占点冷存储钱）。

---

## 第四部分 · Bundle 文件名三种风格与覆盖风险

### 4.1 三种风格（`PackageManifestHelper.cs:184-208`）

| 风格 | 产物文件名 | 举例 |
|---|---|---|
| `HashName`（默认） | `{fileHash}{.bundle}` | `9f8a7b6c5d4e.bundle` |
| `BundleName` | `{bundleName}` 原样 | `assets_hero_icon.bundle` |
| `BundleName_HashName` | `{bundleName去后缀}_{fileHash}{扩展名}` | `assets_hero_icon_9f8a7b6c.bundle` |

`BuildParameters.cs:85` 默认 `FileNameStyle = EFileNameStyle.HashName`。

---

### 4.2 `HashName` 与 `BundleName_HashName` 为什么安全

**`HashName`** —— 文件名 = hash。改一丁点资源，hash 就变，新文件名和老文件名**必然不同**。上传 CDN 时是**新增文件**，不会覆盖任何老文件。**100% 安全。**

**`BundleName_HashName`** —— 名字里带 hash。同理，改资源后 hash 变，文件名变。**同样安全。**

---

### 4.3 `BundleName` 风格的两种翻车场景

**`BundleName`** —— 文件名 = 固定的 bundleName。资源改了，hash 变了，但**文件名不变**。这时候再上传 CDN：

```
CDN 上原来有:  assets_hero_icon.bundle  (md5=9f8a... , 1.2 MB)
本地新版产物:  assets_hero_icon.bundle  (md5=b7c2... , 1.4 MB)
aws s3 sync → 覆盖！
```

**翻车 ①：CDN 版本原子性问题**

`aws s3 sync` 上传 1000 个 bundle 要 5 分钟。这 5 分钟里：
- 老玩家 A 刚好在跑热更
- A 已经下了**新的 `.bytes` 清单**（里面记录 `assets_hero_icon.bundle` 应该是 `b7c2...`）
- 但 `assets_hero_icon.bundle` 这个文件**还没上传完，CDN 上还是老的 `9f8a...` 内容**
- A 下了这个老文件，算哈希 `9f8a` ≠ 清单里的 `b7c2` → **下载校验失败，玩家卡热更**

`HashName` 风格没这个问题——**新老文件名本来就是两个独立文件**，新文件在旧文件之外新增上传，清单切到新版时所有新文件都已在位，旧玩家继续用旧清单找旧文件毫无影响。

**翻车 ②：CDN 回滚困难**

`BundleName` 风格下同名文件被新版覆盖了，**旧版 bundle 在 CDN 上不复存在**。如果新版出 bug 要回滚，你得把旧包再 sync 回去——但刚才 sync 时 `aws s3 sync --delete` 把"新版没有、只在旧版有的 bundle"都删了，回滚就残缺。

`HashName` 风格下新老文件是两份独立存在，回滚只要把版本号 `.version` 文件换回旧值，旧 bundle 还都在。

**无伤的情况（为什么看起来"貌似也不会出问题"）：**

- 玩家上次进游戏下的是旧 v3，这次启动先问版本号拿到新 v4，下新 v4 清单，新清单里 `assets_hero_icon.bundle` 对应的 hash 记录就是新的 `b7c2...`，本地缓存如果已有同名 bundle 但 hash 不匹配 → **YooAsset 发现 hash 不对，当差异文件重下**，下下来的就是新内容。✅
- 玩家第一次进游戏，CDN 已经只有新版，直接下的就是新的。✅

---

### 4.4 三种风格选型建议

| 场景 | 推荐风格 |
|---|---|
| 正式线上游戏 | **`HashName`**（默认值就是这个，别改） |
| 调试期想看文件名知道是啥 | `BundleName_HashName`（有 hash 打底，又能看名字） |
| 单机 / 不热更 | `BundleName`（方便调试，反正没 CDN 问题） |

**YooAsset 默认设成 `HashName` 不是随便选的，是规避了这个坑。**

---

## 第五部分 · 内置资源 vs 可热更资源的 Package 划分

### 5.1 结论：一个 Package 内部可以混装内置 + 可热更资源，不需要分 Package

YooAsset 的"内置 / 可热更"是**两个维度**，和 Package 是**正交**关系：

| 维度 | 控制方式 |
|---|---|
| 属于哪个 Package | 构建时 BundleCollector 里配的 Group 归属 |
| 是内置（进 APK）还是可热更（只在 CDN） | 构建时 BundleCollector 里每个 Collector 的 **Tags** + 构建参数里的 `BundledCopyOption` |

---

### 5.2 `BundledCopyOption` 5 种（`EBundledCopyOption.cs`）

| 枚举 | 行为 |
|---|---|
| `None` | 构建产物不拷进 StreamingAssets（所有 bundle 都只在 CDN） |
| `ClearAndCopyAll` | 清空 StreamingAssets 旧的，把**所有** bundle 拷进 APK（全内置，下载永远无差异） |
| **`ClearAndCopyByTags`** | **只拷带指定 Tag 的 bundle** 进 APK，其余留 CDN |
| `OnlyCopyAll` | 同 `ClearAndCopyAll`，但不清旧 |
| `OnlyCopyByTags` | 同 `ClearAndCopyByTags`，但不清旧 |

---

### 5.3 真实配法（最常用）

比如做一个卡牌游戏：

- 新手引导资源：必须内置（玩家装好就能进游戏，不等热更）
- 主城 UI / 新手关卡：必须内置（首次进主菜单不能等）
- 2000 张卡牌立绘 / 章节剧情 / 战斗特效：热更（APK 瘦身）

**做法：**

1. **一个 `DefaultPackage`**——所有资源都在这个包。
2. 在 BundleCollector 里把上述资源分到不同 Group，**给需要内置的那批 Group 打 Tag `"builtin"`**。
3. 构建时 `BundledCopyOption = ClearAndCopyByTags`，`BundledCopyParam = "builtin"`。

构建产物：
```
Bundles/Android/DefaultPackage/v1_a4b/
├── 新手.bundle        ← 带 builtin tag，会进 APK
├── 主城UI.bundle      ← 带 builtin tag，会进 APK
├── 卡牌立绘1.bundle   ← 无 tag，只在 CDN
├── 卡牌立绘2.bundle
├── DefaultPackage.version
├── DefaultPackage_v1_a4b.bytes   （清单里**包含全部资源**，内置和热更的一起列）
└── DefaultPackage_v1_a4b.hash
```

**关键点：**
- 清单是全的，不管内置还是热更，玩家看到的 Package 都是一个完整资源清单
- 加载时 YooAsset 自动走"**先问 BuiltinFileSystem（APK 里有没有？）→ 再问 CacheFileSystem（沙盒里有没有？）→ 都没有才下载**"的查找链（详见第七部分）
- `CreateResourceDownloader` 算差异时，**内置已经有的 bundle 不会出现在下载列表里**，只有"清单里要、但本地（内置+沙盒）都没有"的才会下

---

### 5.4 什么时候才真需要多个 Package？

**当不同业务想独立升级节奏、独立缓存策略、独立 CDN 域名时**：

| 例子 | 是否拆 Package | 理由 |
|---|---|---|
| 新手+主城+战斗特效+卡牌 | ❌ 都塞一个 | 同一游戏的资源 |
| 主游戏 + Mod（玩家创作内容） | ✅ 拆 | Mod 独立上架审核、独立版本 |
| 通用资源（Shader / UI 字体 / 通用模型）+ 业务资源 | ✅ 拆（通用单独一个） | 通用资源复用率高，单独出小更新，不影响业务大版本 |
| 研发中：一个包放 Bundle 资源，一个包放 HybridCLR 的 `.dll.bytes` | ✅ 拆 | 代码包和资源包热更节奏不同 |

**"可热更资源要不要单独一个 Package"这个问题的答案是：不用，单 Package 完全够，靠 Tag 控制内置/热更。**

---

## 第六部分 · 内置资源能否走热更（FileHash 机制）

### 6.1 短答：可以热更，但原理和直觉不一样

**不是"去 CDN 下载新版覆盖 APK 内置资源"**（物理上做不到，APK 里的文件运行时不能改），而是：

**新清单里 Bundle 的 FileHash 变了 → YooAsset 认为内置版的"不匹配" → 走沙盒缓存去下载新版，不再读内置。** 内置那份原封不动留在 APK 里（占空间但不再使用），新版下到沙盒，后续都从沙盒读。

---

### 6.2 源码链路（3 步确认）

**第 1 步：Bundle 唯一标识是 FileHash**

`PackageBundle.cs:58`：
```csharp
public string BundleGuid { get { return FileHash; } }
```

`BundleGuid` 是 YooAsset 识别一个 Bundle 的唯一 ID，**而它直接等于 FileHash**。资源改一点点 → hash 变 → GUID 变 → **对 YooAsset 来说是"一个全新 Bundle"**。

**第 2 步：找"这 Bundle 归哪个文件系统管"的逻辑（核心）**

`FileSystemHost.cs:126-139`：
```csharp
private IFileSystem GetOwnerFileSystem(PackageBundle packageBundle)
{
    for (int i = 0; i < _fileSystems.Count; i++)
    {
        IFileSystem fileSystem = _fileSystems[i];
        if (fileSystem.CanAcceptBundle(packageBundle))
            return fileSystem;
    }
    return null;
}
```

按文件系统注册顺序遍历（Host 模式下顺序是：BuiltinFileSystem 先、CacheFileSystem 后），**第一个说"我能接"的就负责这个 bundle**。

**第 3 步：两个文件系统的"我能接"判断**

BuiltinFileSystem（`BuiltinFileSystem.cs:361-364`）：
```csharp
public bool CanAcceptBundle(PackageBundle bundle)
{
    return BuiltinBundleCache.IsCached(bundle.BundleGuid);   // 注意：查 BundleGuid == FileHash
}
```

只有 **APK 内置目录里存在 BundleGuid（= FileHash）对应的物理文件** 时，才返回 true。

SandboxFileSystem（`SandboxFileSystem.cs:362-367`）：
```csharp
public bool CanAcceptBundle(PackageBundle bundle)
{
    // 注意：保底加载！
    return true;
}
```

兜底——内置说不行，沙盒无条件接管。

---

### 6.3 关键推论走一遍

**场景：** 把 `AtlasA.bundle` 打进 APK 内置（FileHash=`9f8a`），然后改了资源重新打包上传 CDN（新 FileHash=`b7c2`）。

**玩家启动时：**

| 步骤 | 发生了什么 |
|---|---|
| 下载新版清单 | 清单里 `AtlasA` 这个 Bundle 的 FileHash 记为 `b7c2` |
| `GetOwnerFileSystem` 遍历 | 问 BuiltinFileSystem："你能接 BundleGuid=`b7c2` 吗？" |
| BuiltinFileSystem 查自己的 cache | 内置里只有 `9f8a` 这个 hash 的文件，**没有 `b7c2`** → 返回 false |
| 轮到 SandboxFileSystem | 无条件 true，接手 |
| `IsDownloadRequired(bundle)` | 沙盒还没下过 `b7c2` → 返回 true → **进下载列表** |
| `CreateResourceDownloader` 收集差异 | `AtlasA` 被列入要下载的文件 |
| 下载完 | 以后所有 `LoadAssetAsync("AtlasA 里的某张图")` 都从沙盒读新版 `b7c2.bundle` |
| 内置那个 `9f8a.bundle` | **原地躺着永远不再使用**（除非回滚清单） |

---

### 6.4 结论

| 问题 | 答 |
|---|---|
| 内置资源能不能走热更 | **能**，但"更"的实质是**新版下到沙盒、旧版内置文件被弃用**，不是物理覆盖 |
| 需不需要做特殊配置 | **不需要**，YooAsset 默认机制就是这样工作的。你只管改资源、打新版、上传 CDN |
| 代价 | APK 里那份旧 bundle **永远白占空间**，直到玩家更新 APK 才能真正换掉 |

---

### 6.5 实践建议

**把"几乎不变的资源"放内置**（Shader / 通用字体 / Logo / 首次剧情），这些东西改一次 hash 变一次，热更就多一份冗余。

**把"经常迭代的资源"不放内置、留 CDN**（卡牌立绘 / 装备图标 / 版本活动特效），反正要下载，不放进 APK 瘦身更好。

极端情况，如果所有内置资源都被热更替换过一遍，APK 就变成了"一堆死文件 + 一个壳"——这就是为什么大版本要定期出 APK 新版的原因：**把已经迭代稳定的新资源重新内置进去，清掉沙盒冗余**（触发覆盖安装的 `InstallCleanupMode`）。

---

## 第七部分 · 两个文件系统的查找顺序（Builtin 先，Sandbox 兜底）

### 7.1 源码锁死的注册/查询顺序

`InitializePackageOperation.cs:103-104`，Host 模式初始化把两个文件系统按 **(Builtin, Cache)** 顺序传入：
```csharp
_initializeFileSystemOp = _fileSystemHost.InitializeAsync(
    initializeParameters.BuiltinFileSystemParameters,   // ← 先
    initializeParameters.CacheFileSystemParameters);    // ← 后
```

`InitializeFileSystemOperation.cs:65-91`：把传入的参数列表复制一份 `_cloneList`，然后在每一帧的 `Update` 里 `_cloneList.RemoveAt(0)` 逐个弹出、逐个 `InitializeAsync`，完成后注册到 `_fileSystems` 列表。**注册顺序严格等于传入参数的顺序**，不存在并发 / 乱序。

`FileSystemHost.cs:126-139`：查询时 `for (int i = 0; i < _fileSystems.Count; i++)` **按注册顺序**遍历。

**所以遍历顺序永远是：Builtin 先问 → Sandbox 兜底。**

---

### 7.2 "先 Builtin 后 Sandbox"不是"先读内置"！

这个设计初看反直觉，源头在于 YooAsset 怎么"认"一个 bundle：

**不是按文件名 / 资源路径 / Bundle 名字来找文件**，**而是按当前清单里那条 bundle 的 FileHash 来找**。

`BuiltinFileSystem.CanAcceptBundle` 的判断（`BuiltinFileSystem.cs:363`）：
```csharp
return BuiltinBundleCache.IsCached(bundle.BundleGuid);   // BundleGuid = FileHash
```

它问的不是 "`AtlasA.bundle` 在我这里吗"，而是 "**hash 为 `b7c2` 的那份 `AtlasA.bundle` 在我这里吗**"。

---

### 7.3 直觉模型 vs YooAsset 实际模型

#### 直觉模型（传统 Unity AB 或旧版资源框架）

```
请求加载 AtlasA
    ↓
先查 Persist 目录有没有 AtlasA
    有 → 读 Persist 的（新的）
    没有 → 读 StreamingAssets 的（内置的）
```

**核心假设：文件名相同代表同一份资源，新的覆盖旧的。**

#### YooAsset 实际模型

```
请求加载 AtlasA
    ↓
查"当前激活清单"里 AtlasA 对应哪个 FileHash（比如 b7c2）
    ↓
问 BuiltinFileSystem："有 hash=b7c2 的文件吗？"
    有 → Builtin 接管（这个 hash 内置里就有，直接读 APK）
    没有 → Sandbox 接管（走沙盒缓存，没有就下载）
```

**核心假设：每份资源由 FileHash 唯一标识，文件名只是包装。**

---

### 7.3a 修正你脑中的模型（三行纠正对照）

| 你原本的理解 | 实际情况 |
|---|---|
| 先查 Persist，没有再读 Streaming | 先问 Builtin（Streaming）"你这个 hash 的文件有没有"，没有才走 Sandbox（Persist） |
| 查找顺序是为了"让新文件覆盖旧文件" | 查找顺序是为了"让严格匹配方优先认领"，**覆盖是 hash 变化导致的自然结果**，不是查找顺序的产物 |
| 两个目录可能存同名文件，新的胜 | 两个目录存的是 **hash 命名的不同文件**，根本不存在同名冲突 |

---

### 7.4 为什么是这个顺序，而不是反过来？

**因为 Builtin 的判断更严格（带条件）、Sandbox 是无条件兜底。**

- `BuiltinFileSystem.CanAcceptBundle`：**只有这个具体 hash 真在 APK 里才说"我能接"**
- `SandboxFileSystem.CanAcceptBundle`：**无条件 true**（源码注释原文 "// 注意：保底加载！"）

如果反过来——Sandbox 先问——那它永远返回 true，Builtin 永远没机会接手，APK 内置资源白放了。

**正确的直觉应该是："先让严格的那个挑，它挑不中，再交给兜底的。"** Builtin 先、Sandbox 后，是为了让"hash 匹配的内置文件优先被使用"。

---

### 7.5 三种常见场景走一遍

#### 场景 A：资源从没热更过

- 清单里 `AtlasA` 的 FileHash = `9f8a`
- Builtin 里有 `9f8a.bundle`（打包时进的）
- Sandbox 是空的

**Builtin 说能接 → 读 APK 里的。Sandbox 永远轮不到。** ✅ 这时候直觉里的"优先读 Streaming"也是对的。

#### 场景 B：资源刚刚热更过

- 新清单里 `AtlasA` 的 FileHash = `b7c2`
- Builtin 里**还是**原来那个 `9f8a.bundle`（APK 里没动）
- Sandbox 里已下好 `b7c2.bundle`

**Builtin 问自己：我有 hash=`b7c2` 的吗？没有（我只有 `9f8a`）→ 拒绝。**
**Sandbox 接管，读沙盒的 `b7c2.bundle`。** ✅

#### 场景 C：第一次打开，Bundle 要从 CDN 拉

- 新清单里 `NewCardPic` 的 FileHash = `c3d4`（全新的卡牌立绘，APK 打包时没有）
- Builtin 里**根本没有**这个 bundle
- Sandbox 里也没有

**Builtin 没有 → 拒绝。Sandbox 接管 → `IsDownloadRequired` = true → 列入下载。** ✅

---

### 7.6 两个文件系统的实质区别

| 维度 | BuiltinFileSystem | SandboxFileSystem |
|---|---|---|
| 物理路径 | `Application.streamingAssetsPath/yoo/` | `Application.persistentDataPath/yoo/` |
| 内容来源 | 打包时随 APK 一起塞进去，**只读** | 运行时从 CDN 下下来，可读写 |
| 是否能下载 | `IsDownloadRequired` **永远返回 false**（`BuiltinFileSystem.cs:368`） | `IsCached == false` 时返回 true，触发下载 |
| 接受哪些 bundle | 只接"我手上有这个 hash 的"（精确匹配） | 无条件兜底 |
| 生命周期 | 玩家卸载/覆盖安装才会变 | 热更流程随时写入 |

---

### 7.7 一句话总结

**YooAsset 里不存在"新版覆盖旧版"的概念，只有"hash 变了 → APK 内置版被清单遗忘 → 新版从 CDN 下到沙盒"。** 所以查询顺序是"Streaming 先抢，Persist 兜底"——这和传统资源框架恰好相反，源头是因为 YooAsset 用 **FileHash** 而不是**文件名**作为资源身份证。

---

## 第八部分 · 排障 Cheat Sheet

### 8.1 索引：症状 → 章节

| 报错/现象 | 看哪节 |
|---|---|
| `package is invalid` / 加载抛空引用 | 8.2 |
| `YooHandleInvalidException: is invalid. It may have been released or the provider was destroyed` | 8.3 |
| `LoadAssetAsync` 卡住不完成 / `Progress` 卡在 0 | 8.4 |
| CDN 连通但下载 404 / 版本不匹配 | 8.5 |
| SBP build 失败：`CreateBuiltInShadersBundle IBundleExplictObjectLayout` | 8.6 |
| Editor 能跑、打包就找不到资源 | 8.7 |
| 热更包玩家下载完，下次启动还在重下 | 8.8 |
| 同一份 Prefab 实例化 N 次，Release 了 N-1 次仍不卸载 | 8.9 |
| 玩家卸载重装后资源错乱 | 8.10 |

---

### 8.2 "package is invalid" / 加载抛空引用

**症状：** `package.LoadAssetAsync(...)` 直接空引用或 API 调用抛异常。

**3 个候选根因，按概率排序排查：**

| 根因 | 判断方式 | 解决 |
|---|---|---|
| 没调 `InitializePackageAsync` | 7 步里漏了 ② | 补上，`await` 成功再继续 |
| `packageName` 和构建时的 Package 名不一致 | 打开 BundleCollector 窗口看左上角下拉列表里的名字 | 改成一模一样（大小写敏感） |
| 清单没加载（漏了 `LoadPackageManifestAsync`） | `RequestPackageVersionAsync` 跑了但 `LoadPackageManifestAsync` 没跑 | 补上 ④ 步 |

**自测：** 7 步少任何一步，package 就不能正常工作。顺序神圣不可违背。

---

### 8.3 `YooHandleInvalidException: ... is invalid. It may have been released or the provider was destroyed`

**源码出处：** `AssetHandle.cs:28` / `SceneHandle.cs:33` 等所有 Handle 类都有此异常。

**这条异常只在一种情况下抛：访问已释放的 Handle。**

#### 典型翻车代码

```csharp
var handle = package.LoadAssetAsync<GameObject>("player");
await handle;
var go = handle.AssetObject;
handle.Release();   // ← 释放了

// 500 ms 后某个回调里
var go2 = handle.AssetObject;   // 🔥 抛 YooHandleInvalidException
```

#### 排查步骤

1. **全局搜 `.Release()` 调用点**：看是不是"释放了之后还在用"
2. **检查 `handle.Completed += ...` 订阅链**：回调里访问 handle 时机是否在 Release 之后
3. **SceneHandle 特殊**：Scene 不走 `Release`，而是 `UnloadSceneAsync()`——调完之后 SceneHandle 也失效了

#### 防御模式

```csharp
if (handle == null || !handle.IsValid) return;   // ← 所有 Handle 有 IsValid 属性
var obj = handle.AssetObject;
```

---

### 8.4 `LoadAssetAsync` 卡住 / 进度永远 0

**症状：** `await handle` 永远不返回；`handle.Progress` 一直 0。

**卡点清单：**

| 根因 | 判断方式 |
|---|---|
| 没人调 `YooAssets.Update()` 驱动（或框架层没 tick） | 加 `Log` 看 Update 有没有每帧跑 |
| Bundle 下载中但 CDN 网络断了 | 看 Unity Console 是否有 `DownloadError` 警告；用 `Fiddler` 抓包看请求 |
| 依赖链里某个 Bundle 下载失败 | Handle 的 `Status = Failed` 但你没检查，以为还在 loading |
| Host 模式，当前资源在 CDN 上根本没下载下来（清单里有但文件未补齐） | 先调 `CreateResourceDownloader` 把差异下完再加载 |

**排查顺序：**
1. 检查 `handle.Status`——如果是 `Failed` 看 `handle.LastError`
2. 检查 `handle.IsDone`——`true` 说明完成了，只是 await 的位置有问题
3. 检查 YooAsset 驱动：`YooAssets.Update()` 或 `YooAssets.Initialize()` 里注册的驱动

---

### 8.5 CDN 连通但下载 404 / 版本不匹配

**症状：** 本地 `RequestPackageVersionAsync` 拿到版本号 `v3_xxx`，`LoadPackageManifestAsync` 404。

**3 种可能：**

| 根因 | 判断 | 解决 |
|---|---|---|
| CDN 上传时没把 `.hash` / `.bytes` 清单文件都传 | 用浏览器访问 `<CDN_ROOT>/<pkg>_<version>.bytes`，404 就是没上传 | 构建产物目录下所有文件整个拷到 CDN，不能只拷 `.bundle` |
| `IRemoteService.GetRemoteUrls` 拼错 | 打 Log 输出 `GetRemoteUrls(fileName)` 的返回值，浏览器复制链接试 | 对照最简版实现改 |
| 玩家本地缓存了旧版本号，但 CDN 已经把旧版本的清单删了 | 问运维是否清理过历史版本 | CDN 保留最近 N 个版本的清单（最佳实践） |

**调试小技巧：** 在 `MyRemoteService.GetRemoteUrls` 里 Log 每一个返回值：
```csharp
public IReadOnlyList<string> GetRemoteUrls(string fileName)
{
    var url = $"{_cdnRoot}/{fileName}";
    Debug.Log($"[CDN Query] {fileName} => {url}");
    return new[] { url };
}
```

---

### 8.6 SBP build 失败：`CreateBuiltInShadersBundle IBundleExplictObjectLayout`

**完整堆栈特征：** `GetContextObject<IBundleExplictObjectLayout>()` 抛 `KeyNotFoundException`。

**根因：**

`com.unity.scriptablebuildpipeline@2.6.1 / Editor/Tasks/CreateBuiltInBundle.cs:72-81` —— 当收集器里所有资源**都不引用任何 Unity 内置 Shader** 时，`m_Layout.ExplicitObjectLocation.Count == 0`，内部把 `m_Layout` 置 null，但外层 Task 仍 `GetContextObject<IBundleExplictObjectLayout>()` 导致抛异常。

**3 种修法：**

| 方案 | 难度 | 推荐度 |
|---|---|---|
| A. 在收集器里加一个依赖内置 Shader 的资源（比如一个用 `Standard` Shader 的 Material） | 5 分钟 | ⭐⭐⭐ |
| B. 改 YooAsset 源码绕过这个 Task（`SBPBuildTasks.Create` 里注释掉 `CreateBuiltInShadersBundle` ） | 10 分钟 | ⭐ |
| C. 换 SBP 版本（降回 2.1.x 或升级到更新） | 不可控，跟 Unity 兼容性绑死 | ⭐ |

**推荐 A**：最稳，让 Unity 自己管内置 Shader 的打包，你不碰它。

---

### 8.7 Editor 能跑、打包就找不到资源

**Top 3 候选根因：**

| 根因 | 检查方式 |
|---|---|
| 构建产物没拷到 StreamingAssets | 打包前有没有在 Bundle Builder 里勾 `BundledCopyOption = ClearAndCopyAll` |
| 打的是 EditorSimulateMode 的配置，运行时用 HostPlayMode 去找资源 | 打包流程里确认用的是 `ScriptableBuildPipeline`，不是 EditorSimulate |
| `Application.streamingAssetsPath` 在 Android 是 `jar:...`，某些 IO 读法不适用 | YooAsset 的 `BuiltinFileSystem` 内部已处理；但若手写读 `StreamingAssets` 的代码要注意 |

**验证清单：**
1. Bundle Builder 里的 `BuildOutput` 指向哪？→ `Bundles/<BuildTarget>/<pkg>/<version>/`
2. `BundledCopyOption` 是不是 `None`？→ 若是 None，产物不会自动进 StreamingAssets，打包出来的 APK/IPA 里没有内置资源
3. APK 解压出来看 `assets/yoo/<pkg>/` 下有没有文件

---

### 8.8 玩家下载完，下次启动还在重下

**症状：** `TotalDownloadCount` 每次启动都 > 0，明明上次下载成功了。

**3 个检查点：**

| 原因 | 检查 |
|---|---|
| CDN 版本又变了（没意识到又上传了新版） | 对比 `versionOp.PackageVersion`，两次启动是否相同 |
| 缓存写入失败（磁盘满 / 权限问题） | 看 Console 有没有 `SetError("Failed to move ...")` 警告 |
| 代码里每次启动都 `ClearCacheAsync(ClearAllBundleFiles)` | 搜 `ClearCacheAsync`，确认用的是 `ClearUnusedBundleFiles` 而不是 `ClearAllBundleFiles` |

**关键事实：** `ClearUnusedBundleFiles`（7 步流程里用的）**只清当前清单没引用的**，当前清单要用的会留着；`ClearAllBundleFiles` 是核弹，**所有缓存清光**。别用错。

---

### 8.9 Prefab 实例化 N 次，Release 了 N-1 次仍不卸载

**先搞清楚两件事的区别：**

| 操作 | 影响 |
|---|---|
| `handle.Release()` | 降 Provider 引用计数 |
| `Object.Destroy(instance)` | 销毁 GameObject 实例，**和引用计数无关** |

**关键：** Instantiate 出来的 GameObject 不占 Handle 的引用计数。引用计数只管**Asset 本体**（你 Load 的那个 Prefab 资源）。

**正确流程：**
```csharp
var handle = package.LoadAssetAsync<GameObject>("player");
await handle;

// 实例化 5 个——不增加 Handle 引用计数
for (int i = 0; i < 5; i++) Object.Instantiate(handle.AssetObject);

handle.Release();   // 5 个实例不受影响，继续在场景里；只是不能再通过 handle 访问 Asset 了
```

**这时候 Prefab 资源会卸载吗？** 不会。默认 `AutoUnloadBundleWhenUnused = false`（`InitializePackageOptions.cs:18`），Bundle 引用计数归零后也不会自动卸载，**得调 `package.UnloadUnusedAssetsAsync()` 主动触发**。

---

### 8.10 玩家卸载重装后资源错乱

**症状：** 卸载 APK 重新安装，游戏启动报各种清单/资源错误。

**根因：** 沙盒 `persistentDataPath/yoo/` 可能没跟 APK 一起删（iOS 和 Android 行为不同，具体看平台），导致沙盒存着**旧 APK 时代的缓存**，和新 APK 的内置资源版本不匹配。

**YooAsset 的防御：** `BFSInitializeOperation.cs:50-86` 通过 `ApplicationFootprint`（水印文件）检测覆盖安装。新 APK 首次启动发现水印变了，根据 `InstallCleanupMode` 参数：

| 枚举值 | 行为 |
|---|---|
| `EInstallCleanupMode.None` | 啥都不做，只 Log warning（默认） |
| `EInstallCleanupMode.ClearAllBundleFiles` | 删掉所有沙盒 Bundle，保留清单 |
| `EInstallCleanupMode.ClearAllManifestFiles` | 删所有沙盒清单 |
| `EInstallCleanupMode.ClearAllCacheFiles` | 全删（Bundle + Manifest + Temp） |

**最稳做法：** 正式版设置 `InstallCleanupMode = ClearAllCacheFiles`，覆盖安装后重新走一次完整热更流程，避开所有版本不匹配风险。

> ⚠️ 设置该参数所用的 `EFileSystemParameter.xxx` 具体 key 名本节未核验，使用前请在项目中 grep 确认。

---

### 8.11 排障通用流水线（套路化）

任何 YooAsset 问题都按这个顺序先打日志：

```csharp
Debug.Log($"[YooAsset] Package exists: {YooAssets.ContainsPackage(packageName)}");
Debug.Log($"[YooAsset] InitStatus: {initOp.Status}, Error: {initOp.Error}");
Debug.Log($"[YooAsset] CDN Version: {versionOp.PackageVersion}");
Debug.Log($"[YooAsset] Manifest OK: {manifestOp.Status}");
Debug.Log($"[YooAsset] Downloader: count={downloader.TotalDownloadCount}, bytes={downloader.TotalDownloadBytes}");
Debug.Log($"[YooAsset] Load Status: {handle.Status}, Error: {handle.LastError}");
```

**每一条对应 7 步流程的一步**。卡在哪一步，日志就断在哪一步。**99% 的 YooAsset 问题都能靠这 6 行日志定位到。**

---

### 8.12 Console 常见警告/错误快查表

| 日志关键字 | 出处 | 含义 |
|---|---|---|
| `Failed to copy builtin file` | `CopyBuiltinFileOperation.cs:69` | StreamingAssets 拷到沙盒失败（Android WebRequest 兜底会接管） |
| `Downloaded manifest file is too small` | `DownloadPackageManifestOperation.cs:110` | CDN 返回了损坏/空的清单文件 |
| `is invalid. It may have been released` | 所有 Handle 类 | 访问已释放 Handle，见 8.3 |
| `No action required on overwrite installation` | `BFSInitializeOperation.cs:61` | 覆盖安装了但 `InstallCleanupMode=None`，建议改值 |
| `does not support the WebGL platform` | `BFSInitializeOperation.cs:44` | WebGL 不能用 BuiltinFileSystem，改用 WebServerFileSystem |

---

## 附录 A · 源码关键文件对照表

便于后续回查：

| 主题 | 源码文件 |
|---|---|
| YooAssets 静态入口 API | `Runtime/YooAssets.cs` |
| ResourcePackage 实例 API | `Runtime/ResourcePackage/ResourcePackage.cs` |
| Host 模式初始化流程 | `Runtime/ResourcePackage/Operations/InitializePackageOperation.cs` |
| 文件系统宿主（管理多 FS） | `Runtime/ResourcePackage/FileSystemHost.cs` |
| 文件系统初始化顺序 | `Runtime/ResourcePackage/Operations/Internal/InitializeFileSystemOperation.cs` |
| Builtin 文件系统 | `Runtime/FileSystem/Services/BuiltinFileSystem/BuiltinFileSystem.cs` |
| Builtin 初始化（含 CopyBuiltinPackageManifest） | `Runtime/FileSystem/Services/BuiltinFileSystem/Operations/BFSInitializeOperation.cs` |
| 内置清单拷贝 3 步 | `Runtime/FileSystem/Services/BuiltinFileSystem/Operations/Internal/CopyBuiltinPackageManifestOperation.cs` |
| 内置文件拷贝（File.Exists 短路） | `Runtime/FileSystem/Services/BuiltinFileSystem/Operations/Internal/CopyBuiltinFileOperation.cs` |
| Sandbox 文件系统 | `Runtime/FileSystem/Services/SandboxFileSystem/SandboxFileSystem.cs` |
| Sandbox 清单下载 | `Runtime/FileSystem/Services/SandboxFileSystem/Operations/Internal/DownloadPackageManifestOperation.cs` |
| Bundle 唯一标识定义 | `Runtime/ResourcePackage/PackageBundle.cs` (`BundleGuid` => `FileHash`) |
| 清单/版本/hash 文件名规则 | `Runtime/Settings/YooAssetConfiguration.cs` |
| Bundle 远端文件名风格 | `Runtime/ResourcePackage/PackageManifestHelper.cs` (`GetRemoteBundleFileName`) |
| 构建时文件名风格配置 | `Editor/BundleBuilder/BuildParameters.cs` |
| `EFileNameStyle` 枚举 | `Runtime/ResourcePackage/EFileNameStyle.cs` |
| `EBundledCopyOption` 枚举 | `Editor/BundleBuilder/EBundledCopyOption.cs` |
| Space Shooter 样例（状态机参考） | `Samples~/Space Shooter/GameScript/Runtime/PatchLogic/` |

---

## 附录 B · 术语速查

| 术语 | 含义 |
|---|---|
| **Package** | YooAsset 资源包"命名空间"，一个项目可有多个 Package 独立升级 |
| **Bundle** | AssetBundle 物理下载/缓存单位，由多个 Asset 打包而成 |
| **Asset** | 单个资源对象，`LoadAssetAsync` 的单位，有 Address、被引用计数管理 |
| **Manifest（清单）** | 每个 Package 的"户口本"，记录所有 Asset 的 Address/Bundle/Hash/依赖 |
| **BundleGuid** | Bundle 的唯一 ID，**等于 FileHash** |
| **FileHash** | Bundle 文件内容的哈希值，资源改一丁点 FileHash 就变 |
| **Builtin（内置）** | 打包时塞进 APK/IPA 的资源，物理路径 `StreamingAssets/yoo/` |
| **Sandbox / Cache（沙盒）** | 运行时下载到本地的资源，物理路径 `persistentDataPath/yoo/` |
| **IRemoteService** | 用户实现的接口，告诉 YooAsset 如何拼出 CDN 完整 URL |
| **Handle** | 加载资源后返回的句柄，通过 Release 释放引用计数 |

---

## 附录 C · PatchManager 状态机架构（B 层进阶参考）

> 本节内容对应 Space Shooter 官方样例 `Samples~/Space Shooter/GameScript/Runtime/PatchLogic/` 下的 9 个源码文件（`PatchManager.cs` + 8 个 FSM 节点）。
>
> **再次声明**：这是 B 层"参考架构"，不是 YooAsset 内部实现、也不是用户必须照抄的 API。0 基础阶段请走附录外的 A 层 7 步写法（第一部分 1.4）。本附录仅为项目做大、需要精细化 UX 管理时提供源码级路线图。

### C.1 8 节点状态机流水线

```
① InitializePackage        初始化 Package（连文件系统）
        ↓
② RequestPackageVersion    问 CDN「最新版本号是多少」
        ↓
③ UpdatePackageManifest    下载并加载对应版本的清单
        ↓
④ CreateDownloader         对比本地缓存 vs 新清单，算出差异清单
        ↓  （有差异时挂起，等玩家点确认；没差异直接跳 ⑧）
⑤ DownloadPackageFiles     实际下载差异 Bundle
        ↓
⑥ DownloadPackageOver      下载完成占位（发事件供 UI 埋点）
        ↓
⑦ ClearCacheBundle         清理被新版本淘汰的旧 Bundle
        ↓
⑧ StartGame                SetGamePackage + 切主菜单
```

### C.2 3 阶段归并

| 阶段 | 节点 | 做的事 |
|---|---|---|
| **A. 对清单** | ①②③ | 搞清楚"我是谁、我该有什么" |
| **B. 补文件** | ④⑤⑥⑦ | 把缺的下回来、把过时的扔掉 |
| **C. 起游戏** | ⑧ | 正式开跑 |

### C.3 `PatchManager.cs` 核心骨架（97 行纯 static）

```csharp
public static void Create(string packageName, EPlayMode playMode)
{
    // 1. 订阅 5 种"玩家重试事件"
    _eventGroup.AddListener<UserTryInitializePackageEvent>(OnHandleEventMessage);
    _eventGroup.AddListener<UserBeginDownloadWebFilesEvent>(OnHandleEventMessage);
    _eventGroup.AddListener<UserTryRequestPackageVersionEvent>(OnHandleEventMessage);
    _eventGroup.AddListener<UserTryUpdatePackageManifestEvent>(OnHandleEventMessage);
    _eventGroup.AddListener<UserTryDownloadWebFilesEvent>(OnHandleEventMessage);

    // 2. 把 8 个节点都注册进状态机
    _machine = new StateMachine(null);
    _machine.AddNode<FsmInitializePackage>();
    _machine.AddNode<FsmRequestPackageVersion>();
    _machine.AddNode<FsmUpdatePackageManifest>();
    _machine.AddNode<FsmCreateDownloader>();
    _machine.AddNode<FsmDownloadPackageFiles>();
    _machine.AddNode<FsmDownloadPackageOver>();
    _machine.AddNode<FsmClearCacheBundle>();
    _machine.AddNode<FsmStartGame>();

    // 3. 黑板存跨节点共享数据
    _machine.SetBlackboardValue("PackageName", packageName);
    _machine.SetBlackboardValue("PlayMode", playMode);
}

public static void Start()  => _machine.Run<FsmInitializePackage>();
public static void Update() => _machine.Update();   // 宿主每帧必须驱动
```

### C.4 3 个关键设计

1. **黑板（Blackboard）** 是各节点共享上下文的唯一通道，样例里塞进去 4 个 key：
   - `PackageName`（`Create` 时写入）
   - `PlayMode`（`Create` 时写入）
   - `PackageVersion`（②节点写入，③节点读）
   - `Downloader`（④节点写入，⑤节点读）

2. **事件驱动退回**——任一节点失败时不 throw、不 `return`；而是 `SendEventMessage`，由 UI 层弹对话框让玩家点"重试"；玩家点击后再发 `UserTryXxxEvent`，`PatchManager.OnHandleEventMessage` 把状态机 `ChangeState` 回退到那一步。

3. **`Update()` 必须由宿主每帧驱动**——不然状态机不会推进（`UniFramework.Machine` 不是 MonoBehaviour）。

### C.5 各节点详解

#### 节点 ① FsmInitializePackage

**职责：** 根据 `EPlayMode` 构造 `InitializePackageOptions` 并 `package.InitializePackageAsync()`。

```csharp
if (!YooAssets.TryGetPackage(packageName, out var package))
    package = YooAssets.CreatePackage(packageName);
```

**4 种模式的关键差异**（源码 L37-93）：

| 模式 | 挂的 FileSystem | 关键点 |
|---|---|---|
| **EditorSimulateMode** | `EditorFileSystem`（指向 `EditorSimulateBuildInvoker.Build()` 产出目录） | 绕开打包，直接读 AssetDatabase；附加 `VirtualWebglMode` / `VirtualDownloadMode` 等模拟参数 |
| **OfflinePlayMode** | 仅 `BuiltinFileSystem` | 只挂内置（StreamingAssets），没缓存也没远端 |
| **HostPlayMode** | `BuiltinFileSystem` + `CacheFileSystem`（带 `IRemoteService`） | 3 样齐全：内置、缓存、远端回退；开启 `CopyBuiltinPackageManifest` |
| **WebPlayMode** | `WebServerFileSystem`（或微信 WASM 特化） | WebGL 专用，浏览器直接从 HTTP 拿 |

**`IRemoteService`：** CDN 多域名回退接口，样例内嵌实现（L142-159）返回 `[default, fallback]` 两条 URL，YooAsset 下载器会按顺序尝试。

**节点收尾：**
```csharp
if (initializationOperation.Status != EOperationStatus.Succeeded)
    PatchInitializeFailedEvent.SendEventMessage();   // → UI 弹"初始化失败，重试"
else
    _machine.ChangeState<FsmRequestPackageVersion>();
```

#### 节点 ② FsmRequestPackageVersion

```csharp
var operation = package.RequestPackageVersionAsync();
yield return operation;
_machine.SetBlackboardValue("PackageVersion", operation.PackageVersion);
_machine.ChangeState<FsmUpdatePackageManifest>();
```

从 CDN 下载 `<PackageName>.version` 文件——几十字节的字符串版本号（如 `v3_a4b5c6...`），非常快。失败分支同 ①：发 `PatchPackageVersionRequestFailedEvent`。

#### 节点 ③ FsmUpdatePackageManifest

```csharp
var options = new LoadPackageManifestOptions(packageVersion, 60);   // 60 秒超时
var operation = package.LoadPackageManifestAsync(options);
yield return operation;
```

产出：清单树在内存里构建完成——此后 `GetAssetInfo()` / `CheckLocationValid()` 这类查询才有数据可查。失败分支：`PatchPackageManifestUpdateFailedEvent`。

#### 节点 ④ FsmCreateDownloader（唯一分叉点）

```csharp
int downloadingMaxNum = 10;      // 并发数
int failedTryAgain = 3;          // 单文件失败重试次数
var options = new ResourceDownloaderOptions(downloadingMaxNum, failedTryAgain);
var downloader = package.CreateResourceDownloader(options);
_machine.SetBlackboardValue("Downloader", downloader);

if (downloader.TotalDownloadCount == 0)
{
    _machine.ChangeState<FsmStartGame>();   // 无差异，跳过 ⑤⑥⑦
}
else
{
    PatchFoundUpdateFilesEvent.SendEventMessage(totalDownloadCount, totalDownloadBytes);
    // 此后状态机停在本节点，等玩家点确认
}
```

**两条重要事实：**
1. **本节点是整个流程唯一的"分叉点"**：有差异 → 挂起等确认；无差异 → 直接跳 ⑧（这是为什么玩家二次进游戏几乎秒进）。
2. **节点不会自动往下走**——`OnEnter()` 里发完事件立刻返回，状态机卡在这里。恢复的钥匙是玩家在 UI 上点"开始下载"，由 UI 层发 `UserBeginDownloadWebFilesEvent`。

**参数名严格对应** `ResourceDownloaderOptions(int downloadingMaxNum, int failedTryAgain)` 构造签名。

#### 节点 ⑤ FsmDownloadPackageFiles

```csharp
var downloader = (ResourceDownloaderOperation)_machine.GetBlackboardValue("Downloader");
downloader.DownloadError            += PatchWebFileDownloadFailedEvent.SendEventMessage;
downloader.DownloadProgressChanged  += PatchDownloadUpdatedEvent.SendEventMessage;
downloader.StartDownload();
yield return downloader;

if (downloader.Status != EOperationStatus.Succeeded)
    yield break;   // 失败不前进，等玩家重试

_machine.ChangeState<FsmDownloadPackageOver>();
```

**注意：** `yield break` 表示节点直接退出但**不切换状态**——状态机继续停留在本节点，配合 `UserTryDownloadWebFilesEvent` 重试。

#### 节点 ⑥ FsmDownloadPackageOver

空节点，只发一条 `PatchStepChangedEvent("Resource files download completed.")` 就跳 ⑦。**语义占位**，为 UI 埋点。

#### 节点 ⑦ FsmClearCacheBundle

清掉本地缓存里**当前清单不再引用**的旧 Bundle。

```csharp
var options = new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles);
var operation = package.ClearCacheAsync(options);
operation.Completed += Operation_Completed;
// Operation_Completed: _machine.ChangeState<FsmStartGame>();
```

#### 节点 ⑧ FsmStartGame

```csharp
GameManager.Instance.SetGamePackage(YooAssets.GetPackage("DefaultPackage"));
SceneChangeToHomeEvent.SendEventMessage();
```

做两件事：
1. **把当前 Package 注册为业务全局引用**（通过业务自己的 `GameManager.SetGamePackage`——该方法就是把 `package` 存到业务单例字段 `_gamePackage`；YooAsset 并没有 `SetDefaultPackage` 这种 API）。
2. **发"切主菜单"事件**——把舞台交给游戏本体。

至此热更流程结束，YooAsset 退居二线，只在业务每次 `LoadAssetAsync` 时被唤起。

### C.6 错误重试机制全景表（5 种失败 + 5 种玩家事件 = 5 个回跳点）

全在 `PatchManager.OnHandleEventMessage`：

| 失败事件（由 FSM 节点发出） | 玩家点"重试"后的事件（由 UI 发出） | PatchManager 的反应 |
|---|---|---|
| `PatchInitializeFailedEvent` | `UserTryInitializePackageEvent` | `ChangeState<FsmInitializePackage>` |
| `PatchPackageVersionRequestFailedEvent` | `UserTryRequestPackageVersionEvent` | `ChangeState<FsmRequestPackageVersion>` |
| `PatchPackageManifestUpdateFailedEvent` | `UserTryUpdatePackageManifestEvent` | `ChangeState<FsmUpdatePackageManifest>` |
| `PatchFoundUpdateFilesEvent` ⚠️（非失败，挂起等确认） | `UserBeginDownloadWebFilesEvent` | `ChangeState<FsmDownloadPackageFiles>` |
| `PatchWebFileDownloadFailedEvent` | `UserTryDownloadWebFilesEvent` | `ChangeState<FsmCreateDownloader>`（⚠️ 回跳到 ④ 而不是 ⑤：**重新算差异，因为重试期间清单可能又变了**） |

**关键细节：** 下载失败时回跳的是 ④ 不是 ⑤ ——样例作者的考虑是"既然失败了就从头校核一遍差异"，比较保守稳妥。

---

## 附录 D · 综合速查 / 后续专题

### D.1 一句话 Q&A 总结

| 你的问题 | 答 |
|---|---|
| `.bytes` / `.hash` / `.version` 分别干嘛 | 清单本体 / 清单校验 / 版本号入口——分开是为了省流量、防损坏 |
| `BundleName` 风格会不会覆盖翻车 | 单个玩家使用上**多数时候不翻车**，但 CDN 同步过程中有**中间态瞬间窗口**会让正在热更的玩家校验失败；且回滚困难。**线上用 `HashName` 就对了** |
| 热更是否必须分多个 Package | **不必须**。单 Package 内用 Tag + `BundledCopyByTags` 即可区分内置/热更 |
| 内置资源能不能走热更 | **能**。FileHash 变了 → 内置版被清单遗忘 → 新版下到沙盒，内置文件原地弃用 |
| 查找顺序为什么是 Builtin 先 | 因为 Builtin 的判断严格（精确 hash 匹配），Sandbox 无条件兜底——让严格的先挑、兜底的接剩下 |

### D.2 下一步学什么（按需自取）

以下 6 个方向本文未展开，**仅在项目真正需要时再回头学**：

1. **加密** —— `EncryptionServices` 自定义实现，防 Bundle 被直接解包
2. **自定义 FileSystem** —— 特殊平台（比如腾讯小游戏、字节小游戏的专属缓存）
3. **CDN 调度策略** —— 多 CDN 域名轮询、故障转移、CDN 刷新
4. **增量构建** —— 改少量资源如何减小补丁包体
5. **异步加载优先级 / 单帧最大加载数** —— 性能调优
6. **PatchManager 状态机架构精细化** —— 项目做大、UX 要求高时回看附录 C
