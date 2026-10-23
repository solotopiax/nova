# EditorUtil.Build

**类签名**：`public static partial class EditorUtil.Build`
**命名空间**：`NovaFramework.Editor`

BuildPipeline.BuildPlayer 薄封装，提供统一输入校验与日志。

正式 Player 构建会由全局构建回调临时生成唯一的 `Resources/YooAssetSettings.asset`：源文件取当前 `ConfigMasterSO` 三维坐标解析出的 Editor 权威配置，其中 Platform 实时映射 Unity Active BuildTarget，构建结束、失败或取消后自动清理。该机制覆盖 Build Profiles、Pipify、Debug Inspector 与 CLI 等所有 `BuildPipeline.BuildPlayer` 入口；`EditorUtil.Build` 自有入口另有 `finally` 清理兜底。

WebGL Player 构建完成后，`NovaBuildPostprocessor` 会扫描输出目录中的 YooAsset 官方 `BuiltinCatalog.bytes`，生成 `StreamingAssets/nova-webgl-layout.txt`。该清单始终包含格式头，纯 CDN 构建也不会成为空文件；Host 运行时据此选择文件系统，不再用预期 404 探测首包布局。

iOS 导出使用 EDM4U 生成 Podfile 时，`IOSPodsDeploymentTargetPostprocessor` 在 Podfile 生成后、`pod install` 前读取 `PlayerSettings.iOS.targetOSVersionString`，给各 Pods target 的 `IPHONEOS_DEPLOYMENT_TARGET` 设置下限。仅低于下限或缺少该设置的配置会被提升；等于或高于下限的值保持原样。若 Podfile 已包含其他 `post_install`，构建明确失败，需先将两段规则合并，避免 CocoaPods 的重复回调错误。

