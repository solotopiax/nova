# MaxAdPluginBuildProcessor

**类签名**：`public sealed class MaxAdPluginBuildProcessor : NovaSDKBuildProcessor`
**命名空间**：`NovaFramework.SDK.MaxAdPlugin.Editor`
**全局访问**：由 `NovaBuildProcessor` 反射自动发现并按优先级回调，无需业务层显式调用。

构建预处理器：从 `ConfigRuntimeSO → AdPluginConfig → MaxAdChannelConfig` 读取 `AppKey` 与 `AdMobAppIdAndroid/IOS`，在 Android/iOS 构建预处理回调中写入官方 AppLovin UPM 包提供的 `AppLovinSettings.asset`；iOS 后处理由基类 `NovaSDKBuildProcessor` 通过 `GetEmbedXcframeworkNames` 名单托管，自动 Embed MAX 依赖的 6 个 xcframework。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Editor/BuildProcessor/MaxAdPluginBuildProcessor.cs` | `MaxAdPluginBuildProcessor` | 全部定义 |

---

## §5 完整公开 API

```csharp
public sealed class MaxAdPluginBuildProcessor : NovaSDKBuildProcessor
{
    /// 预处理回调优先级，固定 600；值越小越早执行。
    public override int PreprocessPriority { get; }

    /// 后处理回调优先级，固定 600。
    public override int PostprocessPriority { get; }

    /// Android 预处理：从 MaxAdChannelConfig 读取 SdkKey + AdMobAppIdAndroid 写入 AppLovinSettings.asset。
    public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context);

    /// iOS 预处理：从 MaxAdChannelConfig 读取 SdkKey + AdMobAppIdIOS 写入 AppLovinSettings.asset。
    public override void OnPreprocessBuildOniOS(BuildReport report, NovaBuildContext context);

    /// iOS 后处理 xcframework 名单：返回 6 个依赖名单，由基类 OnPostprocessBuildOniOS 默认实现负责 Embed & Sign。
    /// 仅在 #if UNITY_IOS 条件下编译。
    protected override string[] GetEmbedXcframeworkNames();
}
```

**xcframework Embed 列表（`OnPostprocessBuildOniOS` 处理，共 6 项）：**

| xcframework | 说明 |
|---|---|
| `DTBiOSSDK.xcframework` | Amazon TAM |
| `IASDKCore.xcframework` | IronSource Core |
| `OMSDK_Appodeal.xcframework` | Open Measurement SDK（Appodeal） |
| `OMSDK_Pubmatic.xcframework` | Open Measurement SDK（Pubmatic） |
| `OMSDK_Pubnativenet.xcframework` | Open Measurement SDK（Pubnativenet） |
| `OMSDK_Smaato.xcframework` | Open Measurement SDK（Smaato） |

---

## §11 使用示例

业务层无需直接调用。完整配置流程：

1. 打开 Nova ConfigWindow → 广告聚合 → MAX 渠道；
2. 填写 `AppKey`、`AdMobAppIdAndroid`、`AdMobAppIdIOS`；
3. 导出 `ConfigRuntimeSO`；
4. 触发 Unity Build（Android 或 iOS），处理器在预处理阶段自动把上述值写入 AppLovin 官方 SDK 的 `AppLovinSettings.asset`。官方 UPM 默认会在 `Assets/MaxSdk/Resources/AppLovinSettings.asset` 创建该资产。

预期控制台日志（Android）：

```
[MaxAdPluginBuildProcessor] Android AppLovinSettings 已注入：SdkKey 与 AdMob AppId 已写入 AppLovinSettings.asset。
```

iOS 构建完成后，6 个 xcframework 会被自动 Embed 到 Xcode 工程 Unity-iPhone target，无需手动操作。

---

## §13 关联文档

- [`MaxAdPlugin.md`](./MaxAdPlugin.md) — MAX 渠道插件主文档
