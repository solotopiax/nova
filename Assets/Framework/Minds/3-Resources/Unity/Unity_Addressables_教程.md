---
title: Unity Addressables 教程
summary: Addressables 入门与 Nova 对照整理稿
category: external
status: active
keywords:
  - Unity
  - Addressables
  - Addressable
  - 资源管理
---

# Unity_Addressables_教程（2.9.1）

## 使用说明

- 本文定位为 Addressables 学习整理稿，覆盖概念、Editor 配置、运行时 API 与远程更新。
- 正文保留完整教学原稿，方便后续回查细节；快速复习优先阅读本节与各章标题。
- 与 Nova 直接相关的重点章节是 Ch.3、Ch.5、Ch.8、Ch.9。

> 完整版（原稿保全）· 基于 Unity 官方 `com.unity.addressables@2.9` 手册
> Unity 版本：6000.4.2f1 · 项目：Nova Framework · 编写日期：2026-05
>
> 本文档完整保留教学整理稿与必要的 Q&A 澄清，按主题顺序组织。

---

## 速览

- **Addressables 的核心价值**：统一处理地址化、分组打包、依赖加载与引用计数释放。
- **Editor 侧关键对象**：Group、Schema、Profile、Catalog。
- **Runtime 侧关键闭环**：`Load*Async` / `InstantiateAsync` / `Release` / `UpdateCatalogs`。

## 学习大纲（保留）

本稿按“系统讲解 → 最后整合 Nova”的顺序展开。

Nova 项目已存在 `Assets/AddressableAssetsData/` 与 `ProjectSettings/ScriptableBuildPipeline.json`，说明 Addressables 已导入并初始化；后续整合关注的是“如何在现有配置上接线”，而不是从零搭建。

---

## Addressable 零基础学习大纲（10 章）

### 第一部分｜概念与心智模型（不写代码）

**Ch.1 为什么需要 Addressable**
- 对比 Resources / AssetBundle / Addressable 三者的痛点与解法
- Addressable 解决的三个核心问题：**定位**（地址化）、**打包**（Group）、**加载**（引用计数）
- 一句话心智模型："给资源一个 URL + 自动管 bundle + 自动管引用"

**Ch.2 核心概念词表**
- Address / Label / Group / Profile / Schema
- AssetReference vs string address
- Catalog（2.x 默认 `catalog.bin`，可切 JSON）与 Bundle 的关系
- Local vs Remote 加载路径

**Ch.3 生命周期与引用计数**
- `LoadAssetAsync` → `AsyncOperationHandle` → `Release` 的完整闭环
- Bundle 级引用计数 vs Asset 级引用计数
- 常见泄漏场景与检测方式

### 第二部分｜Editor 操作面（看 UI，暂不写代码）

**Ch.4 Groups 窗口全解**
- Default Local Group / Built In Data
- BundledAssetGroupSchema 的每个字段（Compression / Bundle Mode / Include in Build / Cache Clear Behavior…）
- ContentUpdateGroupSchema：Static vs Can Change Post Release

**Ch.5 Profiles 与路径变量**
- `LocalBuildPath` / `LocalLoadPath` / `RemoteBuildPath` / `RemoteLoadPath`
- 多 Profile（Default / Staging / Production）切换套路
- 环境变量插值（`[UnityEngine.AddressableAssets.Addressables.BuildPath]` 这种）

### 第三部分｜代码 API（仍不动手，先建立地图）

**Ch.6 加载 API 全景**
- `LoadAssetAsync<T>` / `LoadAssetsAsync<T>`（按 Label 批量）
- `InstantiateAsync` 与普通 `LoadAssetAsync` + `Instantiate` 的区别
- `LoadSceneAsync` / `LoadResourceLocationsAsync`
- 同步化：`WaitForCompletion` 的坑

**Ch.7 AssetReference 家族**
- `AssetReference` / `AssetReferenceGameObject` / `AssetReferenceT<T>`
- Inspector 拖拽绑定 + 运行时 Load/Release
- 与 `string address` 的选型指南

### 第四部分｜远程热更（重头戏）

**Ch.8 Content Build 与 Update Build**
- `New Build` vs `Update a Previous Build`
- `addressables_content_state.bin` 的作用（版本差分依据）
- Static / Can Change Post Release 在更新时的行为差异
- Catalog 版本号（`.hash`）校验机制

**Ch.9 CDN 部署流程**
- `ServerData/` 目录结构
- `RemoteLoadPath` 指向 CDN 前缀的配置
- `CheckForCatalogUpdates` → `UpdateCatalogs` → `DownloadDependenciesAsync` 三段式更新流程
- 断点续传 / 缓存清理 / 下载进度

---

## 节奏建议

9 章建议分 **5 个阅读段落**消化（每次 2 章，一次控制在可消化范围内）：
- 阅读段落 1：Ch.1 + Ch.2（心智模型打底）
- 阅读段落 2：Ch.3 + Ch.4（生命周期 + Groups）
- 阅读段落 3：Ch.5 + Ch.6（Profiles + 加载 API）
- 阅读段落 4：Ch.7 + Ch.8（AssetReference + Update Build）
- 阅读段落 5：Ch.9（CDN）

---

## 教学正文


开始 Ch.1。

---

# Ch.1 为什么需要 Addressable

## 1.1 资源加载的三个时代

Unity 的资源加载方案演进了三代，每一代都是上一代痛点的解药。

### 时代一：Resources.Load（石器时代）

```csharp
GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
```

**规则**：只要资源放在 `Assets/Resources/` 下，就能用字符串路径同步加载。

**看起来很爽，实际四个致命伤**：

| 痛点 | 说明 |
|------|------|
| **全量打进包** | `Resources/` 下所有资源**无差别**塞进首包，哪怕你一次都不加载。首包必然膨胀。 |
| **启动时全量扫描** | 引擎启动会扫描整个 Resources 目录建索引，资源越多启动越慢。 |
| **不支持热更** | 资源烘进主包，发版后想改一张图只能重新发版。 |
| **没有依赖管理** | 一个 Prefab 引用的贴图/材质会被**重复打进**多个使用它的包里。 |

**结论**：Resources 只适合**极少量、必然常驻**的资源（比如一个空白贴图占位符）。现代项目里它是**反模式**。

---

### 时代二：AssetBundle（青铜时代）

AssetBundle（以下简称 AB）是 Unity 官方给出的"打包 + 按需加载 + 热更"方案。

```csharp
// 加载 bundle
AssetBundle bundle = AssetBundle.LoadFromFile("path/to/player.bundle");
// 从 bundle 里加载资源
GameObject prefab = bundle.LoadAsset<GameObject>("Player");
// 用完手动卸载
bundle.Unload(false);
```

**解决了 Resources 的所有问题**：
- 按需加载（不用的 bundle 不进内存）
- 支持热更（bundle 可以放 CDN）
- 可分包

**但自己引入了更多痛点**：

| 痛点 | 说明 |
|------|------|
| **依赖地狱** | bundle A 依赖 bundle B，加载 A 前你得**自己**先加载 B。漏加载就丢材质。 |
| **引用计数得自己写** | bundle 卸载时 `Unload(true)` 会销毁所有已实例化资源，`Unload(false)` 又可能泄漏。啥时候卸载？自己数。 |
| **路径 vs bundle 名映射得自己维护** | 代码里写 `"player.bundle"` 和 `"Player"` 两个字符串，资源重命名/分组后全要改。 |
| **打包脚本得自己写** | Unity 只提供 `BuildPipeline.BuildAssetBundles` 这一个底层 API，分组策略、Label、增量构建全自己实现。 |
| **远程下载得自己写** | 版本比对、断点续传、Catalog 管理、缓存清理全是自研。 |

**典型 AB 项目的现实**：每个中大型项目都要自研一套 **AssetBundle 框架**（XAsset、YooAsset、HybridFramework 里的 ResourceComponent 都是这类）。最后大家都在重复造同一个轮子。

---

### 时代三：Addressable（现在）

Addressable = **Unity 官方**帮你把 AssetBundle 那一套上层封装全写完了。

```csharp
// 异步加载
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
GameObject prefab = handle.Result;
// 用完释放
Addressables.Release(handle);
```

**对比一下三代代码量你就懂了**：

```csharp
// Resources
var p = Resources.Load<GameObject>("Prefabs/Player");

// AssetBundle（省略了依赖加载、manifest 解析）
var manifestBundle = AssetBundle.LoadFromFile("StreamingAssets/AssetBundles");
var manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
foreach (var dep in manifest.GetAllDependencies("player.bundle"))
    AssetBundle.LoadFromFile($"StreamingAssets/AssetBundles/{dep}");
var bundle = AssetBundle.LoadFromFile("StreamingAssets/AssetBundles/player.bundle");
var p = bundle.LoadAsset<GameObject>("Player");

// Addressable
var handle = await Addressables.LoadAssetAsync<GameObject>("Player").Task;
var p = handle.Result;
```

---

## 1.2 Addressable 到底解决了什么

把 AB 时代那些"得自己写"的事情，**一次性官方托管**：

| AB 时代要自己写 | Addressable 做了什么 |
|-----------------|---------------------|
| 资源名 → bundle 名映射表 | **Catalog**（2.x 默认 `catalog.bin`，可切 JSON）自动生成 |
| 依赖收集与加载 | 加载 A 时自动加载 A 依赖的 bundle |
| 引用计数 | `LoadAssetAsync`/`Release` 一一对应，bundle 引用归零自动卸载 |
| 分组打包策略 | **Groups 窗口** + Schema 配置，无需写打包脚本 |
| 本地 vs 远程路径切换 | **Profile** 抽象，一个变量切 Dev/Staging/Prod |
| 版本更新、CDN、差分 | **Content Update Build** + Catalog Hash 校验 |
| 打包后资源不可变 | 运行时 Play Mode Script 支持"直接读源文件"模拟打包效果 |

---

## 1.3 三个核心心智模型（最重要）

Addressable 的一切设计围绕三件事，**这三件事记住，后面所有章节都好理解**：

### 模型①：定位（Addressing）

**一个资源 = 一个 URL（地址）**。

代码里永远只用地址取资源，**不关心它在哪个 bundle、哪个路径、本地还是远程**。

```csharp
// 你只需要说："把叫 Player 的东西给我"
Addressables.LoadAssetAsync<GameObject>("Player");
// 至于 Player 在哪个 bundle、在 C 盘还是 CDN，Addressable 查 Catalog 自己搞定
```

类比：Addressable 之于资源，相当于 DNS 之于 IP——**你记域名，解析交给系统**。

### 模型②：打包（Grouping）

**Group 是打包单位**。一个 Group 里的所有资源按规则（一个 bundle / 一资源一 bundle / 按 Label 分 bundle）打成一个或多个 AssetBundle。

- 你在 Groups 窗口把资源拖进不同 Group
- 每个 Group 有一份 Schema（配置打包/加载行为）
- 点一下 "Build" 按钮，Addressable 调用底层 AB API 帮你打好

**关键转变**：你不再关心"哪个资源打进哪个 bundle"，只关心"**这批资源应该一起打**还是**应该分开打**"。

### 模型③：加载（Ref-Counted Loading）

**每次 Load 必须对应一次 Release**（和 `new`/`delete`、`malloc`/`free` 一样的配对原则）。

```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Player");  // refCount+1
// ... 使用 ...
Addressables.Release(handle);                                    // refCount-1，归零则卸 bundle
```

**bundle 的加载/卸载完全由引用计数驱动**，你不再手动 `Unload`。
**但你必须诚实 Release**——漏 Release = 内存泄漏。

> 这是 Addressable 最容易出 bug 的地方。Ch.3 整章讲这个。

---

## 1.4 什么时候**不**用 Addressable

不是银弹，不适合用的场景：

| 场景 | 原因 |
|------|------|
| 纯单机小游戏（<50MB 首包） | 全量 Resources 就够，引入 Addressable 是杀鸡用牛刀 |
| 只有几个常驻资源 | 直接序列化引用（Inspector 拖拽）就行 |
| 资源迭代极少 + 不热更 | Addressable 的打包流程是额外负担 |

**适合**：中大型项目 / 有远程热更需求 / 需要分包 / 需要精细化内存控制——**也就是你的 Nova 项目**。

---

## 1.5 本章自测题

不用回答给我，自问自答一下，能答上来说明 Ch.1 过了：

1. Resources 为什么会让首包膨胀？
2. AB 时代有哪三件事是"必须自己写"的，Addressable 帮你托管了？
3. Addressable 的三个核心心智模型分别是什么？
4. 为什么说 Addressable 的内存管理基于"引用计数"？
5. 什么情况下**不**应该用 Addressable？

---

**Ch.1 结束**。本章只讲"为什么"，没有一行真正的代码要写。

下一章 **Ch.2 核心概念词表**：把 Address / Label / Group / Profile / Schema / Catalog / AssetReference 这些名词**准确定义清楚**，避免后面讨论时鸡同鸭讲。

确认继续 Ch.2？或者 Ch.1 里哪里想再展开？

---

## 📌 用户：切换到下一章


# Ch.2 核心概念词表

本章目标：把 Addressable 里七个高频名词**精确定义**，建立共享术语表。之后所有章节都会回头引用这里。

---

## 2.1 名词全景图

先看一张整体关系图，心里有个大概：

```
┌────────────────────────────────────────────────────────────┐
│                    Addressable Settings                     │
│  （AddressableAssetSettings.asset — 全局配置入口）         │
├────────────────────────────────────────────────────────────┤
│                                                             │
│   Profile（一套路径变量）                                   │
│   ├── Default                                              │
│   ├── Staging                                              │
│   └── Production                                           │
│                                                             │
│   Group（打包单位）                                         │
│   ├── Default Local Group                                  │
│   │   ├── Schema：BundledAssetGroupSchema（打包配置）      │
│   │   ├── Schema：ContentUpdateGroupSchema（更新策略）     │
│   │   └── Entries：                                        │
│   │       ├── Player.prefab   Address="Player"   Label=[Char]│
│   │       └── Enemy.prefab    Address="Enemy"    Label=[Char]│
│   └── Remote Group                                         │
│       └── Entries：...                                     │
│                                                             │
└────────────────────────────────────────────────────────────┘
                         │ Build
                         ▼
         ┌───────────────────────────────┐
         │   catalog.bin (+ catalog.hash)│ ← 运行时查表（2.x 默认二进制）
         │   xxx.bundle, yyy.bundle      │ ← 实际资源
         └───────────────────────────────┘
                         │ Runtime
                         ▼
         Addressables.LoadAssetAsync("Player")
              │
              ├─ 查 Catalog → Player 在 xxx.bundle
              ├─ 加载 xxx.bundle
              └─ 从 bundle 取出 Player 资源
```

---

## 2.2 七个核心名词

### 概念①：Address（地址）

**定义**：每个资源在 Addressable 里的**唯一字符串标识**。

- 默认值 = 资源路径（`Assets/Art/Prefabs/Player.prefab`）
- 可以改成任何你想要的名字（`Player` / `char/player_001` / `ui/login_panel`）
- **全局唯一**，不允许两个资源用同一个 Address

```csharp
// 加载时就用这个地址
Addressables.LoadAssetAsync<GameObject>("Player");
```

**类比**：Address 就是资源的"主键"。

**最佳实践**：
- 默认路径太长，建议手动设短地址
- 用命名空间分层：`ui/login/panel`、`char/enemy/goblin`
- 禁止出现大小写混乱、空格

---

### 概念②：Label（标签）

**定义**：资源的**分类标签**，一个资源可以有**多个 Label**，多个资源可以共享同一个 Label。

- Address 是 1:1（主键）
- Label 是 N:M（分类）

**典型用途**：**批量加载**。

```csharp
// 加载所有带 "Enemy" 标签的资源
Addressables.LoadAssetsAsync<GameObject>("Enemy", obj => {
    // 每个资源加载完成时回调一次
});
```

**举例**：
```
Player.prefab    Address="Player"    Labels=[Character, Hero, Preload]
Enemy.prefab     Address="Enemy"     Labels=[Character, Monster]
Loading.png      Address="Loading"   Labels=[UI, Preload]
```

- `LoadAssets("Character")` → Player + Enemy
- `LoadAssets("Preload")` → Player + Loading

**Address vs Label 选型**：
- 知道具体是哪个资源 → Address
- 想加载"一批"资源 → Label

---

### 概念③：Group（组）

**定义**：**打包单位**。一个 Group 里的资源按规则打成 **1 个或 N 个 AssetBundle**。

Group 不影响运行时加载（运行时只认 Address），只影响**打包产物**：
- 同一个 Group 里的资源**可能**打进同一个 bundle（取决于 Bundle Mode）
- 不同 Group 的资源**一定**打进不同 bundle

**Nova 项目里的典型 Group**（可在 `Assets/AddressableAssetsData/AssetGroups/` 下查看）：
- `Built In Data`：Unity 内置资源（Shader 等），别动
- `Default Local Group`：默认本地 Group，新资源默认进这里

**分组策略**（Ch.4 详讲）：
- 按使用时机分（Preload / Scene1 / Scene2）
- 按更新频率分（Static / DLC）
- 按远近分（Local / Remote）

---

### 概念④：Schema（配置档）

**定义**：挂在 Group 上的**配置 ScriptableObject**，决定这个 Group 怎么打、怎么加载。

一个 Group 可以挂**多个** Schema，常用两个：

| Schema | 作用 |
|--------|------|
| **BundledAssetGroupSchema** | 打包配置：压缩方式、bundle 命名、本地/远程、包含到 Build 与否 |
| **ContentUpdateGroupSchema** | 更新策略：Static（资源定版不变）/ Can Change Post Release（可变） |

**重点字段预览**（Ch.4 逐字段拆）：

BundledAssetGroupSchema：
- `Build Path` / `Load Path`：用 Profile 变量
- `Bundle Mode`：Pack Together / Pack Separately / Pack Together By Label
- `Compression`：LZ4 / LZMA / Uncompressed
- `Include In Build`：打不打进构建

ContentUpdateGroupSchema：
- `Update Restriction`：Static / Can Change Post Release

**类比**：Group 是"项目"，Schema 是"项目的配置文件"。

---

### 概念⑤：Profile（环境档案）

**定义**：一套**路径变量**的集合，用于切换 Dev / Staging / Prod 环境。

Profile 里常见的变量：

| 变量 | 含义 | 默认值 |
|------|------|--------|
| `LocalBuildPath` | 本地资源打包输出目录 | `Library/com.unity.addressables/aa/[BuildTarget]` |
| `LocalLoadPath` | 本地资源运行时加载路径 | `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` |
| `RemoteBuildPath` | 远程资源打包输出目录 | `ServerData/[BuildTarget]` |
| `RemoteLoadPath` | 远程资源运行时加载路径 | `http://localhost/[BuildTarget]`（默认占位，需改成 CDN） |

**切换 Profile 的效果**：
- Dev 时：RemoteLoadPath = `http://192.168.1.100/dev/`
- Prod 时：RemoteLoadPath = `https://cdn.your-game.com/prod/`
- 不用改一行代码，切 Profile 就行

**和 Group 的关系**：Group 的 Schema 里的路径字段**引用** Profile 变量，而不是写死路径。

```
Group.BundledAssetGroupSchema.LoadPath = "{RemoteLoadPath}"  ← 引用 Profile 变量
                                                    ↓
Profile.RemoteLoadPath = "https://cdn.your-game.com/"        ← 真实值
```

---

### 概念⑥：Catalog（目录）

**定义**：**运行时查表**用的清单文件，记录"Address → Bundle + Bundle 内资源名"的映射。

构建后会生成（2.x 默认为**二进制** Catalog）：
- `catalog.bin`：清单内容（二进制，更小更快）
- `catalog.hash`：清单 hash（仅当 `Build Remote Catalog` 勾选时生成，用于远程更新时比对版本）
- 各种 `.bundle` 文件

> **Catalog 格式开关**：Group Schema / Addressables Settings 里的 `Enable Json Catalog`（2.9.1 仍保留）勾上后输出 `catalog.json`，调试可读；不勾就是 `catalog.bin`。生产默认 `.bin`。

**运行时加载流程**：

```
Addressables.LoadAssetAsync("Player")
    │
    ├─ 查 Catalog：Player → player_assets_xxx.bundle 里的 Player
    ├─ Catalog 发现需要加载 player_assets_xxx.bundle
    ├─ 加载 bundle（本地或远程）
    └─ 从 bundle LoadAssetAsync("Player") 返回资源
```

**Catalog 的两种形态**：
- **打进 StreamingAssets**：首次启动用这个
- **远程 Catalog**：每次启动对比远程 hash，有更新就下载新 Catalog（**热更的基石**）

> Catalog 是整个热更机制的心脏。Ch.8 专门讲它。

---

### 概念⑦：AssetReference（资源引用）

**定义**：一个**可以在 Inspector 拖拽赋值**的资源引用类型，代码里持有这个引用而不是 Address 字符串。

```csharp
public class UILoginView : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_PlayerPrefab;
    // Inspector 里把 Player.prefab 拖进来即可

    private AsyncOperationHandle<GameObject> m_Handle;

    private async void Start()
    {
        m_Handle = m_PlayerPrefab.LoadAssetAsync();
        await m_Handle.Task;
        Instantiate(m_Handle.Result);
    }

    private void OnDestroy()
    {
        if (m_Handle.IsValid())
            Addressables.Release(m_Handle);
    }
}
```

**AssetReference vs string address**：

| 维度 | AssetReference | string address |
|------|----------------|----------------|
| 编辑器检查 | ✓ 拖拽即赋值，找不到资源 Inspector 会警告 | ✗ 字符串拼错了运行时才炸 |
| 重构友好 | ✓ 改地址 Inspector 自动跟 | ✗ 改地址全项目搜字符串 |
| 动态加载 | ✗ 编辑时就要确定资源 | ✓ 可以拼字符串 |

**选型原则**：
- 已知是哪个资源 → AssetReference
- 运行时动态决定 → string address

**家族成员**：
- `AssetReference`：任意类型
- `AssetReferenceGameObject`：只能拖 GameObject
- `AssetReferenceSprite` / `AssetReferenceTexture` / `AssetReferenceT<T>`：限定类型

> 详细用法 Ch.7 讲。

---

## 2.3 名词关系速查表

```
资源（Asset）
  ├── 有一个 Address（唯一主键）
  ├── 有 0..N 个 Label（分类标签）
  └── 属于 1 个 Group（打包单位）
      └── 挂着 1..N 个 Schema（配置档）
          └── 路径字段引用 Profile（环境档案）
                     ↓ Build
           生成 Catalog + Bundles
                     ↓ Runtime
           Addressables.LoadAssetAsync(address)
                     ↓
           代码可以用 string address 或 AssetReference
```

---

## 2.4 易混淆点澄清

### Q1：Group 和 Bundle 是一回事吗？

**不是**。Group 是**配置**，Bundle 是**打包产物**。
- 一个 Group **可能**打成 1 个 bundle（默认 Pack Together）
- 一个 Group 也可能打成 **N 个 bundle**（Pack Separately / Pack Together By Label）

### Q2：Address 和资源路径是一回事吗？

**默认**是。但可以手动改。
- 默认 Address = 资源 AssetPath（`Assets/Xxx/Yyy.prefab`）
- 改成 `yyy` 之后，代码里 `LoadAssetAsync<T>("yyy")` 就能加载
- **改 Address 不改文件路径**

### Q3：Label 能用来加载单个资源吗？

**可以，但不推荐**。
- `LoadAssetAsync<T>("label")` 会只加载**第一个**匹配的资源，顺序不稳定
- Label 是为**批量**设计的，单个资源该用 Address

### Q4：Profile 切换后要不要重新打包？

**不一定**。
- 只改 LoadPath → 不用重打包（运行时路径变了，Catalog 里记的是变量引用）
- 改了 BuildPath → 要重打包（打包产物去哪改了）

### Q5：Catalog 是每个 Group 一份还是全局一份？

**全局一份**。整个项目所有 Group 的资源都写进一份 `catalog.bin`（勾 `Enable Json Catalog` 则输出 `catalog.json`）。

---

## 2.5 和 Nova 现有体系的名词对照（先埋个点）

| Nova 现有 | Addressable 对应 |
|-----------|------------------|
| `IAssetLoadManager.LoadAsync<T>(path)` | `Addressables.LoadAssetAsync<T>(address)` |
| HotfixComponent 下载的资源包 | Addressable Remote Group 的 bundle |
| 资源路径字符串（`Assets/Game/Configs/ConfigRuntime.asset`） | Address（可以改成 `ConfigRuntime`） |
| `ConfigRuntimeSO.AotMetadataDlls` 列出的 DLL 文件 | 可以标 Label = `AotMetadata` 批量加载 |

> 这些对应关系只是心智映射，真动 Nova 整合要等到需求真的到了再拆。

---

## 2.6 本章自测题

1. Address 和 Label 的区别是什么？各自的使用场景？
2. Group 和 Bundle 是什么关系？
3. Schema 有什么作用？列举你记得的两种 Schema。
4. Profile 解决了什么问题？为什么切环境不需要改代码？
5. Catalog 在运行时扮演什么角色？为什么它是热更的关键？
6. AssetReference 相比 string address 有什么优势？