iOS 隐私构建处理器从当前已导出的 `ConfigRuntimeSO.PrivacyConfigs` 读取两份配置：`PrivacyInfoConfig` 合并到应用级 `PrivacyInfo.xcprivacy`，`InfoPlistUsageDescriptions` 按键写入应用 `Info.plist`。后者仅接受非空的 `UsageDescription` 文案，不自动补齐 SDK 可能需要的键；归档提交前仍需检查最终 `.app/Info.plist`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.Build.cs` | `EditorUtil.Build` | public API：BuildPlayer |
| `EditorUtil.Build.Visitors.cs` | `EditorUtil.Build` | 常量：c_LogPrefix |
| `EditorUtil.Build.Methods.cs` | `EditorUtil.Build` | 私有方法：ResolveScenes |
| `EditorUtil.Build.BuildReadiness.cs` | `EditorUtil.Build` | 内部只读构建前置探针，供 `nova.project.build.inspect-readiness` 使用 |
| `EditorUtil.Build.Definitions.cs` | `EditorUtil.Build` | 嵌套类型：BuildMode 枚举 |
| `../../BuildProcessor/Core/YooAssetRuntimeSettingsStaging.cs` | `YooAssetRuntimeSettingsStaging` | YooAssetSettings 临时副本的解析、所有权记录、恢复与清理 |
| `../../BuildProcessor/Core/YooAssetRuntimeSettingsBuildCallbacks.cs` | 构建回调 | 正式 Player 构建前 staging、构建后清理；跳过 HybridCLR 裁剪 AOT 临时构建 |
| `../../BuildProcessor/Core/NovaBuildPostprocessor.cs` | `NovaBuildPostprocessor` | 分发平台后处理；WebGL 额外生成无 404 的资源布局清单 |
| `../../BuildProcessor/Core/IOSPodsDeploymentTargetPostprocessor.cs` | `IOSPodsDeploymentTargetPostprocessor` | 在 EDM4U 的 Podfile 生成与 pod install 之间设置 Pods iOS 部署版本下限 |

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
- `InvalidOperationException`：BuildResult 为 `Failed` 时主动抛出，message 包含 BuildResult 枚举值；`Cancelled` / `Unknown` 不在该主动抛错分支，调用方仍须检查最终 BuildReport，不能据方法返回宣称成功。
- `InvalidOperationException`：当前 ConfigMaster、三维解析出的 YooAssetSettings 缺失，或工程中已有任意常驻 `Resources/YooAssetSettings.asset` 时，在构建预处理阶段中止。
- `InvalidOperationException`：HybridCLR 启用但 MethodBridge 的 DEVELOPMENT 标记缺失、非法或与 `developmentBuild` 不一致时，在 Player 构建前中止。

### YooAssetSettings 构建期 staging

- 开发态目标固定为当前 Demo 根目录的 `Resources`。
- UPM Sample 消费态只在当前 ConfigMaster 所属 Demo 目录树内，由浅到深、同层按路径排序选择已有 `Resources`；不存在时创建 Demo 根目录的 `Resources`。
- staging 使用 `FileUtil.CopyFileOrDirectory` 仅复制已冻结的资产正文，再同步导入生成独立 GUID，不复制 Editor 源资产的 `.meta`；避免当前 Unity 的 `AssetDatabase.CopyAsset` 隐式保存其他脏资产，不全局保存其他正在编辑的资产。
- 所有权 marker 写在 `Library/Nova/YooAssetRuntimeSettingsStaging.json`。构建后只删除 marker 指向且正文 SHA-256 未变化的副本；内容被外部修改时保留并报错，避免误删。
- 正常结束、失败、取消、域重载和下次 Editor 启动都会尝试恢复；HybridCLR `StrippedAOTDllsTempProj` 内部构建不触发 staging。
- 正式构建后处理与 `EditorUtil.Build.BuildPlayer` 的同步清理无法安全完成时抛出异常，保留副本与 marker；构建不能因此返回成功。后台恢复仍保留原有报错和不误删行为。
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

## 只读构建前置检查

`EditorUtil.Build.InspectBuildReadiness` 为内部探针，由 `nova.project.build.inspect-readiness` Action 公开。消费端通过 `nova-project-preflight-build` 获取 live Schema 后执行，不直接调用内部 C# 方法。

保留的 `NOVA-BUILD-001` 至 `NOVA-BUILD-012` 检查覆盖活动目标、Build Support、启用场景、持久化 ConfigMaster 绑定与有效坐标、Runtime 坐标和场景渠道快照、YooAsset 配置与 Package、HybridCLR 安装和 link.xml。探针不初始化缺失配置、不保存、不生成、不构建；报告的 `ready` 只代表当前前置条件。

已删除的 Project Guard 场景结构与 Resources 规则不属于该探针，不再执行 canonical Prefab/根数量/Resources 策略扫描或自动 Play 拦截。场景使用约束与真实运行由当前任务按 [验证与构建](../../../Onboarding/VALIDATION.md) 落实。

## 三平台构建与验收导航

完整 Android/iOS/WebGL 任务复用 [Player Skill 的三平台验收参考](../../../../Agents/Skills/nova-project-build-player/references/platform-validation.md)，由现有 Config、生成、Bundle、Player Operation 与已保存 Pipify Batch 串联。Agent 先发现当前工作区、Batch 参数、平台工具与产物，再核对业务选择；可读取的 GUID、配置值、路径与工具位置不要求新手重复填写。固定调用保持现有类型化 Action/Pipify 入口，单项操作只加载相应分支。

参考区分代码生成先于热更 DLL、Target/DevelopmentBuild ABI 一致、批次外 Scene/Master/PlayerSettings 快照与串行恢复。当前 Batch Plan 只冻结设置文件、活动目标与 Step 参数，不提供完整构建状态冻结或任意重载后的自动继续。Runtime Export Action 与 Pipify `export.config` 不写场景快照，ConfigWindow 导出会写活动场景；组合任务需显式复用 `nova-project-preflight-build` 执行只读构建前检查。

验收分别报告 Android 构建/运行、iOS 工程/Pods/原生编译/签名设备、WebGL 首包/HTTP/浏览器，以及所需独立进程存档证据。`BuildReport` 或 Batch 成功不代替更高层级；WebGL `OfflinePlayMode` 必须匹配 `ClearAndCopyAll`。完整任务交付同时报告本轮临时状态恢复和清场，保留正式产物与用户已有数据。

### 受控 Player Action 的写前与 AOT 后验

`nova.project.player.build` 保持 `target / outputPath / developmentBuild / buildMode` 四字段请求，使用原有 `EditorUtil.Build.BuildPlayer`。强化门只影响该 Action 的计划与回执，不改变公共 Build API 或 Pipify 的调用方式。

HybridCLR 的 `CopyStrippedAOTAssemblies.OnPreprocessBuild` 会重建目标 AOT 目录，即使 HybridCLR 当前关闭也执行该预回调。Action 因此在写入前核对其目录与所有实际冻结输入、正规 Bee 裁剪源及 Player 主/伴随产物互不重叠，防止自定义输出删除热更 DLL、IL2CPP 工具或本次构建产物；按目录分隔符比较，名称前缀相似的独立目录仍允许。

HybridCLR 启用时，执行和 Verify 核对本次 Bee `ManagedStripped` 与最终 AOT 的非空平铺 DLL 集、逐名 SHA 及完整目录摘要。源缺失、DLL 缺失或陈旧、额外文件/子目录、符号链接及缺少目录回执都不能被已有输出目录掩盖；此证据仅证明正规裁剪后的复制，不代表 Config 指定的 `.dll.bytes` 拷贝、Bundle 或运行时元数据加载完成。

`Unity.ProjectAuditor.Editor.Build.LastBuildReportProvider` 按已审引擎实现接入异步报告闭环：Plan 独立冻结 `Library/LastBuild.buildreport` 与 `Library/LastCleanBuild.buildreport`，旧延迟任务未结束时阻断。两报告属于构建输出，不纳入执行前后必须相同的输入 SHA。Build 前将准确基线备份到 Plan 展示的本地 `Library/Nova/PlayerReportRecovery/<本轮唯一目录>`；成功或安全恢复后仅清理本轮目录和本轮新建的空祖先。

该官方回调仅在 `packedAssets` 非空时登记 `EditorApplication.update`，复制 LastBuild 到 LastClean 并在成功后自行注销。Action 使用主线程 UniTask 最多等待十秒，不阻塞 update，不执行或移除该官方回调；完成后核对实际 BuildReport 对象、GUID、两文件摘要及无复制分支的 LastClean 基线。执行失败或取消时，只有官方复制已结束且归属、当前字节和备份完整性都准确才恢复原 LastClean；构建自身产生的 LastBuild 保留。持续失败、超时或归属不明返回 `partial`，明确保留恢复目录，不宣称恢复完成；Verify 只读复核本轮 GUID、报告摘要和待执行状态，不自动重放或恢复报告。其他尚未审完的回调和平台继续保持各自原门禁，单项报告合同完成不等于整个 Player 构建验收。

SRP 的 `UnityEditor.Rendering.CorePreprocessBuild` 与 `UnityEditor.Rendering.ProbeVolumeBuildProcessor` 仅接纳 Builtin 分支：通过当前精确包源码及实际 Editor/Runtime DLL，调用已审 `BuildTargetExtensions.TryGetRenderPipelineAssets` 读取目标完整资产集，不用当前单一 Graphics 资产替代平台 Quality 与 fallback。Core 按官方 `CreateInstance` 的实际 `activeBuildTarget` 读取，Probe 按本次构建目标读取；两者都要求资产集为空、原 `m_BuildData / CoreBuildData.m_Instance` 为空、上次 Probe 处理集合为空。非空 SRP 及其 `supportProbeVolume / maxSHBands / GPU` getter 继续阻断，其他 shader 回调不因这两项放行。

Builtin Probe 会在所有 APV 文件写入前自然返回；Action 冻结 `Library/TempAPVStreamingAssets` 原树摘要并在构建后只读比较，保留原数据。Core 只允许官方回调建立临时内存缓存，成功后双槽必须恢复为空。同步构建中无法唯一记录残留缓存的创建者时，不调用懒初始化 `instance`、不猜归属 `Dispose` 或清空静态槽；完成其他已授权临时恢复后报告 `partial`，保留准确残留。该局部回调合同与定向测试不代替真实 Player 构建或平台运行验收。

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

## 调用与失败边界

薄 BuildPlayer 只在 BuildResult.Failed 主动抛错，Cancelled/Unknown 仍可能返回 BuildReport；调用方必须检查 summary，不能以有返回值判成功。ResolveScenes 只构造 Options，没有显式冻结 scenes，实际沿 Unity 启用场景；目标模块、活动平台一致性与输出归属由上层任务合同承担。

Development 全局值在 finally 恢复，随后 YooSettings CleanupAfterBuild 本身可能抛 InvalidOperationException 并覆盖原构建异常，冲突副本/marker 留作恢复，不能保证所有入口最终总是无残留。BuildPackage 的分钟级名字也不保证唯一；文件名 DevelopMode、Unity DevelopmentBuild 和实际导出的 Runtime Config 三者独立，薄 API 不替调用方导出或修正配置。

## 中央回调、规则模型与观察租约

NovaBuildShared 每轮收集实际 SDK processor 实例，构造失败记 Error 后继续，原链可能只得到部分列表；中央 pre/post 再按共享 Context.Target 分发，不完整核报告与共享上下文是同一轮。优先级相同的 List.Sort 不保证稳定顺序；普通 hooks 后收口，再 AfterNova，任何 SDK/IO/观察异常中止余下工作，没有下游副作用回滚。Context 主要保存原引用和值，不自校阶段；ActivityName 非空注册先到者优先，不证明类存在。

NovaBuildParentObservers 与 NativeBuildPreviewObservers 默认无租约时空返，不执行另一套构建。登记只 CAS 从空安装并绑定原线程/实例，不能覆盖或嵌套抢占，过期/异线程拒绝；Dispose 只清自己的登记。Before 观察失败会阻止原调用，原 SDK 抛错没有 After，不能 finally 补成功帧。Native Preview 保留原一次 Open、短路读取和一次 Close，观察只接收实际值及 evaluated 标记，不为取证再读 getter；scan/beforeClose/close/after 的真实多错聚合，取消也不吞。

Android 安装权限扫描基于真实 BuildSettings 保存场景，enabled→active→config→总门逐级短路，inactive 不触发 getter；POST_NOTIFICATIONS 先登记，只有扫描实际 true 才 REQUEST_INSTALL_PACKAGES。观察失败不撤销已登记规则。Manifest bootstrap 的冻结预测入口只改私有 XML，不读 Unity设置或写盘；原入口仍按原时点逐个求值，缺 launcher 保留原早退，不能提前补值。

CustomAndroidManifest 的 Mode 并非每类都支持 Remove：Activity 已实现，其它若不支持可能按 Add/Merge 继续。Merge 追加 intent children 不去重，匹配只看 actions 子集和可选 scheme，不核 categories/host/path；meta-data resource/value 可残留互斥旧属性，空属性通常不能清旧值。DOM部分失败不回滚，Save 原地覆写。UnityManifest.xml 含 Activity/GameActivity 两候选，先按 applicationEntry 留一项；具体 SDK 可再注册名称，不能将模板双候选当最终产物。

YooAsset staging 先写归属 marker，再复制正文并生成独立 meta；Hash 只绑正文，内容漂移保留副本和 marker，正式 cleanup 失败会抛使构建不能成功，重载/退出后台只日志保留。空 Resources 删除返回值未完整核，marker tmp 写失败也可能残留；只有确认本轮创建且空的目录可清。内部 HybridCLR 跳过是输出路径子串判断，不是通用路径授权。

iOS 中 PBX/plist/entitlements 多文件先后写，SDK异常可部分保留；Pods xcframework 同名递归首命中，缺失仅 Warning。XcodeHelper 多数只改内存，类型不符可覆盖旧 plist 值/URL entry，AddCapability 不证明签名账号权限。PrivacyInfo 只按类别/理由合并配置，不证明商店合规；Podfile 下限规则拒外部 post_install，写后仍需原链 pod install。WebGL layout 只依据 BuiltinCatalog.bytes 存在并写固定头，不核 bundle内容。

编译输入以真实 asmdef 为准：NovaFramework.Editor 与 NovaFramework.Mcp.Editor 均限 Editor，MCP 桥和 Framework Editor 职责分开。前者的 OSS version define 只表示编译分支，不证明远端权限；namespace、目录与实际加载 DLL 身份不能互推。上述静态合同不替代本轮真实报告、平台产物和设备验收。
