# MaxAdPluginBuildProcessor

**类签名**：`public sealed class MaxAdPluginBuildProcessor : NovaSDKBuildProcessor`
**命名空间**：`NovaFramework.SDK.MaxAdPlugin.Editor`
**全局访问**：由 `NovaBuildPreprocessor` 与 `NovaBuildPostprocessor` 通过共享发现入口收集处理器，并按优先级分派平台回调，无需业务层显式调用。

构建预处理器：从 `ConfigRuntimeSO → AdPluginConfig → MaxAdChannelConfig` 读取 `AppKey` 与 `AdMobAppIdAndroid/IOS`，在 Android/iOS 构建预处理回调中写入官方 AppLovin UPM 包提供的 `AppLovinSettings.asset`；Android 预处理会向 `NovaBuildContext` 注册 MAX 相关 ProGuard 规则，保护 AndroidX Startup / WorkManager / Room 在开启 Minify 时的启动期反射入口；iOS 后处理由基类 `NovaSDKBuildProcessor` 通过 `GetEmbedXcframeworkNames` 名单托管，查找并 Embed 本次 Pods 中实际存在的 5 项候选 xcframework；随后离线枚举当前已注册官方 MAX adapter / DSP 网络，再联网请求 ID 并增量补齐 `SKAdNetworkItems`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs` | `MaxAdPluginBuildProcessor` | 全部定义 |
| `Nova/Scripts/Editor/BuildProcessor/MaxSkAdNetworkPlistCompleter.cs` | `MaxSkAdNetworkPlistCompleter` | 离线枚举网络，联网获取并增量补齐 SKAdNetwork ID |
| `Nova/Scripts/Editor/BuildProcessor/MaxIOSPodOwnershipPostprocessor.cs` | `MaxIOSPodOwnershipPostprocessor` | 顺序 44 统一 MAX iOS 原生依赖归属 |
| `Nova/Scripts/Editor/BuildProcessor/MaxIOSPodOwnership.rb` | 固定 Ruby 脚本，无自定义函数 | 使用 CocoaPods 的 `xcodeproj` 公开对象 API 定向回写生成工程 |

---

## §5 完整公开 API

```csharp
public sealed class MaxAdPluginBuildProcessor : NovaSDKBuildProcessor
{
    /// 预处理回调优先级，固定 600；值越小越早执行。
    public override int PreprocessPriority { get; }

    /// 后处理回调优先级，固定 600。
    public override int PostprocessPriority { get; }

