---
id: PAT-33
title: 新增 SDK Plugin 的 6 步 SOP
type: pattern
status: active
date: 2026-05-18
summary: SDK Plugin SOP UPM包加ISDKPlugin
category: module
aliases:
  - sdk-plugin-sop
  - new-sdk-plugin
keywords: [PAT-33, new-sdk-plugin, sdk-plugin-sop, 新增 SDK Plugin 的 6 步 SOP]
tags:
  - pattern
  - nova
  - framework
  - sdk
  - sop
related:
  - "[[ADR-022-sdk-plugin-architecture]]"
  - "[[PAT-32-runtime-module-sop]]"
  - "[[PAT-27-config-no-serialize]]"
---

# PAT-33：新增 SDK Plugin 的 6 步 SOP

## 适用场景

- 接入新的第三方 SDK（埋点 / 广告 / 支付 / 推送 / 远程配置 / 账号登录等）
- 评审 SDK 接入 PR 或拆分 / 合并 Plugin 时的对照表

## 6 步 SOP

```text
1. 创建独立 UPM 包 com.nova.sdk.<vendor>
     └─ asmdef 引用 NovaFramework.Runtime
        （子包之间零依赖；禁止反向引用其他子包）

2. 定义 Config（非 MonoBehaviour、非 ScriptableObject）
     public sealed class MyConfig : ISDKPluginConfig
     {
         public string AppId;
         public string Secret;
     }

3. 实现 Plugin（继承 SDKPluginBase 或 AdPluginBase，纯 C# 类）
     public sealed class MyPlugin : SDKPluginBase, ITrackPlugin
     {
         public override string Name     => "MyVendor";
         public override int    Priority => 50;
         protected override Type ConfigType => typeof(MyConfig);

         protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
         {
             var cfg = (MyConfig)config;
             // Native 调用可在任意线程，完成前必须切回主线程
             await MyNativeSdk.InitAsync(cfg.AppId, ct);
             await UniTask.SwitchToMainThread(ct);
         }

         protected override UniTask OnDisposeAsync(CancellationToken ct) => UniTask.CompletedTask;

         // 业务接口实现（ITrackPlugin / IAdPlugin / ...）
         public void TrackEvent(TrackEvent evt) { ... }
     }

4. 在使用方项目的 SDKComponent Inspector 勾选 MyPlugin → 设置 Priority
     （Inspector 自动反射扫描 ISDKPlugin 实现类型；新装 UPM 包自动 append Entry，默认 disabled）

5. Bootstrap 物料注入 + 统一初始化（业务层）
     Nova.SDK.SetConfig<MyPlugin>(new MyConfig { AppId = "..." });
     await Nova.SDK.InitializeAsync();
     // SetConfig 必须在 InitializeAsync 之前；之后调用 Log.Warning + 忽略

6. 业务侧使用
     Nova.SDK.Get<MyPlugin>().TrackEvent(...);
     // 接口扇出：Nova.SDK.GetAll<IMonetizeTrackPlugin>() 遍历埋点
```

## 引申约束

- **Plugin 形态**：必须是纯 C# 类，**禁止** `: MonoBehaviour`、禁止挂 Prefab、禁止持有场景对象（详 [[ADR-022-sdk-plugin-architecture]] 决策 4）
- **Priority 语义**：值越小越先 Await 提交；同批内 `UniTask.WhenAll` 并行；Plugin 之间不存在拓扑依赖时按数值经验排（埋点 10 / 归因 20 / 远程配置 50 / 广告 100）
- **ConfigType 声明**：声明后未 SetConfig 必抛 `SDKConfigMissingException`（失败隔离不影响其他 Plugin）；不需要配置的 Plugin 返回 `null` 即可
- **多接口实现**：一个 Plugin 可同时实现多个业务接口（如 `FirebasePlugin : IMonetizeTrackPlugin, IPushPlugin, IRemoteConfigPlugin`）；序列化层一个 Type 一条 Entry，查询层 `GetAll<I>` 自动扇出
- **失败处理**：`InitializeAsync` 内部不要 `try/catch + return`；让异常抛出，由 SDKManager 统一捕获置 `IsAvailable=false`
- **主线程契约**：Plugin 完成点（UniTask continuation）+ Event/Action 触发前必须 `await UniTask.SwitchToMainThread(ct)`（详 [[ADR-022-sdk-plugin-architecture]] 决策 6）

## 反模式

- ❌ 在 Plugin `: MonoBehaviour` 上挂 GameObject（违反 ADR 决策 4，热重载/单测失效）
- ❌ Plugin Inspector 直填 AppID/Secret（机密入 Git）
- ❌ `SetConfig` 在 `InitializeAsync` 之后调用并依赖其生效（已被 Log.Warning 忽略）
- ❌ Plugin 内部 catch 异常不抛、自行降级处理 → 框架失去失败隔离观测能力，应该让异常抛出
- ❌ 子 UPM 包之间互相引用（违反 ADR 决策 3）

## 完整伪代码示例（TGATrackPlugin）

```csharp
// com.nova.sdk.tga 包内
public sealed class TGAConfig : ISDKPluginConfig
{
    public string AppId;
    public string ServerUrl;
}

public sealed class TGATrackPlugin : SDKPluginBase, ITrackPlugin
{
    public override string Name => "ThinkingAnalytics";
    public override int Priority => 10;
    protected override Type ConfigType => typeof(TGAConfig);

    protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
    {
        var cfg = (TGAConfig)config;
        await UniTask.RunOnThreadPool(() => TDAnalytics.Init(cfg.AppId, cfg.ServerUrl), cancellationToken: ct);
        await UniTask.SwitchToMainThread(ct);
    }

    protected override UniTask OnDisposeAsync(CancellationToken ct) => UniTask.CompletedTask;

    public void TrackEvent(TrackEvent evt) => TDAnalytics.Track(evt.Name, evt.Parameters);
}
```

---
