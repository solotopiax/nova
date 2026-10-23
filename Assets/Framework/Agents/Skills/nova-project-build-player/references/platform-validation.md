# 三平台构建与运行验收

本参考由现有 Config、HybridCLR、Bundle、Player Operation 共同复用。用户要求完整 Android/iOS/WebGL、Player 运行或独立进程存档验证时才读取共同准备及相应平台段落；普通单项操作仍按其契约执行。这里描述 Agent 的组合责任，不代表新增 Action、自动重载继续或完整状态冻结能力。

## 发现与复用

1. 从消费项目读取持久化工作区的 Master/PipifySettings 身份与路径、当前场景和启用场景闭包、已保存 Batch、Config 坐标、HybridCLR/Collector/资源模式、PlayerSettings、平台模块、现有本地工具和设备；只收集当前任务所需事实。先发现工具与产物，再判断可执行层级，不把可读取的 GUID、参数、路径或工具位置转嫁给新手。
2. 审查 Batch 的全部有序 Step、有效参数来源、输入依赖、写入范围、失败与重载行为。Batch 名称和旧 `Target/Platform` 数字不证明实际平台；Pipify 执行时以 Unity Active BuildTarget 覆盖平台字段，字符串占位符在每个 Step 调用前解析，时间不在 Plan 中固定。
3. 复用现存类型化 Action、Operation 与已审 Batch。完整三平台授权覆盖必要且可恢复的本地准备和验证时继续执行，只就无法确定的业务选择或确实缺少的外部权限补充输入。Action 使用当前 Schema 与一次性 PlanId；不得用任意 C#、反射、临时脚本或手写 YAML 补成“稳定执行入口”。
4. 已保存 Batch 不符合依赖或混有本次未授权远端操作时，先给出具体缺口。通过既有 Pipify 窗口/受控 Unity 编辑，在已授权范围内修正或保存本地组合，再重新审查；不盲目执行原 Batch，也不直接改序列化 YAML。CDN、商店、Webhook 通知等外部行为只按明确授权目标执行。

## 生成顺序与 ABI

组合任务按实际依赖准备：坐标与运行配置/已保存场景 → 适用的表、UI、Network 与其它生成代码 → Unity 导入与必要编译 → HybridCLR 生成/校验及业务 DLL 刷新 → Package/Collector 对应 Bundle → Player → 请求的运行证据。必要 Android Resolve 在其原生消费者前完成。各环节复用其已有 Operation；单次 Bundle/Generate 不自动扩大为所有导出。

所有参与热更程序集的生成代码必须先于首次依赖它的编译或 `GenerateAll` 完成。`export.excel.all` 已包含 Table、UI、Localization、HostKey/NetCmd、Sound、Vibrate 的数据/代码，明确不含 `export.config` 和 `export.network.proto`；当前有 Proto 或资源密钥源码依赖时单独完成它们。生成源码后等待实际导入和需要的 Unity 编译稳定，不能把 Runner 结束当作编译完成。Runner 未提供任意生成步骤后的通用编译/重载继续保证；必要时复用独立的准备 Batch/导出 Action，重载稳定并复核输出后重新计划后续步骤，不重放已经执行的整批任务。

冻结并对齐当前 Target、Unity `DevelopmentBuild`、Master `Platform/Channel/DevelopMode` 与最终 Player 档位。`DevelopMode` 控制配置和自动命名，不能代替 Development ABI；MethodBridge 的独立 `// DEVELOPMENT=0|1` 标记、热更 DLL 与最终 Player 必须匹配。Pipify 会将 HybridCLR Step 的临时 development 值对齐其后首个 `build.package`，之后恢复；独立 Generate Action 要求当前 Editor 值已经匹配。

`GenerateAll → ValidateLinkXml`、业务 DLL compile/copy 和 AOT metadata copy 各按现有契约验收。若最终 Player 产生更新的裁剪 AOT DLL，核对 CopyAotDlls 消费的实际来源与时序；发现业务资源目标已过期时按项目交付链刷新并重建受影响产物。预构建 AOT 目录、Bundle 或旧 DLL 存在均不能证明最终 Player 使用了本次内容。

## 批次外快照与串行执行

当前 `nova.project.pipify.run-batch` Plan 只记录 PipifySettings GUID/path/hash、Batch 名称/描述、activeBuildTarget 和有序 Step/脱敏 ParamsJson。它不冻结 Scene 内容/启用闭包、Master 内容/当前坐标、PlayerSettings、全部动态参数或工具环境。Runner 的逐步工作区复核只校验 Master/PipifySettings 的持久化绑定与 GUID；资源锁不覆盖人工或外部修改。

Agent 在 Batch 外独立记录：原 active Target/development、完整启用场景及顺序、当前已加载场景与脏态、Master 身份/内容与当前坐标、Runtime/解析资源配置、影响本次产物的 PlayerSettings、输出与 StreamingAssets 基线。签名密码不进入日志或快照正文。只备份需要临时改动的范围，已有脏资产先保留，不能通过保存全部资产覆盖用户草稿。