---

**Ch.2 结束**。词表已经建立，下一章开始触及真正的机制。

**Ch.3 生命周期与引用计数**：讲 `LoadAssetAsync` 到底返回什么、`Release` 什么时候调、bundle 什么时候真正卸载、最常见的三种内存泄漏场景。这是 Addressable 最容易出 bug 的地方，也是面试常考点。

继续 Ch.3？

---

## 📌 用户：切换到下一章


# Ch.3 生命周期与引用计数

本章是 Addressable 的**最核心机制**。理解了引用计数，后面所有"为什么我的内存没释放"、"为什么资源加载不出来"都能自己推出答案。

---

## 3.1 核心命题

> **Addressable 的内存管理 = 引用计数**

- 每次 `Load` → refCount + 1
- 每次 `Release` → refCount - 1
- refCount 归零 → 真正卸载

这条规则贯穿**资源**和**bundle**两个层次，本章拆清楚。

---

## 3.2 AsyncOperationHandle：一切操作的返回值

### 3.2.1 它是什么

每次调用 `Addressables.LoadAssetAsync` / `InstantiateAsync` / `LoadSceneAsync`，**都会返回一个 `AsyncOperationHandle`**（泛型版本 `AsyncOperationHandle<T>`）。

```csharp
AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("Player");
```

这个 handle 是**所有后续操作的抓手**：
- 查询加载进度
- 等待完成
- 取结果
- 释放引用

### 3.2.2 Handle 的状态机

```
┌─────────┐  LoadAssetAsync()  ┌─────────┐  加载完成  ┌──────────┐
│  None   │────────────────────►│Executing│───────────►│Succeeded │
└─────────┘                     └─────────┘            └──────────┘
                                     │                       │
                                     │ 加载失败              │ Release()
                                     ▼                       ▼
                                ┌─────────┐            ┌──────────┐
                                │ Failed  │            │ Released │
                                └─────────┘            └──────────┘
```

**Status 枚举**（`handle.Status`）：
- `None`：未开始
- `Succeeded`：成功
- `Failed`：失败（查 `handle.OperationException`）

**其他查询属性**：
- `handle.IsDone`：是否完成（成功或失败都算）
- `handle.IsValid()`：handle 是否还有效（Release 后返回 false）
- `handle.PercentComplete`：进度 0~1
- `handle.Result`：加载结果（成功后才有值）

### 3.2.3 等待完成的三种写法

```csharp
// 写法 1：回调
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
handle.Completed += h => {
    if (h.Status == AsyncOperationStatus.Succeeded)
        Instantiate(h.Result);
};

// 写法 2：await Task（最常用）
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
if (handle.Status == AsyncOperationStatus.Succeeded)
    Instantiate(handle.Result);

// 写法 3：同步阻塞（慎用）
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
GameObject prefab = handle.WaitForCompletion();  // 阻塞主线程直到完成
```

> `WaitForCompletion` 的坑 Ch.6 专门讲，现在只需知道它存在且危险。

---

## 3.3 两层引用计数模型

这是本章最重要的图。**资源**和**bundle**各有一份引用计数，互相联动。

```
┌────────────────────────────────────────────────────────┐
│  Bundle: player_assets.bundle        [ bundle refCount ] │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Asset: Player.prefab             [asset refCount]│  │
│  │  Asset: Enemy.prefab              [asset refCount]│  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### 3.3.1 资源引用计数

```csharp
var h1 = Addressables.LoadAssetAsync<GameObject>("Player");  // Player refCount: 0 → 1
var h2 = Addressables.LoadAssetAsync<GameObject>("Player");  // Player refCount: 1 → 2
// 注意：h1 != h2，是两个不同的 handle，但指向同一个资源
Addressables.Release(h1);                                    // Player refCount: 2 → 1
Addressables.Release(h2);                                    // Player refCount: 1 → 0（资源可被回收）
```

### 3.3.2 Bundle 引用计数

**只要 bundle 里有任何一个资源 refCount > 0，bundle 本身就保持加载**。

```csharp
// 假设 Player 和 Enemy 都在 player_assets.bundle 里
var h1 = Addressables.LoadAssetAsync<GameObject>("Player");  
// bundle refCount: 0 → 1（bundle 被加载）
// Player refCount: 0 → 1

var h2 = Addressables.LoadAssetAsync<GameObject>("Enemy");   
// bundle refCount: 1 → 2
// Enemy refCount: 0 → 1

Addressables.Release(h1);
// Player refCount: 1 → 0（Player 资源可回收）
// bundle refCount: 2 → 1（bundle 仍在）

Addressables.Release(h2);
// Enemy refCount: 1 → 0
// bundle refCount: 1 → 0（bundle 被卸载，整个 bundle 从内存消失）
```

**关键结论**：
1. bundle 的生命周期 = 它内部所有资源 refCount 的 OR
2. **你不直接管 bundle**，你只管资源的 Load/Release，bundle 自动跟着走
3. 这意味着——**少 Release 一次，整个 bundle 都卸不掉**

### 3.3.3 依赖 bundle 的引用计数

Addressable 会自动追踪依赖。比如 Player.prefab 引用了 common_tex.bundle 里的贴图：

```csharp
var h = Addressables.LoadAssetAsync<GameObject>("Player");
// player_assets.bundle refCount: 0 → 1
// common_tex.bundle refCount: 0 → 1（自动加载依赖）

Addressables.Release(h);
// player_assets.bundle refCount: 1 → 0（卸载）
// common_tex.bundle refCount: 1 → 0（卸载，若无其他引用）
```

**依赖链是自动的**，你永远不需要手动加载依赖 bundle。

---

## 3.4 Release 的三种形态

### 3.4.1 `Addressables.Release(handle)`

最基本形态：释放一个 handle 的引用。

```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
// 用完
Addressables.Release(handle);
```

**等价写法**：`handle.Release()`（扩展方法）。

### 3.4.2 `Addressables.ReleaseInstance(instance)`

专用于 `InstantiateAsync` 创建的实例。

```csharp
var handle = Addressables.InstantiateAsync("Player");
await handle.Task;
GameObject instance = handle.Result;

// 错误做法：
Destroy(instance);           // ✗ 只销毁 GameObject，引用计数不减，bundle 泄漏

// 正确做法：
Addressables.ReleaseInstance(instance);  // ✓ 销毁 GameObject 且减引用计数
```

**为什么 Destroy 不够**：`InstantiateAsync` 内部做了两件事：
1. 加载 prefab（refCount + 1）
2. 实例化 GameObject

`Destroy` 只回滚第 2 步，`ReleaseInstance` 同时回滚两步。

> **例外（见附录 Q&A Q3 详解）**：在同一帧 `OnDestroy` 回调里对 `gameObject` 调 `ReleaseInstance` 是可行的——此时 instance 还没变 Unity "fake null"，Addressables 内部字典仍能命中。跨帧再补 `ReleaseInstance(instance)` 则会失败。所以"Destroy 后再 ReleaseInstance"**唯一安全写法**是把 `ReleaseInstance` 放进组件 `OnDestroy`。

### 3.4.3 `Addressables.Release(handle)` 对 `LoadAssetsAsync`

批量加载也只给一个 handle，释放一次就行。

```csharp
var handle = Addressables.LoadAssetsAsync<GameObject>("Enemy", null);
await handle.Task;
// handle.Result 是 IList<GameObject>
foreach (var enemy in handle.Result)
    Instantiate(enemy);

// 只需 Release 一次，内部批量处理
Addressables.Release(handle);
```

---

## 3.5 三种常见泄漏场景

**记住这三种，90% 的内存泄漏都是它们**。

### 泄漏场景①：Load 后忘记 Release

```csharp
// ✗ 错误
public async void LoadPlayer()
{
    var handle = Addressables.LoadAssetAsync<GameObject>("Player");
    await handle.Task;
    Instantiate(handle.Result);
    // 函数结束，handle 作为局部变量被 GC，但 Addressable 内部引用计数没减
    // bundle 永远不会卸载
}
```

**修法**：把 handle 存为字段，在合适时机 Release。

```csharp
// ✓ 正确
private AsyncOperationHandle<GameObject> m_Handle;

public async void LoadPlayer()
{
    m_Handle = Addressables.LoadAssetAsync<GameObject>("Player");
    await m_Handle.Task;
    Instantiate(m_Handle.Result);
}

private void OnDestroy()
{
    if (m_Handle.IsValid())
        Addressables.Release(m_Handle);
}
```

### 泄漏场景②：InstantiateAsync 用 Destroy 销毁

```csharp
// ✗ 错误
var handle = Addressables.InstantiateAsync("Player");
await handle.Task;
GameObject instance = handle.Result;
// ... 游戏中 ...
Destroy(instance);  // bundle refCount 不减，泄漏
```

**修法**：

```csharp
// ✓ 正确
Addressables.ReleaseInstance(instance);
```

**进阶修法**：挂一个 AutoRelease 组件，GameObject 被 Destroy 时自动 Release。Addressable 提供了 `AddressablesInstantiationExtensions`，也可以自己写：

```csharp
public class AutoReleaseOnDestroy : MonoBehaviour
{
    private void OnDestroy()
    {
        Addressables.ReleaseInstance(gameObject);
    }
}
```

### 泄漏场景③：重复 Load 只 Release 一次

```csharp
// ✗ 错误
for (int i = 0; i < 10; i++)
{
    var handle = Addressables.LoadAssetAsync<GameObject>("Player");
    await handle.Task;
    // 每次循环 refCount + 1，10 次后 refCount = 10
}
// 只在结尾 Release 一次 → refCount = 9，永远不归零
Addressables.Release(handle);
```

**修法**：每个 handle 都要 Release，或者只加载一次复用。

```csharp
// ✓ 正确：加载一次复用
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
for (int i = 0; i < 10; i++)
    Instantiate(handle.Result);
Addressables.Release(handle);  // 一次加载一次释放
```

---

## 3.6 Handle 被无效化的情况

调用 `Release` 后 handle 变成 **invalid**，再用就会报错。

```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
Addressables.Release(handle);

// ✗ 报错：handle 已 invalid
var r = handle.Result;

// ✓ 安全检查
if (handle.IsValid())
    Addressables.Release(handle);
```

**实践**：所有 Release 前都加 `IsValid()` 检查，防止重复释放或空 handle。

---

## 3.7 Scene 加载的生命周期

Scene 是特殊资源，独立一套。

```csharp
// 加载
var handle = Addressables.LoadSceneAsync("BattleScene", LoadSceneMode.Additive);
await handle.Task;
SceneInstance scene = handle.Result;

// 卸载（两种等价）
var unloadHandle = Addressables.UnloadSceneAsync(handle);
await unloadHandle.Task;
// 或
var unloadHandle2 = Addressables.UnloadSceneAsync(scene);
```

**注意**：
- **不要**对 Scene handle 用 `Addressables.Release`
- **必须**用 `UnloadSceneAsync`
- Single 模式加载会自动卸载旧场景，Additive 模式要手动卸载

---

## 3.8 泄漏检测工具

### 3.8.1 Event Viewer（官方工具）

菜单：`Window → Asset Management → Addressables → Event Viewer`

运行时能看到：
- 每个 bundle 的 refCount 实时变化
- 每个资源的加载/释放时间轴
- 哪个资源还挂着没释放

**前提**：`AddressableAssetSettings` → `Send Profiler Events` 要勾上。

### 3.8.2 Profiler Memory

Unity Profiler 的 Memory 面板里能看到 bundle 占用。泄漏时 bundle 数会只增不减。

### 3.8.3 自检清单（code review 时对照）

- 每个 `LoadAssetAsync` 有没有对应的 `Release`？
- 每个 `InstantiateAsync` 是用 `ReleaseInstance` 还是 `Destroy`？
- 每个 `LoadSceneAsync` 有没有对应的 `UnloadSceneAsync`？
- 协程/异步里 Load 到一半提前退出，有没有处理取消？
- 组件在 `OnDestroy` 是否全部释放了持有的 handle？

---

## 3.9 Load + Release 标准模板

把下面这个模式背下来，90% 场景通用。

### 3.9.1 加载单资源（持久持有）

```csharp
public class UILoginView : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> m_LogoHandle;

    private async void Awake()
    {
        m_LogoHandle = Addressables.LoadAssetAsync<GameObject>("Logo");
        await m_LogoHandle.Task;
        if (m_LogoHandle.Status == AsyncOperationStatus.Succeeded)
            Instantiate(m_LogoHandle.Result, transform);
    }

    private void OnDestroy()
    {
        if (m_LogoHandle.IsValid())
            Addressables.Release(m_LogoHandle);
    }
}
```

### 3.9.2 加载即用即丢（一次性实例化）

```csharp
public async void SpawnEnemy()
{
    var handle = Addressables.InstantiateAsync("Enemy", transform.position, Quaternion.identity);
    await handle.Task;
    // Enemy 实例在 ReleaseInstance 或 Scene 卸载时自动清理
}

// 敌人死亡时
public void OnEnemyDie(GameObject enemy)
{
    Addressables.ReleaseInstance(enemy);
}
```

### 3.9.3 批量加载（一次 Load，批量用）

```csharp
public class EnemyPool : MonoBehaviour
{
    private AsyncOperationHandle<IList<GameObject>> m_Handle;
    private IList<GameObject> m_Prefabs;

    public async UniTask Preload()
    {
        m_Handle = Addressables.LoadAssetsAsync<GameObject>("Enemy", null);
        await m_Handle.Task;
        m_Prefabs = m_Handle.Result;
    }

    public GameObject Spawn(int index)
    {
        return Instantiate(m_Prefabs[index]);  // 直接用 Unity 原生 Instantiate
    }

    private void OnDestroy()
    {
        if (m_Handle.IsValid())
            Addressables.Release(m_Handle);
    }
}
```

---

## 3.10 本章自测题

1. `AsyncOperationHandle` 是什么？它为什么是 Addressable 所有操作的核心？
2. Load 一个资源时，引用计数在哪几个层次上增加？
3. bundle 什么时候会真正被卸载？
4. `Destroy(instance)` 和 `ReleaseInstance(instance)` 的区别？
5. 列出三种常见的内存泄漏场景和各自的修法。
6. Scene 的生命周期管理和普通资源有什么不同？
7. 连续 Load 同一个资源 10 次，只 Release 一次，会发生什么？

---

## 3.11 本章要点回顾

**四条底层规则**，背下来：

1. Load 返回 handle，handle 是一切操作的抓手。
2. 每次 Load 必对应一次 Release，资源和 bundle 两层引用计数。
3. `InstantiateAsync` 创建的实例必须用 `ReleaseInstance`，不能用 `Destroy`。
4. bundle 的生命周期 = 内部所有资源 refCount 的 OR，归零才卸载。

---

**Ch.3 结束**。到这里 Addressable 的**运行时机制**已经讲透了。下一部分进入**编辑器操作面**，开始看 Unity 界面。

**Ch.4 Groups 窗口全解**：打开 Groups 窗口，逐字段讲清楚 BundledAssetGroupSchema 和 ContentUpdateGroupSchema 的每一个选项意味着什么、该怎么配。这是动手部分的开始。

继续 Ch.4？

---

## 💬 用户提问

> 开始ch4之前，我还有几个ch3中的问题：
>   1.构建后会生成：
>   - content_catalog.json：清单内容
>   - content_catalog.hash：清单 hash（用于远程更新时比对版本）
> 在哪里？我没看到有这两个文件
> 
> 2.调用LoadAssetAsync时，如何区分传入的参数是label还是Address
> 
> 3.Addressables.InstantiateAsync("Player"); 以后可以先Destroy后，再执行 Addressables.ReleaseInstance(instance);吗？
> 
> 4.菜单：Window → Asset Management → Addressables → Event Viewer  在我的工程中没有看到EventViewer
> ，AddressableAssetSettings → Send Profiler Events 也没有找到，是不是版本的问题。目前我安装的是2.9.1


好，收集到实际数据了。逐题回答。

---

## Q1：`content_catalog.json` / `.hash` 在哪？

我在 Ch.2 里的描述是**老版本 Addressables（≤1.x）**的样子。你装的是 **2.9.1**，格式已经升级。实际情况如下：

**你项目里真实存在的文件**（我刚查过）：

```
Library/com.unity.addressables/aa/Android/
  ├── catalog.bin          ← 新版 Catalog（二进制，不是 JSON 了）
  ├── catalog.hash         ← 版本 hash，和旧版作用一样
  └── Android/             ← 实际的 bundle 目录
      └── xxx.bundle

ServerData/Android/
  └── remotegroup_assets_all_39fd4a45c91a5be7eeeaedf9805906f1.bundle
```

**三点修正你要记住**：

1. **Catalog 2.x 默认是 `catalog.bin`（二进制）**，不是 `content_catalog.json`。更小、加载更快。老版本或显式勾 "JSON Catalog" 才会生成 `.json`。
2. **Catalog 不在 `Assets/` 下**，打包产物统一在：
   - 本地：`Library/com.unity.addressables/aa/[Platform]/`（Unity 生成，不提交 git）
   - 远程：`ServerData/[Platform]/`（你上传到 CDN 的内容）
3. **`Assets/AddressableAssetsData/` 下只有配置**（Settings/Groups/Profiles），**没有构建产物**。构建产物的目录由 Profile 里的 `LocalBuildPath` / `RemoteBuildPath` 决定。

所以"你没看到"**正常**——Ch.2 那里我该注明版本差异，这是我的疏漏。记住新的心智：

```
配置  →  Assets/AddressableAssetsData/      （提交 git）
本地产物 →  Library/com.unity.addressables/  （不提交，Unity 生成）
远程产物 →  ServerData/                      （上传 CDN，也不提交 git 仓库主分支）
```

---

## Q2：`LoadAssetAsync` 怎么区分 Address 和 Label？

**其实不区分**——API 接受的是一个叫 **key** 的通用参数，Address 和 Label 都是合法的 key。真正区分行为的是**你调用的是哪个方法**：

| 方法 | 行为 |
|------|------|
| `LoadAssetAsync<T>(key)` | 返回**第一个**匹配 key 的资源。key 是 Address 时精确命中；key 是 Label 时返回第一个带该 Label 的资源（**顺序不稳定**） |
| `LoadAssetsAsync<T>(key, callback)` | 返回**所有**匹配 key 的资源列表。通常用于 Label 批量加载 |

**判断优先级**：Addressable 内部查 Catalog 时，**Address 查找和 Label 查找共用同一张表**。它本质上是"给我所有 key == X 的资源"，然后单数方法取第一个、复数方法取全部。

所以**约束来自约定**，不是来自类型：
- 约定：Address 全局唯一 → 单数 Load 永远命中你要的那个
- 约定：Label 多对多 → 单数 Load 行为不确定，必须用复数 Load

**极端情况**：如果一个 Address 和一个 Label 同名（比如 Address="Enemy"，某资源 Label 里也有 "Enemy"），`LoadAssetsAsync("Enemy")` 会返回**并集**。这是反模式，实践上**禁止 Address 和 Label 重名**。

**一句话总结**：API 不区分，你在命名规范上自己区分——Address 用小写+斜杠（`ui/login/panel`），Label 用首字母大写（`UI`、`Preload`）。

---

## Q3：先 `Destroy` 再 `ReleaseInstance` 可以吗？

**不行，绝对不可以**。两个原因：

### 原因 1：`ReleaseInstance` 需要 GameObject 来反查内部映射

`Addressables.InstantiateAsync` 在内部维护了一张 **instance → handle** 的字典：

```
Addressables 内部字典：
{
  <GameObject player_instance_A> → <handle_A, refCount+1 记录>,
  <GameObject enemy_instance_B>  → <handle_B, refCount+1 记录>,
}
```

`ReleaseInstance(instance)` 的实现大致是：
1. 用 instance 查字典找到对应 handle
2. 减 handle 的 refCount
3. `Destroy(instance)`
4. 从字典移除

你先 `Destroy(instance)`，GameObject 已经销毁。再调 `ReleaseInstance(instance)`，**传进去的是一个已销毁的引用**（Unity 的 "fake null"）。

### 原因 2：实际后果

`ReleaseInstance` 在 Addressables 2.x 里对"不是通过 InstantiateAsync 创建的"或"已销毁"的对象会：
- **返回 false**（表示没识别这个实例）
- **不会**减 refCount

结果：GameObject 销毁了，但**内部引用计数没减，bundle 永远泄漏**。

### 正确做法

**二选一，不能混用**：

```csharp
// 方案 A：统一用 ReleaseInstance（推荐）
Addressables.ReleaseInstance(instance);   // 内部会自动 Destroy，不用你再 Destroy

// 方案 B：自写 AutoRelease 组件，让 Destroy 路径也能减 refCount
public class AutoReleaseOnDestroy : MonoBehaviour
{
    private void OnDestroy()
    {
        // 注意这里也是 ReleaseInstance，不是别的
        Addressables.ReleaseInstance(gameObject);
    }
}
// 这样写完后，外部代码 Destroy(instance) 或走场景卸载都能正确释放
```

**心法**：`InstantiateAsync` 创建的对象，**销毁入口永远只有 `ReleaseInstance` 一个**。要么直接调，要么通过 OnDestroy 间接调。

---

## Q4：Event Viewer / Send Profiler Events 找不到

**是版本问题**，但原因可能出乎你意料：不是你装得太旧，而是**装得太新**。

### Addressables 2.x 的变化

- **Event Viewer 已从独立窗口移除**，功能并入 Unity 标准 Profiler（Profile Analyzer 模块和自定义 Counter）
- `Send Profiler Events` 开关位置换了，叫法也变了

### 2.9.1 里怎么开启与查看

**步骤 1 — 打开 Addressables Settings**
- Project 窗口选 `Assets/AddressableAssetsData/AddressableAssetSettings.asset`
- Inspector 里展开 **Diagnostics** 折叠组（有些版本是 **Build and Play Mode Scripts** 下面）
- 勾上 **Send Profiler Events**（2.x 可能显示为 `Log Runtime Exceptions` + `Send Profiler Events` 两项）

**步骤 2 — 运行时查看**
- 菜单 `Window → Analysis → Profiler`（Unity 标准 Profiler）
- 顶部 `Profiler Modules` → `+` → 添加 **Addressable Assets** 模块
- 运行项目，在 Profiler 里能看到 bundle 加载/卸载、refCount 变化

**步骤 3（可选）— 官方新工具 Asset Hub**
Addressables 2.x 给了新窗口 `Window → Asset Management → Addressables → Hub`（Asset Hub），里面有诊断入口。2.9.1 应该有。

### 如果 Send Profiler Events 也找不到

有两种可能：

1. **Inspector 默认折叠**：AddressableAssetSettings 的 Inspector 有多个折叠组，`Diagnostics` 默认是收起的，看起来像不存在。点开试试。
2. **被移除了**：最新几个 2.x 版本里这个开关被转成"默认始终开启"，UI 上就不显示了。

**你直接告诉我结果**：
- 打开 `AddressableAssetSettings.asset` 的 Inspector，把**所有折叠组**都展开，找找有没有 `Diagnostics` / `Send Profiler Events` / `Log Runtime Exceptions` 字眼？
- 找到了就勾上；没找到说明默认已启用，不用管。

然后试菜单：`Window → Analysis → Profiler` → 加 `Addressable Assets` 模块，看看能不能出来。

---

## 小结

| 问题 | 答案速记 |
|------|----------|
| Q1 Catalog 在哪 | `Library/com.unity.addressables/aa/[Platform]/catalog.bin + .hash`，2.x 是二进制，不在 Assets 下 |
| Q2 Address vs Label 区分 | API 不区分，`LoadAssetAsync` 取第一个、`LoadAssetsAsync` 取全部；靠命名规范自律 |
| Q3 先 Destroy 再 ReleaseInstance | **不行**，会泄漏。二选一：直接 ReleaseInstance，或用 AutoRelease 组件兜底 |
| Q4 Event Viewer | 2.x 移除独立窗口，用 `Window → Analysis → Profiler` + Addressable Assets 模块 |

---

回答完毕。准备好进入 **Ch.4 Groups 窗口全解**了吗？Ch.4 开始要你打开 Unity 跟着我一起操作界面了。

截图看到了，给你对照一下我之前讲的 vs 你看到的实际 UI。

---

## 对 Q4 的更新（你的 2.9.1 实际情况）

**图 2（AddressableAssetSettings Inspector）证实**：

- `Diagnostics` 组里**只有一个选项**：`Log Runtime Exceptions`
- **没有** `Send Profiler Events` 开关

**结论修正**：
Addressables 2.x 里 `Send Profiler Events` 已经被**移除/默认常开**，不再作为独立开关暴露。你不用管它，只要保留 `Log Runtime Exceptions` 勾选（已勾上，就这样）即可——这个开关确保 Addressable 内部异常会打印到 Console，对 debug 很有用。

**图 1（菜单）证实**：

Addressables 2.x 的子菜单是：
- `Groups`（打包配置主窗口）
- `Addressables Report`（替代老版 Event Viewer，**构建产物分析工具**，打包后用）
- `Optimize Binary Catalog`
- `Profiles`（环境档案管理）
- `Settings`（等同于点开 AddressableAssetSettings.asset）
- `Analyze`（资源依赖/重复资源分析）
- `Extract Binary Catalog`（把 `.bin` Catalog 转成 JSON 查看）
- `Auto Group Generator`

**关键替代关系**：

| 老版本概念（Ch.3 我讲的） | Addressables 2.9.1 对应 |
|--------------------------|-------------------------|
| Event Viewer（运行时实时看 refCount） | `Window → Analysis → Profiler` → 加 `Addressable Assets` 模块 |
| 构建后的资源分析 | `Window → Asset Management → Addressables → Addressables Report` |
| 资源依赖/重复检查 | `Window → Asset Management → Addressables → Analyze` |
| 查看 Catalog 内容（旧 .json 可直接打开） | `Extract Binary Catalog`（把 .bin 转成 .json 再看） |

---

## 一个小任务，确认一下工具能跑起来

你现在可以实际走一遍，把工具摸熟：

**步骤 1**：`Window → Analysis → Profiler` 打开标准 Profiler

**步骤 2**：Profiler 左上角 `Profiler Modules` 下拉 → 点 `+ Add` 或 `Edit` → 找到 `Addressable Assets` 勾上（如果列表里没有 Addressable Assets，说明该模块要通过代码 Counter 注册，先跳过，打包后用 Addressables Report 看也够）

**步骤 3**：`Window → Asset Management → Addressables → Addressables Report` 打开试一下。这是新版主打的分析窗口，点右上角 `Build`（或者加载一个已有的 build 报告）就能看到每个 bundle 里装了什么、大小多少。

---

## 对 Q1 的一个小补充（从你图里看到的）

图 2 里 `Catalog` 组的两个选项值得注意，Ch.8 会详讲，先埋个点：

- **`Build Remote Catalog`**（未勾）：**这个不勾，就没法热更**。勾了之后远程更新时会生成/上传 Catalog 到 `RemoteBuildPath`，客户端才能检测到新版本。你现在没勾是正常的，因为还没到远程热更阶段。
- **`Enable Json Catalog`**（未勾）：勾了会把 Catalog 输出成 JSON 而不是 `.bin`。调试时偶尔有用（可读），生产环境不勾（`.bin` 更小更快）。你现在看到的 `catalog.bin` 就是因为这里没勾。

---

## 总结到这

你现在对工具链的心智应该是：

```
配置时     →  Addressables Settings Inspector
管资源     →  Groups 窗口
管环境路径 →  Profiles 窗口
打包分析   →  Addressables Report / Analyze
运行时诊断 →  标准 Profiler + Addressable Assets 模块
想看 .bin  →  Extract Binary Catalog
```

---

你有两个选择：

1. **现在开始 Ch.4**：打开 Groups 窗口，我带你逐字段拆 Schema
2. **先去试一下 Addressables Report**：构建一次现有的 Group（哪怕只有你那个测试资源），看看报告长什么样，有直观感受再进 Ch.4

选哪个？

---

## 💬 用户提问

> Addressables.InstantiateAsync("Player"); 以后可以先Destroy后，再执行 Addressables.ReleaseInstance(instance);吗？  你刚才的回答是不可以，但是你给的例子B中是把Addressables.ReleaseInstance放在了OnDestroy中，就还是意味着我可以先调用Destroy，然后之后再执行Addressables.ReleaseInstance啊


你抓到了一个真实矛盾，我上一轮回答里把话说绝对了。这次把边界讲清楚。

---

## 真实规则

问题不是 "Destroy 能不能先于 ReleaseInstance"，而是 **ReleaseInstance 在什么时机调用才能成功**。

关键事实：**Unity 的 `Destroy` 不是立刻销毁**。

```
Destroy(instance)  调用时刻
       │
       ▼
  Unity 把 instance 标记为"待销毁"
       │
       ▼
  本帧结束前的某个时刻：
       │
       ├─ 触发 OnDestroy（此时 instance 引用【仍然可用】）
       ├─ 真正释放原生对象
       └─ instance 变成"fake null"
       │
       ▼
  下一帧开始，instance 彻底无效
