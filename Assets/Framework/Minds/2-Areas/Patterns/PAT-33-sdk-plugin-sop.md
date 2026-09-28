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

4. 在 ConfigMaster 当前坐标启用 MyConfig，并导出 ConfigRuntime
     （SDKComponent 的 PluginEntries 只保留 Inspector 元数据，不决定运行时启用）

5. 声明真正的能力依赖并等待所需插件
     // 需要其他能力时实现 ISDKInitializationDependencies
     await Nova.SDK.WaitForPluginAsync<MyPlugin>();
     // InitializeTask 仍表示所有已启用插件得出最终状态

6. 业务侧使用
     Nova.SDK.Get<MyPlugin>().TrackEvent(...);
     // 接口扇出：Nova.SDK.GetAll<IMonetizeTrackPlugin>() 遍历埋点
```

## 引申约束

- **Plugin 形态**：必须是纯 C# 类，**禁止** `: MonoBehaviour`、禁止挂 Prefab、禁止持有场景对象（详 [[ADR-022-sdk-plugin-architecture]] 决策 4）
- **Priority 语义**：只控制稳定遍历与释放顺序；插件间先后由 `ISDKInitializationDependencies` 声明框架能力依赖，其他插件并发启动。依赖缺失、重复提供者和循环由 Manager 阻止受影响插件。
- **ConfigType 声明**：优先继承 `PluginBase<TConfig>`，特殊情况使用 `SDKPluginConfigTypeAttribute`，以便 Manager 在构造前匹配 ConfigMaster 已启用配置；取不到配置时插件状态为 `Failed`。
- **多接口实现**：一个 Plugin 可同时实现多个业务接口（如 `FirebasePlugin : IMonetizeTrackPlugin, IPushPlugin, IRemoteConfigPlugin`）；序列化层一个 Type 一条 Entry，查询层 `GetAll<I>` 自动扇出
- **失败处理**：`InitializeAsync` 内部不要 `try/catch + return`；让异常抛出，由 SDKManager 统一捕获置 `IsAvailable=false`
- **主线程契约**：Plugin 完成点（UniTask continuation）+ Event/Action 触发前必须 `await UniTask.SwitchToMainThread(ct)`（详 [[ADR-022-sdk-plugin-architecture]] 决策 6）
- **启用与注入**：SDK 启用真相源为 `ConfigMaster.EnabledSDKs`；config 经 `IConfigManager` 注入，详 [[ADR-070-sdk-enable-via-configmaster-enabledsdks|ADR-070]]。
- **登录与切换账号**：需要 UID 的插件实现 `ISDKLoginReceiver`；需要业务属性的插件实现 `ISDKLoginContextReceiver`。异步结果核对 `sessionId`；业务登录调用 `Nova.SDK.Login(uid, properties)`，登出调用 `EndLoginSession()`。插件初始化早于或晚于登录都应取得最新账号。
- **第三方封装只读**：封装原厂 SDK 时对其源目录只读、文件完整搬入不增删改，详 [[PAT-141-vendor-source-readonly|PAT-141]]。

## 反模式

- ❌ 在 Plugin `: MonoBehaviour` 上挂 GameObject（违反 ADR 决策 4，热重载/单测失效）
- ❌ Plugin Inspector 直填 AppID/Secret（机密入 Git）
- ❌ 只在 `SDKComponent` 面板勾选插件，却没有在 ConfigMaster 启用并导出配置
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
