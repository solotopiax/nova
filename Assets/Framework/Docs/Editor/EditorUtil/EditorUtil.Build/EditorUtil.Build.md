# EditorUtil.Build

**类签名**：`public static partial class EditorUtil.Build`
**命名空间**：`NovaFramework.Editor`

BuildPipeline.BuildPlayer 薄封装，提供统一输入校验与日志。

正式 Player 构建会由全局构建回调临时生成唯一的 `Resources/YooAssetSettings.asset`：源文件取当前 `ConfigMasterSO` 三维坐标解析出的 Editor 权威配置，其中 Platform 实时映射 Unity Active BuildTarget，构建结束、失败或取消后自动清理。该机制覆盖 Build Profiles、Pipify、Debug Inspector 与 CLI 等所有 `BuildPipeline.BuildPlayer` 入口；`EditorUtil.Build` 自有入口另有 `finally` 清理兜底。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Build.cs` | `EditorUtil.Build` | public API：BuildPlayer |
| `EditorUtil.Build.Visitors.cs` | `EditorUtil.Build` | 常量：c_LogPrefix |
| `EditorUtil.Build.Methods.cs` | `EditorUtil.Build` | 私有方法：ResolveScenes |
| `EditorUtil.Build.Definitions.cs` | `EditorUtil.Build` | 嵌套类型：BuildMode 枚举 |
| `../../BuildProcessor/Core/YooAssetRuntimeSettingsStaging.cs` | `YooAssetRuntimeSettingsStaging` | YooAssetSettings 临时副本的解析、所有权记录、恢复与清理 |
| `../../BuildProcessor/Core/YooAssetRuntimeSettingsBuildCallbacks.cs` | 构建回调 | 正式 Player 构建前 staging、构建后清理；跳过 HybridCLR 裁剪 AOT 临时构建 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── Build (public static partial class)
```

---

## §5 完整公开 API

```csharp
/// <summary>
/// 启动一次 Player 打包。
/// </summary>
/// <param name="target">目标平台。</param>
/// <param name="outputPath">完整产物路径（Android 为 apk 文件路径 / iOS 为工程目录路径）。</param>
/// <param name="developmentBuild">是否开发构建。</param>
/// <param name="buildMode">打包方式，对应 Build Profiles 中 Build 按钮的三种触发形态。</param>
/// <returns>Unity 构建结果。</returns>
public static BuildReport BuildPlayer(BuildTarget target, string outputPath, bool developmentBuild, BuildMode buildMode)

/// <summary>
/// 按文件夹路径打包：自动生成文件名并处理 Android AAB/APK 相关 Build Settings 临时切换。
/// 文件名中的 Debug/Release 段取自 developMode（ConfigRuntimeSO.DevelopMode），与 developmentBuild 独立。
/// </summary>
/// <param name="target">目标平台。</param>
/// <param name="outputFolder">输出文件夹路径（遵循项目根相对路径规范；不存在时自动创建）。</param>
/// <param name="developmentBuild">是否 Unity 开发构建（控制 BuildOptions.Development；与文件名环境段无关）。</param>
/// <param name="buildMode">打包方式，对应 Build Profiles 中 Build 按钮的三种触发形态。</param>
/// <param name="buildAppBundle">Android 专用：是否构建 AAB（仅 Android 非工程导出模式生效）。</param>
/// <param name="splitApplicationBinary">Android 专用：是否拆分应用 Binary。</param>
/// <param name="developMode">文件名环境段来源（Debug/Release），取自 ConfigRuntimeSO.DevelopMode；与 developmentBuild 独立。默认 Debug。</param>
/// <returns>Unity 构建结果。</returns>
public static BuildReport BuildPackage(BuildTarget target, string outputFolder, bool developmentBuild, BuildMode buildMode, bool buildAppBundle, bool splitApplicationBinary, DevelopMode developMode = DevelopMode.Debug)
```

**BuildMode 枚举（`EditorUtil.Build.BuildMode`）：**

| 枚举值 | 对应 Build Profiles 操作 | 附加 BuildOptions flag |
|---|---|---|
| `Build` | 直接点 Build 按钮（不展开下拉） | 无（`BuildOptions.None`） |
| `CleanBuild` | Build 下拉「Clean Build…」 | `BuildOptions.CleanBuildCache` |
| `ForceSkipDataBuild` | Build 下拉「Force skip data build」 | `BuildOptions.BuildScriptsOnly` |

> 三档互斥；`developmentBuild` 与 `buildMode` 正交，可叠加（如 Dev + ForceSkipDataBuild）。

**BuildPackage 文件名格式：**

```
{productName字母数字}_{Debug|Release}_{bundleVersion}_{yyyy_MM_dd_HH_mm}[后缀]
```

> 文件名中的 Debug/Release 段由 `developMode` 参数（`ConfigRuntimeSO.DevelopMode`）决定，与 `developmentBuild`（Unity 开发构建选项）是两个独立概念。`RunPackage` Step 通过 `EditorUtil.Config.RuntimeProvider.GetCurrent()` 读取激活 ConfigRuntimeSO，未找到时降级为 Debug 并打印 Warning。