```

**`OnDestroy` 运行在销毁流程的"中间"**，那一刻 GameObject 还没变 fake null，内部字典查找还能命中。所以方案 B 能工作。

---

## 三种调用顺序的结果对比

假设 `instance = await Addressables.InstantiateAsync("Player")`，refCount 已 +1。

### 情况 ①：`ReleaseInstance(instance)` 直接调（推荐）

```csharp
Addressables.ReleaseInstance(instance);
```

内部顺序：查字典 → refCount -1 → Destroy(instance)。
**refCount 正确减到 0**，bundle 可卸载。✓

### 情况 ②：`Destroy(instance)` + OnDestroy 里 `ReleaseInstance(gameObject)`

```csharp
Destroy(instance);
// 本帧末，Unity 调用 OnDestroy：
//   Addressables.ReleaseInstance(gameObject);  ← 此时 gameObject 还能被字典查到
```

**refCount 正确减到 0**。✓

这就是方案 B 能工作的原因。

### 情况 ③：`Destroy(instance)` 然后手动延后调 `ReleaseInstance(instance)`

```csharp
Destroy(instance);
await UniTask.Yield();              // 等到下一帧
Addressables.ReleaseInstance(instance);  // instance 已是 fake null
```

ReleaseInstance 传入 fake null，字典里找不到对应记录，**返回 false，refCount 不减**。✗ **泄漏**。

---

## 所以上一轮回答该怎么修

**错误表述（我的原话）**："先 Destroy 再 ReleaseInstance，绝对不可以。"

**正确表述**：
> ReleaseInstance 必须在 GameObject 还没变成 fake null 之前调用。
> - 直接 `ReleaseInstance(instance)` 替代 Destroy —— 最干净。
> - 挂 `AutoReleaseOnDestroy` 组件，让 `Destroy` 触发的 OnDestroy 里调 ReleaseInstance —— 也可以，因为 OnDestroy 那一刻引用还有效。
> - 但**手动** `Destroy` 之后**跨帧再调** ReleaseInstance —— 会失败，泄漏。

所以 Q3 的准确答案是：**"可以，但只有'Destroy 触发 OnDestroy 同步调 ReleaseInstance'这一条路径能工作"**。

---

## 为什么要推荐"挂组件"方案而不是"直接 ReleaseInstance"

你可能会问：既然直接 ReleaseInstance 最干净，为什么还要方案 B？

**现实原因**：项目里销毁 GameObject 的代码**到处都是**——对象池回收、场景卸载、父节点销毁连锁、外部逻辑 `Destroy(target.gameObject)`……你不可能要求所有人都记得"这个对象是 Addressable 创建的，必须用 ReleaseInstance"。

挂一个 `AutoReleaseOnDestroy` 组件之后，**无论谁用什么方式销毁它**，都能走到 OnDestroy → ReleaseInstance 的正确路径。**兜底保险**。

Addressables 官方在 `InstantiateAsync` 的默认行为里其实已经内置类似机制（挂 `AddressableAssetsInstantiation` 追踪组件），所以很多时候即使你 `Destroy` 也不会泄漏——但**不要依赖这个默认行为**，显式挂一个 AutoRelease 或直接用 ReleaseInstance 最稳。

---

## 一句话收束

**`Destroy` 和 `ReleaseInstance` 不是互斥操作，它们解决不同问题**：
- `Destroy` 销毁 GameObject
- `ReleaseInstance` 减 refCount + 销毁 GameObject

两者**必须在同一个销毁流程里配对**，跨帧就断链。"先 Destroy 后 ReleaseInstance" 只有在 **OnDestroy 同步回调**这种场景下才合法。

---

感谢你挑出来，这个细节讲不清的话后面做对象池就会踩坑。继续 Ch.4？

---

## 📌 用户：切换到下一章


# Ch.4 Groups 窗口全解

本章要动手了。打开 Unity，跟着我一起看 Groups 窗口。

---

## 4.1 打开 Groups 窗口

菜单：`Window → Asset Management → Addressables → Groups`

快捷方式：之前图里 `AddressableAssetSettings.asset` Inspector 顶部那个大按钮 `Manage Groups`。

第一次打开你会看到类似这样的结构：

```
┌─ Addressables Groups ─────────────────────────────────┐
│ [Play Mode Script ▾] [New ▾] [Build ▾] [Tools ▾]     │
├───────────────────────────────────────────────────────┤
│ Group Name            │ Labels     │ Notes            │
├───────────────────────────────────────────────────────┤
│ ▼ Built In Data                                       │
│ ▼ Default Local Group (Default)                       │
│     └─ xxx.prefab      Default Local                  │
│ ▼ remoteGroup                                         │
│     └─ yyy.prefab      Remote                         │
└───────────────────────────────────────────────────────┘
```

顶部工具栏四个下拉：
- **Play Mode Script**：Editor 里测试时的加载模式（后面单独讲）
- **New**：新建 Group 或 Folder
- **Build**：`New Build` / `Update a Previous Build` / `Clean Build`
- **Tools**：杂项（Inspect System Settings、Check for Content Update Restrictions…）

---

## 4.2 Group 的"默认"标记

你会看到某个 Group 名字旁边有 `(Default)` 字样。

**含义**：当你在 Project 窗口勾选任意资源的 `Addressable` 复选框时，它会**自动进入 Default Group**。

右键任意 Group → `Set as Default` 可以切换默认 Group。

---

## 4.3 给资源打地址的两条路径

### 路径 A：Project 窗口勾选

选中资源 → Inspector 顶部有 `Addressable` 复选框 → 勾上。
- 自动用资源路径作为 Address
- 自动进入 Default Group

### 路径 B：拖进 Groups 窗口

直接把资源从 Project 窗口拖到 Groups 窗口里某个 Group 下。

**我推荐路径 B**——你想放哪组就拖哪组，不用后续再改。

---

## 4.4 Entry（条目）的四个字段

点开 Group，每个资源（Entry）有四个可编辑字段：

| 字段 | 含义 | 改法 |
|------|------|------|
| **Address** | 唯一地址 | 双击编辑，或 Inspector 改 |
| **Labels** | 标签列表 | 点 Labels 列下拉框勾选/新建 |
| **Notes** | 备注，仅 Editor 可见 | 双击编辑 |
| **(资源引用)** | 实际资源，不可改，只能移动到别的 Group | 拖拽 |

**批量改 Address 的技巧**：Tools 菜单 → `Simplify Addressable Names`，一键把所有 `Assets/Xxx/Yyy.prefab` 形式的 Address 简化成 `Yyy`。

---

## 4.5 Group 的 Schema

点击 Group 名字（而不是 Entry），右边 Inspector 会显示这个 Group 挂的 **Schema** 列表。

默认两个 Schema：

1. **Content Packing & Loading**（`BundledAssetGroupSchema`）—— 打包与加载配置
2. **Content Update Restriction**（`ContentUpdateGroupSchema`）—— 更新策略

下面是本章主菜：**逐字段拆解**这两个 Schema。

---

## 4.6 BundledAssetGroupSchema 逐字段拆解

打开任意 Group 的 Inspector，你会看到一长串字段。按逻辑分组讲。

### 4.6.1 路径：Build Path / Load Path

```
Build Path:  Local.BuildPath  ▾   Path Preview: Library/com.unity.addressables/aa/Android
Load Path:   Local.LoadPath   ▾   Path Preview: {UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android
```

**含义**：
- **Build Path**：打包时 bundle 输出到哪儿
- **Load Path**：运行时从哪儿加载这些 bundle

**Path Preview** 展示 Profile 变量解析后的真实路径。

**两个路径为什么分开**？
- Build 到 `[Addressables.BuildPath]/[BuildTarget]`（默认 = `Library/com.unity.addressables/aa/[Platform]/[BuildTarget]`）→ 构建产物，不提交 git
- Load 从 `{Addressables.RuntimePath}/[BuildTarget]`（默认 = `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]`）→ 运行时解析；本地 Group 的 Build Path 在 Player Build 时会被 Addressables 自动拷进 StreamingAssets（真机上 `RuntimePath` 指向 StreamingAssets 下的 aa 目录，Editor 模式下指向 Library）

**典型配置**：
- **本地 Group**：`Local.BuildPath` + `Local.LoadPath`（资源打进 APK/IPA 随包发）
- **远程 Group**：`Remote.BuildPath`（= `ServerData/Android`）+ `Remote.LoadPath`（= CDN URL）

如果项目中已经出现 `ServerData/` 目录和 `remotegroup` 等构建产物，就说明当前配置里已有 Group 走 Remote 路径。

**改这两个字段怎么改**：
- 下拉选择预设值（`Local`、`Remote`、`<custom>`）
- 选 `<custom>` 可以直接写 Profile 变量字符串
- 本质上都是 **Profile 变量引用**，不直接写绝对路径

### 4.6.2 Advanced Options（Inspector 里展开）

#### Compression（压缩方式）

| 选项 | 特点 | 使用场景 |
|------|------|----------|
| **Uncompressed** | 最快加载、最大包体 | 调试或极致加载速度 |
| **LZ4** | 中等压缩比、随机访问快 | **推荐默认**。运行时按需解压，对应 `BuildCompression.LZ4` |
| **LZMA** | 最高压缩比、整包解压慢 | 远程 bundle、下载后缓存到本地用 LZ4 重压 |

**实践**：本地 Group 用 `LZ4`，远程 Group 用 `LZMA`（传输省流量，Unity 下载后本地缓存自动会重压成 LZ4）。

#### Include in Build

勾选控制 **这个 Group 是否参与构建**。
- 勾上：正常打包
- 不勾：跳过（常用于 Demo Group 或临时调试 Group）

#### Force Unique Provider

少数情况用。默认关闭即可。

#### Use Asset Bundle Cache

**只对远程 bundle 生效**。勾上后，下载的 bundle 会缓存到本地（`Application.persistentDataPath/com.unity.addressables/`），下次启动先查缓存命中就不重新下载。

**默认勾上，永远别关**。

#### Asset Bundle CRC

bundle 完整性校验。三个选项：

| 选项 | 含义 |
|------|------|
| **Disabled** | 不校验 |
| **Enabled, Including Cached** | 每次加载都校验，包括缓存 |
| **Enabled, Excluding Cached** | 只校验远程下载时，本地缓存不再校验（**推荐**） |

远程资源建议开，本地资源可以关。

#### Use UnityWebRequest for Local Asset Bundles

**iOS/Android 移动端强烈建议勾上**。不勾会用 `AssetBundle.LoadFromFile`，在某些机型上对 StreamingAssets 里的 bundle 有路径问题。

#### Request Timeout / Retry Count / Http Redirect Limit

远程下载控制参数：

| 字段 | 含义 | 推荐值 |
|------|------|--------|
| **Request Timeout** | 单次请求超时（秒） | 30 |
| **Retry Count** | 失败重试次数 | 3 |
| **Http Redirect Limit** | HTTP 重定向上限 | -1（不限） |

远程资源都要配好，本地资源无所谓。

### 4.6.3 Content Packing & Loading 核心：Bundle Mode

**这是 Schema 里最重要的一个字段**，决定这个 Group 怎么拆 bundle。

```
Bundle Mode:  [Pack Together ▾]
              [Pack Separately]
              [Pack Together By Label]
```

#### Pack Together（默认）

Group 里**所有**资源打成**一个** bundle。

```
Group: UI
 ├─ LoginPanel.prefab
 ├─ MainPanel.prefab
 └─ SettingsPanel.prefab
                ↓
一个 bundle: ui_assets.bundle
```

**适合**：一起加载/释放的资源。任何一个被加载都会把整个 bundle 加载到内存。

**坑**：如果 Group 里有 200 个 prefab，你只想用其中 1 个，整个 bundle 都得进内存。

#### Pack Separately

Group 里**每个资源**打成**独立** bundle。

```
Group: UI
 ├─ LoginPanel.prefab    →  ui_loginpanel.bundle
 ├─ MainPanel.prefab     →  ui_mainpanel.bundle
 └─ SettingsPanel.prefab →  ui_settingspanel.bundle
```

**适合**：各自独立加载、生命周期不同的资源。

**坑**：bundle 数量爆炸，每个 bundle 有固定元数据开销（约 1KB），资源少的情况下包体反而变大。

#### Pack Together By Label

按 **Label 分组**打包。同一 Label 的资源打进同一 bundle。

```
Group: UI  (Labels: Login, Main, Setting)
 ├─ LoginPanel.prefab        Labels=[Login]
 ├─ LoginBg.png              Labels=[Login]
 ├─ MainPanel.prefab         Labels=[Main]
 └─ SettingsPanel.prefab     Labels=[Setting]
                ↓
 ui_login.bundle (LoginPanel + LoginBg)
 ui_main.bundle  (MainPanel)
 ui_setting.bundle (SettingsPanel)
```

**适合**：Group 内有多个使用场景的资源集合。

#### 三种模式选型决策树

```
Group 里所有资源总是一起加载吗？
├─ 是  → Pack Together
└─ 否  ↓
       Group 里资源可以按场景/功能分组吗？
       ├─ 可以 → 打标签 → Pack Together By Label
       └─ 不行 → Pack Separately
```

**实践经验**：

| 场景 | 推荐 Bundle Mode |
|------|-----------------|
| 常驻 UI（主菜单、HUD） | Pack Together |
| 关卡/Scene 资源 | 每个关卡一个 Group + Pack Together |
| 角色/怪物 prefab | Pack Together By Label（按阵营/职业打标） |
| 大型场景贴图 | Pack Separately（每张独立卸载） |
| 语音、字幕（体积大、用一次） | Pack Separately |

### 4.6.4 Bundle Naming Mode

bundle 文件名格式。2.9.1 官方四个枚举值（`BundledAssetGroupSchema.BundleNamingStyle`）：

| 选项（枚举名） | 生成名字示例 | 说明 |
|---------------|--------------|------|
| **Append Hash**（`AppendHash`） | `mygroup_assets_all_39fd4a45c91a5be7.bundle` | 默认值；文件名后追加 bundle 内容 hash |
| **No Hash**（`NoHash`） | `mygroup_assets_all.bundle` | 不带 hash（需搭配 CRC 校验或 Versioned cache 使用） |
| **Only Hash**（`OnlyHash`） | `39fd4a45c91a5be7.bundle` | 仅 hash 作为文件名 |
| **File Name Hash**（`FileNameHash`） | `a1b2c3d4e5f6.bundle` | 使用源文件名 hash（非内容 hash） |

**选型**：
- **本地 Group** → `No Hash`（便于调试，文件名可读）
- **远程 Group** → `Append Hash`（hash 变了就是新 bundle，CDN 不用删旧版，支持多版本并存）

### 4.6.5 Internal Asset Naming Mode

bundle **内部**资源的命名格式。影响 Catalog 大小。

| 选项 | 说明 |
|------|------|
| **Full Path** | 用完整资源路径（Catalog 大） |
| **Filename** | 用文件名（可能冲突） |
| **GUID** | 用 meta 里的 GUID（Catalog 小，resilient） |
| **Dynamic** | Addressable 自己选最短的 |

**推荐**：`Dynamic` 或 `GUID`。你图 2 里目前是 `Full Path`，移动端建议改。

### 4.6.6 Internal Bundle Id Mode

bundle 的**内部 ID**（不是文件名，是 Catalog 里引用 bundle 的标识）。

- **Group Guid**：用 Group 的 GUID
- **Group Guid Project Id Hash**（你图里当前选的）：GUID + Project ID hash
- **Scene Guid**：Scene 专用
- **File Name Hash**：文件名 hash

**推荐**：保持默认 `Group Guid Project Id Hash` 即可，防止跨项目 Catalog 冲突。

### 4.6.7 Asset Load Mode

图 2 里看到的选项，两个：

| 选项 | 行为 |
|------|------|
| **Requested Asset And Dependencies**（默认） | 只加载你请求的资源及其依赖 |
| **All Packed Assets And Dependencies** | 一访问任何资源，bundle 里**所有**资源全加载 |

**一般保持默认**。`All Packed` 适合 bundle 很小、都会用到的场景（省去多次 Load 开销）。

---

## 4.7 ContentUpdateGroupSchema 拆解

这个 Schema 就一个字段：**Update Restriction**。

| 选项 | 含义 |
|------|------|
| **Static Content** | 这个 Group 的资源**发版后不再变**。Update Build 时跳过 |
| **Can Change Post Release** | 这个 Group 的资源**可能变更**，Update Build 会检查并生成差分 bundle |

**选型非常关键**（Ch.8 会详讲，这里先建立概念）：

- 本地 Group（打进包）：**Static Content**。这些资源不会更新，标为 Static 让 Catalog 更紧凑
- 远程 Group（走 CDN）：**Can Change Post Release**。可能热更的资源必须这么标

**错误配置的代价**：
- 远程资源标 Static → 发布后无法热更
- 本地资源标 Can Change → Catalog 每次都检查，浪费构建时间

---

## 4.8 Labels 管理

Groups 窗口右上角 Tools 菜单 → `Window → Asset Management → Addressables → Groups`，工具栏 Tools → `Labels` 打开 Label 管理器。

也可以点任意 Entry 的 Labels 列下拉 → 最底部 `Manage Labels`。

**操作**：
- 新建 Label
- 重命名 Label（**慎用**，所有引用自动更新，但如果代码里硬编码了字符串要手动改）
- 删除 Label

**命名规范**：
- 首字母大写：`UI`、`Preload`、`Character`
- 避免特殊字符
- 避免和 Address 重名

---

## 4.9 Play Mode Script（Editor 测试模式）

Groups 窗口顶部 `Play Mode Script` 下拉。**这个设置只影响 Editor 里按 Play 的行为，不影响打包**。

2.9.1 官方三个选项：

| 选项 | 行为 |
|------|------|
| **Use Asset Database (fastest)** | 不打 bundle，直接读 Assets 下的源文件。**编辑时最快** |
| **Simulate Groups (advanced)** | 模拟远端 bundle 加载延迟与依赖计算，但仍然从 Asset Database 取资源。适合验证 Group 分组策略是否合理 |
| **Use Existing Build (requires built groups)** | 使用最近一次 Build 的产物。**最接近真机** |

**实践**：
- 日常开发：`Use Asset Database`（秒切秒跑）
- 分组验证：`Simulate Groups`（检查 bundle 依赖图是否合理）
- 发布前验证：`Use Existing Build`（模拟真机 bundle 加载流程）

**常见坑**：一直用 `Use Asset Database`，到真机才发现某个资源没勾 Addressable，切 `Use Existing Build` 才炸出来。定期切换测试。

---

## 4.10 Build 菜单

Groups 窗口顶部 `Build` 下拉三个选项：

### New Build → Default Build Script

**全量构建**。所有 Group 重新打包，生成新的 Catalog。

产物去向：
- 本地 Group → `Library/com.unity.addressables/aa/[Platform]/`
- 远程 Group → `ServerData/[Platform]/`

**第一次构建、大规模资源变更、结构变动后用这个**。

### Update a Previous Build

**增量构建**。基于上次 Build 的 `addressables_content_state.bin` 做差分，只重打**发生变化**且 `Can Change Post Release` 的 Group。

**前置条件**：
- `Build Remote Catalog` 必须勾上（图 2 里你当前没勾，用不了）
- 有一次 New Build 作为基线

**日常迭代热更用这个**。

### Clean Build

清除构建缓存。两种：
- `All`：清全部
- `Content Builders / Build Pipeline Cache` 等子项

资源明明改了但 bundle 没更新时用。

---

## 4.11 Nova 项目实操建议

结合你的实际情况（2.9.1，有 `remoteGroup`，有 `ServerData/` 产物），建议的初期 Group 规划：

```
Groups:
├─ Built In Data                        (Unity 内置，别动)
├─ Default Local Group (Default)        (默认本地，新资源进这里)
│     Bundle Mode: Pack Together
│     Update Restriction: Static
│     Build Path / Load Path: Local
│
├─ Preload_Local                        (启动必需，本地)
│     Labels: Preload
│     Bundle Mode: Pack Together
│     Update Restriction: Static
│     Build Path / Load Path: Local
│
├─ Remote_Config                        (ConfigRuntimeSO 等可热更配置)
│     Bundle Mode: Pack Together
│     Update Restriction: Can Change Post Release
│     Build Path / Load Path: Remote
│     Bundle Naming: Append Hash to Filename
│     Compression: LZMA
│
└─ Remote_UI                            (可热更 UI)
      Bundle Mode: Pack Together By Label
      Update Restriction: Can Change Post Release
      Build Path / Load Path: Remote
