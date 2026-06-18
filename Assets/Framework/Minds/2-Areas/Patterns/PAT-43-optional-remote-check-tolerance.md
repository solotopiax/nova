---
id: PAT-43
title: 启动期"可选远端检查"的异常宽容策略
type: pattern
status: active
date: 2026-05-19
summary: 启动期可选远端检查异常宽容降级返回
category: hotfix
aliases:
  - PAT-43-optional-remote-check-tolerance
keywords:
  - PAT-43
  - PAT-43-optional-remote-check-tolerance
  - 启动期"可选远端检查"的异常宽容策略
tags:
  - pattern
  - hotupdate
  - error-handling
  - startup
related: []
---

# PAT-43：启动期"可选远端检查"的异常宽容策略

## 适用场景（When）

启动流程中存在"非阻断性远端检查"——检查通过就走优化路径，检查失败/未配置不能阻断主流程。例如：

- App 大版本检查（GM 后台）
- 灰度白名单查询
- 远端开关 / Feature Flag 拉取
- 公告版本号查询

**不适用**：阻断性远端依赖（必须成功才能继续，如 OAuth 登录、必需配置下发）。

## 核心做法（What & How）

### 第 1 步：约定降级返回值

为每个可选检查定义一个"安全降级值"，使下游路由跳过该检查的影响。

```csharp
// 例：AppVersionResult 三档
public enum AppVersionResult { NoDownload, RecommendedDownload, ForcedDownload }

// 安全降级 = NoDownload（既不强弹窗也不推荐弹窗，主流程正常推进）
```

### 第 2 步：分类异常 + 分级日志 + 统一降级

| 异常类别 | 日志级别 | 处理 |
|---|---|---|
| URL 未配置 | `Log.Warning` | return 降级值 |
| 网络异常 / 超时 / HTTP 非 2xx | `Log.Warning` | return 降级值（弱网正常情况，不刷屏 Error） |
| 响应 JSON 解析失败 | `Log.Error` | return 降级值（服务端协议错误，需运维关注） |
| 业务逻辑异常（字段缺失等） | `Log.Error` | return 降级值 |

```csharp
public async UniTask<AppVersionResult> CheckAsync(CancellationToken ct)
{
    if (string.IsNullOrEmpty(m_Config.AppDownloadCheckUrl))
    {
        Log.Warning(LogTag.App, "AppDownloadCheckUrl 未配置，跳过版本检查。");
        return AppVersionResult.NoDownload;
    }

    try
    {
        var resp = await m_HttpManager.GetAsync(url, m_Config.TimeoutSeconds);
        var body = Util.Json.Deserialize<AppVersionResponse>(resp.Body);
        return Resolve(body);
    }
    catch (HttpException ex)
    {
        Log.Warning(LogTag.App, "App 版本检查网络异常: {0}", ex.Message);
        return AppVersionResult.NoDownload;
    }
    catch (Exception ex)  // JSON / 业务
    {
        Log.Error(LogTag.App, "App 版本检查解析异常: {0}", ex.Message);
        return AppVersionResult.NoDownload;
    }
}
```

### 第 3 步：编排层不**短路**绕过后续检查

可选检查降级**不**意味着"跳过整段编排"。下游的资源差异检查、DLL 加载等步骤继续执行。

```csharp
// ❌ 反例：URL 未配置直接 return SkipRequired，绕过资源差异检查
if (string.IsNullOrEmpty(url)) return LaunchCheckResult.SkipRequired;

// ✅ 正例：降级到 NoDownload，编排层继续走 LoadManifestAsync + HasPatchAsync
if (string.IsNullOrEmpty(url)) {
    Log.Warning(...);
    return AppVersionResult.NoDownload;
}
```

## 为什么这么做（Why）

### 历史踩坑（本会话直接触发）

`LaunchManager.CheckAsync` 旧实现：URL 未配置 → 返回 `SkipRequired` → 编排层把 `SkipRequired` 解读为"整个版本检查都跳过"→ **资源差异检查也被跳过**。结果：HostPlayMode 下 CDN 已有 989 版本资源补丁，但客户端启动后直接走 LoadDll，热更补丁永远触发不了。

### 三层动机

1. **容错优先**：启动期失败成本极高（用户黑屏退出），任何可选检查不能拖死主流程。
2. **日志分级反映运维意图**：弱网是用户问题（Warning），JSON 解析失败是服务端协议问题（Error），运维侧能按级别过滤告警。
3. **职责单一**：CheckAsync 只回答"App 是否要更新"，不替编排层决定"接下来跳哪个 Procedure"——后者是 ProcedureCheckVersion 的事。

## 反模式（Anti-patterns）

```csharp
// ❌ 短路绕过下游
if (urlEmpty) return SkipRequired;

// ❌ 异常向上抛进编排层（破坏宽容契约）
public UniTask<AppVersionResult> CheckAsync(CancellationToken ct)
{
    var resp = await http.GetAsync(url);  // 网络异常向上抛
    return Resolve(resp);
}

// ❌ 用 try-catch 但日志全 Error（弱网刷屏）
catch (HttpException) { Log.Error(...); }   // 弱网频繁 Error 会污染告警通道

// ❌ 静默吞掉异常
catch { return NoDownload; }                  // 没日志 = 永远查不到根因
```

## 跨项目复用提示

可直接复用到任何"启动期可选远端检查"。三步法（降级值 / 分级日志 / 不短路绕过）是通用模板。需要按项目调整的部分：

- 降级值的具体类型
- HTTP / 解析层的异常类别（与基础设施绑定）
- 日志 Tag