**命名兼容边界：**

| 片段 | 当前处理 | 说明 |
|---|---|---|
| `productName` | `Regex.Replace(PlayerSettings.productName, "[^a-zA-Z0-9]", "")` | 只保留英文字母和数字；冒号、空格、引号、浪线、中文等都会被删除。当前没有空结果兜底，若产品名全是非字母数字字符，产物名前缀会为空。 |
| `Debug|Release` | `developMode.ToString()` | 来自枚举值，当前为稳定安全字符串。 |
| `bundleVersion` | 原样拼入 | 不做特殊字符清洗；建议保持 `1.2.3` 这类数字点号版本，避免空格、冒号、引号、斜杠等路径 / shell 敏感字符。 |
| 时间戳 | `DateTime.Now.ToString("yyyy_MM_dd_HH_mm")` | 固定数字与下划线。 |
| `outputFolder` | 相对路径基于项目根解析，绝对路径直接使用 | 不做清洗；`~` 不会展开为用户 Home，而会按普通路径片段处理。 |

**产物后缀规则（`ResolveExtension`）：**

| 条件 | 后缀 |
|---|---|
| Android + 非工程导出模式 + buildAppBundle=true | `.aab` |
| Android + 非工程导出模式 + buildAppBundle=false | `.apk` |
| iOS / WebGL / Android 导出工程（exportAsGoogleAndroidProject）| 空串（产物为目录）|

**Android Build Settings 临时还原说明：**

`BuildPackage` 仅在 `target == Android` 时快照并临时写入以下两项，构建结束后在 `try/finally` 中还原，不污染工程 Build Settings：
- `EditorUserBuildSettings.buildAppBundle`
- `PlayerSettings.Android.splitApplicationBinary`（核实 API：unity_reflect 确认，非 obsolete 的 `useAPKExpansionFiles`）

**Android Minify / ProGuard 说明：**

启用 Android Minify 时，项目必须同时启用 Unity 的 Custom Proguard File，使 `Assets/Plugins/Android/proguard-user.txt` 被 `mainTemplate.gradle` 的 `**USER_PROGUARD**` 接入构建。Nova 构建预处理会创建或重建该文件，并内置保留 AndroidX Startup、WorkManager 与 Room 生成实现类的启动期反射规则，避免 `InitializationProvider` 在进程绑定阶段初始化 WorkManager 时因 R8 裁剪或改写 Room 实现类而崩溃。

**HybridCLR Development ABI 校验：**

当 HybridCLR 已启用时，`BuildPlayer` 会在调用 `BuildPipeline.BuildPlayer` 前临时将显式参数 `developmentBuild` 镜像到 `EditorUserBuildSettings.development`，再严格校验生成的 `MethodBridge.cpp` 中 `// DEVELOPMENT=0|1` 标记；构建完成、失败或取消后恢复原全局值。

标记缺失、格式非法或与 `developmentBuild` 不一致时会抛出 `InvalidOperationException` 并停止导出，要求先以相同 DevelopmentBuild 档位执行 HybridCLR Generate All。这样可避免条件编译类型在桥接代码和最终 IL2CPP 产物之间出现 ABI 不一致。

**iOS Entitlements 命名注意：**

iOS 构建完成后，`NovaBuildPostprocessor` 会为 Xcode capability 注入准备 entitlements 文件路径。该路径当前使用：

```csharp
Application.productName.Replace(" ", "") + ".entitlements"
```

这条链路只移除空格，并不复用 `BuildPackage` 的字母数字白名单。Firebase、AppsFlyer、AppleSignIn 等 SDK 在添加 capability 时会使用 `NovaBuildContext.RelativeEntitlementFilePath`。因此 iOS 包如果启用这些 capability，`PlayerSettings.productName` 仍应避免冒号、引号、浪线、斜杠等 Xcode / 文件系统敏感字符。

**异常：**
- `ArgumentException`：outputPath / outputFolder 为空时抛出。
- `InvalidOperationException`：BuildResult 不为 Succeeded 时抛出，message 包含 BuildResult 枚举值。
- `InvalidOperationException`：当前 ConfigMaster、三维解析出的 YooAssetSettings 缺失，或工程中已有任意常驻 `Resources/YooAssetSettings.asset` 时，在构建预处理阶段中止。
- `InvalidOperationException`：HybridCLR 启用但 MethodBridge 的 DEVELOPMENT 标记缺失、非法或与 `developmentBuild` 不一致时，在 Player 构建前中止。

### YooAssetSettings 构建期 staging