```

**先别急着建**，Ch.5 讲完 Profile 再动手——Group 的路径字段引用 Profile 变量，Profile 没对 Group 也白配。

---

## 4.12 本章自测题

1. Group 和 Bundle 的关系由哪个字段决定？有哪三种模式？
2. `Pack Together` 和 `Pack Separately` 各自适合什么场景？
3. `Update Restriction` 两个选项的区别？远程 Group 该选哪个？
4. `Compression` 的 LZ4 和 LZMA 有什么差别？
5. `Play Mode Script` 的 `Use Asset Database` 和 `Use Existing Build` 有什么区别？
6. `Build Path` 和 `Load Path` 为什么要分开？
7. `Bundle Naming Mode` 本地 Group 和远程 Group 分别推荐哪种？为什么？

---

## 4.13 动手小任务（推荐做）

在你的 Nova 项目里：

1. 打开 Groups 窗口，找到那个 `remoteGroup`，截个图或文字描述一下它当前配置
2. 点 `Default Local Group`，把 Inspector 里每个字段对照本章过一遍
3. 尝试新建一个测试 Group，名字随意，Bundle Mode 设成 `Pack Separately`，不要 Build，下一章需要

做完告诉我，或者直接描述你看到的实际字段、有没有和我讲的不一样的地方。

---

**Ch.4 结束**。Groups 的配置面讲完了，但路径字段现在还是"引用 Profile 变量"这种抽象说法。

**Ch.5 Profiles 与路径变量**：打开 Profile 窗口，讲清楚 Profile 到底是怎么工作的、`{UnityEngine.AddressableAssets.Addressables.RuntimePath}` 这种变量怎么解析、多环境切换怎么玩。讲完你就能真正理解 Build Path 和 Load Path 的含义。

继续 Ch.5？

---

## 💬 用户提问

> 4.2 Group 的"默认"标记,没有找到你说的右键选项，我的右键是：[Image #4]
> 
> “Load 从 {Addressables.RuntimePath}/Android → 运行时自动解析成 StreamingAssets 下的路径”  
> 意思是说需要我在运行前手动将原拷贝到SreamingAssets路径下？
> 
> ContentUpdateGroupSchema中只有一个Static Content勾选项


三个问题都看到了，逐个回答。

---

## Q1：`Set as Default` 在哪？

你截图是对 **Entry（资源条目）** 右键，这是 Entry 级菜单。

`Set as Default` 是在 **Group（组名本身）** 上右键出现的菜单，跟我说的一致，但 2.x 有时候要**在 Group 名字那一行的空白区域**点一下先选中整行，再右键。

**准确操作**：
- 鼠标点 Group 标题行（比如 `Default Local Group`）**本身**，让它整行高亮
- **不要展开**，右键就会出现 Group 级菜单

2.x 里这个菜单大概长这样：
```
Remove Group(s)
Set as Default
Rename
Create New Group  →
Clear Content Update Warnings
```

**如果你确实只看到 Entry 菜单**，有两种可能：

1. 你点到的是子 Entry 行（展开后的子节点）。**收起 Group**，只点组名那一行
2. 某些 2.x 版本把 `Set as Default` 移到了 **Group Inspector 顶部**——选中 Group 后看右边 Inspector，顶部可能有个 `Set as Default` 按钮

**其实不重要**：哪个 Group 是 Default 只影响"Project 窗口勾 Addressable 复选框时进哪个 Group"。你如果始终用**拖拽**方式分组，永远不碰那个复选框，Default 是谁都无所谓。**跳过这个功能**，不影响后续所有内容。

---

## Q2：Load Path 和 StreamingAssets 的关系

**不需要你手动拷贝**。Addressable 全自动。

拆开讲清楚这个过程：

### 打包阶段（点 Build 按钮时）

```
你的 Group 配置：
  Build Path: Local.BuildPath = Library/com.unity.addressables/aa/Android/
  
点 Build
    │
    ▼
Addressables 做两件事：
  ① 把 bundle 打到 Library/com.unity.addressables/aa/Android/Android/xxx.bundle
  ② 自动把这些 bundle【拷贝】到 Assets/StreamingAssets/aa/Android/xxx.bundle
```

**第 ② 步是 Addressables 内部自动做的**。你会看到 Unity 在构建结束后在 `StreamingAssets/aa/` 下多出一些文件——这不是你操作的，是 Addressables 帮你拷的。

**为什么要这么绕**：
- `Library/` 不进 Build（Unity 规则），Unity Build Player 时只看 `Assets/` 下的内容
- `StreamingAssets/` 里的东西会**原样打包**到 APK/IPA
- 所以 Addressables 的"本地 bundle"要先在 `Library/` 临时区打好，再搬到 `StreamingAssets/` 才能被 Build Player 捎上

### 运行时（真机或 Editor Play）

```
代码：Addressables.LoadAssetAsync<GameObject>("Player")
    │
    ▼
查 Catalog 发现 Player 在 xxx.bundle，记录的 Load Path 是：
  {Addressables.RuntimePath}/Android/xxx.bundle
    │
    ▼
Addressables.RuntimePath 运行时自动解析为：
  - 真机：Application.streamingAssetsPath/aa
  - Editor: Library/com.unity.addressables/aa
    │
    ▼
最终加载路径：
  - 真机 Android：jar:file:///data/app/.../base.apk!/assets/aa/Android/xxx.bundle
  - Editor：        Library/com.unity.addressables/aa/Android/xxx.bundle
```

**关键点**：`{Addressables.RuntimePath}` 是一个**运行时常量变量**，由 Addressables 库内部实现，自动映射到当前平台的正确目录。

### 所以你完全不用管

流程就是：
1. 点 Build → bundle 自动进 `Library/` + 自动进 `StreamingAssets/`
2. Unity Build Player → `StreamingAssets/` 被打包进 APK
3. 真机启动 → Addressables 自动从 APK 里的 `StreamingAssets` 读 bundle

**唯一需要注意**：不要手动去 `StreamingAssets/aa/` 目录里删文件或提交 git。它是构建产物。`.gitignore` 里应该屏蔽 `Assets/StreamingAssets/aa/`（我猜你项目里已经配了）。

你 `StreamingAssets/` 下目前没有 `aa/` 目录，说明你**还没做过一次完整的 Addressables Build**（或者构建产物被清理过）。这是正常的，下一章做 Build 的时候会自动出现。

---

## Q3：ContentUpdateGroupSchema 只有一个 `Static Content` 勾选框

完全正常，不是少了什么。我 Ch.4 写得不够准确——`Update Restriction` 在 2.x 里**就是一个布尔勾选**，不是下拉框。语义是：

```
☐ Static Content   (未勾)  = Can Change Post Release
☑ Static Content   (勾上)  = Static Content
```

两个"选项"其实就是这个勾选框的两种状态。

**对应关系**：

| 勾选框状态 | 含义 | 使用场景 |
|-----------|------|---------|
| **未勾** | 资源可能变更，Update Build 会检查并生成差分 | 远程 Group、可热更资源 |
| **勾上** | 资源定版不变，Update Build 跳过这个 Group | 本地 Group、绝不热更的资源 |

**所以之前说的两个选项 `Static Content` / `Can Change Post Release`，在 UI 上就是勾或不勾这一个框**。

---

## 三个问题小结

| 问题 | 结论 |
|------|------|
| Q1 Set as Default | 右键要点 Group **标题行**不是 Entry；或在 Group Inspector 里找按钮；**实际上不重要**，忽略也行 |
| Q2 StreamingAssets 拷贝 | **全自动**，不用手动操作。构建时 Addressables 会自动把本地 bundle 搬到 StreamingAssets |
| Q3 Static Content 只有勾选框 | **正常**，勾=Static / 不勾=Can Change，就是一个布尔 |

---

顺带——我看到你 `.gitignore` 根目录那里没搜到 Addressables 相关的忽略规则，**这是个隐患**。真做第一次 Build 后 `StreamingAssets/aa/` 会冒出一堆 bundle，如果不忽略会污染 git。但这个问题建议**等真的 Build 一次之后再处理**，现在先放着。

Ch.4 的问题都清了，继续 Ch.5 吗？

---

## 💬 用户提问

> Q2不对，我发现编译完成后，StreamingAssets依旧没有aa资源


你抓得对，我上一次回答确实错了。刚核对了你项目实际状态和 Schema 配置，把准确机制讲清楚。

---

## 实际发生的事

你项目里已经 Build 过了，证据：

```
Library/com.unity.addressables/aa/Android/Android/
  ├── defaultlocalgroup_assets_all_xxx.bundle    ← 本地 Group 的 bundle 确实生成了
  └── f731eb4da4472192f5a1fa0f40ad7c9f_monoscripts_xxx.bundle
```

而 `Assets/StreamingAssets/` 下**没有 `aa/` 目录**。

这是 **Addressables 2.x 正确且期望的行为**，不是 bug，也不是你漏了什么步骤。

---

## 正确的机制（修正我之前的错误）

Addressable 的构建实际有**两个独立阶段**，我之前把它们混成一回事了：

### 阶段 A：Addressables Content Build（你点 Groups 窗口里的 Build）

```
Build Path = Local.BuildPath
           = [UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]
           = Library/com.unity.addressables/aa/Android
```

点 Build → bundle 生成到 **`Library/com.unity.addressables/aa/Android/Android/`** 下。

**到此为止，StreamingAssets 不会被碰**。这就是你看到的状态。

### 阶段 B：Player Build（File → Build Settings → Build，打 APK）

这时才会触发 Addressables 的 **BuildPlayerProcessor** 钩子，它做的事：

```
1. 构建开始前：把 Library/com.unity.addressables/aa/ 下的 bundle 【临时】复制到 
   Assets/StreamingAssets/aa/
2. Unity 把 StreamingAssets 连同其他资源打进 APK
3. 构建结束后：【删除】StreamingAssets/aa/ 临时目录
```

**关键**：这个 StreamingAssets 下的 `aa/` 目录**只在 Player Build 期间瞬时存在**，构建前没有、构建后也没有。你几乎不会在 Project 窗口看到它。

### 为什么这么设计

保持 `Assets/` 干净：构建产物不污染工程目录、不进 git、不被 Unity 反复重新 import。

Library 下的文件在 Build Player 时被"借过去用一下再还回去"，效率高又不留痕。

---

## 验证方式

想亲眼看到 bundle 进 APK，两种办法：

### 办法 1：真·打包一次 Player

`File → Build Settings → 选 Android → Build`（不用安装，导出 APK 就行）。
导出的 APK 用解压工具（APK 本质是 zip）打开，里面 `assets/aa/Android/` 就是那些 bundle。

### 办法 2：看构建日志

Unity Console 里 `Build Player` 过程中会打印：

```
[Addressables] Adding bundle 'defaultlocalgroup_assets_all_xxx.bundle' to StreamingAssets
```

搜这个关键字能看到拷贝动作。

### 办法 3：开关"同步构建"

你的 `AddressableAssetSettings.asset` 里有一个字段：

```yaml
m_BuildAddressablesWithPlayerBuild: 0
```

值是 **0（Disabled）**，意思是：点 Unity 的 Build Player 时**不会**自动先做 Addressables Build，它只会用"上次 Addressables Build 的产物"。

改成 1（Build Addressables content on Player Build）后，每次 Build Player 会自动先跑 Addressables Build 再打 APK，避免忘记更新的情况。

Inspector 里改法：`AddressableAssetSettings.asset` → `General` 折叠组（你截图里可能还没展开这组）→ `Build Addressables on Player Build` → 选 `Build Addressables content on Player Build`。

---

## Editor 里 Play 时怎么找 bundle

既然 StreamingAssets 里没有 bundle，你可能会想：Editor 按 Play 时能正常加载吗？能。

Addressables 运行时的路径解析分平台：

```
{UnityEngine.AddressableAssets.Addressables.RuntimePath} 解析为：
├─ Editor 下：      Library/com.unity.addressables/aa            ← 你当前的 bundle 就在这
├─ Standalone：     [DataPath]/StreamingAssets/aa
├─ Android/iOS：    [StreamingAssetsPath]/aa
└─ WebGL/其他：     各自不同
```

**Editor 直接读 Library**，所以你现在 Editor 按 Play 用 `Use Existing Build` 模式就能跑通，不需要 StreamingAssets 里有东西。

---

## 修正后的心智模型

```
                 ┌─────────────────────────┐
Addressables ──► │ Library/.../aa/Android  │  ◄── Editor 直接读
Build            └─────────────────────────┘
                         │
                         │  Player Build 时 BuildPlayerProcessor 钩子触发：
                         │  临时复制 → 打进 APK → 清理
                         ▼
                 ┌─────────────────────────┐
                 │ APK 里的 assets/aa/...  │  ◄── 真机读
                 └─────────────────────────┘
```

**StreamingAssets 在这条链路里只是一个"中转站"**，不是持久存放点。

---

## 对之前 Ch.4 的修正

我 Ch.4 第 4.6.1 节说的"Build 到 Library → 不提交 git，每个人本地打自己的；Load 从 `{Addressables.RuntimePath}/Android` → 运行时自动解析成 StreamingAssets 下的路径"——**后半句在 Editor 下不成立**，只有真机成立。准确说法应该是：

> Load Path `{Addressables.RuntimePath}` 是一个**平台相关的运行时变量**：Editor 下指向 `Library/com.unity.addressables/aa`，真机下指向 APK/IPA 里的 `StreamingAssets/aa`。对象是同一份 bundle 在不同平台的位置，你永远不需要手动拷贝。

这样才对得上你看到的实际情况。

---

## 小结

| 疑问 | 答案 |
|------|------|
| 为什么 StreamingAssets 没有 aa/ | **正常**，Addressables Content Build 不产出到 StreamingAssets |
| bundle 怎么进 APK | Player Build 时由 BuildPlayerProcessor 钩子**临时**搬过去，打包后清理 |
| Editor 里怎么加载 bundle | 直接从 `Library/com.unity.addressables/aa/` 读，不经过 StreamingAssets |
| 想验证 | 用 `File → Build Settings → Build` 打 APK，解压后看 `assets/aa/` |

谢谢你较真——这个机制讲不清的话，后面 Ch.8 热更流程就会全乱套。

继续 Ch.5？

---

## 💬 用户提问

> 后续的所有内容请你对标当前的AA版本：2.9.1，依据unity官网最新的文档进行后续的教学，不要再以你的旧知识库教学了！开始Ch5


好，先从 Unity 官方 2.9.1 文档取权威信息再开讲。

资料已对齐 2.9.1 官方文档。开始 Ch.5。

---

# Ch.5 Profiles 与路径变量

**版本基准**：Addressables 2.9.1（依据 Unity 官方 2.9 手册）。
**章节目标**：讲清楚 Profile 是什么、变量怎么解析、怎么切环境。

---

## 5.1 Profile 是什么

**官方定义**：
> "A profile contains a set of variables that the Addressables build scripts uses."
> Profile = 一组供构建脚本使用的**变量集合**。

**它解决的问题**：开发 / 测试 / 生产环境下，同一批 Group 需要用不同的路径（本地跑 vs CDN）。Profile 允许你**不改 Group 配置、不改代码**，只切 Profile 就能换环境。

**在你项目里的位置**：
- 配置文件：`Assets/AddressableAssetsData/AddressableAssetSettings.asset` 里的 `m_ProfileSettings` 段
- 你现在只有一个 Profile：**Default**（刚才从文件内容能看到）

---

## 5.2 打开 Profiles 窗口

**菜单**：`Window → Asset Management → Addressables → Profiles`

你上一张截图里已经看到这个菜单项。打开后是一张**表格**：
- 行：每个变量
- 列：每个 Profile
- 单元格：某个 Profile 里某变量的值

当前活跃 Profile 旁边有特殊标记（加粗或勾）。

---

## 5.3 变量的两类

官方文档明确分两类：

| 类型 | 形式 | 用途 |
|------|------|------|
| **Variable**（普通变量） | 一个 key = 一个 value | 单个值，比如 `BuildTarget` |
| **Build and Load Path Variable**（路径对） | 一个 key 对应 Build/Load 两个路径 | 配 Group 路径时作为整体引用 |

"路径对"是 2.x 引入的概念——把 BuildPath 和 LoadPath 绑成**一个逻辑单元**（比如 `Local`、`Remote`），在 Group Schema 里选 `Local` 就同时设定了 BuildPath 和 LoadPath 两个路径。

---

## 5.4 你项目里的默认 Profile 实况

从你 `AddressableAssetSettings.asset` 里提取的实际配置：

| 变量名 | 实际值 |
|--------|--------|
| `BuildTarget` | `[UnityEditor.EditorUserBuildSettings.activeBuildTarget]` |
| `Local.BuildPath` | `[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]` |
| `Local.LoadPath` | `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` |
| `Remote.BuildPath` | `ServerData/[BuildTarget]` |
| `Remote.LoadPath` | `http://localhost/[BuildTarget]` |

**这组是 Addressables 2.x 的出厂默认值**，官方文档里原样列出。

注意 `Local` 和 `Remote` 是两个**路径对**，每对有 BuildPath/LoadPath 两个具体变量。

---

## 5.5 `[]` vs `{}` —— 2.x 最重要的语法差异

**这是 2.x 最容易踩坑的地方，也是我之前没讲到的**。

官方明确定义：

| 语法 | 解析时机 | 含义 |
|------|---------|------|
| `[xxx]` 方括号 | **Build Time**（打包时） | 值会在构建时被**求值后写进 Catalog** |
| `{xxx}` 花括号 | **Runtime**（运行时） | 值在**程序运行时**才解析，Catalog 里保留变量字面量 |

### 为什么这么设计

对照 5.4 表里 Local 的两个路径：

**Local.BuildPath = `[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]`**
- 全部用 `[]`
- 打包时就确定为 `Library/com.unity.addressables/aa/Android`
- **这个路径只有打包机器需要知道**，Catalog 里记下结果即可

**Local.LoadPath = `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]`**
- `RuntimePath` 用 `{}`，`BuildTarget` 用 `[]`
- `[BuildTarget]` 打包时就固定为 `Android`（这台打包机的目标平台）
- `{RuntimePath}` **必须运行时解析**——因为：
  - Editor 下要解析成 `Library/com.unity.addressables/aa`
  - 真机下要解析成 `<StreamingAssets>/aa`
  - 同一个 Catalog **在不同平台/环境要给出不同答案**

**核心规则**：
> **凡是"打包机和运行机必然是同一台"的东西，用 `[]`；凡是"Catalog 做好之后要被不同设备加载"的东西，用 `{}`。**

### 具体实例

```
[BuildTarget]                  → 打包时决定的平台，Android/iOS，用 []
[UnityEditor.PlayerSettings.bundleVersion] → 打包时的版本号，用 []
{UnityEngine.Application.persistentDataPath}  → 每台设备不同，用 {}
{UnityEngine.AddressableAssets.Addressables.RuntimePath} → 每平台不同，用 {}
```

### 你可能会遇到的坑

写 Remote.LoadPath 时犯的典型错误：

```
# ❌ 错误：写成 []
Remote.LoadPath = [MyGame.CDNUrl]/[BuildTarget]
# 后果：打包时就把 CDN 地址烧死在 Catalog 里，发布后想改 CDN 必须重发全量包

# ✅ 正确：CDN 基础部分用 {}，BuildTarget 用 []
Remote.LoadPath = {MyGame.CDNUrl}/[BuildTarget]
# 后果：Catalog 里写的是 {MyGame.CDNUrl}/Android，运行时读 MyGame.CDNUrl 静态字段
```

**判断模板**：

| 字段内容 | 用什么 |
|---------|--------|
| 打包平台、版本号、打包输出目录 | `[]` |
| CDN 地址、设备本地存储、运行时可配置的 URL | `{}` |
| 运行时可能切换的值 | `{}` |

---

## 5.6 Profile 变量能引用 C# 静态成员

你看到的 `[UnityEditor.EditorUserBuildSettings.activeBuildTarget]` 不是 Addressables 的黑魔法，它就是：

```
[类全名.静态属性名]  或  {类全名.静态属性名}
```

Addressables 在解析时用反射读取这个静态成员的值。

**自定义 Profile 变量引用你自己的静态字段**：

```csharp
// Runtime 代码
public static class MyGame
{
    public static string CDNUrl => "https://cdn.your-game.com/prod";
    public static string Branch => "main";
}
```

然后 Profile 里可以写：

```
Remote.LoadPath = {MyGame.CDNUrl}/[MyGame.Branch]/[BuildTarget]
```

- `{MyGame.CDNUrl}` 运行时解析 → 可以根据账号/地区动态返回不同 CDN
- `[MyGame.Branch]` 打包时解析 → 这次打包属于哪个分支

**限制**：必须是**公共静态属性或字段**，Addressables 反射能读到。

---

## 5.7 在 Group Schema 里引用 Profile

打开任意 Group 的 Inspector，找到 `Build & Load Paths` 字段（或展开 `Advanced Options` 看 BuildPath / LoadPath）。

**2.x 的新 UI**：不再直接填路径字符串，而是下拉选**路径对**：

```
Build & Load Paths:  [ Local     ▾ ]    ← 选路径对
                     [ Remote    ]
                     [ <custom>  ]

Path Preview:
  Build Path: Library/com.unity.addressables/aa/Android
  Load Path:  {UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android
```

**三个选项**：
- **Local**：用 `Local.BuildPath` + `Local.LoadPath`
- **Remote**：用 `Remote.BuildPath` + `Remote.LoadPath`
- **`<custom>`**：独立填两个路径（可以用 Profile 变量语法），当前 Group 专用

**最佳实践**：
- 绝大多数情况用 Local/Remote 预设
- 需要特殊路径的个别 Group 才用 `<custom>`，避免 Profile 层面的变量被污染

---

## 5.8 多 Profile 的典型用法

**场景**：你的游戏有 Dev/Staging/Production 三套 CDN。

### 建三个 Profile

1. 打开 Profiles 窗口
2. `Create → Profile`，输入新 Profile 名（比如 `Staging`）
3. 官方说明："A new profile row appears in the table with values copied from the currently selected profile"——**新 Profile 继承当前 Profile 的所有值**
4. 改 `Remote.LoadPath` 为对应环境的 URL

最终三行并列：

| 变量 | Default (Dev) | Staging | Production |
|------|---------------|---------|------------|
| Local.BuildPath | `[...BuildPath]/[BuildTarget]` | 同左 | 同左 |
| Local.LoadPath | `{...RuntimePath}/[BuildTarget]` | 同左 | 同左 |
| Remote.BuildPath | `ServerData/[BuildTarget]` | 同左 | 同左 |
| Remote.LoadPath | `http://localhost/[BuildTarget]` | `https://staging.cdn.com/[BuildTarget]` | `https://cdn.your-game.com/[BuildTarget]` |

注意三个 Profile 的 **Local 路径完全一致**——本地资源不会因为环境变。差异只在 Remote.LoadPath。

### 切换活跃 Profile

官方说明有两种方式：

**方式 1（Groups 窗口）**：
- 打开 `Window → Asset Management → Addressables → Groups`
- 工具栏的 **Profile** 下拉 → 选目标 Profile

**方式 2（Profiles 窗口）**：
- 在 Profiles 窗口里右键任意 Profile 行
- 选 `Set Active`

切换后**立刻生效**，下一次 Build 就用新 Profile 的变量。

### 注意

切换 Profile 后：
- 只改了 Load Path → **不用重打包**，Catalog 里的 `{}` 变量下次 Play 时按新环境解析
- 改了 Build Path 或者任何 `[]` 变量的值 → **必须重打包**，因为 `[]` 的值已经烧进 Catalog

---

## 5.9 Profile 变量的完整解析链

把整个过程串起来，建立完整心智模型：

```
Group Schema                             Profile
 │                                        │
 │ Build Path: Local.BuildPath  ─────────►│ Local.BuildPath
 │ Load Path:  Local.LoadPath   ─────────►│ = [Addressables.BuildPath]/[BuildTarget]
 │                                        │
 │                                   打包时解析（[]）
 │                                        │
 │                                        ▼
 │                               Library/.../aa/Android
 │
 │ Load Path 的 {RuntimePath} 部分：
 │
 │                       Catalog 里保留字面量 "{RuntimePath}/Android"
 │                                        │
 │                                   运行时解析（{}）
 │                                        │
 │                                        ▼
 │                                 Editor: Library/.../aa
 │                                 真机 : <StreamingAssets>/aa
```

---

## 5.10 "Profile in Use" 概念

官方文档有个精炼例子：

> 本地开发时，本地和远程 bundle 都指向本地路径；上生产时，只改一次活跃 Profile，远程 bundle 就指向 CDN。

**"本地开发时远程指向本地"**怎么实现？在 Dev Profile 里把 `Remote.LoadPath` 写成 `http://localhost:8080/[BuildTarget]`，用 Unity 官方的 **Asset Hosting Services**（Groups 窗口 → Tools → Hosting）起一个本地 HTTP 服务指向 `ServerData/`，就能在本地模拟"远程下载"流程而不真的上 CDN。

这个进阶用法留到 Ch.9（远程热更）讲。

---

## 5.11 与 Nova 现状对标

你项目现状：

| 项目 | 实际情况 |
|------|----------|
| 有多少 Profile | 只有一个 **Default** |
| Remote.LoadPath | `http://localhost/[BuildTarget]` （出厂默认，**未配置真实 CDN**） |
| Build Remote Catalog | **未勾**（`m_BuildRemoteCatalog: 1` ← 重看文件发现是 1，已勾上） |

