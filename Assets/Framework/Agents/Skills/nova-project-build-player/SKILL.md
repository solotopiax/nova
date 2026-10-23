---
name: nova-project-build-player
description: Use when Nova 项目组需要按明确平台、场景、模式与本地路径构建 Player 并核验 BuildReport，或把已核验的既有 AAB 转为指定 Android 设备的 APK 集并安装时使用。
---

# Nova 构建本地 Player

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`、`Docs/Onboarding/VALIDATION.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md`。发现适合当前任务的已有 Pipify Batch 时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；仅在当前项目启用 HybridCLR 或目标为 Android 时读取对应 ABI、输出与签名段落。用户要求完整三平台、Player 运行或独立进程存档验收时，按需读取 [三平台验收参考](references/platform-validation.md) 的共同准备与所需平台段落；普通单次构建不加载全部验收链。

该 Operation 默认生成本地 Player 安装包或平台工程与 BuildReport。用户明确要求运行验收时，再读取 [平台 CLI](references/platform-cli.md) 的相应分支，复用实际宿主工具或本 Skill 的 WebGL 脚本；不部署 CDN、不上传商店，不执行 Git 或 UPM 发布，也不以“打包”为由自动运行无关导出。

## Input

冻结项目根、执行入口、目标平台、当前启用的 Build Settings Scene 闭包、完整输出路径或输出文件夹、`BuildMode`、`developmentBuild` 与 `DevelopMode`。`developmentBuild` 控制 Unity Development 选项；`DevelopMode` 只决定自动产物名中的 Debug / Release 段，二者必须分别确认。

Android 还要冻结 APK/AAB/导出工程、SplitApplicationBinary 与签名策略。目标平台、Scene 闭包、输出路径、任一构建模式或平台专属选项不明确时返回 `blocked`；不要依赖 `BuildPackage` 的 Debug 降级替用户猜测模式。

先从项目读取这些事实：活动工作区绑定、已保存 Batch 的有序 Step/参数、当前场景和 Master、平台选项、工具与已有产物。用户已要求完整 Android/iOS/WebGL 验收时，主动审查现存 Batch 并提出可执行的复用组合；不要要求新手抄写可读取的 GUID、渠道、输出路径或构建参数。项目事实只用于提出和冻结计划，不能以旧产物替代本次证据；无法唯一确定的业务目标与真实外部权限才需要补充。已有明确授权持续有效，具体 Action 仍使用绑定本次计划的一次性确认值。

## 已有 Action Adapter

Framework 已注册并开放 `nova.project.player.build`。先用 `describe` 读取当前 Schema，再以冻结的 Target、Scene、模式和输出路径执行 `plan -> execute -> verify`；含覆盖或外部路径写入时仍必须使用当前一次性 PlanId 确认。不得退化到 `execute_code`、反射或临时 C#。

Player 的输入与回调捕获可能超过宿主传输时限。当前 Tool Schema 包含 `plan_mode/request_id` 时，先由 Agent 生成并保留 N 格式 GUID，以 `operation=plan, plan_mode=start` 登记同一个四字段 `request`，再用 `plan_mode=status` 只带同一 `action_id/request_id` 取回原响应。登记响应丢失时同号同请求重试，不换号重复捕获；只有原响应 `ready` 才进入原 `execute -> verify`。外层 `completed` 与 `plan_candidate` 不证明仍可执行，Core 仍核输入、generation 与一次性计划。域重载或过期后明确失效；先归档诊断并 `forget` 释放终态传输记录，再用新号重新计划，不能重放 Execute。未知回调等阻碍保持原样。优先使用实际可调用的新 Schema 路由；若外部服务仍以旧 Schema 拒绝字段，但 Unity custom-tools 已提供同名 Tool 的新参数，可用现有 `execute_custom_tool(tool_name="nova_project_action", parameters=完整请求对象)` 转交同一 Tool 和 Gateway。单独新建客户端会话未必刷新服务端缓存；无此已核实入口时保留具体阻碍，不改服务器配置或擅自重启。细节见 [慢计划传输](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md#慢计划的传输恢复)；普通快计划继续原同步流程，不新增 Skill/Action。

该 Action 当前仅支持与活动目标一致的 Standalone 桌面平台及 WebGL；Android / iOS 的输出、导出和签名选项尚未进入该 Action 契约。请求只有 `target / outputPath / developmentBuild / buildMode`，场景与 ProjectSettings 由 Plan 读取并冻结，不能传入本文的业务输入名冒充请求字段。Android / iOS 复用已保存且通过前置审查、包含平台构建步骤的 Pipify Batch。没有适用 Batch 时，先复用 `nova-project-configure-pipeline`，在已授权的本地构建目标和当前 `PipifySettingsSO` 内创建或修正组合，保存与 Verify 后重新计划本次运行；不存在 Batch 本身不构成永久阻塞。已有构建授权覆盖必要的同范围本地准备，不逐步增加审批，不扩大平台、场景、输出或远端 Step；缺真实配置宿主、Settings、无法消歧的构建选择或外部权限时只阻断对应缺项。Action 使用精确输出路径，不通过 `DevelopMode` 自动命名；自动命名属于已有 Pipify `build.package` 分支。

WebGL 回调准入按当前程序集和精确分支核验。Builtin 管线检查只接受全 null 资产列表及原空 Quality 缓存；构建后只恢复可准确归属的官方临时完成态。空 VolumeProfile GUID 集允许原回调直接结束；SRP/Adaptive 保留当前 Analytics 设置：启用时只接纳 null global 的准确空列表池和空 metadata/无请求分支，声明正常借还与已审初始化，不清表或调用 provider。Core usage 与 BuildEvents 仅按准确真实报告和 typed 前后态验证，未知订阅仍阻断；详见 [Player 合同](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md)。遇到未知缓存、回调或动态图集写入时保留阻断/恢复材料，按实际 Plan 处理，不能由定向测试推断 Player 构建通过。

三个已审官方构建后 reset 只按正常回调运行：Plan 冻结 `Generated / HasWarned / scenesProcessed` 的原值，写前逐项核对，作用域结束后记录真实归零结果；schema 4 Receipt 和 Verify 绑定同一字段与模块身份，不主动预置、归零或恢复旧计数。Firebase Xcode 非 iOS 分支另冻结实际 Logger、空文件名/委托、级别、目标和命令行摘要，声明可能输出的固定文本。三个 SRP 空分支接纳已核 Android/WebGL 成对 DLL/MVID，仍要求正式包源码及完整资产/缓存条件吻合。上述合同和定向测试不替代真实 Player 构建。

复用项目中已有的完整 Pipify Batch 时，使用已开放的 `nova.project.pipify.run-batch`，传入当前活动 `PipifySettings` GUID 与精确 Batch 名称。Plan 展示设置文件 Hash、活动 BuildTarget 和有序 Step/参数快照；Execute 只登记一次异步任务，随后用 recovery token 轮询 Verify。任务失败、断线或 domain reload 后不得自动重放或宣称自动继续。

该 Batch Plan 不冻结 Scene 内容/启用闭包、Master 内容/当前坐标或 PlayerSettings，也不展示所有动态解析后的有效参数。Agent 必须在 Batch 外独立记录这些状态、适用的生成输入与输出基线，按平台串行准备、执行和前后复核；资源锁不代替完整快照。源码生成先于所有依赖它的热更 DLL 编译/Generate，进入构建前复用 `nova-project-preflight-build`；Pipify `build.package` 不自动执行完整构建就绪检查。准备导出与本地设置由对应已有 Operation 承担，批次含 CDN、商店或通知时另按实际目标授权。

TMP 与 Facebook 仅按独立已审分支进入构建：已有字体合法加载与空表单 atlas 的官方清理，以及空 singleton 到唯一既有 Resources 配置的正常绑定。Plan 可能初始化明确的内存缓存，磁盘内容保持；schema 4 保存实际 before/after/error，未知状态或不完整后验保持 partial。需要这些分支时再读 [TMP 细节](../../../Docs/Editor/PlayerTmpFontBuildContract.md) 或 [Facebook 细节](../../../Docs/Editor/PlayerFacebookBuildContract.md)，不预清、补配置或主动调用 getter。

## 既有 AAB 的设备安装分支

用户只要求把已核验的既有 AAB 转成指定设备 APK 集并安装时，选择契约 `android-aab-device-install` 分支，复用 [平台 CLI](references/platform-cli.md#android-aab) 的同一 `build-apks / install-apks` 入口。冻结既有 AAB 字节身份、实际工具与设备、签名策略、新 APKS 输出和保留数据的替换范围；不根据当前 PlayerSettings 推断旧 AAB，也不自动重新 Unity 构建。

该分支分别要求 `apk-set-build` 与 `device`：前者绑定正式 bundletool 转换回执、完整 APK 集及每项签名、输入 AAB/设备/输出与清场，后者证明实际安装读回、入口及业务运行。`build-apks` 不产生 Unity BuildReport；不能以该新证据替代普通 Player/Bundle 构建、Google Play 分发或其他设备验收。默认本地 Player 构建仍保持 `player-build` 和原 BuildReport 要求，所有既有副作用、凭据、确认、锁和写域约束继续生效。

## Artifact → Evidence

本地交付验收按契约 `externalCompletionPolicy=incidental-build-telemetry` 区分正式构建产物与附带统计外发：不要求以远端统计收件证明本地产物完成。原 `external-write`、凭据、确认、回调及失败检查全部保留；不推断统计送达，不扩大到上传、支付、部署等外部业务任务。显式追加的外部验收与同一 Workflow 内其他外部任务仍独立必需，详见 [本地构建验收](../../../Docs/Onboarding/LOCAL_BUILD_EVIDENCE.md)。

当前强化后的输入、回调与恢复实现仍在逐分支验收，未知分支保持 `blocked`。2026-10-15 阶段收尾仅取得最新 Android 编辑器编译和 WebGL Config 导出证据，尚未取得强化实现的 Player 构建、三平台启动或新 Player 测试执行证据；后续按实际 Plan 补齐，不能把阶段审查当作构建完成。

后续续作已取得 AOT/官方报告等待版本的 Player 38/38 与 Android 26/26 定向测试；当前 Builtin 两回调版本已编译并新增定向测试 3/3 通过，旧测试未在此版本重跑。SRP 空资产集、双 Core 空槽与 Probe 空集合取得当前 Unity 只读证据。两项 SRP 回调的局部受控实现只接受完整目标资产集为空，并冻结 APV 原树后验；非空 SRP、自定义 getter、其他尚未审完的 shader 回调继续阻断。构建后 Core 缓存残留无法唯一归属时完成其他授权恢复并返回 `partial`，不清旧缓存或 APV 数据。测试及只读证据不代表强化 Player 实际构建或三平台启动完成。

直接 Player Action 在写入前拒绝 HybridCLR AOT 重建目录与冻结输入、正规 Bee 裁剪源或本次 Player 产物重叠；构建后仅接受与本次 `ManagedStripped` 逐名及 SHA 一致的非空平铺 DLL 集，额外文件、子目录和缺失回执摘要均不通过。`Unity.ProjectAuditor.Editor.Build.LastBuildReportProvider` 已接入独立报告基线和官方延迟复制等待：最多等待十秒，保持主线程且不阻塞 Editor update，不执行或移除官方回调；完成后核对本轮 BuildReport GUID、源和复制目标完整摘要。原报告备份只保存在 Plan 明确展示的本地恢复目录，正常完成或准确恢复后清理。超时、复制持续失败或归属不明时返回 `partial` 并保留恢复材料，不假称恢复；Verify 只读检查，不重放构建或自动覆盖报告。该回调局部合同不解除其他尚未审完的 Player 门，不能通过普通 Build API、任意代码或未审 Batch 绕过它们。

WebGL 本机验收复用随包 `scripts/serve_webgl.py`：`inspect --build-root` 返回目录 `contentHash`，`serve --build-root --expected-sha256 --port` 绑定同一摘要，仅监听 `127.0.0.1`；实际线程构建才启用 `--cross-origin-isolation`。冻结完整导出文件，正确处理 gzip/Brotli 与 Wasm MIME；端口占用、路径越界或文件漂移直接失败，不接管服务、不改产物。Android/iOS 的安装、签名及运行按平台 CLI 分层处理，本地构建请求不自动触发设备写入。

Artifact 是 `BuildReport.summary.outputPath` 指向的本地 APK、AAB、Xcode/Android 工程或 WebGL 目录，以及对应 BuildReport。只有 BuildResult 为 `Succeeded`、输出存在并对应冻结的 Target、Scene、模式和路径时才返回 `success`；代码编译、旧包存在或 Bundle 成功都不能代替 Player 构建证据。

报告实际 Adapter、冻结参数、BuildReport 摘要、产物路径、临时设置恢复结果与未执行的 Player smoke。YooAsset staging 只复制并同步导入本轮临时资产；不会全局保存其他正在编辑的资产。同步清理失败会保留副本与 marker 并抛错，须处理冲突后重新执行；不能以 BuildReport 成功替代清场结果。构建失败、产物归属不明或恢复异常时返回 `partial` 或 `blocked`；本 Skill 不据此声称真机运行、商店上传或外部服务验证成功。

用户同时要求运行、原生编译、签名设备或存档验收时，按参考逐层采集证据并分别报告；默认本地构建成功不代表组合任务完成。Batch Job 的 `Succeeded` 只证明 Runner 结束。清理本轮临时对象、生成噪声与测试态配置，保留要求交付的正式产物和用户已有数据；清场结果须随产物交付。


Android 内部父合同的阶段性实现见 [Agent Actions 的 Android Nova 父链合同](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md#android-nova-父链内部合同)。它提供实际八型构造/排序/同 Context 观察与准确 Manifest/Proguard 预测，不执行 SDK，也尚未接入公开 Android Player 准入；不能把离线纯回归、WebGL pin 或旧 Bundle 成功当作 Android Player/Generate 证据。正常 SDK 叶子、导入/Gradle/EDM/Burst 和真实平台编译仍须分别闭合，外部凭据 POST/未知脚本由用户手动。

Android SDK叶子验证按真实Nova八型完整工厂和本次原Runtime配置展开，不按EnabledSDKs删减工厂成员。内部`NovaAndroidSdkLeafBuildContract`只提供配置来源/引用/完整JSON摘要、原纯规则复用及准确未接效果查询；它不构造或提前执行SDK回调，不包含Complete许可，也不接收可伪造的Parent frame。真实Context/list/report/同SDK实例和constructed/pre/post历史只由原`NovaAndroidParentBuildContract`及唯一内部observer scope负责，叶子不能用外层较晚状态补造。Framework不新增SDK程序集强依赖：两个SDK内部纯builder只通过已核factory类型的固定metadata签名复用；无cctor、无任意方法/ctor/业务getter，仍需真实Android独立DLL/Module/MVID pins。

Google/FB/MAX规则从原const读取，Firebase与AppsFlyer的纯树由原回调调用同一builder，不复制Manifest转换算法。配置冻结仅未来实际Android Plan可调用：原active Runtime及Assets主资产path/GUID/localId/Native ID/dirty、源/meta与完整Native JSON摘要，四配置类型独立ManifestModule/MVID/DLL，以及原选择位置/引用都须保持；不输出Key/Token/配置正文。provider读取与序列化自然效果仍须真实观察，不称Native零变化。配置存在不等于对应SDK必定写入，也不等于SDK ready。

本批的固有缺口不会因调用者把八个可变模型自报complete或清空required而消失：Native启用场景的PreviewScene生命周期/AppInstaller、AF无条件properties重写及正规ImportAsset，以及实际provider捕获仍待接。非空AdMob广告配置需核自然唯一enabled选择、固定官方Settings资产/可能目录/meta创建与定向保存；MAX必须按原首个Max渠道（不按Enabled过滤）与原Instance真实选择/可能创建路径冻结，不能泛放Assets/MaxSdk；Facebook非空配置涉及原全局dirty保存集、Refresh、同实例m_AndroidSettingsApplied及官方AfterNova Manifest完整写集。自然no-op也需真实分支证明，不假设缺配置、不单独提取/输出/传输Key或Token、不预热单例或改开关。AppLovin携sdk_key的credential POST、iOS ios_setup2 POST及其未知下载Ruby执行三步继续用户手动，设备/业务环境/UPM/Raw按原延期。

叶子候选的纯规则、缺口与组合测试不能代替Android活动域TypeCache/SDK Native回调、正式Generate/Player或产物验收；公开Android门在全部callee合同闭合前继续阻断。

配置捕获及binding复核在读取原provider或调用任何纯builder前，均直接复用`Parent.VerifyFactoryBefore`重核真实Android同域TypeCache、完整处理器Module/DLL、源码及BCL；不能靠可变Factory DTO或同名类型自签。这沿用原休眠桥要求，活动observer租约期间拒绝另取工厂；在租约之外复核，不把外层后态补历史。永久规则fixture只读当前已加载的真实SDK类型，缺当前平台模块时明确Ignore/not_applicable，不引入任何NovaSDK派生测试型；独占tmp单源SDK片段只作离线纯回归，不能充作真实Android身份。