备份不能只依赖 Git：预处理器、依赖 Resolve 与 SDK postprocessor 可能覆盖被忽略但原先存在的 Manifest、原生模板或 DLL。按实际写入集保存这些文件的原文、存在性与 hash，区分原有文件和本轮生成物；缺原文时不能用模板或旧 Git 版本猜恢复，也不能删除原有文件并称清场成功。

按单写者串行准备 Android、iOS、WebGL，目标切换稳定后重新解析并 Plan。每平台构建前、执行后、最终清场复核状态与产物；授权的本次写入作为预期差异记录，发现其它漂移即停止后续平台，不假称批次内每一时刻都被完整冻结。异步 Job 只提交一次；断线/重载后用 recovery token 查询，Interrupted/Failed/未知状态不自动重放。

Config Export Action 与 Pipify `export.config` 不修改 Master 当前 Channel/DevelopMode，也不写场景快照。需要时在本地授权范围内通过 ConfigWindow/受控 Unity 编辑准备当前坐标并保存目标场景；ConfigWindow 导出写的是活动场景，不能替代全部启用场景检查。每平台构建前复用 `nova-project-preflight-build` 的 `nova.project.build.inspect-readiness`，确认 Runtime 与 Asset/App 场景渠道一致。Pipify `build.package` 不自动调用该完整探针；`ready=true` 仍只代表当前静态前置。

## 平台证据

当前宿主的 APK/AAB 安装、Xcode 原生编译/设备命令，以及随包 WebGL HTTP 入口，按需读 [平台 CLI](platform-cli.md)。CLI 的 help 或本机服务可启动只证明入口存在，必须继续采集下面各层对应的本次产物与运行证据。

| 平台与层级 | 本次必须对应的证据 | 能力边界 |
|---|---|---|
| Android 构建 | 本次 BuildReport、实际 APK/AAB/工程、包标识/版本/ABI、资源与签名策略 | 工程导出或 AAB 文件不证明设备已安装运行 |
| Android 运行 | 已发现设备/模拟器的身份、ABI/API、匹配的本次安装产物、真实入口/UI/表/资源/存档探针与时间窗日志 | Emulator 结果只对应该环境；不宣称真机、厂商登录/支付或远端服务成功 |
| iOS 工程 | 本次 Unity BuildReport 与对应 Xcode 工程、Bundle ID/版本、资源与生成物 | 工程存在不证明 Pods、原生编译、签名或运行 |
| iOS Pods | 当前工程的 Podfile、解析结果/lock、必要资源与成功安装记录 | 无 Pods 依赖时说明不适用；不以 Podfile 存在代替 pod install 成功 |
| iOS 原生编译 | 已发现 Xcode/SDK、实际 workspace/project 与 scheme/configuration/destination、本次 xcodebuild 结果与产物 | 模拟器与 iphoneos 分开报告；未签名编译不能标为签名设备通过 |
| iOS 签名设备 | 明确团队/证书/profile/entitlements 与设备、匹配的签名产物、安装启动及业务探针 | 缺有效签名权限或设备时保留已完成层级，组合任务为 partial，不凭编译推断设备运行 |
| WebGL 构建与浏览器 | 本次 BuildReport、Wasm/数据/loader、首包与 `nova-webgl-layout.txt`，正确 HTTP MIME/压缩响应及真实浏览器启动/业务探针 | 页面打开或 Wasm 下载不等于 Nova 启动成功；记录 Console/请求中的实际失败 |

Android 输出格式、签名和安装/启动以用户任务为准，已授权完整运行验收时发现可用工具后继续必要本地步骤；需要新设备、额外账号或凭据时才说明缺口。iOS 原生与签名验收先发现当前 Xcode、Pods、工程、证书/profile 与设备，避免将本机可读事实重新交给新手填写；不自动变更团队、购买证书或上传商店。

iOS 使用 Pods 与 Swift Package Manager 时分别核对本次工程的有效声明、lock/Package.resolved、实际 SDK 版本与重复链接。某适配包的 Unity UPM 四段版本号不等于有效 SPM SemVer；发现非法版本或占位声明时，优先核验官方修复版及其真实 tag/SDK/最低系统版本，不通过禁用全局解析、删除厂商声明或假 native stub掩盖失败。Unity BuildReport 中 SDK postprocessor 的 Error 必须保留，即使 BuildResult 为 Succeeded。

首次运行 Xcode 前核对实际 Run Script 阶段的外部副作用。Crashlytics 的符号脚本与 AppLovinQualityService 构建脚本可能自动上传；本地原生构建不能默认视为无上传操作。依据当前配置和已有授权处理这些阶段，报告实际网络行为与证据；不得隐去已发生的上传。只需对已构建应用补做本地签名时，复用该产物，避免无必要的重建重复触发脚本。