实际上 `m_BuildRemoteCatalog: 1` —— 你已经开启了远程 Catalog 构建。之前我看截图（图 2）以为没勾是错的，抱歉。

**下一步建议**（不急着做）：
- 如果后面真要做 CDN 热更，就建 `Production` Profile，改 `Remote.LoadPath`
- 现阶段只用 Default 跑通本地流程即可

---

## 5.12 本章自测题

1. Profile 和 Group 的关系是什么？一个改 Profile，Group 要不要重配？
2. `[]` 和 `{}` 在 Profile 变量里的核心区别是什么？
3. 为什么 `Local.LoadPath` 里 `RuntimePath` 用 `{}` 而 `BuildTarget` 用 `[]`？
4. 配 Remote.LoadPath 指向 CDN 时，CDN 地址应该用 `[]` 还是 `{}`？为什么？
5. Profile 变量可以引用任意 C# 静态成员吗？有什么限制？
6. 切换 Profile 后什么时候必须重打包，什么时候不用？
7. Group Schema 的 Build & Load Paths 字段里的 `Local` / `Remote` 下拉选项本质是什么？

---

## 5.13 动手小任务

打开你的 Profiles 窗口（`Window → Asset Management → Addressables → Profiles`），做这几件事：

1. **观察**：当前几个 Profile？活跃的是哪个？
2. **试着复制一个 Profile**：`Create → Profile`，命名 `Staging`，看看是不是自动继承 Default 的所有值
3. **改一下 Staging 的 Remote.LoadPath**：改成 `https://staging.example.com/[BuildTarget]`
4. **切回 Default**：右键 Default → Set Active
5. **不要保留 Staging**：测试完右键 Staging → 删掉（菜单里应该有 Delete）

做完描述一下你看到的 UI 和实际操作体验，我根据你反馈微调 Ch.6 的讲法。

---

**Ch.5 结束**。到这一步，**配置面**（Groups + Profiles）已经讲全，后面可以真正开始写代码了。

**Ch.6 加载 API 全景**：按官方 2.9.1 文档，把 `LoadAssetAsync`、`LoadAssetsAsync`、`InstantiateAsync`、`LoadSceneAsync`、`LoadResourceLocationsAsync` 这几个运行时 API 逐一讲清楚，包括 `AsyncOperationHandle` 的用法和 `WaitForCompletion` 的坑。

继续 Ch.6？

---

## 📌 用户：切换到下一章


资料齐了，开始 Ch.6。

---

# Ch.6 加载 API 全景

**版本基准**：Addressables 2.9.1（依据 Unity 官方 2.9 手册）。
**章节目标**：把 `LoadAssetAsync` / `LoadAssetsAsync` / `InstantiateAsync` / `LoadSceneAsync` / `LoadResourceLocationsAsync` 五个核心 API 讲清楚，以及 `AsyncOperationHandle` 的等待模式、`WaitForCompletion` 的适用边界。

---

## 6.1 全景地图

Addressables 在运行时只有一个入口：**静态类 `UnityEngine.AddressableAssets.Addressables`**。所有加载 API 都是它的静态方法，**全部返回 `AsyncOperationHandle`**。

```
Addressables
  ├── LoadAssetAsync<T>(key)                 →  AsyncOperationHandle<T>
  ├── LoadAssetsAsync<T>(keys, cb, mergeMode) →  AsyncOperationHandle<IList<T>>
  ├── InstantiateAsync(key, ...)             →  AsyncOperationHandle<GameObject>
  ├── LoadSceneAsync(key, mode, activate)    →  AsyncOperationHandle<SceneInstance>
  ├── LoadResourceLocationsAsync(key, type)  →  AsyncOperationHandle<IList<IResourceLocation>>
  ├── DownloadDependenciesAsync(key)         →  AsyncOperationHandle
  └── GetDownloadSizeAsync(key)              →  AsyncOperationHandle<long>
```

---

## 6.2 `AsyncOperationHandle`：一切操作的返回值

本节修正 Ch.3 里的描述，对齐 2.9.1 官方文档。

### 6.2.1 四种等待模式（官方明确列出）

> "AsyncOperationHandle enables you to support multiple waiting patterns: Coroutines with IEnumerator, Event delegates for callbacks, C# async/await patterns, Synchronous completion via WaitForCompletion"

**① await Task**（最常用，官方推荐）
```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
if (handle.Status == AsyncOperationStatus.Succeeded)
    Instantiate(handle.Result);
```

**② Completed 回调**
```csharp
var handle = Addressables.LoadAssetAsync<GameObject>("Player");
handle.Completed += h =>
{
    if (h.Status == AsyncOperationStatus.Succeeded)
        Instantiate(h.Result);
};
```

**③ 协程 yield**
```csharp
IEnumerator LoadRoutine()
{
    var handle = Addressables.LoadAssetAsync<GameObject>("Player");
    yield return handle;   // AsyncOperationHandle 实现了 IEnumerator
    if (handle.Status == AsyncOperationStatus.Succeeded)
        Instantiate(handle.Result);
}
```

**④ 同步阻塞（WaitForCompletion）**——见 6.8，有严格使用条件。

### 6.2.2 常用属性

| 成员 | 含义 |
|------|------|
| `Status` | `AsyncOperationStatus` 枚举：`None` / `Succeeded` / `Failed` |
| `Result` | 加载结果。成功后才可读 |
| `IsDone` | 操作是否结束（成功或失败都算） |
| `IsValid()` | handle 是否还有效（Release 后返回 false） |
| `PercentComplete` | 进度 0~1 |
| `Task` | 用于 `await` 的 `System.Threading.Tasks.Task` |
| `Completed` | 完成时的事件回调 |
| `GetDownloadStatus()` | 返回 `DownloadStatus` 结构，含已下载/总大小等信息 |
| `OperationException` | 失败时的异常对象 |

### 6.2.3 `Release` vs `ReleaseHandle`

**2.x 细节，你得知道两者差异**：

- `Addressables.Release(handle)`：最常用。减 handle 引用计数，归零卸资源
- `handle.Release()`：**扩展方法**，等同于 `Addressables.Release(handle)`

两者完全等价。写哪种都行，**组里统一一种风格**就好。

---

## 6.3 `LoadAssetAsync<T>`：加载单个资源

### 6.3.1 基本用法

```csharp
AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("Player");
await handle.Task;
GameObject prefab = handle.Result;
```

### 6.3.2 支持的 key 类型

官方列出三种：
- **string**（Address 或 Label）
- **AssetReference**（Ch.7 详讲）
- **IResourceLocation**（6.7 讲 `LoadResourceLocationsAsync` 时回来看）

### 6.3.3 传 Label 的行为

> "if the key resolves to more than one asset, only the first asset found is loaded."

传 Label 时 `LoadAssetAsync<T>` **只返回命中列表里的第一个**，**顺序不保证**。

**结论**：Label 场景必须用 `LoadAssetsAsync`（复数），单数方法永远配 Address。

### 6.3.4 加载子资源

官方写法：

```csharp
// 加载一个 Sprite Atlas 里所有子 Sprite
var all = Addressables.LoadAssetAsync<IList<Sprite>>("hero_atlas");

// 加载指定名字的子 Sprite（方括号语法）
var one = Addressables.LoadAssetAsync<Sprite>("hero_atlas[hero_idle_0]");
```

子资源名用 `address[SubassetName]` 这种方括号语法。这是 2.x 加载 Sprite Atlas 或多子对象资源的标准做法。

---

## 6.4 `LoadAssetsAsync<T>`：批量加载

### 6.4.1 两种签名

**单 key（一个 Label，批量取所有匹配资源）**：
```csharp
AsyncOperationHandle<IList<GameObject>> handle =
    Addressables.LoadAssetsAsync<GameObject>(
        "Enemy",                         // key
        obj => Debug.Log(obj.name),      // 每加载完成一个资源就回调一次
        releaseDependenciesOnFailure: true);
```

**多 key（IList，支持 MergeMode）**：
```csharp
var keys = new List<string> { "Boss", "Elite" };
AsyncOperationHandle<IList<GameObject>> handle =
    Addressables.LoadAssetsAsync<GameObject>(
        keys,
        null,
        Addressables.MergeMode.Union,    // 合并模式
        releaseDependenciesOnFailure: true);
```

### 6.4.2 MergeMode（2.9.1 官方四个值）

`Addressables.MergeMode` 枚举：

| 值 | 含义 |
|----|------|
| `UseFirst`（默认，= 0） | **只**加载匹配第一个 key 的资源 |
| `None`（= `UseFirst` 的别名，= 0） | 同 `UseFirst`。官方 API 文档列出的显式别名 |
| `Union` | 匹配**任一** key 的资源都加载（取并集） |
| `Intersection` | **同时**匹配**所有** key 的资源才加载（取交集） |

> `None` 与 `UseFirst` 是**同一个整型值**（不同名常量），日常写代码用 `UseFirst` 即可，`None` 只是官方保留的兼容别名。

**实例**：
```
资源 A：Labels=[Enemy, Fire]
资源 B：Labels=[Enemy, Ice]
资源 C：Labels=[Fire]

keys = ["Enemy", "Fire"]：
  Union        → A, B, C
  Intersection → A（同时有 Enemy 和 Fire）
  UseFirst     → A, B（只按 "Enemy" 匹配）
```

### 6.4.3 `releaseDependenciesOnFailure` 参数

| 值 | 行为 |
|----|------|
| `true` | 任一资源加载失败 → 整个操作视为失败，**全部已加载资源自动释放** |
| `false` | 尽力加载，失败的资源在结果列表里为 `null` |

**推荐 `true`**：失败时不留残骸。除非你就是要"能拿到多少就算多少"的语义。

### 6.4.4 回调时机

第二个参数 `Action<T> callback` 在**每个资源加载完成时**调用一次。用来做增量处理（例如进度条更新）。**最终结果**还是要等 handle 完成后从 `handle.Result` 拿完整 `IList<T>`。

### 6.4.5 Release 一次就够

```csharp
// ✓ 批量加载只释放一次
var handle = Addressables.LoadAssetsAsync<GameObject>("Enemy", null);
await handle.Task;
// 用 handle.Result
Addressables.Release(handle);   // 内部批量释放所有子资源
```

**不要**对 `handle.Result` 里每个元素分别 Release——它们不是独立 handle。

---

## 6.5 `InstantiateAsync`：加载并实例化

### 6.5.1 基本用法

```csharp
AsyncOperationHandle<GameObject> handle =
    Addressables.InstantiateAsync("Player", position, rotation);
await handle.Task;
GameObject instance = handle.Result;
```

**多个重载**（位置、父节点、保持世界空间等），签名和 `Object.Instantiate` 一致加上 `key`。

### 6.5.2 与 "LoadAssetAsync + Instantiate" 的区别

| 场景 | Load + Instantiate | InstantiateAsync |
|------|---------------------|------------------|
| 需要实例化几十个同资源 | ✓ 推荐（一次 Load，多次 Instantiate） | ✗ 每次都 refCount+1，开销大 |
| 一次性用一个实例 | ✗ 手动管两步生命周期 | ✓ 一步到位 |
| 要持久化持有原始 Prefab | ✓ 有 `handle.Result` 可作 Prefab | ✗ 拿到的是已实例化的 GameObject |

### 6.5.3 ReleaseInstance 的正确姿势（Ch.3 讨论过，这里定论）

```csharp
// ✓ 直接释放（最干净）
Addressables.ReleaseInstance(instance);

// ✓ 兜底：挂自动释放组件
public class AutoReleaseOnDestroy : MonoBehaviour
{
    private void OnDestroy() => Addressables.ReleaseInstance(gameObject);
}
```

**场景卸载时的行为**：当实例所在 Scene 卸载，Unity 会 Destroy 所有场景物体，**如果挂了 AutoRelease 组件，OnDestroy 会触发 ReleaseInstance**；没挂就会泄漏。所以 InstantiateAsync 创建的对象，**要么直接 ReleaseInstance，要么挂 AutoRelease 组件**，二选一。

---

## 6.6 `LoadSceneAsync` / `UnloadSceneAsync`：加载 Addressable 场景

### 6.6.1 官方标准写法

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class LoadSceneByAddress : MonoBehaviour
{
    public string key;
    private AsyncOperationHandle<SceneInstance> m_LoadHandle;

    void Start()
    {
        m_LoadHandle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive);
    }

    void OnDestroy()
    {
        Addressables.UnloadSceneAsync(m_LoadHandle);
    }
}
```

### 6.6.2 参数逐条

| 参数 | 含义 |
|------|------|
| `key` | 场景的 Address |
| `loadMode` | `LoadSceneMode.Single` / `Additive` |
| `loadSceneParameters` | 指定物理模式（2D/3D），默认跟 Single 一致 |
| `activateOnLoad` | 加载完成是否立即激活，默认 `true` |
| `priority` | 异步优先级，默认 `100`（对齐 Unity SceneManager） |

### 6.6.3 Single vs Additive

- **Single**：卸载当前所有场景，额外调用一次 `Resources.UnloadUnusedAssets`
- **Additive**：叠加加载，现有场景不动

### 6.6.4 ⚠️ `activateOnLoad = false` 的陷阱

官方警告：
> "Setting activateOnLoad to false blocks the AsyncOperation queue, preventing other Addressable assets from loading until the scene is manually activated via SceneInstance.ActivateAsync()."

**不激活 = 卡住整个 Addressables 的异步队列**，其他资源都加载不了。所以：

**正确用法**：
```csharp
var h = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, activateOnLoad: false);
await h.Task;   // 这一步其实在 90% 时会卡住
// ... 做你的过场 ...
await h.Result.ActivateAsync();   // 尽快激活
```

**实践**：除非你做"加载完成后手动激活"的过场流程，**`activateOnLoad` 保持默认 `true`**。

### 6.6.5 卸载用 `UnloadSceneAsync`

**千万别用 `Addressables.Release(handle)` 卸场景**。Scene 的生命周期走 `UnloadSceneAsync`，内部会正确处理场景对象、引用计数和 bundle。

---

## 6.7 `LoadResourceLocationsAsync`：只查不加载

### 6.7.1 它是什么

返回匹配 key 的 `IResourceLocation` 列表——**资源的定位信息**，不实际加载资源本身。

```csharp
AsyncOperationHandle<IList<IResourceLocation>> handle =
    Addressables.LoadResourceLocationsAsync("Enemy", typeof(GameObject));
await handle.Task;
IList<IResourceLocation> locations = handle.Result;
// 对每个 location 可以拿到 PrimaryKey、ResourceType、ProviderId 等元数据
```

### 6.7.2 官方关键特性

> "LoadResourceLocationsAsync never fails. If no assets match your query, it returns an empty list."

**永远不失败**，没匹配就返回空列表。适合做"存在性检查"。

### 6.7.3 三个典型用例

**① 存在性检查 / 动态资源探测**
```csharp
var h = Addressables.LoadResourceLocationsAsync("optional_dlc_boss");
await h.Task;
if (h.Result.Count > 0)
    // 这个 DLC 资源存在，可以加载
```

**② 批量加载时绑回原始 key**

官方文档明确指出：
> "When loading multiple assets, the order returned may differ from your input order. Using IResourceLocation instances (which contain PrimaryKey data) lets you build dictionaries correlating assets to their original keys."

用法：
```csharp
var locH = await Addressables.LoadResourceLocationsAsync(keys).Task;
var map = new Dictionary<string, GameObject>();
foreach (var loc in locH.Result)
{
    var assetH = await Addressables.LoadAssetAsync<GameObject>(loc).Task;
    map[loc.PrimaryKey] = assetH.Result;
}
```

**③ 查询子对象位置**
```csharp
// FBX 包含 GameObject/Mesh/Material 等多个子对象
var h = await Addressables.LoadResourceLocationsAsync("character.fbx").Task;
// 每个子对象会有独立的 IResourceLocation
```

### 6.7.4 记得 Release

locations handle 也有引用计数，用完 `Addressables.Release(locHandle)`。

---

## 6.8 `WaitForCompletion`：同步化的代价

### 6.8.1 它是什么

任意 handle 都能调 `handle.WaitForCompletion()`，**阻塞当前线程**直到操作完成，直接返回结果。

```csharp
GameObject prefab = Addressables.LoadAssetAsync<GameObject>("Player").WaitForCompletion();
Instantiate(prefab);
```

看起来像 `Resources.Load` 的同步返回，但**坑很多**。

### 6.8.2 官方明确的警告列表

> **① 远程资源禁用**
> "Don't call WaitForCompletion on an operation that's going to fetch and download a remote AssetBundle."
> 阻塞主线程等网络下载 = 游戏直接卡死。

> **② 所有活跃操作一起完成**
> "All active asset load operations complete synchronously when you call this on any operation, potentially causing unexpected stalls."
> 你调 A 的 WaitForCompletion，B/C/D 所有在飞的操作也都被迫同步完成。

> **③ Scene 激活不支持同步**
> "Scene loading cannot complete synchronously; scene activation must remain asynchronous."
> 场景加载的"激活"步骤永远是异步的，WaitForCompletion 卡不住它。

> **④ Awake 内死锁风险**
> "Calling it during Awake on unloaded scenes can deadlock the main thread."
> 场景还没加载完，组件 Awake 里又调 WaitForCompletion → 死锁。

> **⑤ WebGL 完全不支持**
> "WebGL doesn't support it at all due to single-threaded architecture."

### 6.8.3 什么时候可以用

只在**全部满足**以下条件时可以考虑：
- 资源是**本地的**（Local Group 或已缓存到本地）
- 是**启动期关键路径**（比如首屏 Splash 配置，必须同步拿）
- **不在 Awake 里用在未加载场景上**
- **不是 WebGL**

即便如此，**默认首选 `await`**。WaitForCompletion 是逃生出口，不是日常方案。

---

## 6.9 `DownloadDependenciesAsync` / `GetDownloadSizeAsync`：预下载

### 6.9.1 它们做什么

**Addressables 的 LoadAssetAsync 本身就会自动下载依赖 bundle**——首次加载远程资源时，bundle 会从 CDN 下到本地缓存，然后再从 bundle 取资源。

**预下载 API 的意义**：在合适时机（比如"进入关卡前的 Loading 界面"）**主动**把 bundle 下完，避免实际加载资源时卡顿。

### 6.9.2 基本用法

```csharp
// 查下载体积（可用于给用户看 "需要下载 XX MB"）
AsyncOperationHandle<long> sizeH = Addressables.GetDownloadSizeAsync("Level1");
await sizeH.Task;
long bytes = sizeH.Result;
Addressables.Release(sizeH);

// 预下载
AsyncOperationHandle downloadH = Addressables.DownloadDependenciesAsync("Level1");
while (!downloadH.IsDone)
{
    float p = downloadH.PercentComplete;   // 进度
    DownloadStatus s = downloadH.GetDownloadStatus();  // s.DownloadedBytes / s.TotalBytes
    UpdateProgressUI(p);
    await UniTask.Yield();
}
Addressables.Release(downloadH);
```

### 6.9.3 什么时候该预下载

- **远程资源**：首次使用前一定要预下载，不然用户点击时会卡顿
- **大体积 bundle**：进入 Loading 界面时启动下载
- **本地资源**：不需要（bundle 本来就在本地）

### 6.9.4 Release 问题

`DownloadDependenciesAsync` 的 handle **默认不持有 bundle 的引用**（下完就让 bundle 进缓存，bundle 实际由后续 Load 调用来持有）。但按**规则一切 handle 都要 Release**，用完就释放。

---

## 6.10 速查卡：API × 场景

| 场景 | 首选 API |
|------|---------|
| 加载单个 Prefab（已知 Address） | `LoadAssetAsync<GameObject>(address)` |
| 加载一批带同 Label 的资源 | `LoadAssetsAsync<T>(label, cb)` |
| 加载并放进场景（一次性） | `InstantiateAsync(key)` |
| 加载 Sprite Atlas 里某个 Sprite | `LoadAssetAsync<Sprite>("atlas[sprite_name]")` |
| 加载 Addressable 场景 | `LoadSceneAsync(key, LoadSceneMode.Additive)` |
| 查资源是否存在、不加载本体 | `LoadResourceLocationsAsync(key)` |
| 进入关卡前提前下 bundle | `DownloadDependenciesAsync(key)` |
| 显示 "需下载多少 MB" | `GetDownloadSizeAsync(key)` |
| 本地启动期必须同步拿 | `LoadAssetAsync(...).WaitForCompletion()` |

---

## 6.11 本章自测题

1. `LoadAssetAsync` 和 `LoadAssetsAsync` 的返回值类型有什么差别？为什么传 Label 建议用后者？
2. `MergeMode` 的 `Union` / `Intersection` / `UseFirst`（= `None`）四个值各自什么意思？
3. `LoadSceneAsync` 的 `activateOnLoad = false` 有什么副作用？
4. `WaitForCompletion` 绝对不能用在哪些场景？（至少列三条）
5. `DownloadDependenciesAsync` 和 `LoadAssetAsync` 在下载行为上有什么分工？
6. `LoadResourceLocationsAsync` 的典型使用场景是什么？它会失败吗？
7. `InstantiateAsync` 创建的对象为什么必须用 `ReleaseInstance` 而不是 `Destroy`？

---

## 6.12 Nova 项目实操建议

你目前 `IAssetLoadManager.LoadAsync` 是框架封装层。如果将来要接 Addressables，底层实现就是：

```csharp
// 框架层内部
public async UniTask<T> LoadAsync<T>(string address) where T : Object
{
    var handle = Addressables.LoadAssetAsync<T>(address);
    await handle.Task;
    if (handle.Status != AsyncOperationStatus.Succeeded)
    {
        Log.Error($"Load failed: {address}");
        return null;
    }
    // 注意：这里要把 handle 存起来，由业务方后续调 ReleaseAsync(address) 时释放
    m_HandleMap[address] = handle;
    return handle.Result;
}

public void ReleaseAsync(string address)
{
    if (m_HandleMap.TryGetValue(address, out var h))
    {
        Addressables.Release(h);
        m_HandleMap.Remove(address);
    }
}
```

**关键设计决策**：框架层要么**暴露 handle**给业务方自管，要么**自己维护 address → handle 映射**。后者对业务方友好，但要求业务方不能漏调 ReleaseAsync。真到 Nova 整合时再细化。

---

**Ch.6 结束**。所有运行时加载 API 都讲了一遍。

**Ch.7 AssetReference 家族**：Inspector 里能拖拽的那种引用类型。这是编辑时绑定 + 运行时加载的推荐方式，和 Unity 原生 `SerializeField` 直接引用对比，核心优势就是"不会把整个 bundle 强拉进内存"。

继续 Ch.7？

---

## 💬 用户提问

> “  AsyncOperationHandle<GameObject> handle =
>       Addressables.InstantiateAsync("Player", position, rotation);
>   await handle.Task;
>   GameObject instance = handle.Result;
> 
>   多个重载（位置、父节点、保持世界空间等），签名和 Object.Instantiate 一致加上 key。”
> 
> 什么叫签名和 Object.Instantiate 一致加上 key，没懂！


这句话我简写过头了，展开讲。

---

## "签名一致" 的含义

**签名 = 方法的参数列表**（参数类型、顺序、数量）。

"InstantiateAsync 的签名和 `Object.Instantiate` 一致 + 加上 key" 的意思是：

Unity 原生的 `Object.Instantiate` 本来就有一堆重载（不同参数组合）。Addressables 把这些重载**照抄一遍**，每个重载的第一个参数**从 "prefab 引用" 换成 "key（地址字符串）"**，其余参数完全一样。

---

## 对照表

### Unity 原生 `Object.Instantiate`（Unity 官方 API）

```csharp
// 重载 1：只实例化，不指定位置/父节点
public static Object Instantiate(Object original);

// 重载 2：指定位置 + 旋转
public static Object Instantiate(Object original, Vector3 position, Quaternion rotation);

// 重载 3：指定位置 + 旋转 + 父节点
public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform parent);

// 重载 4：只指定父节点
public static Object Instantiate(Object original, Transform parent);

// 重载 5：指定父节点 + 是否保持世界坐标
public static Object Instantiate(Object original, Transform parent, bool instantiateInWorldSpace);
```

### Addressables.InstantiateAsync（对照的 2.9.1 API）

```csharp
// 对应重载 1
public static AsyncOperationHandle<GameObject> InstantiateAsync(object key);

// 对应重载 2：加上位置 + 旋转
public static AsyncOperationHandle<GameObject> InstantiateAsync(
    object key, Vector3 position, Quaternion rotation);

// 对应重载 3：加上位置 + 旋转 + 父节点
public static AsyncOperationHandle<GameObject> InstantiateAsync(
    object key, Vector3 position, Quaternion rotation, Transform parent);

// 对应重载 4：只指定父节点
public static AsyncOperationHandle<GameObject> InstantiateAsync(
    object key, Transform parent);

// 对应重载 5：父节点 + 是否保持世界坐标
public static AsyncOperationHandle<GameObject> InstantiateAsync(
    object key, Transform parent, bool instantiateInWorldSpace);