- 开发态目标固定为当前 Demo 根目录的 `Resources`。
- UPM Sample 消费态只在当前 ConfigMaster 所属 Demo 目录树内，由浅到深、同层按路径排序选择已有 `Resources`；不存在时创建 Demo 根目录的 `Resources`。
- staging 使用 `AssetDatabase.CopyAsset` 生成独立 GUID，不复制 Editor 源资产的 `.meta`。
- 所有权 marker 写在 `Library/Nova/YooAssetRuntimeSettingsStaging.json`。构建后只删除 marker 指向且正文 SHA-256 未变化的副本；内容被外部修改时保留并报错，避免误删。
- 正常结束、失败、取消、域重载和下次 Editor 启动都会尝试恢复；HybridCLR `StrippedAOTDllsTempProj` 内部构建不触发 staging。
- UPM 发布不生成永久 Resources 副本；项目仓库与已发布 Sample 都只保存 `Editor/YooAssetSettings.asset` 权威源。

---

## §11 使用示例

```csharp
// Android Release 打包（默认增量，指定完整路径）
BuildReport report = EditorUtil.Build.BuildPlayer(
    BuildTarget.Android,
    "/output/game.apk",
    developmentBuild: false,
    buildMode: EditorUtil.Build.BuildMode.Build);
Log.Debug(LogTag.Editor, "产物路径：{0}", report.summary.outputPath);

// Android AAB 打包（自动生成文件名，相对路径）
// 产物示例：Builds/MyGame_Release_1.0.0_20260603120000.aab
// developMode 取自 ConfigRuntimeSO.DevelopMode；developmentBuild 是 Unity 开发构建选项
EditorUtil.Build.BuildPackage(
    BuildTarget.Android,
    "Builds/Android",
    developmentBuild: false,
    buildMode: EditorUtil.Build.BuildMode.Build,
    buildAppBundle: true,
    splitApplicationBinary: false,
    developMode: DevelopMode.Release);

// Android APK 开发包（自动生成文件名，developMode 默认 Debug）
try
{
    EditorUtil.Build.BuildPackage(BuildTarget.Android, "Builds/Android", true, EditorUtil.Build.BuildMode.Build, false, false, DevelopMode.Debug);
}
catch (InvalidOperationException ex)
{
    Log.Error(LogTag.Editor, "打包失败：{0}", ex.Message);
}
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.ProcessRunner.md](../EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)

## Pipify Android Signing

`EditorUtil.Build.BuildPackage` still owns only package output naming and Android AAB/split temporary settings. Android keystore signing for Pipify is applied by the `build.package` Step immediately around the `BuildPackage` call, then restored in `finally`.

This keeps `EditorUtil.Build` as a thin BuildPipeline wrapper while allowing Pipify batches to carry Android keystore path, keystore password, key alias, and key alias password without requiring manual PlayerSettings edits before each build.

## Android Manifest Launcher Selection

Android builds use `Assets/Framework/Scripts/Editor/BuildProcessor/Android/UnityManifest.xml` as a clean manifest baseline. The baseline contains both Unity default launcher candidates:

- `com.unity3d.player.UnityPlayerActivity`
- `com.unity3d.player.UnityPlayerGameActivity`

`NovaBuildPreprocessor` normalizes the copied `Assets/Plugins/Android/AndroidManifest.xml` before SDK processors run. It reads `PlayerSettings.Android.applicationEntry`, keeps the matching Unity launcher activity, removes the other default launcher block, and records the selected activity as the build context default.

SDK processors may still override `NovaBuildContext.ActivityName` through `RegisterActivityName`, for example Firebase FCM replacing the launcher activity class. Manifest rules that need to modify the launcher should use `UseMainActivity` instead of hardcoding `UnityPlayerActivity`, because the actual launcher may be Activity or GameActivity depending on Unity PlayerSettings.

## BuildProcessor AfterNova Hooks

`NovaSDKBuildProcessor` exposes platform-specific AfterNova hooks for SDK logic that must run after Nova has finished its own build-stage work:

- `OnAfterNovaPreprocessBuildOnAndroid`
- `OnAfterNovaPreprocessBuildOniOS`
- `OnAfterNovaPreprocessBuildOnWebGL`
- `OnAfterNovaPostprocessBuildOnAndroid`
- `OnAfterNovaPostprocessBuildOniOS`
- `OnAfterNovaPostprocessBuildOnWebGL`

The normal platform hooks still run first by `PreprocessPriority` or `PostprocessPriority`. Nova then performs its own stage close-out, such as Android Manifest injection/rule application/save, ProGuard rebuild, or iOS Xcode/Plist/Entitlements write-back. After that, the matching AfterNova hooks run again in the same priority order.

Use AfterNova hooks only for SDK official tools or supplemental work that depends on Nova-owned artifacts already being materialized on disk. For Android Manifest nodes, SDK processors should still prefer `NovaBuildContext.AddManifestRules`; AfterNova is intended for vendor generators that directly read or rewrite `Assets/Plugins/Android/AndroidManifest.xml`, such as a Facebook SDK manifest generator. SDK processors must not use AfterNova to register new Manifest rules that depend on the earlier unified rule-application phase, because that phase has already completed.