### WebGL 离线首包

`OfflinePlayMode` 必须使用 `ClearAndCopyAll`，核对同一 Package/版本的 Manifest、Catalog、全部必要 Bundle 已进入 StreamingAssets 并被本次 Player 使用。`HostPlayMode` 可选 `None / ClearAndCopyByTags / ClearAndCopyAll`；`None` 依赖当前完整 CDN 部署。`LaunchHotfixTags`/`WebGLAssetStrategy` 只决定启动预热/请求时机，不替代首包拷贝范围。离线资源可启动也不意味着应用没有其它后台网络请求。

浏览器验证使用本次导出目录的正确 HTTP 服务，记录首包布局、实际运行模式、资源加载结果与业务探针。缓存命中不能替代本次产物验证，清缓存不得误删用户存档。没有对应网络部署的 Host 模式不能仅靠本地页面宣称通过。

### 独立生命周期存档

用户要求持久化验收时，使用真实 Nova Persist 调用和唯一测试 classify/item：第一次运行写入并显式 Save，结束应用进程，第二次全新进程在相同应用身份与存储位置读取，再清理本次 key 并核对既有数据。两个 Editor Play、新 Manager 或同一进程内重建对象只能证明较低层级，不能替代 Player 进程重启。

WebGL 使用相同 origin/浏览器 profile，在存储提交后关闭并创建全新页面/Wasm 运行实例；记录浏览器持久化层与刷新/重启边界，不把它描述为 Android/iOS OS 进程证据。测试不能用不同 origin 或无痕 profile造成的空存储误判耐久性，也不使用 DeleteAll 清用户数据。

## 交付与清场

逐平台报告已完成、已验证、未验证/依赖外部条件；证据对应本次输入、产物、环境与时间窗。Batch Job `Succeeded` 只证明 Runner 没有捕获异常并结束，不能替代 BuildReport、Bundle 报告、浏览器、原生编译、签名设备或存档验收。单项 Operation 的 success 不代表完整组合任务 success；所需层级缺失时保留 partial 与最小缺口。

恢复本轮临时 Target/development、Master/Runtime、Scene/Build Settings、PlayerSettings 和测试态资源，清理临时 runner/script/.meta、marker、日志/结果、临时目录、测试对象与唯一存档 key；删除临时 Unity 脚本后完成必要 Refresh。只恢复能确认由本轮产生的变化，不覆盖用户改动或其它 Agent 环境；用户要求保留的正式 Bundle/Player/工程/验收产物保留并列出。最后复核 git status 与临时残留，清场失败仍属未完成。

## 按需来源

当前实现与 API 细节从随包 `Docs/INDEX.md` 导航到 `EditorUtil.Pipify/PipifySteps.md`、`EditorUtil.Pipify.Runner.md`、`EditorUtil.Build.md`、`EditorUtil.HybridCLR.md`、`ConfigWindow.md`、`AssetComponent.md` 与 `PlayerPrefsManager.md`。只读当前决策所需文档，不依赖 Nova 开发仓 `.nova`、Minds 或固定机器路径。

WebGL 计划遇到包回调门时，按需读 [BuildEvents 与 UGUI 本地报告合同](../../../../Docs/Editor/PlayerBuildEventsContract.md)、[SRP/Adaptive 与精确包空分支](../../../../Docs/Editor/PlayerEmptyUsageBuildContract.md) 和 [原生 usage 范围](../../../../Docs/Editor/PlayerBuiltinUsageBuildContract.md)。这些现有合同负责准确准入、副作用与回执验收；不得改 SDK/Analytics 设置、清统计或私调 callback 来制造可执行条件。

TMP 分支按需读 [完整字体与资源登记合同](../../../../Docs/Editor/PlayerTmpFontBuildContract.md)。只有原 before 已加载且明确 marker 字体可作源预测首次登记；三 map 原 winner、完整 weak 槽、非清理字体 JSON/dirty/源/atlas 与文件保持。完整 typed after 缺失不能用较晚 current 代替，不因 InvariantHash 投影跳过独立逐行验收。

编译式诊断（包括会生成新程序集的 execute_code）应在 Plan 前完成；ready Plan 与 Execute 之间只核对不编译的现有资源/磁盘。非动态诊断程序集仍属于实际冻结闭包，漂移时重新 Plan，不排除它或重放旧 Execute。

TMP 的 Scope 后与 Verify 只观察原许可 marker 的现有对象，不重复 Prepare/Load。原分类行是明确的历史 baseline；当前 present 必须完整实读，absent 不冒充旧 JSON。只有原 clean、无原/后强行或活 weak且真实 Scope after 与当前均缺席的非清理候选可缺席。新增路径/未知输入/新实例或元数据变化拒绝，原 dirty 和清理字体约束保留；新域没有独立证明仍 partial。