```

---

## 两点变化

对比两边，Addressables 只做了两件事：

**变化 ①**：首参数 `Object original`（Prefab 的直接引用）→ `object key`（Address / AssetReference / IResourceLocation）

- Unity 原生：你手里必须已经有 prefab 的引用（比如 `[SerializeField] GameObject m_PlayerPrefab`）
- Addressables：你只要知道"地址"字符串就行，引擎帮你加载

**变化 ②**：返回值 `Object` → `AsyncOperationHandle<GameObject>`

- Unity 原生：同步返回实例
- Addressables：异步返回 handle（因为要先加载 bundle），完成后 `handle.Result` 才是实例

---

## 具体对照示例

**"在世界坐标 (10, 0, 0) 实例化一个 Player 作为 rootTransform 的子节点"**：

```csharp
// Unity 原生写法
GameObject prefab = m_PlayerPrefab;   // 自己拿到 prefab
GameObject inst = Object.Instantiate(prefab, new Vector3(10, 0, 0), Quaternion.identity, rootTransform);

// Addressables 对应写法
var handle = Addressables.InstantiateAsync("Player", new Vector3(10, 0, 0), Quaternion.identity, rootTransform);
await handle.Task;
GameObject inst = handle.Result;
```

**参数位置、顺序、类型完全对应**——`Vector3 position`、`Quaternion rotation`、`Transform parent` 一字不差，只是最前面把 `prefab` 换成 `"Player"`。

---

## 为什么要"保持一致"

官方这么设计是为了**学习成本降到最低**：

- 你已经会用 `Object.Instantiate`
- 想把某个 prefab 改成 Addressable 时，只需把 `Instantiate(prefab, ...)` 改成 `InstantiateAsync(address, ...)`
- 其他参数照搬，不用重学

---

## 一句话总结

> **"签名和 Object.Instantiate 一致 + 加上 key"** 
> = 参数列表和 `Object.Instantiate` 的各种重载完全对应，只是首参数从"直接的 prefab 引用"换成"地址 key"。

这个规律在 Addressables 的 API 设计里很常见——尽量贴近 Unity 原生 API 的形态，降低迁移成本。

清楚了吗？清楚的话进 Ch.7？

---

## 💬 用户提问

> “查询子对象位置
>   // FBX 包含 GameObject/Mesh/Material 等多个子对象
>   var h = await Addressables.LoadResourceLocationsAsync("character.fbx").Task;
>   // 每个子对象会有独立的 IResourceLocation”
> character.fbx 这个语义是什么？character下的子节点fbx吗？


不是子节点，这里的 `"character.fbx"` 就是**一个资源的 Address 字符串**，我写成带 `.fbx` 后缀是因为 Addressables 默认生成的 Address 带文件扩展名，容易让你误解。展开讲清楚。

---

## 1. `"character.fbx"` 是什么

**它是一个资源的 Address**，和 `"Player"`、`"ui/login_panel"` 这种 Address 字符串**完全同类**。

例子里之所以写成 `character.fbx`，是因为：
- Addressables 默认用资源的 AssetPath 作为 Address（比如 `Assets/Art/Chars/character.fbx`）
- 你如果没改 Address 或点过 Tools → `Simplify Addressable Names`，默认 Address 就可能是 `character.fbx` 或 `Assets/Art/Chars/character.fbx`
- 文档作者取这种带扩展名的字符串做例子，只是为了让你一眼看出"这个资源是个 fbx 文件"

**如果我当时把例子写成 `"Character"`，语义完全一样**：

```csharp
var h = await Addressables.LoadResourceLocationsAsync("Character").Task;
```

只要 `Character` 是你在 Groups 窗口里给那个 fbx 资源设的 Address，结果和写 `"character.fbx"` 没有任何区别。

---

## 2. FBX 的"子对象"是什么

这才是例子想讲的重点。和 Addressables 的"子节点 / 父子关系"**毫无关系**，指的是 **Unity 导入 FBX 后形成的内部子资源**。

在 Unity 里，一个 FBX 文件本身是**一个资源**，但 Unity 导入时会从中解析出一堆**子对象**（sub-assets）：

```
Assets/Art/Chars/character.fbx               ← 主资源
  ├── character (GameObject)                  ← 子对象 1：根 GameObject
  ├── character_mesh (Mesh)                   ← 子对象 2：网格
  ├── character_material_body (Material)      ← 子对象 3：材质
  ├── character_material_face (Material)      ← 子对象 4：材质
  ├── character_idle (AnimationClip)          ← 子对象 5：动画
  └── character_run (AnimationClip)           ← 子对象 6：动画
```

在 Project 窗口里，点 fbx 旁边的小三角展开，能看到这些子对象。每一个都可以独立被代码引用。

---

## 3. Addressables 给每个子对象一个 IResourceLocation

这就是官方文档那段话的意思：

> "Unity generates locations for sub-objects at runtime. Querying locations for an FBX file returns separate IResourceLocation instances for each component (GameObject, mesh, material)."

**查询时传的是"主资源的 Address"，Addressables 返回的是"该资源下所有子对象的 location 列表"**：

```csharp
// 传主资源 Address（不管它原文件叫什么）
var h = await Addressables.LoadResourceLocationsAsync("character.fbx").Task;

// h.Result 会包含多个 IResourceLocation：
//   loc[0]: GameObject,    PrimaryKey="character.fbx"
//   loc[1]: Mesh,          PrimaryKey="character.fbx"
//   loc[2]: Material (body)
//   loc[3]: Material (face)
//   loc[4]: AnimationClip (idle)
//   loc[5]: AnimationClip (run)
```

每个 location 有自己的 `ResourceType`（类型），你可以根据类型做过滤：

```csharp
var locs = h.Result;
var meshLoc = locs.FirstOrDefault(l => l.ResourceType == typeof(Mesh));
if (meshLoc != null)
{
    var meshHandle = await Addressables.LoadAssetAsync<Mesh>(meshLoc).Task;
    // 只加载那一个 Mesh，不加载整个 fbx 里所有子对象
}
```

---

## 4. 对比澄清

**"子对象" vs "子节点"** 是两回事：

| 词 | 含义 |
|----|------|
| **子对象（sub-asset）** | 一个资源文件内部解析出的多个资源（FBX 里的 Mesh/Material/Animation、SpriteAtlas 里的 Sprite） |
| **子节点（child GameObject）** | 一个 GameObject 的 Transform 子节点（场景层级关系） |

那段例子讨论的是前者——**文件结构层面**的子对象，和场景层级无关。

---

## 5. 把扩展名从例子里拿掉

更干净的写法：

```csharp
// 假设 Groups 里把 character.fbx 的 Address 设为 "Character"
var h = await Addressables.LoadResourceLocationsAsync("Character").Task;

// 返回的依然是那个 fbx 里所有子对象的 location 列表
// 因为 "Character" 这个 Address 指向的是那个 fbx 主资源
```

**语义重点**：`LoadResourceLocationsAsync(主资源的 key)` → 返回主资源 **+ 所有子对象**的 location 列表，不是什么"子节点"。

---

清楚了吗？清楚我们进 Ch.7。

---

## 📌 用户：切换到下一章


资料齐了，开始 Ch.7。

---

# Ch.7 AssetReference 家族

**版本基准**：Addressables 2.9.1（依据 Unity 官方 2.9 手册）。
**章节目标**：讲清楚 AssetReference 是什么、和 string address / SerializeField 直接引用的区别、全部子类型、加载/释放 API、Inspector 限制标签的用法。

---

## 7.1 AssetReference 是什么

**官方定义**：
> "AssetReference 是一个可序列化的字段类型，用于在 MonoBehaviour 或 ScriptableObject 脚本里引用 Addressable 资源。"

一句话：**能在 Inspector 拖拽、内部本质是 Address 字符串、用完要手动释放**的资源引用类型。

### 三方对比

| 方式 | 编译期检查 | 加载时机 | 是否进首包 |
|------|-----------|----------|------------|
| `SerializeField GameObject m_Prefab`（直接引用） | ✓ | 启动期随宿主加载 | **会进宿主 bundle**，即使没用 |
| `string m_Address`（字符串地址） | ✗ 拼错运行时才炸 | 手动 LoadAssetAsync | ✓ |
| **AssetReference m_Ref**（本章主角） | ✓ Inspector 限制类型 | 手动 `m_Ref.LoadAssetAsync()` | ✓ |

**核心价值**：编辑时像 SerializeField 一样拖拽赋值 + 运行时像 Addressable 一样按需加载。既有类型安全，又不预占内存。

---

## 7.2 AssetReference 子类全清单（2.9.1 官方）

| 类型 | 用途 |
|------|------|
| `AssetReference` | 任意资源类型（泛任意） |
| `AssetReferenceT<T>` | 泛型版本，Inspector 只能拖 T 类型资源 |
| `AssetReferenceGameObject` | 只能拖 Prefab / GameObject |
| `AssetReferenceTexture` | 任意 Texture |
| `AssetReferenceTexture2D` | 只 Texture2D |
| `AssetReferenceTexture3D` | 只 Texture3D |
| `AssetReferenceSprite` | 只 Sprite（单个 Sprite 文件） |
| `AssetReferenceAtlasedSprite` | Sprite Atlas 里的某个 Sprite |

**选型原则**：能用具体类型就用具体类型。用 `AssetReference`（无约束）意味着 Inspector 里任何资源都能拖进来，失去类型安全。

---

## 7.3 声明字段

### 7.3.1 基本声明

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_PlayerPrefab;
    [SerializeField] private AssetReferenceSprite m_Icon;
    [SerializeField] private AssetReferenceT<AudioClip> m_BGM;    // 泛型指定类型
}
```

Inspector 里会出现三个可拖拽的"点"字段，类型不匹配时拖不进去。

### 7.3.2 自定义子类（Unity 2020.1 前必须，现在可选）

官方说明：
> "For earlier Unity versions (pre-2020.1), create a concrete subclass instead of using generic types directly, since the Inspector couldn't display generic fields by default."

Unity 6000（你的版本）**完全支持 `AssetReferenceT<T>` 直接序列化**，所以通常不用自定义子类。但自定义仍有用——比如给某类资源加专有逻辑：

```csharp
[System.Serializable]
public class AssetReferenceAudioClip : AssetReferenceT<AudioClip>
{
    public AssetReferenceAudioClip(string guid) : base(guid) { }
}

// 使用
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AssetReferenceAudioClip m_Track;
}
```

日常不用，现在泛型就够。

---

## 7.4 `AssetReferenceUILabelRestriction`：限制可拖的 Label

只允许带某些 Label 的资源能被拖进字段：

```csharp
public class CharacterSpawner : MonoBehaviour
{
    [AssetReferenceUILabelRestriction("Character", "Enemy")]
    [SerializeField]
    private AssetReferenceGameObject m_EnemyPrefab;
    // Inspector 里只能拖带 "Character" 或 "Enemy" 标签的 prefab
}
```

**⚠️ 官方警告**：
> "This attribute only enforces restrictions in the Inspector; script-based assignments bypass it."

**只约束 Inspector 拖拽**，代码里用 `new AssetReferenceGameObject(guid)` 构造时不会被检查。**是编辑器防呆，不是运行时校验**。

---

## 7.5 核心属性

实例化一个 AssetReference 后能用的主要成员：

| 成员 | 类型 | 含义 |
|------|------|------|
| `AssetGUID` | string | 资源在 Unity 里的 GUID，序列化存的就是它 |
| `RuntimeKey` | object | 实际加载时用的 key，通常就是 AssetGUID |
| `SubObjectName` | string | 子对象名（比如 Sprite Atlas 里的某个 Sprite） |
| `editorAsset` | Object | **仅 Editor** 能用，拿到资源本体的直接引用 |
| `Asset` | Object | **运行时**属性，`LoadAssetAsync` 完成后才有值 |
| `OperationHandle` | AsyncOperationHandle | 最近一次 Load 返回的 handle |

### `editorAsset` vs `Asset`

常见混淆点：

```csharp
#if UNITY_EDITOR
// editorAsset：编辑器专用，不经过加载流程，直接拿到资源
GameObject prefab = m_Ref.editorAsset as GameObject;
#endif

// Asset：运行时属性，必须先 Load 才有值
await m_Ref.LoadAssetAsync().Task;
GameObject prefabAtRuntime = m_Ref.Asset as GameObject;
```

**`editorAsset` 只能在 Editor 代码或 `#if UNITY_EDITOR` 里用**，真机打包后会编译错误。

---

## 7.6 判断 AssetReference 是否有效

**三种"空"状态**要区分：

```csharp
// 状态 1：字段未赋值（Inspector 里是 None）
if (m_Ref == null)               // 这个判断不够，AssetReference 是 struct-like 的类
    return;

// 状态 2：官方推荐判断
if (m_Ref == null || !m_Ref.RuntimeKeyIsValid())
    return;

// 状态 3：加载后还要判断是否成功
await m_Ref.LoadAssetAsync().Task;
if (m_Ref.OperationHandle.Status != AsyncOperationStatus.Succeeded)
    return;
```

**`RuntimeKeyIsValid()` 是标准判空姿势**，直接 `== null` 不保险。

---

## 7.7 加载 AssetReference

### 7.7.1 LoadAssetAsync

```csharp
public class UILoginView : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_LogoPrefab;

    private async void Awake()
    {
        if (!m_LogoPrefab.RuntimeKeyIsValid())
            return;

        AsyncOperationHandle<GameObject> handle = m_LogoPrefab.LoadAssetAsync<GameObject>();
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            Instantiate(handle.Result, transform);
    }

    private void OnDestroy()
    {
        if (m_LogoPrefab != null && m_LogoPrefab.IsValid())
            m_LogoPrefab.ReleaseAsset();   // 释放 AssetReference 持有的 handle
    }
}
```

**关键点**：
- `m_Ref.LoadAssetAsync<T>()` 返回 `AsyncOperationHandle<T>`
- **handle 同时存到 `m_Ref.OperationHandle` 属性里**，所以可以不自己存——通过 `m_Ref.OperationHandle` 再访问
- 释放时用 `m_Ref.ReleaseAsset()`（扩展方法，内部 `Addressables.Release(OperationHandle)`）

### 7.7.2 InstantiateAsync

```csharp
var handle = m_PlayerPrefab.InstantiateAsync(position, rotation);
await handle.Task;
GameObject instance = handle.Result;

// 释放
m_PlayerPrefab.ReleaseInstance(instance);
```

和 Ch.6 讲的 `Addressables.InstantiateAsync` 行为一致，只是入口换到 AssetReference 上。

### 7.7.3 多次 Load 的坑

```csharp
// ❌ 错误写法：重复 Load 覆盖 OperationHandle
await m_Ref.LoadAssetAsync<GameObject>().Task;
await m_Ref.LoadAssetAsync<GameObject>().Task;   // 第二次会覆盖前一个 handle
m_Ref.ReleaseAsset();                            // 只释放了第二次，第一次泄漏
```

**AssetReference 内部只记最后一次的 handle**。如果你要加载同一个资源多次（对象池），用 `InstantiateAsync` 或自己把 handle 存进列表。

### 7.7.4 作为 key 传给 `Addressables.LoadAssetAsync`

AssetReference 本身实现了作为 Addressables API 的 key，两种写法等价：

```csharp
// 写法 A：从 AssetReference 调（推荐）
await m_Ref.LoadAssetAsync<GameObject>().Task;

// 写法 B：传给 Addressables 静态方法
var handle = Addressables.LoadAssetAsync<GameObject>(m_Ref);
await handle.Task;
```

写法 B 的好处：handle 完全由你管，不和 `m_Ref.OperationHandle` 纠缠。做对象池时写法 B 更灵活。

---

## 7.8 子对象加载（SubObjectName）

### 7.8.1 Sprite Atlas 里的某个 Sprite

```csharp
public class IconView : MonoBehaviour
{
    [SerializeField] private AssetReferenceAtlasedSprite m_IconRef;
    // Inspector 里：先拖 SpriteAtlas，再从下拉选具体 Sprite

    private async void Awake()
    {
        var handle = m_IconRef.LoadAssetAsync<Sprite>();
        await handle.Task;
        GetComponent<UnityEngine.UI.Image>().sprite = handle.Result;
    }
}
```

AssetReferenceAtlasedSprite 在 Inspector 里会显示两个选择框：
1. SpriteAtlas 本体
2. Atlas 里的具体 Sprite（即 SubObjectName）

### 7.8.2 代码里设 SubObjectName

```csharp
m_Ref.SubObjectName = "hero_idle_0";
var handle = m_Ref.LoadAssetAsync<Sprite>();
```

相当于 Ch.6 里的方括号语法 `"atlas[hero_idle_0]"`。

---

## 7.9 用在 ScriptableObject 上

AssetReference 不仅能用在 MonoBehaviour，也能用在 ScriptableObject：

```csharp
[CreateAssetMenu(fileName = "CharTable", menuName = "Game/CharTable")]
public class CharTable : ScriptableObject
{
    [SerializeField] private AssetReferenceGameObject[] m_Characters;

    public async UniTask<GameObject> LoadCharacter(int index)
    {
        var handle = m_Characters[index].LoadAssetAsync<GameObject>();
        await handle.Task;
        return handle.Result;
    }
}
```

**这是配置型数据的推荐形态**——配置表里列出一堆资源引用，真正用哪个再加载哪个。

---

## 7.10 AssetReference 的限制（官方强制）

官方明确：
> "The group's 'Include GUIDs in Catalog' property must be enabled in Advanced Options to assign assets from that group to an AssetReference field."

**硬性前提**：资源所在 Group 的 Schema **必须勾上 `Include GUIDs in Catalog`**（默认是勾上的，你项目里 `m_IncludeGUIDInCatalog: 1`，正常）。不勾就拖不进 AssetReference 字段。

**原因**：AssetReference 序列化存的是 GUID，Catalog 里必须能根据 GUID 查到资源才能加载。

---

## 7.11 标准模板三连

### 模板 A：单资源，长期持有

```csharp
public class HUD : MonoBehaviour
{
    [SerializeField] private AssetReferenceT<Texture2D> m_BgTex;

    private async void Awake()
    {
        if (!m_BgTex.RuntimeKeyIsValid()) return;
        await m_BgTex.LoadAssetAsync().Task;
        GetComponent<RawImage>().texture = m_BgTex.Asset as Texture2D;
    }

    private void OnDestroy()
    {
        if (m_BgTex != null && m_BgTex.IsValid())
            m_BgTex.ReleaseAsset();
    }
}
```

### 模板 B：一次性实例化

```csharp
public class EnemyFactory : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_EnemyPrefab;

    public async UniTask<GameObject> Spawn(Vector3 pos)
    {
        var handle = m_EnemyPrefab.InstantiateAsync(pos, Quaternion.identity);
        await handle.Task;
        return handle.Result;   // 调用方负责 ReleaseInstance
    }
}
```

### 模板 C：多实例（对象池）—— 用 `Addressables.InstantiateAsync` 绕开 `OperationHandle` 单槽

```csharp
public class EnemyPool : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_EnemyPrefab;
    private List<AsyncOperationHandle<GameObject>> m_Handles = new();

    public async UniTask<GameObject> Spawn(Vector3 pos)
    {
        var handle = Addressables.InstantiateAsync(m_EnemyPrefab, pos, Quaternion.identity);
        m_Handles.Add(handle);
        await handle.Task;
        return handle.Result;
    }

    private void OnDestroy()
    {
        foreach (var h in m_Handles)
            if (h.IsValid()) Addressables.Release(h);
        m_Handles.Clear();
    }
}
```

---

## 7.12 AssetReference vs string address 最终选型

| 场景 | 首选 |
|------|------|
| 编辑时确定是哪个资源 | **AssetReference** |
| 运行时根据数据动态拼 key（如 `"enemy_" + id`） | string address |
| 配置表里一堆固定资源 | AssetReference |
| 策划热改的表格里的资源引用 | string address（表里存 Address 字符串） |
| 同一字段可能拖任意类型 | 不推荐（设计有问题，拆字段） |

**团队协作考量**：拖拽比字符串更不容易出错，但重构时两边都要跟。AssetReference 更偏向"强类型 OOP"，string 更偏向"数据驱动"。

---

## 7.13 本章自测题

1. AssetReference 和 `[SerializeField] GameObject m_Prefab` 的核心差别是什么？
2. `AssetReferenceGameObject` 和 `AssetReferenceT<GameObject>` 的关系是什么？
3. `editorAsset` 和 `Asset` 属性分别什么时候能用？
4. `AssetReferenceUILabelRestriction` 的约束强度如何？能阻止代码里的错误赋值吗？
5. AssetReference 的标准判空姿势是什么？
6. 为什么同一个 AssetReference 不能重复 `LoadAssetAsync`？对象池场景怎么办？
7. 用 AssetReference 的前提条件（Group Schema 里哪个字段必须开）？

---

## 7.14 动手小任务

先别着急做——这个任务依赖你至少有一个 Addressable 资源。检查一下：

1. 打开 Groups 窗口，看 `Default Local Group` 里有没有资源
2. 如果没有，把任意一个 Prefab 拖进去，设个 Address
3. 新建一个 MonoBehaviour 脚本：

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetRefTest : MonoBehaviour
{
    [SerializeField] private AssetReferenceGameObject m_Prefab;

    private async void Start()
    {
        if (!m_Prefab.RuntimeKeyIsValid())
        {
            Debug.LogError("AssetReference not set");
            return;
        }

        var handle = m_Prefab.InstantiateAsync(transform.position, Quaternion.identity);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            Debug.Log($"Spawned: {handle.Result.name}");
    }

    private void OnDestroy()
    {
        // 注意：InstantiateAsync 的实例应该在实例的 OnDestroy 里释放
        // 这个简单版本先不管释放，等 Ch.8 做完整项目时再处理
    }
}
```

4. 挂到一个空 GameObject 上，Inspector 里把你的 Prefab 拖到 `m_Prefab` 字段
5. 按 Play，Console 看 `Spawned: xxx`

**验证目的**：确认 AssetReference 拖拽 + 运行时加载链路正常。

做完告诉我结果，或者如果 Play Mode Script 选的是 `Use Existing Build` 且没构建过会报错，你也告诉我。

---

**Ch.7 结束**。Addressable 的**编辑时绑定 + 运行时加载**闭环完整走通。

**Ch.8 Content Build 与 Update Build**：按官方 2.9.1 文档讲 `New Build` / `Update a Previous Build` 两个工作流、`addressables_content_state.bin` 的作用、Static vs Can Change Post Release 在更新时的具体行为、Catalog hash 校验机制。这是 CDN 热更前的核心准备。

继续 Ch.8？

---

## 📌 用户：切换到下一章


资料齐了，开始 Ch.8。

---

# Ch.8 Content Build 与 Update Build

**版本基准**：Addressables 2.9.1（依据 Unity 官方 2.9 手册）。
**章节目标**：理解两种构建流程、`addressables_content_state.bin` 的角色、Static vs Can Change Post Release 在更新时的真实行为、Catalog Hash 版本校验、常见陷阱。

**前置**：本章不涉及代码运行，是**构建流程的概念与规则**。跑 CDN 热更的代码在 Ch.9。

---

## 8.1 两种构建的分工

Addressables 的构建有两种**互不替代**的工作流：

| 构建 | 目的 | 产物变化 |
|------|------|----------|
| **New Build**（全量构建） | 每次发版 / 结构性变更时用 | 生成**全新**的 Catalog + 全部 bundle + **全新** `.bin` 状态文件 |
| **Update a Previous Build**（增量更新） | 发版后热更内容时用 | 基于上次 `.bin` 做差分，只重打**发生变化**的 bundle，生成新 Catalog |

**关系**：
```
                发版 v1.0.0
                    │
          New Build（打全量）
                    │
          ──►  content_state.bin (v1.0.0)  ◄── 必须保存！
                    │
                    │
                 玩家装 APK
                    │
           改资源
                    │
          Update a Previous Build（基于 v1.0.0 的 bin）
                    │
          上传差分 bundle + 新 Catalog 到 CDN
                    │
                 玩家热更
                    │
                    │   ← 继续基于同一份 bin 做下一次 Update
                    │
                发版 v1.1.0
                    │
          New Build（又打全量，产生新 bin）