    /// Android 预处理：注册 MAX 相关 ProGuard 规则，并从 MaxAdChannelConfig 读取 AppKey + AdMobAppIdAndroid 写入 AppLovinSettings.asset。
    public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context);

    /// iOS 预处理：从 MaxAdChannelConfig 读取 AppKey + AdMobAppIdIOS 写入 AppLovinSettings.asset。
    public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context);

    /// iOS 后处理：先执行基类 Embed，再离线枚举网络、联网补齐 SKAdNetworkItems。
    public override void OnPostprocessBuildOniOS(BuildReport report, NovaBuildContext context);

    /// iOS 后处理 xcframework 候选名单：返回 5 个文件名，由基类对 Pods 中实际存在的文件执行 Embed & Sign。
    /// 仅在 #if UNITY_IOS 条件下编译。
    protected override string[] GetEmbedXcframeworkNames();
}
```

**xcframework Embed 查找名单（`OnPostprocessBuildOniOS` 处理，共 5 项候选）：**

| xcframework | 说明 |
|---|---|
| `DTBiOSSDK.xcframework` | Amazon TAM |
| `IASDKCore.xcframework` | IronSource Core |
| `OMSDK_Pubmatic.xcframework` | Open Measurement SDK（Pubmatic） |
| `OMSDK_Pubnativenet.xcframework` | Open Measurement SDK（Pubnativenet） |
| `OMSDK_Smaato.xcframework` | Open Measurement SDK（Smaato） |

---

## 构建前广告配置边界

构建注入读取已正式导出的 `ConfigRuntimeSO → AdPluginConfig → ChannelConfigs.Items`，不是 ConfigWindow 尚未导出的编辑数据。它使用列表中第一个 `MaxAdChannelConfig`，不检查该项 `Enabled`，也不校验 SDK Key、平台 App ID 的格式或非空。缺少 Ad 配置时直接跳过；缺少 MAX 项、原厂宏或 `AppLovinSettings.Instance` 时记录相应 Warning 并跳过。空值仍可能被写入官方设置，因此“构建注入日志已出现”不证明广告配置可用。

当前处理器不调用 `MaxAdGroupConfigValidator.Validate`，也不因组表达式、重复 ID 或跨格式冲突自动阻止构建。构建前应核对目标 Platform / Channel / DevelopMode 对应的正式 Config、SDK Key、平台 AdMob App ID、已安装 adapter 与 registry，并对 MAX 完整配置运行纯校验；无效格式会在运行时注册阶段报告配置失败并跳过。空全屏列表是合法空计划，Banner 每项只填真实单 ID。校验方法只返回诊断，不自动导出或写入构建结果，配置用法见[主文档](./MaxAdPlugin.md#广告组配置与原厂边界)。

MAX 渠道在运行时由当前聚合为每个新实例初始化一次，释放后不复用；SDKManager 的新初始化周期创建新聚合及渠道，业务重新获取接口和订阅事件。这一生命周期不由本构建处理器负责，运行时顺序见[共享释放与新生命周期](./MaxAdPlugin.md#共享释放与新生命周期)。CMP、隐私政策 URL、ATT 与 mediation 后台设置仍需项目配置，AppLovinSettings 注入不替代这些前置。

## §11 使用示例

业务层无需直接调用。完整配置流程：

1. 打开 Nova ConfigWindow → 广告聚合 → MAX 渠道；
2. 填写 `AppKey`、`AdMobAppIdAndroid`、`AdMobAppIdIOS`；
3. 按目标平台、渠道和开发模式正式导出 `ConfigRuntimeSO`，并核对完整 MAX 配置校验结果；
4. 触发 Unity Build（Android 或 iOS），处理器在预处理阶段自动把上述值写入 AppLovin 官方 SDK 的 `AppLovinSettings.asset`。官方 UPM 默认会在 `Assets/MaxSdk/Resources/AppLovinSettings.asset` 创建该资产。

成功执行设置回写时的 Debug 日志（Android）：

```
[MaxAdPluginBuildProcessor] Android AppLovinSettings 已注入：SdkKey 与 AdMob AppId 已写入 AppLovinSettings.asset。
```

Android 构建预处理还会注册以下 ProGuard 规则，最终由 Nova 构建链写入 `Assets/Plugins/Android/proguard-user.txt`：

```proguard
-keep class androidx.startup.** { *; }
-keep class androidx.work.** { *; }
-keep class * extends androidx.room.RoomDatabase { *; }
-keep class **_Impl { *; }
-dontwarn androidx.room.paging.**
```

iOS 后处理会在 Pods 中寻找名单里的 5 项候选 xcframework，将实际存在的文件 Embed 到 `Unity-iPhone` target，无需按名单手动补入不存在的文件。实际存在的文件取决于本次已安装的 mediation 网络，名单本身不是本包已声明了这 5 个网络依赖的证明。

### SKAdNetwork 网络与请求边界

本处理在 AppLovin 的 plist 写入之后执行。`PackageInfo.GetAllRegisteredPackages()` 本地枚举 adapter / DSP 包的 `dir:` 关键字，必要时从其磁盘 `package.json` 读取 keywords，避开原厂 `Client.List()` 的等待超时；这一部分不请求 registry。完整 ID 清单仍通过 `https://unity.applovin.com/max/1.0/skadnetwork_ids?ad_networks=...` 同步请求，当前 HTTP 超时为 60 秒，不能称为全流程离线补全。

已有 `SKAdNetworkItems` 只增不删且按 ID 去重。缺少 plist、未发现网络、请求失败、响应为空或解析异常会 Warning 并保留原厂已有内容；这些分支不主动令构建失败，也不能证明 plist 完整。成功日志包含合并前后数量、新增数、网络数与接口返回数，不能把某个历史固定条数作为所有项目的验收值。构建验证需按本次实际安装网络核对输出 plist 和客户端请求结果。

## iOS 原生依赖归属

包安装后，官方 adapter 的原生依赖就会参与构建，与是否配置 `MaxAdChannelConfig` 无关。本包的 `MaxIOSPodOwnershipPostprocessor` 在 `UNITY_IOS && NOVA_APPLOVIN_MAX` 条件下自动运行，用户仍使用现有 Player 构建流程，无需另开一个 Skill 或手动运行脚本。

