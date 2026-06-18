---
id: PAT-42
title: 命名具体化 / 去抽象 / 去重复 三原则
type: pattern
status: active
date: 2026-05-19
summary: 命名具体化去抽象去类型重复三原则
category: naming
aliases:
  - PAT-42-naming-concrete-deduplicate
keywords:
  - PAT-42
  - PAT-42-naming-concrete-deduplicate
  - 命名具体化 / 去抽象 / 去重复 三原则
tags:
  - pattern
  - naming
  - methodology
related:
  - "[[PAT-04-read-what-you-change|PAT-04]]"
---

# PAT-42：命名具体化 / 去抽象 / 去重复 三原则

## 适用场景（When）

- 给新增类 / 接口 / 方法 / 字段命名时
- 重构既有命名时
- code-reviewer 审查别人代码遇到名字"语义模糊"时
- 跨会话 / 跨模块讨论时反复出现"那个 XX"指代不清时

## 核心做法（What & How）

### 原则 1：具体优先（Concrete > Abstract）

**抽象动词不能描述实际行为时，必须替换为具体动词。**

| 不好 | 好 | 原因 |
|---|---|---|
| `PrepareAsync` | `LoadManifestAsync` | "Prepare" 没说做了什么；"LoadManifest" 直接告诉读者本方法加载清单 |
| `OptionalUpdate` | `RecommendedDownload` | "Optional" 太弱，读不出语义；"RecommendedDownload" 能直接读出是启动期推荐更新下载提示 |
| `HandleAsync` / `ProcessAsync` | 看实际动作命名 | 这两词等于没命名 |

**判定法**：把方法名读出来回答"这个方法干了什么"。如果还需要看注释/实现，名字就不够具体。

### 原则 2：去历史残留（Drop Stale Prefixes）

**字段名带"目前是 X 但语义已变"的痕迹时，立即重命名。**

| 不好 | 好 | 历史 |
|---|---|---|
| `m_PreparedPackages` | `m_ManifestLoadedPackages` | 字段语义=已完成清单加载的包；旧名"Prepared"是 PrepareAsync 时代的残留 |
| `LaunchCheckResult` | （删除）→ `AppVersionResult` | "Check"动词太宽，不知道检查的是什么 |

**触发信号**：当方法/类被改名时，**关联字段、变量、文件名也要同步改**。

### 原则 3：去重复（Deduplicate Within Scope）

**类型已声明 X，方法名内不再重复 X。**

| 不好 | 好 | 原因 |
|---|---|---|
| `IAppManager.DownloadAppAsync` | `DownloadAsync` | 已经是 AppManager，"App"是冗余 |
| `IAssetManager.LoadAssetAsync` | `LoadAsync` (or `LoadAssetAsync` 若有歧义) | 视上下文 |
| `UserService.GetUserById` | `GetById` | 类已带 User |

**例外**：当作用域内有多个类似方法（如 `LoadAsset` / `LoadScene`）需要区分时保留前缀。

## 为什么这么做（Why）

1. **可读性 > 历史延续**：代码会被读 N 次，写一次。命名优化的成本一次性，可读性收益终身受用。
2. **新人理解成本**：抽象名词需要查实现才知道做啥；具体名直接表达意图。
3. **跨模块沟通**：开发会议指代某方法时，具体名能减少歧义（"那个 Prepare" vs "那个 LoadManifest"）。
4. **重构防止信号丢失**：保留历史残留名等于把"前提已变"这个信号埋进代码，下个改动者会被误导。

## 反模式（Anti-patterns）

```csharp
// ❌ 抽象动词
public UniTask PrepareAsync(string package);
public UniTask HandleRequestAsync(Request req);
public UniTask DoUpdateAsync();

// ❌ 类型重复
class AppManager { void DownloadAppAsync(); }
class UserService { User GetUserById(int id); }

// ❌ 历史残留
class AssetManager
{
    // 字段名锁死在 "Prepare" 时代，但 PrepareAsync 已改成 LoadManifestAsync
    private HashSet<string> m_PreparedPackages;
}
```

```csharp
// ✅ 具体 + 去重复 + 名实一致
public UniTask LoadManifestAsync(string package);
public UniTask<HttpResponse> SendAsync(HttpRequest req);
public UniTask UpdateScoreAsync();

class AppManager
{
    UniTask DownloadAsync();
    private HashSet<string> m_ManifestLoadedPackages;
}
```

## 跨项目复用提示

完全可复用。三原则不依赖 Unity / Nova 任何特性，是通用 OO 命名实践。