```

**铁律**：
- 一个发版周期内，**只有一次 New Build**（发版前那次）
- 之后的所有热更都是 **Update a Previous Build**
- 下次发版前再做 New Build

---

## 8.2 `addressables_content_state.bin` 是什么

**这是 Update Build 工作流的灵魂**。

### 8.2.1 它记录什么

`addressables_content_state.bin` 是 New Build 时生成的**状态快照**，包含：

- 当次 Build 里每个资源的 **hash**（用于比对资源是否变更）
- 每个 Group 的配置快照
- 每个 bundle 的 ID 和依赖关系

### 8.2.2 存放位置

默认路径：
```
Assets/AddressableAssetsData/[BuildTarget]/addressables_content_state.bin
```

比如 Android 目标平台：
```
Assets/AddressableAssetsData/Android/addressables_content_state.bin
```

`AddressableAssetSettings` 里的 `Content State Build Path` 字段控制这个路径。

### 8.2.3 官方强制规则

> "You must save this file for each published full application release on every platform."

**每次发版时，必须保存那次 New Build 产生的 `.bin` 文件**。理由：
- Update Build 需要对比 `.bin` 才知道资源有没有变
- 每个平台一份（Android 用 Android 的 bin，iOS 用 iOS 的 bin）
- 不保存就**永远无法为那个版本做热更**

### 8.2.4 实践存放方式

三种常见做法：

| 做法 | 优点 | 缺点 |
|------|------|------|
| **提交 git**（`Assets/AddressableAssetsData/Android/addressables_content_state.bin`） | 简单，版本自带 | 二进制文件污染仓库 |
| **放专用发版仓库** | 仓库干净 | 需要额外流程 |
| **上传到远程**（`Content State Build Path` 指向 CDN） | 团队共享 | 需要配置 |

Nova 的建议：**发版时打 tag，把该平台的 `.bin` 提交到主仓库某个固定位置**，便于后续 Update Build 找到。

### 8.2.5 Update Build 使用 bin 的流程

```
Update a Previous Build
    │
    ├─ 读取 Content State Build Path 指向的 bin（上次 New Build 的快照）
    ├─ 计算当前资源的 hash
    ├─ 对比：哪些资源变了？
    ├─ 根据 Update Restriction 决定变更资源怎么办（见 8.3）
    └─ 只重打变化的 bundle，不动的 bundle 复用
```

**bin 本身不会被 Update Build 修改**——下次 Update 还是基于同一份 bin 对比。这样保证所有热更包都向同一个发版基线看齐。

---

## 8.3 Static Content vs Can Change Post Release：更新行为的分水岭

这是 Groups 窗口里每个 Group 的 `ContentUpdateGroupSchema` 的那个勾选框（Ch.4 讲过）。Update Build 的**所有分支逻辑**都基于这个开关。

### 8.3.1 Static Content（勾上 "Prevent Updates"）

**含义**：这个 Group 的资源**承诺不变**。

Update Build 时如果发现这个 Group 里有资源变了：
- **不会重打原 bundle**（原 bundle 保持不变，客户端里旧的还在用）
- **把变了的资源"挪"到一个新生成的 Group**
- 新 Group 里打出新 bundle，作为增量下载

### 8.3.2 Can Change Post Release（**不**勾 "Prevent Updates"）

**含义**：这个 Group 的资源**预期会变**。

Update Build 时如果发现资源变了：
- **整个 Group 的 bundle 全部重打**
- 新 bundle 替换旧的（文件名 hash 变了，CDN 上多一份）

### 8.3.3 两者对比

| 维度 | Static Content | Can Change Post Release |
|------|----------------|-------------------------|
| 资源变更时 | 改动资源移到新 Group 新 bundle | 整个 Group 的 bundle 全重打 |
| 客户端需要下载的 bundle | 仅新增的差分 bundle | 整个 Group 的新 bundle |
| 客户端本地旧 bundle | **一直留着占空间** | 缓存自动替换 |
| 每次更新的 Catalog 变更 | 小（只加新条目） | 大（bundle 全换） |
| 适合的资源 | 关卡资源、一次性资产、配置常量 | 每次更新都会动的资源、UI/配置表 |

### 8.3.4 官方明确的坑（Static 的代价）

> "Any changed local assets remain unused on the user's device forever."

**Static 的副作用**：旧 bundle 永远不清理。玩家设备上存了一堆无用 bundle。

**何时接受这个代价**：
- 关卡资源很大，极少变
- 想让"小幅 hotfix"不触发大包下载

**何时不要用 Static**：
- 这个 Group 预期会**经常**更新（配置表、UI 文案）
- 每次都多出一个 Group 会让 Catalog 和设备缓存越来越臃肿

### 8.3.5 铁律

官方最强警告：
> "Don't change the Update Restriction setting of a group unless you're performing a full build."

**中途不要改**：一旦发版 New Build 确定了某个 Group 是 Static/Can Change，**在同一发版生命周期内别动这个开关**。改了之后 Update Build 对比 bin 会出错，生成错误的差分包。

想改 → **发新版（走 New Build）一起改**。

---

## 8.4 Catalog Hash 的更新校验机制

这是客户端检测"有没有新内容"的机制。

### 8.4.1 Catalog 的两个文件

开启 `Build Remote Catalog` 后，每次构建会生成到 `Remote.BuildPath`（默认 `ServerData/[BuildTarget]/`）：

```
catalog_<timestamp>.bin       ← Catalog 正文（2.x 默认二进制；勾 Enable Json Catalog 则为 .json）
catalog_<timestamp>.hash      ← Catalog 的 hash 标记
```

`<timestamp>` 由构建时间戳生成，所以每次 Update Build 后文件名本身就不同。

### 8.4.2 客户端校验流程（运行时）

```
App 启动
    │
    │  Addressables.InitializeAsync()（默认自动调用）
    │
    ├─ 读取包里打包进的 Catalog（发版时的快照）
    │
    ├─ 如果 m_DisableCatalogUpdateOnStart = 0（默认）：
    │      ├─ 请求 Remote.LoadPath/catalog_<xxx>.hash
    │      ├─ 对比本地记住的 hash vs 远程 hash
    │      │    相同 → 无新内容
    │      │    不同 → 下载新 Catalog
    │      └─ 新 Catalog 里会记录哪些 bundle 的 hash 变了
    │
    └─ 应用新 Catalog（已加载资源的 bundle 下次 Load 时才更新）
```

### 8.4.3 手动触发：CheckForCatalogUpdates / UpdateCatalogs

官方写法（Ch.9 会详讲，这里先了解 API）：

```csharp
// 检查哪些 Catalog 有更新，不下载
var checkHandle = Addressables.CheckForCatalogUpdates(autoReleaseHandle: false);
await checkHandle.Task;
List<string> updatedLocators = checkHandle.Result;

if (updatedLocators.Count > 0)
{
    // 应用更新（真实下载新 Catalog）
    var updateHandle = Addressables.UpdateCatalogs(
        catalogs: updatedLocators,
        autoCleanBundleCache: true);   // 自动清理过期 bundle 缓存
    await updateHandle.Task;
}
```

**使用场景**：
- 开了 `Disable Catalog Update on Start = true`，自己控制更新时机（推荐：要给用户看"有新内容"弹窗的场景）
- 游戏中想重新检查（长时间在线的游戏）

---

## 8.5 几个关键 Settings 字段

`AddressableAssetSettings`（Inspector）里和更新相关的字段一次性过完：

| 字段 | 作用 | 推荐值 |
|------|------|--------|
| **Build Remote Catalog** | 是否构建远程 Catalog | 要热更 → **勾上** |
| **Only update catalogs manually** | 禁止启动时自动对比 Catalog | 要手动控制更新 → 勾上 |
| **Disable Catalog Update on Startup** | 同上（不同版本换过名字） | 同上 |
| **Check for Update Issues** | 改 Static Group 时警告等级（Fail/Warning/Disabled） | **List Restricted Assets**（默认） |
| **Content State Build Path** | `.bin` 存放路径 | 默认就行 |
| **Player Version Override** | 打进 Catalog 文件名的版本标识 | 默认 `[UnityEditor.PlayerSettings.bundleVersion]` |
| **Unique Bundle IDs** | bundle 文件名是否每次 Build 都变 | 热更有坑，默认**不勾** |

### Player Version Override 的含义

每次 Build 生成的 Catalog 文件名形如 `catalog_<Player Version Override>.json`。

- 默认 `[UnityEditor.PlayerSettings.bundleVersion]` → 用 PlayerSettings 里的版本号
- 每次发新版时 PlayerSettings.bundleVersion 变 → Catalog 文件名变 → **不同 APK 版本的 Catalog 不会互相覆盖**（多版本并存）

**错误做法**：填死一个固定值。后果：v1.0.0 和 v1.1.0 的 Catalog 文件名相同，互相覆盖，老版本玩家拿到了新 Catalog 却下不了对应 bundle → 全炸。

---

## 8.6 操作手册：两种构建怎么跑

### 8.6.1 发版前的 New Build

**步骤**：

1. 打开 `Window → Asset Management → Addressables → Groups`
2. 顶部 `Profile` 下拉，选对应发版的 Profile（如 `Production`）
3. 顶部 `Build → New Build → Default Build Script`
4. 等待构建完成
5. 产物：
   - 本地 bundle → `Library/com.unity.addressables/aa/Android/`
   - 远程 bundle + Catalog → `ServerData/Android/`
   - 状态文件 → `Assets/AddressableAssetsData/Android/addressables_content_state.bin`
6. **把 ServerData 上传到 CDN**
7. **把 `.bin` 妥善保存**（提交到 git tag 或发版仓库）
8. File → Build Settings → Build APK

### 8.6.2 发版后的 Update Build（热更）

**步骤**：

1. 改资源（别动结构、别改 Group 的 Update Restriction）
2. **（推荐）** `Build → Check for Content Update Restrictions`
   - 列出所有修改了但在 Static Group 里的资源，自动挪到新 Group
   - 不选这步，Update Build 也会做相同处理，但你没机会先审一遍
3. `Build → Update a Previous Build`
4. 第一次会弹出文件选择框，定位到上次 New Build 产生的 `.bin` 文件
5. 等待构建完成
6. 产物：
   - `ServerData/Android/` 里**新增**差分 bundle + 新 Catalog（旧文件仍在，别删）
7. **把新增文件上传到 CDN**（合并到已有目录，不清空）
8. APK **不用重打**

### 8.6.3 Check for Content Update Restrictions

这个工具做的事：
- 扫描所有 Static Group
- 找出**改过**的资源
- 自动创建一个新 Group，把改过的资源挪进去
- 新 Group 默认设为 Can Change Post Release

**建议每次 Update Build 前都跑一遍**，心里有数。

---

## 8.7 Unique Bundle IDs：一个容易踩的坑

`AddressableAssetSettings` 里的 **Unique Bundle IDs** 开关：

### 勾上会发生什么

每次 Build 时，**所有** bundle 的内部 ID 都重新生成（即使内容没变）。结果：
- 好处：强制走"完全新版本"语义，减少旧 bundle 复用出问题
- 坏处：**Update Build 失去大部分优化**——所有 bundle 都被视为"变了"，客户端几乎全量下载

官方：
> "any AssetBundles containing assets that reference a changed asset must also be rebuilt."

### 默认不勾，99% 情况不需要动

只在**确定遇到 bundle 缓存冲突 bug** 时才打开。日常保持默认。

---

## 8.8 依赖变更的连锁反应

官方原话：
> "If you modify any of the assets in this group, when a dependency is changed the entire dependency tree needs to be rebuilt."

**实例**：
```
TextureX.png  (在 Group_Texture)
    ↑ 被依赖
Material_A    (在 Group_Material)
    ↑ 被依赖
Prefab_Hero   (在 Group_Prefab)
```

改了 `TextureX.png`：
- `Group_Texture` 里的 bundle 要重打
- `Group_Material` 里**只要引用了 TextureX 的资源**所在的 bundle 也要重打
- `Group_Prefab` 里同理

**结果**：一张贴图动一下，三个 Group 的 bundle 都要重打，下载体积膨胀。

**规避**：把"经常变"的资源和"不经常变"的资源**分 Group**。低频变更的贴图单独 Group + Static Content，别和高频变的 prefab 放一起。

---

## 8.9 Nova 项目的建议配置

根据你现状（单 Profile、有 remoteGroup、Build Remote Catalog 已勾），建议：

### 现阶段（本地学习）
- Profile：Default 保持不动，所有资源都在 Default 这组路径下
- Build：只跑 `New Build`，不用 Update Build（还没上 CDN）
- `m_BuildRemoteCatalog: 1` 已开，没问题

### 真上 CDN 前的准备
1. 建 `Production` Profile，改 `Remote.LoadPath` 指向真 CDN
2. 设计 Group 划分：
   ```
   ├─ Local_Preload               Static Content     Local        ← 启动必需
   ├─ Local_Default               Static Content     Local        ← 打包进 APK
   ├─ Remote_Static_Art           Static Content     Remote       ← 不常变的美术
   ├─ Remote_UI_Dynamic           Can Change         Remote       ← 高频变的 UI
   └─ Remote_Config               Can Change         Remote       ← 配置表
   ```
3. 发 v1.0.0 前：
   - 切到 Production Profile
   - New Build
   - **保存 `.bin` 到 git**（比如 `Publishing/v1.0.0/Android/addressables_content_state.bin`）
   - 上传 ServerData 到 CDN

### 日常热更流程
1. 改资源 → Check for Content Update Restrictions → Update a Previous Build
2. 增量上传到 CDN

---

## 8.10 Player Build 与 Addressables Build 的同步

Ch.4 提过这个问题：`m_BuildAddressablesWithPlayerBuild` 的三种模式：

| 值 | 含义 |
|----|------|
| `Use Global Settings (Currently Disabled)` | 用户项目设置控制 |
| `Build Addressables content on Player Build` | 每次 Build Player 前自动跑 Addressables Build |
| `Do Not Build Addressables Content on Player build` | Build Player 前**不**跑 Addressables Build |

**建议**：`Build Addressables content on Player Build`，防止你改了资源后忘了先跑 Addressables Build 就打 APK，导致 APK 里是旧 bundle。

**例外**：CI/CD 环境里你可能想分两步执行，那就用 "Do Not" + 脚本显式调用 `AddressableAssetSettings.BuildPlayerContent()`。

---

## 8.11 本章自测题

1. New Build 和 Update a Previous Build 的分工是什么？一个发版周期内各跑几次？
2. `addressables_content_state.bin` 的作用是什么？为什么必须保存？
3. Static Content 和 Can Change Post Release 的 Update Build 行为差异？
4. 为什么不要在两次 New Build 之间改 Group 的 Update Restriction？
5. `Build Remote Catalog` 没勾会怎样？你的项目里开了吗？
6. `Player Version Override` 为什么不能填死？
7. `Check for Content Update Restrictions` 工具做了什么？什么时候跑？
8. 依赖变更（比如一张贴图）会触发哪些 bundle 重打？
9. Update Build 产生的差分 bundle 上传时要**覆盖**还是**合并**到 CDN？

---

## 8.12 Ch.8 关键洞察

**一句话版**：
> Addressables 的热更本质 = 客户端拿**发版时 bin 的 hash** 去对比**CDN 上新 Catalog 的 hash**，变了就拉新 bundle；Static/Can Change 决定"变了怎么重打"；`.bin` 丢了就没法热更。

---

**Ch.8 结束**。构建流程的理论讲完，下一章开始**实战 CDN**。

**Ch.9 CDN 部署流程**：
- 本地跑 Unity Hosting Services 模拟 CDN
- Profile 切到本地 HTTP 端口
- `CheckForCatalogUpdates` / `UpdateCatalogs` / `DownloadDependenciesAsync` 三段式代码
- 进度监听 + 错误处理 + 缓存清理
- 真 CDN 部署注意点（HTTPS、CORS、缓存策略）

继续 Ch.9？

---

## 📌 用户：切换到下一章


资料齐了，开始 Ch.9。

---

# Ch.9 CDN 部署流程

**版本基准**：Addressables 2.9.1（依据 Unity 官方 2.9 手册）。
**章节目标**：建立完整的远程热更跑通路径——从本地模拟到真 CDN 的流程、运行时三段式更新代码、缓存机制、预下载。

---

## 9.1 整条链路全景

```
     ┌──────────────────────┐
     │ Unity Editor         │
     │                      │
     │ 1. New Build         │   ← 产出 Catalog + bundle
     │    产物到:           │
     │    ServerData/Android│
     │                      │
     │ 2. 上传到 CDN        │   ← 一次性动作
     └──────────────────────┘
                │
                ▼
     ┌──────────────────────┐
     │ CDN / HTTP Server    │
     │                      │
     │ catalog_xxx.json     │
     │ catalog_xxx.hash     │
     │ remote_bundle_a.bundle│
     │ remote_bundle_b.bundle│
     └──────────────────────┘
                │
                ▼
     ┌──────────────────────┐
     │ 客户端（真机）        │
     │                      │
     │ 启动 → InitializeAsync│
     │   自动或手动检查更新  │
     │ CheckForCatalogUpdates│
     │ UpdateCatalogs        │  → 拉新 Catalog
     │ GetDownloadSizeAsync  │  → 查体积
     │ DownloadDependenciesAsync → 下 bundle
     │ LoadAssetAsync        │  → 从缓存 bundle 取资源
     └──────────────────────┘
```

---

## 9.2 前置：启用远程内容构建

官方三步清单（Ch.8 做过一次，这里集中回顾）：

### 9.2.1 AddressableAssetSettings

打开 `AddressableAssetSettings.asset` Inspector：

| 字段 | 值 |
|------|---|
| **Build Remote Catalog** | ✓ 勾上（你项目已勾） |
| **Remote Catalog Build Path** | Remote（默认） |
| **Remote Catalog Load Path** | Remote（默认） |

### 9.2.2 Profile 配置 Remote 路径对

`Window → Asset Management → Addressables → Profiles`：

当前 Default Profile 的 Remote 是 `http://localhost/[BuildTarget]`（出厂默认，仅占位）。实际部署时要改：

| 环境 | Remote.LoadPath |
|------|----------------|
| 本地测试 | `http://localhost:8080/[BuildTarget]` |
| 测试 CDN | `https://test-cdn.example.com/[BuildTarget]` |
| 生产 CDN | `https://cdn.your-game.com/[BuildTarget]` |

`Remote.BuildPath` 一般不用改，保持 `ServerData/[BuildTarget]`。

### 9.2.3 Group 里设 Remote

每个走远程的 Group，在 Inspector 里把 **Build & Load Paths** 下拉选 `Remote`。

**`Include in Build` 这个字段很关键**：
- **勾上**：Group 的 bundle 会被**打进 APK**（即使是 Remote 路径也是先打进包）
- **不勾**：Group 的 bundle 只上 CDN，**不打进包**

远程热更场景基本都要**不勾**——资源全靠 CDN 下。

---

## 9.3 本地模拟 CDN：跑通流程不用真上线

**建议先在本地完整跑通一遍**，再上真 CDN。三种方式：

### 方式 A：Python HTTP 服务（最简单）

终端里到 `ServerData` 目录起一个 HTTP 服务：

```bash
cd <项目根>/ServerData  # 替换为你的项目根路径
python3 -m http.server 8080
```

Profile 里 `Remote.LoadPath` 写：

```
http://localhost:8080/[BuildTarget]
```

这样客户端请求 `http://localhost:8080/Android/xxx.bundle` 就能拿到本地文件。**效果和真 CDN 完全等价**。

### 方式 B：Unity 官方 Hosting Services

Groups 窗口 → 顶部 **Tools → Hosting Services**（2.9.1 里叫 `Asset Hosting Services`）。
- 添加一个 Local Hosting 服务
- 指定端口（默认 `http://localhost:1234`）
- 服务会自动读取 Profile 的 `RemoteBuildPath`

优点：Unity 内置、无需外部工具。
缺点：断开 Unity 就停了，重启 Editor 要手动开。

### 方式 C：Nginx / CDN 的 staging 环境

部署前的最后验证阶段用真 HTTP 服务。本章先跳过。

---

## 9.4 运行时代码：三段式更新流程

这是本章的**核心代码**。对齐 2.9.1 官方 API。

### 9.4.1 第一段：初始化 + 检查 Catalog

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class AddressableUpdater
{
    // ① 初始化（通常不用手动调，首次 Load* 会自动触发）
    public async UniTask InitializeAsync()
    {
        var initHandle = Addressables.InitializeAsync();
        await initHandle.Task;
        // InitializeAsync 的 handle 默认会 auto release，不用手动 Release
    }

    // ② 检查有没有新 Catalog
    public async UniTask<List<string>> CheckForUpdatesAsync()
    {
        var handle = Addressables.CheckForCatalogUpdates(autoReleaseHandle: false);
        await handle.Task;

        List<string> updatedLocators = handle.Result;   // 返回需要更新的 Catalog ID 列表
        Addressables.Release(handle);                   // 手动释放
        return updatedLocators;   // 空列表 = 没新内容
    }
}
```

**关键点**：
- `autoReleaseHandle: false` 是为了在 `Task` await 完成后还能读 `handle.Result`
- 返回的 `List<string>` **为空**就代表**没有更新**
- 官方推荐在**用户进入主界面前**跑这段

### 9.4.2 第二段：应用 Catalog 更新

```csharp
// ③ 把新 Catalog 下下来
public async UniTask UpdateCatalogsAsync(List<string> locatorsToUpdate)
{
    if (locatorsToUpdate == null || locatorsToUpdate.Count == 0)
        return;

    var handle = Addressables.UpdateCatalogs(
        catalogs: locatorsToUpdate,
        autoCleanBundleCache: true);   // 自动清理旧 bundle 缓存

    await handle.Task;

    if (handle.Status != AsyncOperationStatus.Succeeded)
    {
        Debug.LogError("Catalog update failed");
        Addressables.Release(handle);
        return;
    }

    // handle.Result 是 List<IResourceLocator>，更新后的 Catalog locator
    Addressables.Release(handle);
}
```

**`autoCleanBundleCache: true` 的作用**：
> "Enable the autoCleanBundleCache parameter in Addressables.UpdateCatalogs to automatically clean cache after catalog updates."

新 Catalog 生效后，本地那些**已经不在新 Catalog 里**的旧 bundle 缓存会被清掉——防止存储越积越多。**绝大多数场景都建议开**。

### 9.4.3 第三段：预下载 bundle

```csharp
// ④ 查要下多少字节
public async UniTask<long> GetDownloadSizeAsync(object key)
{
    var handle = Addressables.GetDownloadSizeAsync(key);
    await handle.Task;
    long bytes = handle.Result;   // 已缓存的 bundle 不计入
    Addressables.Release(handle);
    return bytes;
}

// ⑤ 预下载
public async UniTask DownloadAsync(object key, System.Action<float> onProgress)
{
    var handle = Addressables.DownloadDependenciesAsync(key);
    while (!handle.IsDone)
    {
        // PercentComplete 是 "完成了多少个子操作"，不是字节比例
        // GetDownloadStatus() 返回字节精确进度
        DownloadStatus status = handle.GetDownloadStatus();
        float progress = status.TotalBytes > 0
            ? (float)status.DownloadedBytes / status.TotalBytes
            : 0f;
        onProgress?.Invoke(progress);
        await UniTask.Yield();
    }

    if (handle.Status != AsyncOperationStatus.Succeeded)
    {
        Debug.LogError($"Download failed: {handle.OperationException}");
    }
    Addressables.Release(handle);
}
```

### 9.4.4 三段串起来的标准流程

```csharp
public class HotfixFlow : MonoBehaviour
{
    [SerializeField] private string m_PreloadLabel = "Preload";

    private AddressableUpdater m_Updater = new();