EDM4U `1.2.188` 在顺序 `35` 写入 SPM、`40` 生成 Podfile、`50` 安装 Pods。Nova MAX 在 `44` 回写两项 adapter，Nova 部署下限在 `45` 保持既有 Podfile 规则；随后 `NovaBuildPostprocessor` 从修正后的工程读取上下文，继续常规后处理。

| 已审官方 UPM 包 | 被移除的直接 SPM repository/product | 恢复到 `UnityFramework` 的 Pod |
|---|---|---|
| `com.applovin.mediation.adapters.line.ios@3010102.0.0` | `AppLovin-MAX-Swift-Package-Line.git` / `AppLovinMediationLineAdapter` | `AppLovinMediationLineAdapter`, `3.1.1.2` |
| `com.applovin.mediation.adapters.unityads.ios@4200001.0.0` | `AppLovin-MAX-Swift-Package-UnityAds.git` / `AppLovinMediationUnityAdsAdapter` | `AppLovinMediationUnityAdsAdapter`, `4.20.0.1` |

两个 SPM adapter 会传递引入 `AppLovinSDK`，其他 MAX Pods 也会引入同名 framework。在当前混合图下，Xcode 会因同名 framework 的两个生产者报 `Multiple commands produce`；版本相同也不能消除双生产者。本处理只让 MAX 的两项 adapter 回到 CocoaPods，让 MAX 继续使用 Podfile 既有的 `AppLovinSDK` 版本；保留 Firebase SPM、其他 Pods、部署版本 block、其他 target 和配置，不关闭全局 SPM，不改官方 XML。

执行前会核对自身包身份、两个官方 UPM 版本、`Editor/Dependencies.xml` 的精确 Pod/SPM 契约、唯一 `UnityFramework` target、引用图与 Podfile。Podfile 中已存在且同版本的两项 Pod 会复用；重复执行不再写文件。未知 MAX SPM 仓库、版本冲突、未知引用图、缺失工具或文件会令 Unity 构建失败，修复直接原因后重新导出。脚本在内存中保留原始 `project.pbxproj` 与 Podfile 快照，已开始回写时发生普通异常，会恢复这两文件；`120000` 毫秒超时、进程被强制终止或系统中断无法保证执行恢复，不复用可能中断的生成工程，应重新导出。没有写入永久备份或修改项目真源。

固定 Ruby 脚本随本包正式携带。C# 复用 `EditorUtil.ProcessRunner.RunSync`，在当前 `pod` 子进程设置 `RUBYOPT=-r./MaxIOSPodOwnership.rb`，使用该 CocoaPods 启动器自己的 Ruby 与 gem 环境；脚本完成后在 CocoaPods CLI 前退出。包路径可以包含空格，不写死本机 Homebrew 路径，不改全局环境，不再次执行 `pod install`。脚本输入只有导出根目录与两个已解析官方 XML 路径，超时为 `120000` 毫秒。

验收须分清 Unity 导出和 Xcode 原生构建：导出成功后核对两项 Pod、保留的 SPM 图、`Podfile.lock` 和最终 `SKAdNetworkItems`；再实际编译链接。未做签名、设备安装或运行时，不能把原生编译成功当作真机验证。

### 验证边界

接入或升级时，须在目标工程核对实际配置及原生依赖输出，并执行 Android/iOS Player 构建、CocoaPods 回写与安装、Xcode 编译、签名安装及真机广告运行。固定版本、脚本恢复、HTTP 失败保留和日志说明属于源码契约，不能代替平台验收；结果在当次对话报告。

---

## §13 关联文档

- [`MaxAdPlugin.md`](./MaxAdPlugin.md) — MAX 渠道插件主文档
- [README](../../README.md#安装) — 固定 UPM 依赖、adapter 与 registry
- [构建预处理源码](../Scripts/Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs)、[SKAdNetwork 补齐源码](../Scripts/Editor/BuildProcessor/MaxSkAdNetworkPlistCompleter.cs)、[iOS 依赖归属源码](../Scripts/Editor/BuildProcessor/MaxIOSPodOwnershipPostprocessor.cs) — 当前构建事实依据