    private async UniTaskVoid Start()
    {
        // 1. 初始化
        await m_Updater.InitializeAsync();

        // 2. 检查 Catalog
        var locators = await m_Updater.CheckForUpdatesAsync();
        if (locators.Count > 0)
        {
            await m_Updater.UpdateCatalogsAsync(locators);
        }

        // 3. 查预下载体积
        long bytes = await m_Updater.GetDownloadSizeAsync(m_PreloadLabel);
        Debug.Log($"Need to download {bytes / 1024 / 1024} MB");

        // 4. 预下载
        await m_Updater.DownloadAsync(m_PreloadLabel,
            progress => Debug.Log($"Progress: {progress:P}"));

        // 5. 进游戏
        Debug.Log("Ready to enter game");
    }
}
```

---

## 9.5 关掉自动更新，改由你掌控时机

默认 Addressables 在 `InitializeAsync` 里会**自动**对比 Catalog hash。大部分游戏想**自己**在合适时机（"请用户确认下载"那个弹窗）才检查。

### 9.5.1 关闭自动更新

`AddressableAssetSettings` Inspector 里有个字段：

```
m_DisableCatalogUpdateOnStart: 1    ← 改成 1 就关闭启动时自动更新
```

Inspector 里对应字段名可能叫：
- `Only update catalogs manually`（老版本）
- `Disable Catalog Update on Startup`（新版本）

勾上之后，启动时 Addressables 初始化不会联网查 Catalog，完全由你手动调 `CheckForCatalogUpdates`。

### 9.5.2 标准应用场景

```csharp
// 带用户交互的更新流程
public async UniTask RunHotfixWithUserConfirm()
{
    var locators = await m_Updater.CheckForUpdatesAsync();
    if (locators.Count == 0)
    {
        EnterGame();
        return;
    }

    // 找出要下多少
    await m_Updater.UpdateCatalogsAsync(locators);
    long bytes = await m_Updater.GetDownloadSizeAsync(m_PreloadLabel);

    if (bytes > 0)
    {
        // 弹窗让用户确认
        bool confirmed = await UIConfirmDialog.Show(
            $"发现新内容，需要下载 {bytes / 1024 / 1024} MB。是否继续？");
        if (!confirmed)
        {
            Quit();
            return;
        }

        // 开始下载
        await m_Updater.DownloadAsync(m_PreloadLabel, UpdateProgressBar);
    }

    EnterGame();
}
```

---

## 9.6 缓存机制

### 9.6.1 缓存位置

官方：bundle 下载后缓存到设备本地。实际路径：

- Android: `/data/data/<packageName>/cache/UnityCache/Shared/<bundle_name>_<hash>/__data`
- iOS: `Library/Caches/UnityCache/...`
- 编辑器: `%LOCALAPPDATA%/Unity/cache/...` (Windows)

你不用知道具体路径，API 层面用 `UnityEngine.Caching` 或 `Addressables.CleanBundleCache` 操作。

### 9.6.2 Use Asset Bundle Cache（Group Schema）

每个 Group 的 Schema 里有 `Use Asset Bundle Cache` 开关（Ch.4 讲过）：
- **勾上**（默认）：下载后缓存到本地，下次启动命中缓存不重下
- **不勾**：每次都重下（基本没用，除非你就是要省设备存储）

**永远保持默认勾上**。

### 9.6.3 清理缓存的三种方式

| 方式 | 粒度 | 使用场景 |
|------|------|---------|
| `Addressables.ClearDependencyCacheAsync(key)` | 精确到 key（Label/Address） | 特定资源下新版 |
| `Addressables.CleanBundleCache(catalogIds)` | 移除当前 Catalog 里**没有**的旧 bundle | 手动清理历史垃圾 |
| `Caching.ClearCache()` | **全部** Unity 缓存（不止 Addressables） | 出厂重置、用户主动清理 |

```csharp
// 示例：精确清某个 Label 对应的 bundle
var h = Addressables.ClearDependencyCacheAsync("OldLevel", autoReleaseHandle: true);
await h.Task;
```

### 9.6.4 autoCleanBundleCache 自动清理

`UpdateCatalogs` 传 `autoCleanBundleCache: true` 时，会**自动**调用 `CleanBundleCache` 清掉新 Catalog 里不再需要的 bundle。**推荐默认开**。

---

## 9.7 bundle 的下载语义澄清

容易混淆的三层含义：

| 操作 | 实际做了什么 |
|------|-------------|
| `CheckForCatalogUpdates` | 只比对 Catalog hash，**不下载任何 bundle** |
| `UpdateCatalogs` | 下载新 Catalog，**不下载 bundle**，仅建立"本地知道有哪些新 bundle"的认知 |
| `DownloadDependenciesAsync` | 主动下载 bundle 文件到本地缓存，**不加载到内存** |
| `LoadAssetAsync` | 从缓存 bundle 加载资源到内存（缓存没有就同步下 bundle） |

**所以预下载可有可无**：不预下载，`LoadAssetAsync` 自己也会下 bundle。但没预下载的情况下，用户点击时会卡顿（下载时卡）。**大资源必预下载，小资源可以省**。

---

## 9.8 真 CDN 部署注意点

### 9.8.1 HTTPS 强制要求（移动端）

iOS（ATS）和部分 Android 新机型默认禁止明文 HTTP。**生产 CDN 必须用 HTTPS**。

### 9.8.2 CORS 限制（WebGL）

WebGL 跨域加载 bundle 必须 CDN 响应头带：
```
Access-Control-Allow-Origin: *
```

你这个项目以移动端为主，先不管。

### 9.8.3 缓存策略 / CDN 配置

Catalog 文件建议设**极短缓存时间**或 `no-cache`——不然玩家拿到的是 CDN 边缘节点的旧 Catalog。

```
catalog_*.json    → Cache-Control: no-cache
catalog_*.hash    → Cache-Control: no-cache
*.bundle         → Cache-Control: public, max-age=31536000  (永久，文件名带 hash 不怕冲突)
```

### 9.8.4 上传合并，不要清空

Update Build 产物和已有 CDN 内容**合并上传**，不要清空目录：
- 旧版本玩家可能还在拉旧 bundle
- Static Content Group 的旧 bundle 必须保留

### 9.8.5 多平台分目录

`ServerData/Android/`、`ServerData/iOS/` 分别对应不同平台的 CDN 目录。**不要混**。

### 9.8.6 灰度 / 回滚

做多个 Profile（`Prod_v1.0`、`Prod_v1.1`），Catalog 文件名里带版本标识（`Player Version Override`），不同 APK 取不同 Catalog。想回滚就让客户端回到老 Player Version。

---

## 9.9 `InternalIdTransformFunc`：动态 URL

官方提到：
> "If your remote URL structure can't be expressed statically, use the InternalIdTransformFunc approach to rewrite asset and AssetBundle URLs at runtime."

**场景**：
- CDN 签名 URL（每个请求都带时效 token）
- 根据用户地区路由到不同 CDN 域名
- 灰度分流

```csharp
void Awake()
{
    Addressables.InternalIdTransformFunc = location =>
    {
        string original = location.InternalId;
        if (original.StartsWith("https://cdn.your-game.com"))
        {
            // 改写 URL，比如加 token
            return original + "?token=" + GenerateToken();
        }
        return original;
    };
}
```

**必须在第一次 Load 之前注册**。这个钩子是 CDN 集成的万能逃生出口。

---

## 9.10 错误处理与重试

Addressables 自带基础重试（Schema 里的 `Retry Count`），但是上层容错要你自己做：

```csharp
public async UniTask DownloadWithRetry(object key, int maxRetry = 3)
{
    for (int i = 0; i < maxRetry; i++)
    {
        var handle = Addressables.DownloadDependenciesAsync(key);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(handle);
            return;
        }

        Debug.LogWarning($"Download failed (attempt {i + 1}): {handle.OperationException}");
        Addressables.Release(handle);

        await UniTask.Delay(System.TimeSpan.FromSeconds(2));   // 退避
    }

    throw new System.Exception($"Download failed after {maxRetry} retries: {key}");
}
```

**常见失败原因**：
- 网络断开 → 重试
- CDN 404（资源没上传全）→ 不应重试，要报错
- Catalog 和 bundle 版本不一致 → 说明上传 Catalog 时漏了 bundle，**运维问题**

---

## 9.11 完整 HotfixFlow 示例（对标 Nova 现有 Procedure）

把上面所有拼起来，给你一个可以直接塞进 Nova 的 Procedure 骨架：

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

public class ProcedureAddressableHotfix
{
    private const string PRELOAD_LABEL = "Preload";

    public async UniTask RunAsync(Action<string> onStatus, Action<float> onProgress)
    {
        onStatus("Initializing Addressables...");
        await Addressables.InitializeAsync().Task;

        onStatus("Checking for updates...");
        List<string> locators = await CheckCatalogs();

        if (locators.Count == 0)
        {
            onStatus("Up to date.");
            return;
        }

        onStatus("Applying catalog...");
        await UpdateCatalogsInternal(locators);

        long bytes = await GetSize(PRELOAD_LABEL);
        if (bytes == 0)
        {
            onStatus("All cached.");
            return;
        }

        onStatus($"Need {bytes / 1024 / 1024} MB");
        // 这里可以弹窗让用户确认

        await DownloadInternal(PRELOAD_LABEL, onProgress);
        onStatus("Hotfix done.");
    }

    private async UniTask<List<string>> CheckCatalogs()
    {
        var h = Addressables.CheckForCatalogUpdates(autoReleaseHandle: false);
        await h.Task;
        var result = h.Result ?? new List<string>();
        Addressables.Release(h);
        return result;
    }

    private async UniTask UpdateCatalogsInternal(List<string> locators)
    {
        var h = Addressables.UpdateCatalogs(locators, autoCleanBundleCache: true);
        await h.Task;
        Addressables.Release(h);
    }

    private async UniTask<long> GetSize(object key)
    {
        var h = Addressables.GetDownloadSizeAsync(key);
        await h.Task;
        long r = h.Result;
        Addressables.Release(h);
        return r;
    }

    private async UniTask DownloadInternal(object key, Action<float> onProgress)
    {
        var h = Addressables.DownloadDependenciesAsync(key);
        while (!h.IsDone)
        {
            var s = h.GetDownloadStatus();
            float p = s.TotalBytes > 0 ? (float)s.DownloadedBytes / s.TotalBytes : 0f;
            onProgress?.Invoke(p);
            await UniTask.Yield();
        }
        if (h.Status != AsyncOperationStatus.Succeeded)
            throw new Exception($"Download failed: {h.OperationException}");
        Addressables.Release(h);
    }
}
```

---

## 9.12 本章自测题

1. 启用远程内容构建需要改哪几个设置？
2. `Remote.LoadPath` 的 URL 要用 `[]` 还是 `{}` 包裹？为什么？
3. 本地模拟 CDN 最简单的方式是什么？
4. `CheckForCatalogUpdates` / `UpdateCatalogs` / `DownloadDependenciesAsync` 这三步分别做什么？是否都会真下 bundle？
5. `autoCleanBundleCache` 参数是什么意思？默认推荐开还是关？
6. 为什么 `PercentComplete` 和 `GetDownloadStatus()` 返回的"进度"含义不同？给用户看应该用哪个？
7. `InternalIdTransformFunc` 解决什么问题？什么时候必须注册？
8. Update Build 产物上传 CDN 时应该**合并**还是**覆盖旧文件**？为什么？
9. Catalog 文件在 CDN 上的缓存策略应该是什么？bundle 呢？

---

## 9.13 动手小任务（强烈建议做）

这是 Addressable 学习的分水岭——跑通一次真实的热更循环，后面所有概念就都"活"起来了。

### Step 1：本地起 HTTP 服务
```bash
cd <项目根>/ServerData  # 替换为你的项目根路径
python3 -m http.server 8080
```
浏览器打开 `http://localhost:8080/Android/`，能看到 bundle 列表就 OK。

### Step 2：改 Profile 的 Remote.LoadPath
打开 Profiles 窗口 → Default Profile → `Remote.LoadPath` 改成：
```
http://localhost:8080/[BuildTarget]
```

### Step 3：确保有一个 Remote Group
打开 Groups 窗口 → 你现有的 `remoteGroup`：
- Build & Load Paths = **Remote**
- Include in Build = **不勾**
- 放一个测试 prefab 进去，打个 Label 叫 `RemoteTest`

### Step 4：New Build
Groups 窗口 → Build → New Build → Default Build Script
确认 `ServerData/Android/` 下新生成了 bundle 和 catalog。

### Step 5：Play Mode 跑更新代码
写个测试 MonoBehaviour 跑 9.11 的 HotfixFlow，观察 Console：
- 有 "Checking for updates..." → "Up to date." 说明和 CDN Catalog 一致
- 改一下 prefab 再 `Update a Previous Build` → 重启客户端 → 应该有 "Need X MB"

跑通了就告诉我，没跑通贴 Console 错误。

---

## 9.14 本章小结

到此 Addressables 的**独立**体系讲完了。建议先做 Ch.9 的动手任务，**真的跑通一次本地热更**，把心智模型变成肌肉记忆。

---

## 💬 用户提问

> 我有2个问题
> 1.上传到CDN的内容（多个不同版本的内容）全部放到CDN的一个目录中吗？包括不同版本的catalog与bundle


这是个好问题，涉及到"CDN 目录如何组织多个发版 + 多次热更"的实战策略。先给结论，再拆清楚。

---

## 结论速记

**Addressables 原生只假设一个目录，多版

---

## 💬 用户提问

> [Request interrupted by user]


---

## 💬 用户提问

> 我有2个问题
> 1.上传到CDN的内容（多个不同版本的内容）全部放到CDN的一个目录中吗？包括不同版本的catalog与bundle
> 2.Addressables.CheckForCatalogUpdates它返回的list是什么信息？


两个都是关键问题，逐个展开。

---

# Q1：多版本的 CDN 目录组织

结论先行：**Addressables 原生假设一个平台一个目录**，多版本共存靠**文件名 hash 自然分流**，但**生产实践里通常会按 Player 版本再分子目录**，以便回滚和隔离。下面拆两种策略。

## 1.1 Addressables 默认行为：单平台单目录

**Addressables 的原生设计逻辑**：每个平台一个目录，所有版本的 bundle 共存在这个目录里，**靠文件名里的 hash 保证不冲突**。

默认路径：
```
ServerData/
  ├── Android/          ← 所有 Android 版本的产物都丢这
  └── iOS/              ← 所有 iOS 版本的产物都丢这
```

### 为什么能共存

还记得 Ch.4 讲过的 **Bundle Naming Mode**？推荐远程 Group 用 `Append Hash to Filename`：

```
v1.0.0 的 bundle：  remotegroup_assets_all_39fd4a45c91a5be7.bundle
v1.0.1 改了资源：   remotegroup_assets_all_8c91a3f4d12e7baa.bundle
```

**hash 在文件名里**，v1.0.0 和 v1.0.1 的 bundle 文件名就是不同的两个文件。CDN 目录里两者**并存**，老版本客户端拉旧 hash，新版本客户端拉新 hash，不会互相覆盖。

Catalog 也一样——`Player Version Override`（Ch.8 讲过）默认就是 `bundleVersion`：

```
v1.0.0:   catalog_1.0.0.json + catalog_1.0.0.hash
v1.0.1:   catalog_1.0.1.json + catalog_1.0.1.hash
```

v1.0.0 的 APK 只读 `catalog_1.0.0.*`，v1.0.1 的 APK 只读 `catalog_1.0.1.*`。

### 单目录策略的上传方式

**每次 Update Build 后，把新产物合并上传，不清空旧文件**：

```
（v1.0.0 发版后）
ServerData/Android/
  ├── catalog_1.0.0.json / .hash
  ├── remotegroup_..._39fd4a45.bundle
  └── ...

（v1.0.0.1 热更后 Update a Previous Build）
ServerData/Android/
  ├── catalog_1.0.0.json / .hash       ← 旧的，被覆盖更新为新内容
  ├── remotegroup_..._39fd4a45.bundle  ← 旧 bundle 保留（可能还有老客户端在拉）
  ├── remotegroup_..._8c91a3f4.bundle  ← 新增
  └── static_content_group_...bundle   ← Static Group 新增的差分 bundle
```

**注意**：`catalog_1.0.0.json` 和 `.hash` **会被覆盖**，内容是新的（指向了新增的 bundle）。所以"合并上传"针对的是 bundle，Catalog 是覆盖的。

### 这种策略的优缺点

**优点**：
- Addressables 原生支持，不用写任何自定义代码
- 客户端无感知，Profile 里 `Remote.LoadPath` 不用变

**缺点**：
- **回滚困难**——`catalog_1.0.0.hash` 被覆盖后，没法让客户端回到老 Catalog
- **目录会一直膨胀**，历史 bundle 永远删不掉（除非你确认所有老版本客户端都升级了）
- 生产出 bug 时，想让某批用户用老配置表救急，做不到

## 1.2 生产推荐：按版本分子目录

**实战里更常见**，用一个版本号维度把 CDN 目录切开：

```
https://cdn.your-game.com/
  └── Android/
      ├── 1.0.0/             ← v1.0.0 及其热更合计
      │   ├── catalog_1.0.0.json / .hash
      │   └── *.bundle
      ├── 1.0.1/             ← v1.0.1 及其热更
      │   ├── catalog_1.0.1.json / .hash
      │   └── *.bundle
      └── 1.1.0/
          └── ...
```

### 怎么实现：Profile 里加版本维度

Profile 的 `Remote.LoadPath` 和 `Remote.BuildPath` 用 `[UnityEditor.PlayerSettings.bundleVersion]` 插值：

```
Remote.BuildPath = ServerData/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]
Remote.LoadPath  = https://cdn.your-game.com/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]
```

- `[bundleVersion]` 打包时解析为当前 APK 版本号（`1.0.0`）
- 每次 `New Build` 产物自动落到对应版本子目录
- 客户端 APK 里写死的 Load URL 永远指向**自己的版本子目录**
- 不同版本的客户端**物理隔离**，互不影响

### 每个子目录里是什么

每个 `X.Y.Z/` 子目录是**一个独立的 Addressables "发版链"**：
- 基线：v1.0.0 的 New Build 产物
- 之后：v1.0.0 的历次 Update Build 产物（增量 bundle + 更新后的 Catalog）
- 合起来就是 "v1.0.0 这个 Player 版本的全部资源历史"

### 好处

1. **物理隔离**：v1.0.0 的资源和 v1.1.0 的资源完全分开，**不会互相污染**
2. **一键回滚**：CDN 上把 `catalog_1.0.0.hash` 恢复到昨天的备份，v1.0.0 客户端立刻回滚
3. **停服清理**：确认没有用户还在 v1.0.0 后，**整个 `1.0.0/` 目录可以删**，省 CDN 存储
4. **灰度可控**：想给部分用户推新 Catalog，只要改 `1.0.0/` 目录下的内容
5. **并行开发**：多个 Player 版本同时在做热更，不冲突

### 缺点

- **客户端 Load URL 里烧死了版本号**——APK 出厂那天起，这台设备永远从 `1.0.0/` 拉资源
- 跨 Player 版本升级必须走**应用商店**（不能用热更从 v1.0 切到 v2.0）
- 但这**正是想要的语义**——不同 Player 版本的代码不兼容，本来也不应共享资源

## 1.3 两种策略对比

| 维度 | 单目录 + hash 分流 | 按版本分子目录 |
|------|---------------------|---------------|
| 配置复杂度 | Addressables 默认 | Profile 改一个变量 |
| 多版本并存 | 依赖 hash 文件名，全混在一起 | 物理隔离 |
| 回滚能力 | 几乎无法 | 简单（换 Catalog 备份） |
| 存储成本 | 历史 bundle 永不清 | 老版本停服可删整目录 |
| 适用规模 | 小项目、内测 | 生产环境、长期运营 |

**Nova 建议**：**按版本分子目录**，未来想回滚、要停服老版本、多版本并行开发时不会后悔。

## 1.4 具体怎么改 Profile

等你真要上 CDN 时，Profile 变量应该长这样：

| 变量 | 值 |
|------|---|
| `Remote.BuildPath` | `ServerData/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]` |
| `Remote.LoadPath` | `https://cdn.your-game.com/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]` |

都用 `[]`——因为 `bundleVersion` 是打包时就确定的，烧进 Catalog。

**上传脚本只要照搬 `ServerData/Android/1.0.0/` 整个目录到 CDN 对应位置就行**。

---

# Q2：`CheckForCatalogUpdates` 返回什么

## 2.1 签名

```csharp
AsyncOperationHandle<List<string>> Addressables.CheckForCatalogUpdates(bool autoReleaseHandle = true);
```

返回 `AsyncOperationHandle<List<string>>`——`handle.Result` 是一个 **`List<string>`**。

## 2.2 List 里装的是什么

**List 里每个 string 是一个 "Locator ID"**。

**什么是 Locator**？Addressables 内部把每份 Catalog 包装成一个 `IResourceLocator`（资源定位器），每个 Locator 有自己的 ID。

项目启动后 Addressables 里通常有**两种 Locator**：
- **内置 Locator**：APK 里打包的 Catalog（出厂快照）。ID 通常是 `"AddressablesMainContentCatalog"` 这类固定字符串
- **远程 Locator**：从 CDN 拉到的 Catalog。ID 是 CDN 上那份 Catalog 的标识（可能是 Catalog 文件名或类似的唯一 ID）

`CheckForCatalogUpdates` 做的事：
1. 遍历所有已加载的 Locator
2. 对每个 Locator，去它对应的远程地址查 `.hash`
3. 如果远程 hash ≠ 本地 hash → **把这个 Locator 的 ID 加进返回 List**

## 2.3 返回值的三种情况

```csharp
var h = Addressables.CheckForCatalogUpdates(false);
await h.Task;
List<string> result = h.Result;

// 情况 1：result 是空 List
// 含义：所有 Catalog 都是最新的，没有任何更新可做

// 情况 2：result 有 1 个元素，比如 ["AddressablesMainContentCatalog"]
// 含义：主 Catalog 有更新

// 情况 3：result 有多个元素
// 含义：多个 Catalog 都有更新（多 Catalog 项目才有，你暂时不会遇到）
```

### 为什么是 List（支持多 Catalog）

Addressables 支持**同时加载多个 Catalog**，每个 Catalog 对应一份独立的资源清单。常见场景：

- **主 Catalog** + **DLC Catalog**：DLC 单独一个 Catalog，可以独立更新
- **跨项目资源**：引用别的 Unity 项目构建的 Catalog
- **多 Mod 支持**：每个 Mod 一个 Catalog

你 Nova 项目目前只有主 Catalog，所以这个 List **要么是空，要么只有一个元素**。

## 2.4 这个 List 怎么用

直接传给 `UpdateCatalogs` 就行：

```csharp
var checkH = Addressables.CheckForCatalogUpdates(false);
await checkH.Task;
List<string> toUpdate = checkH.Result;
Addressables.Release(checkH);

if (toUpdate.Count > 0)
{
    // 把有更新的 Locator ID 列表传给 UpdateCatalogs
    var updateH = Addressables.UpdateCatalogs(toUpdate, autoCleanBundleCache: true);
    await updateH.Task;
    Addressables.Release(updateH);
}
```

**`UpdateCatalogs` 的 `catalogs` 参数**：
- 传 `null` → 更新所有有变化的 Catalog（内部自己调 `CheckForCatalogUpdates`）
- 传具体 List → 只更新指定的 Catalog

`CheckForCatalogUpdates` 的价值就是**把"要不要更新"和"真的去更新"拆成两步**，中间可以插入用户确认、体积查询等自定义逻辑。

## 2.5 你可能关心的一个细节

> **"ID 是什么字符串？我能打出来看吗？"**

能，直接打 Log：

```csharp
foreach (var id in result)
    Debug.Log($"Catalog needs update: {id}");
```

实际 ID 的形式依赖 Addressables 内部，你**不用解析**这个字符串——它是黑盒 ID，唯一用途就是原样回传给 `UpdateCatalogs`。

---

## 两个问题小结

| Q | 答 |
|---|---|
| **Q1** 多版本放一个目录吗 | 原生支持单目录靠 hash 分流，但**生产强烈建议按 `bundleVersion` 分子目录**。Profile 里 `Remote.BuildPath` / `Remote.LoadPath` 加 `/[UnityEditor.PlayerSettings.bundleVersion]` 即可 |
| **Q2** `CheckForCatalogUpdates` 返回什么 | `List<string>`，每个 string 是**有更新的 Locator ID**。空 List = 没更新。直接原样喂给 `UpdateCatalogs` |

---

---

## 整体回顾

九章走完了**Addressables 从零到 CDN 的完整链路**：

**第一部分｜概念地基**
- Ch.1 为什么需要 Addressable（Resources / AB / Addressable 三代演进）
- Ch.2 七个核心名词（Address / Label / Group / Schema / Profile / Catalog / AssetReference）
- Ch.3 生命周期与引用计数（Load/Release 铁律、三种泄漏场景）

**第二部分｜编辑器操作面**
- Ch.4 Groups 窗口全解（BundledAssetGroupSchema / ContentUpdateGroupSchema 每个字段）
- Ch.5 Profiles 与路径变量（`[]` 打包时 / `{}` 运行时、多环境切换）

**第三部分｜运行时 API**
- Ch.6 加载 API 全景（五个核心方法 + WaitForCompletion 的坑）
- Ch.7 AssetReference 家族（Inspector 拖拽 + 按需加载）

**第四部分｜构建与热更**
- Ch.8 Content Build 与 Update Build（两种构建分工、.bin 状态文件、Static vs Can Change）
- Ch.9 CDN 部署流程（三段式更新代码、缓存机制、真 CDN 注意点）

---

## 留下的"可以随时回来补"清单

未完成但有价值的主题，按需再找我：

- **Nova 整合** —— 真要动 `IAssetLoadManager` / Procedure 链路 / HybridCLR 协同时再聊，需求到了讨论最具体
- **Addressables Report / Analyze** —— 想诊断"哪个 bundle 装了什么、有没有重复资源"时看
- **Scriptable Build Pipeline 深入** —— 自定义构建脚本、打包钩子
- **Cloud Content Delivery (CCD)** —— Unity 官方 CDN 服务，想白嫖托管时看
- **多 Catalog / 跨项目共享资源** —— DLC / Mod 系统需要时
- **Build 脚本自动化** —— CI 里一键跑 `AddressableAssetSettings.BuildPlayerContent()` 之类

---

## 最后的建议（不强制）

**在真正做 Nova 整合前，至少把 Ch.9 的动手任务跑一次**：

```bash
cd <项目根>/ServerData  # 替换为你的项目根路径
python3 -m http.server 8080
```

然后 Profile 改 `Remote.LoadPath = http://localhost:8080/[BuildTarget]`，跑一次 New Build，用 9.11 的 `HotfixFlow` 代码真的做一次"本地 CDN 热更"。

**跑通这一次，所有理论就"活"了**。将来真上 Nova 时不会再有"这步到底在干嘛"的疑问。

---

学习告一段落。之后想继续、想问具体实操、或者做 Nova 整合时，随时找我。
