---
name: nova-project-generate-hybridclr-artifacts
description: Use when 已配置 HybridCLR 的 Nova 消费项目需要按明确 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标生成 bridge、AOT generic、Il2CppDef、link.xml 和 AOT 裁剪准备产物时使用。
---

# Nova 生成 HybridCLR 预构建产物

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

以下资料仅在当前决策分支按需读取。

只读取当前决策分支需要的事实：先读 `references/contract.json` 冻结输入、写入集、锁和确认门；需要核对生成行为时再读 `Docs/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md`，选择既有 Pipify Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。不要加载完整构建、Bundle、CDN 或运行时链路。

用户已要求完整三平台验收时，仅按需读取 [三平台验收参考](../nova-project-build-player/references/platform-validation.md) 的发现、生成顺序与快照段落；平台差异使用同一参考，不新增平台 Skill。

## 适用范围

该 Operation 只生成当前 `activeBuildTarget + DevelopmentBuild + 激活 ConfigMaster 当前 Platform/Channel/DevelopMode` 对应的 HybridCLR 预构建产物：热更编译输出、MethodBridge/Reverse PInvoke、AOT 泛型引用、Il2CppDef、最终 link.xml、当前 Target 的 AOT 裁剪目录与裁剪所需临时工程。

它不生成最终 Player、平台工程或安装包，不构建 Bundle，不上传 CDN，不拷贝 AOT/Game DLL 到业务资源目标，不执行运行时加载或设备验证。`GenerateAll` 内部用于产出裁剪 AOT DLL 的 script-only `BuildPipeline.BuildPlayer` 是临时生成步骤，不是最终 Player 成功证据。

## 冻结与预检

执行前读取并冻结：消费项目根、当前 `EditorUserBuildSettings.activeBuildTarget`、期望 `DevelopmentBuild`、当前激活 ConfigMaster 的稳定身份及其 `Platform/Channel/DevelopMode`、Build Settings 中已启用场景闭包、HybridCLR 安装/启用状态、解析后的生成输出集合、最终 link.xml 路径和执行入口。

必须确认目标平台模块与 IL2CPP/HybridCLR 生成前提可用，当前 active Target 与冻结值一致，启用场景非空。分别解析 HybridCLR `outputLinkFile` 与当前坐标 `LinkXmlTargetPath`（空值按 `Assets/link.xml`）；两者均须为合法项目内路径。HybridCLR 生成热更引用保留表，Framework 在 Master 路径补全 AOT metadata 条目，两条路径可不同；Plan 分别冻结并声明写入，Receipt 分别核验，相同路径只计一次，不修改配置来迎合本 Operation。`DevelopMode` 不能代替 `EditorUserBuildSettings.development`。

Plan 同时冻结实际场景资产、PlayerSettings、临时 script-only/Android 导出/构建位置以及全部发现的 Player 回调编译身份。持久化脏资产、旧 YooAsset staging marker 或 SDK/业务/包回调没有确定写入契约时明确 `blocked` 并展示具体回调，不按“临时裁剪”假定安全，不扩大 `Assets/**`，不通过任意代码绕过。可执行计划中的共享构建缓存与 Android Manifest/Proguard 也属于确认范围；正常裁剪目录已有 YooAsset staging 跳过，不应产生新的 Resources 临时副本。

从已保存工作区、Batch、Config 与 HybridCLR 设置读取可确定的输入、输出和最终 Player `DevelopmentBuild`，不要求新手重新填写。所有参与本次编译的 Table、UI、Network HostKey/NetCmd/Proto、资源密钥等生成代码，必须在首次依赖它们的热更 DLL 编译或 `GenerateAll` 前完成正式导出、Unity 导入和必要编译；仅核对本次实际依赖，单次 Generate 不自动触发无关全量导出。`export.excel.all` 不包含 `export.config` 或 `export.network.proto`。导出不在本 Operation 写入集时由组合任务先复用对应已有 Operation 或已审 Batch。

桥接代码、热更 DLL 与最终 Player 必须使用同一 Target/DevelopmentBuild ABI 档位。Pipify Runner 会临时将 HybridCLR Step 对齐其后首个 `build.package` 的 DevelopmentBuild 并恢复，但单独 Action 要求 Editor 当前值已匹配；Agent 在批次外核对实际档位与 MethodBridge 标记。跨平台切换串行进行，每次稳定后重新 Plan，不复用另一平台的 Receipt。

必要诊断结束后，先等待正常编译和域重载完成并进入稳定域，再建立需要重载验证的正式 Generate Plan。当前已审 `execute_code` 通过 `Assembly.Load(byte[])` 载入名为 `MCPDynamic` 的普通内存 PE 程序集，名称含 Dynamic 但 `IsDynamic=false`，正常域重载后不自动重放；不能把该诊断程序集带入正式计划的程序集基线。READY → Execute → Verify 完成期间，不再额外运行 `execute_code` 或 Native probe。保留真实捕获与严格比较，不预热、不删除已捕获的程序集行、不重插程序集，也不在事后回填或升级旧证据。

编译记录的未登记 `firstReloadDomain` 经 Unity `JsonUtility` 往返后可为空字符串；登记首次真实域时仅将 null 与空串视为同一未登记状态。已重载、非空标记、旧协议、缺少有效 nonce、错误令牌或过期记录仍被拒绝；不补签历史操作。

确认绑定上述全部值、两种 link.xml 的规范化路径、完整生成输出集合、临时裁剪目录、会被重建或失效的生成文件/缓存及执行入口。确认后任一值漂移，旧确认失效并返回 `blocked`。

WebGL 当前真实目标复用同一 Action 内的 Player typed adapter；具体输入投影、真实报告、环境转换、重载证书与新域持久配置门统一按既有 HybridCLR 文档执行。它保留完整未知回调拒绝；Android 合同不因 WebGL 复用而变化。所有现存拥有输出后代禁止链接；Assets 输出父链/meta 必须既存，ProBuilder 未自然初始化或有未保存字典时明确 blocked，不预热 getter/Init/Save。

## 执行入口

Framework 已注册并开放 `nova.project.hotfix.generate-artifacts`。先用 `describe` 读取当前 Schema，再按冻结的 Target、DevelopmentBuild 与 Config 坐标执行 `plan -> execute -> verify`；清理或重建生成目录仍必须使用当前一次性 PlanId 确认。Action 内部固定 `GenerateAll -> ValidateLinkXml`、锁、漂移检查和恢复验证，不得退化到 `execute_code`、反射或临时 C#。

Android 使用独立计划与真实阶段证据，未闭合回调或 AF 导入尾链仍 blocked；准确边界见 [Android Generate](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md#android-generate-的独立接线与未完成边界)。用户已要求运行同范围、已审且已保存的 Pipify 组合时，可复用 `nova.project.pipify.run-batch`；缺少适用组合先进入 `nova-project-configure-pipeline`。批次外冻结场景、Master、坐标、Development、生成写集与实际外部副作用，保存后单独 Plan/Execute/Verify；失败或重载只查原任务，不重放。该入口不改变专用 Action 的阻断状态，也不把 Job 结束当作完整生成与编译证据。

## 生成后验证

本地交付验收按契约 `externalCompletionPolicy=incidental-build-telemetry` 区分正式生成物与附带构建统计：不要求以远端统计收件证明本地生成完成。原 `external-write`、凭据、确认、回调及失败检查全部保留；显式追加的外部验收与其他外部任务仍独立必需。回执、生成物或报告问题不因该策略而放行，详见 [本地构建验收](../../../Docs/Onboarding/LOCAL_BUILD_EVIDENCE.md)。

以当前 HybridCLR Settings 的解析结果核对，不猜测硬编码目录：

- 当前 Target 的热更 DLL 编译输出存在；当前 Target 的 `AssembliesPostIl2CppStrip` 目录存在且包含 AOT DLL。
- `GeneratedCppDir` 下 MethodBridge、`UnityVersion.h` 与 `AssemblyManifest.cpp` 存在；MethodBridge 必须包含独立规范行 `// DEVELOPMENT=0` 或 `// DEVELOPMENT=1`，并与冻结 DevelopmentBuild 一致。
- AOT 泛型引用文件、HybridCLR 生成引用 link.xml、Master AOT 保留 link.xml 和临时裁剪工程已生成；两份 XML 均须有 `<linker>` 根节点。随后执行的 `ValidateLinkXml` 已使当前坐标解析出的每个 AOT metadata assembly 在最终 link.xml 中存在不带 `.dll` 后缀的 `<assembly fullname>` 记录；由该入口新补的记录带 `preserve="all"`，已有同名 assembly 不被重写。
- 等待生成引发的 Unity 编译结束，记录无编译错误证据；再次核对 Target、激活 Master、三维坐标与 DevelopmentBuild。当前 Action 要求请求 DevelopmentBuild 已等于 Editor 当前值，不替调用方切换；底层生成步骤的临时状态仍需恢复。

Action Verify 的产物证据只覆盖 Receipt 明列的三个目录与五个文件（两种 link.xml 同路径，共八项）或六个文件（不同路径，共九项），并核对 MethodBridge DEVELOPMENT、生成引用 XML 结构与 Master XML 的 AOT 条目；不证明整个 `GeneratedCpp` 目录、所有 C++ 产物或缓存完整。WriteSet 中的 `Il2cppBuildCache` 仅表示可能被删除/失效，不能当作已验证产物。Verify 另复用既有编译跟踪，以本次令牌和完整载荷 Hash 核对实际程序集编译、零错误及脚本重载，核对执行后坐标与临时选项；Agent 仍记录 live 返回与清场结果。

只接受本次 finalized Receipt；Verify 同时核对完整冻结上下文、回调和临时设置恢复，产物集合必须精确、唯一，不能用一份 link.xml 的证据代替另一份。旧或未完成 Receipt、执行后上下文漂移均为 `partial`，不读取当前配置补造旧计划。进入生成后的异常保留未完成 Receipt 与可能写入项；不重跑、不删除共享缓存或他人残留。

最低证据是 `generated-output+compile`。只拿到日志、临时 script-only BuildReport、部分文件或未结束的编译，均不得返回 `success`。

## 结果状态

- `success`：全部冻结值未漂移；generate -> validate 完成；目标 Target 的生成输出、MethodBridge DEVELOPMENT、AOT preserve 与 Unity 编译全部通过；临时状态已恢复。
- `partial`：已产生允许范围内的生成物，但后续生成、validate、输出核对、编译或恢复复核未完成；列出已写入项与剩余风险。
- `blocked`：输入、平台前提、场景、锁、确认、路径合法性或执行入口不足，且尚未产生本次写入；给出最小缺口。
- `not_applicable`：项目未安装/启用 HybridCLR，或请求核心是最终 Player、Bundle、CDN、DLL Copy、运行时/设备验证、HybridCLR 升级或配置修改。

不得把本 Operation 的成功描述为 Player、Bundle、CDN、运行时、IL2CPP 真机或发布成功。最终 Player 构建使用 `nova-project-build-player`；Bundle 使用 `nova-project-build-bundles`；只刷新业务热更 DLL 使用 `nova-project-refresh-hotfix-dlls`。

清理本次临时验收对象、脚本、marker 和测试态配置；区分 Generate 的正式交付输出与可删除的测试临时产物，不因清场删除契约要求保留的生成文件。报告清场与原状态恢复结果，不清理其它 Agent 的运行环境。

原 Player 回调的统计外发属于当前 Operation 的 `external-write` 效果，计划列明已核条件和字段并保留绑定确认；未审下游仍阻断，不临时关闭 Analytics/telemetry。生成物与编译证据不证明外部统计送达。


当前真实 WebGL Generate 使用与 Player 相同的准确回调捕获、typed 执行后验与真实 BuildReport，输出只接受已审默认 `HybridCLRGenerate/AOTGenericReferences.cs` 和 `HybridCLRGenerate/link.xml`，Master AOT XML 仍按独立冻结路径处理。父目录与其 meta 必须既存；拥有目录内全部现存后代均拒绝链接，包括悬空链接。Android 未闭合的回调合同继续阻断，不能因 WebGL 分支通过而放行。

完整写前/写后 typed 材料分别保存在 `Library/Nova/AgentActions/HybridCLRGenerate/<本次 N-GUID>/before.json` 和 `final.json`。Plan/WriteBefore 保持原完整 before 严格 UTF-8 JSON 原文 8MiB 硬限，即使 files 大量重复、可以压小也不得接纳。final 完整 DTO 原文不超过 8MiB 时继续使用原裸 typed 字节；只有更大的实际 final 才可使用下述固定双槽无损行表 v3，最终未压缩 wire 仍最多 8MiB，完整展开 DTO 另外严格限制 16MiB。Plan 只展示完整计划并保存有限未写指针，不创建证据目录；Execute 要求 GUID 目录不存在，先原子写完整 before，再恰一次生成，正式收尾及全部后验通过后保存完整 final。小 Receipt 继续绑定固定文件的准确 wire SHA-256 与长度，完整 Operation 预算限 192KiB，预留 Store 后续状态空间；不截断快照或扩大 Operation Store。两个文件均列入准确 WriteSet 与正式 Artifacts，须保留供 Live Verify 读取；`report-backup` 子目录与正式证据分离，Auditor Commit 不删除这两个文件。

final 的 `nova-hybridclr-generate-file-rows-v3` 只复用 `before.inputs.files` 和 `afterInputs.files` 两个准确 `FileState[]` 槽；根级字段恰新增 `fileRowCodec`、`expandedLength`、`expandedSha256`、`fileRows`，原有序数组暂以整数索引表示。行表保存完整 path/kind/sha256 或 null，最多 32768 个唯一行，两个槽合计最多 65536 个引用；重复、null、顺序及缺失/空数组完整还原，解码后不共享可变行对象。只接受规范 v3 JSON、准确已知版本/字段/类型、规范首次使用次序、无重复/未用表行及有界整数引用；先按完整行 UTF8 计算整体展开预算，再复制行，完整展开长度与 SHA、全部原 DTO/SVS 严格校验和规范字节必须一致。原证书和回调摘要继续针对原完整 DTO 计算，Reference 版本/独立 before/两个文件长度与 SHA 绑定不变；旧裸 typed 证据继续可读，8MiB 内写入字节不变。此 codec 不使用外层 gzip、不丢 before/after 快照、不引用另一个文件或较晚捕获；非共享巨大字段仍受原 wire 上限约束，缺失 final 的旧 partial 无法借此回填或升级。

编译 payload 绑定上述准确文件摘要和编译前证书。重载后先核原编译令牌、载荷及 requireReload，再读原文件重核历史自洽及当前持久源/配置/输出；不把旧 managed 对象当作当前对象。官方 Refresh 正常执行，当前WebGL执行由本次公开租约保护至Dispatcher持久化尝试后；旧未 finalized 的早重载 Operation 仍保持 partial，已有 before/产物保留，不重放。WebGL 不在跨 await 的外层 finally 写回用户设置，恢复证据来自原同步 StripAOT finally 和真实 typed 后态；尚未尝试final时，失败/取消/收尾错误清除完成标记和证书并只首存一次；final已尝试后的编译前历史不改写或重试，外层失败Receipt保持未finalized。外部 telemetry 仅按原回调自然条件发生，产物或本地证书不证明外部送达。

Plan 的 `webglGeneratePlan` 保持完整可审阅，运输表示使用专用 `nova-hybridclr-generate-plan-gzip-v1` 封装：字段恰为 `codec`、`rawLength`、`rawSha256`、`gzipBase64`。固定本地 serializer 保留所有 null 和默认字段，严格 UTF-8 原文最多 8MiB，单 member gzip 最多 1MiB；规范 Base64 无空白，原文长度与 SHA-256 均绑定。`TryDecodePlanPreview` 只输出审阅 JSON，核严格 JSON/已知 DTO 字段、CRC/ISIZE、精确 DEFLATE 终点，拒绝截断、尾随/串联、超额解压、未知/重复字段、类型元数据和嵌套 codec。解码成功不代表业务 Plan 有效、没有漂移或获准执行。

Plan DataJson 同时保留原小 Receipt 与完整压缩预览，最多 1536KiB，为现有 2MiB 正式 Plan 响应保留外层空间；完整 Operation 仍限 192KiB（Store 256KiB 不变）。预览编码只操作本次内存 DTO，不写 Plan 文件、不截断数组、不更改原 State/Runner 绑定，也不参与 Execute、恢复或正式证书。最终 before/final 文件及其编译载荷绑定继续使用原完整证据协议，超预算明确 blocked。

当前真实 WebGL Generate 的 Execute 在输入重新核对后、写 before/生成前取得本状态唯一的公开 `EditorApplication.LockReloadAssemblies` 租约。正常 Refresh、编译及官方报告自然等待继续；不等待域重载自身，不新增 isCompiling/isUpdating 的 idle 门。完整 typed 后验、final 原文、原编译 Schedule 与 Receipt 返回后，由既有 Dispatcher 在 `TryCompleteExecution` 持久化尝试后的 State.Dispose 主线程配对一次 Unlock。持久化返回 false 时仍释放且保持 partial/warning，不宣称持久化成功。Plan、未执行/过期清理不获取原生锁；普通 Player、Android、Pipify 不变。

30分钟是 linked cancellation token 的协作期限，计时线程只取消，不释放锁；同步原生 Build 不能强杀，只能返回后再观察。取消、异常或超时保存可用实际材料后同样经 Dispatcher 清理自己的锁，绝不循环清除他人计数。仅尚未尝试 final 保存时可降级并首次保存失败材料；已保存的 final 是不可变编译前历史，之后取消/调度失败不改写或重试，partial Receipt 的 finalized 保持 false。最终正常编译与 requireReload 仍由原令牌/完整载荷协议核验；旧早重载操作保持 partial，不重放。不扩大AutoSync输出拥有域或复制授权；后续真实漂移照旧阻断，正式Copy按独立合同消费。

TMP 资源登记只接受原 before 已实际加载且明确 marker 的准确字体，完整 map/weak typed 行证明原 winner/live 槽保持、有限源预测首次登记及首 dead 槽复用。非清理字体 JSON/dirty/源/atlas 内容与磁盘均不变；不可读 atlas 缺有效原生内容 hash 则 blocked，不改 readable 或预热。Scope.Dispose 失败也保留其已捕获的实际 TMP after/error，不能用较晚 current 补造；旧缺行回执仍 partial。只在该分支按需读 [TMP 合同](../../../Docs/Editor/PlayerTmpFontBuildContract.md)，完整材料继续受原 8MiB 单文件预算保护。

会 Roslyn 编译的诊断（例如编译式 execute_code）必须在 Plan 前完成：它可能产生 `IsDynamic=false` 的新程序集并改变冻结的实际程序集闭包。ready Plan 与 Execute 之间只做不编译的现有资源/磁盘核对；发现闭包漂移使用新 Plan，不排除诊断程序集或重放旧 Execute。此规则不将任何 Unity getter 或任意脚本变成可执行 Action。

TMP 的同域后态由已保存 Scope after 与当前冻结观察核对，不再为了验证重载自然卸载的未清理字体。缺席必须原 clean、无原/后强资源或活 weak，并且 Scope after 与当前均已缺席；仍核完整磁盘/元数据/原输入绑定。新实例、正文/文件变化或缺少原 after 保持 partial。生成后新域规则没有因这项同域修复放宽，不能将旧 partial 升级或重放。

WebGL Generate 的场景可见性使用独立合同：Plan 保存唯一既存 Native `SceneVisibilityState`/manager、完整三表原文、原 dirty、Main Stage、全部主场景、既存 Preview、准确 source-owner 与全部无有效场景对象，以及准确 `Library/SceneVisibilityState.asset` 文件。三表和完整观察分别使用有界无损 gzip，保留原文字节长度与原文/规范 SHA，不用摘要代替正文；Preview 与无场景对象保留全部组件的原文、ID、GlobalObjectId、hideFlags 和源码/DLL 身份。仅支持全部持久标记数组为空且全部对象四项隐藏/禁选标记为 false 的已证分支；未知对象、角色、组件、源或 DLL 指纹一律拒绝，不创建、关闭或删除它们，不替用户 ShowAll。既存 SceneView 通过准确 backing 字段绑定两相机与三光源、Preview 及 window GUID；既存 DeformationManager 通过已审 backing 绑定唯一 helper。MAX executor 与 InternalIdentityTransform 只作为原 Native 实例保护，不调用 SDK。

公开四项 flag 查询并非 Native 零变化：本机完整前后材料证明查询可仅在 Visibility 表物化准确 owner 的当前 Preview 空行。因此每次 Capture 独立保存真正 query-before 的完整三表、dirty、Library 状态及查询后态；只允许该 Visibility 空行、dirty 与 Library 完全不变，原行/key/value/顺序全部保留。同域写前和较晚 Verify 必须先核 query-before 原文字节 SHA 等于上一份 post-query，再核完整后态相同，防止删行后查询重建掩盖变化。正式同步 Generate 的三表跨度继续要求原行保留，新增仅严格零 GUID、非零 Int64 handle、两个空数组；原有自然 dirty true→false 与准确单 Library 文件写合同不变。Scope 的真实原材料不可用后来 Capture 补造。输入行仅投影为明确历史 `GenerateSVSProtectedBaseline`，当前实际材料独立保留并绑定原证书。

Generate 的真实 before→scopeAfter→原同域 Observed 另有有限对象转换校验。已保存、加载且 clean 的非 Preview 场景按准确64位 handle、场景 GUID 与完整 GlobalObjectId 唯一双射；GlobalObjectId 自身的场景 GUID 必须相符，所有非 ID 的 ObjectRow 字段逐项保留。每个场景只能全组原 ID 保持或全组 disjoint 重建，拒绝部分替换、旧 ID 跨场景交换、新增/缺失/重复与任何四 flag、名称、hideFlags 变化；scopeAfter→Observed 的新组 ID 须稳定。既存三 Preview 光源、全部 owner、四个核心角色及固定无场景对象仍严格保护。普通场景 ObjectRow 不包含组件正文，不把该双射冒称业务组件验证。

无场景对象唯一临时变化限于同一准确 SceneView owner 的主 Camera：同一 Native/component/globalID、类型、hideFlags、源 DLL 指纹，完整固定 Camera JSON 形状内的 m_HDR 布尔 false→true→false。scopeAfter 原文只能恰一次该布尔替换，所有其它字节不变，原 Observed 必须恢复 before 的完整无场景对象原文；gizmo、未知字段、重复键、其它属性或未恢复均拒绝。不归一化或覆写任何三态 DTO，不用较晚新域观察补造原恢复证据。

正式 Verify 唯一使用不同名 Observed 入口：先验证原证书绑定的完整三态，再以原稳定 Observed 与本次真实 Capture 比较；本次须为当前域且四个活弱见证准确。新域沿用原真实 token/payload、首次域证明及既有重载门，比较基线为已验证的原 Observed；原 scopeAfter 始终保留为 witness key 和 scopeAfterSnapshotSha256，原 final/payload 继续绑定原 Observed。旧无独立 Observed 的编译入口保持原严格 Native 门，不能借较晚 current 获得新例外；全部写前 BeforeEquals/NativeIdentityEquals 与冻结比较保持原文。历史 partial 不升级，必须使用新 Plan、新 Execute 的完整实际证据验收。

跨脚本域默认 blocked；只有原正式编译 token/payload hash 对应本轮 Schedule 的新协议 stamp、随机 nonce、准确起源域，编译实际成功且当前恰为首次不同域时，才允许已核源 owner 的两相机、三光源、Deform helper 的 Native 子 ID 与 Preview handle 自然重建。从原稳定 Observed 到首次重载观察，主场景、owner ID/GlobalObjectId/window GUID、全部原文及非 ID 字段、四项实际 flag、固定 MAX/Internal Native 身份、dirty 与 Library 文件均须精确保持。首次 reload 到 query-before 仅准 Picking 表新增准确新 Preview 空行，随后的公开查询仅准 Visibility 表新增同 handle 空行；不许可 isolated 表或未知句柄。任何后续 reload，或正式周期结束后又开始新编译，都不可补签/复用新许可；旧记录无新协议不补造，但其它导出的旧 `TryVerify` 语义保持不变。旧 schema、缺少完整原材料、未知变化仍为 partial。合同不反写 JSON、不 SetDirty/清 dirty、不保存，也不扩大普通 Player/Bundle 门。单快照96KiB、完整 JSON2MiB/gzip32KiB及完整证据8MiB门保持不变，超量明确拒绝，不截断材料；候选纯测试/离线编译不代表正式 Generate 或 Player 成功。

首次新域完整后验通过后，本域仅以原 token/payload hash、原 Scope 完整指纹及 Schedule nonce 绑定首个实际 post-query 的完整自有 DTO 字符串；后续 Verify 先要求此次 query-before 等于该首 post，再核 post 完全不变，不能再次泛用 P/V 空行许可。见证不强持 Native，也不跨后续域。

真实 WebGL Verify 保留原 `DataJson` artifact 数组，在既有 `Evidence` 中返回 `nova-generate-svs-postcompile-v1:` 前缀与严格 `nova-generate-svs-postcompile-gzip-v1` 信封：完整首次/当前 Snapshot、实际编译记录和域、原 before/final 长度/SHA 指针、Scope 完整 SHA、本次 adapter 结果及新后验证书均无损绑定。ASCII gzip/Base64 信封防止 Gateway 路径脱敏改动原正文；单正文160KiB、gzip32KiB、外信封64KiB，仍不改原8MiB Store。解码只供只读核验，不能产生执行许可。调用方须保存实际工具完整返回并记录 SHA，Operation journal 只保存状态/消息，不能代替这份后验归档；Verify 不偷偷写新 sidecar、不改旧 final 或 Receipt。真实后验不足或返回越界仍 partial，不能把候选纯测试当实际 Generate/Player 通过。

预计 Verify 捕获较慢时，先核当前 Tool Schema 已提供 `verify_mode/request_id`，再用同一 `operation=verify` 的 `start` 携带原 `receipt` 和 Agent 新建编号，随后只用同 `action_id/request_id` 的 `status` 取回同一次完整脱敏响应，字段与边界见 MCP 包内 `Docs/INDEX.md`“长核验的传输记录”。断线不重复核验；`completed` 仅表示收件完成，必须读取原领域状态并归档完整 Evidence 与 SHA，不能把 partial 升级。省略模式仍是原同步协议，旧同步丢失的 Evidence 不补造；过期、重载或失败不自动换号重跑，不扩大原预算或权限。

建立全新 Plan 的首个 SVS 输入许可使用零参数 `CaptureInitial()`：内部恰一次真实 Capture，并完整校验其真实 query-before→post 有限 Visibility 暖化、dirty/Library 不变、当前域及同一活 Native 弱见证；同一个完整 Snapshot 原样作为 baseline/observed 保存。不把首个观察误套“前一 post 必须等于本次 query-before”的两次观察比较，不二次 Capture 预热或丢弃真实前态，也不接受任意外部 DTO 初次审批。随后的准备、输入捕获末尾、Execute 与重复 Verify 继续使用原严格 `BeforeEquals`/已有 witness 门。

完整计划预览与 before/final 存储只在四个准确 typed 位置识别 SVS：`GeneratePlan.sceneVisibilityBefore`，以及 `GenerateEvidence.before.sceneVisibilityBefore`、`sceneVisibilityAfter`、`sceneVisibilityObserved`。每个非空 Snapshot 必须先通过固定 wire 字段/类型、完整 body/observation 解码、原文/规范 SHA、已审来源及全部原有预算校验；只有该原树直系 body/observation 的 `gzipBase64` 得到存储表示许可。存储器原有省略 null 引用字段的表示保持，所有值字段须显式存在，不能用 DTO 默认值补造证据。其它位置、Receipt、未知 codec/嵌套计划封装与任何 `$type`/`$id`/`$ref` 仍拒绝；纯存储校验不读取 Native、不核当前域弱见证、不授予输入投影、执行或恢复许可。单快照96KiB、完整正文2MiB/gzip32KiB、预览及8MiB证据总门均不变，原 gzip 正文不被重编码或摘要替代。

正式Generate Plan若同次prepared输入捕获成功而原`PreparedInputsPreserveOriginal`拒绝，会在原blocked说明后附加纯DTO差异诊断；原比较器、准入及原错误含义保持。诊断仅克隆这同一次original/prepared，用原sha清空及精确TMP dirty行投影规范化，不重捕获、预热或改Native/磁盘。保留两份实际input SHA、规范化成功/失败阶段、完整规范化输入SHA及各变化字段的完整值SHA/类型/数量；一般字段不返回配置正文。effective行按完整文本多重计数返回真实变化行，另保留索引差异总数、序列SHA及纯顺序变化，重复不能被集合差分漏掉。

诊断含中文前缀最多15KiB，给原失败和Dispatcher前缀预留1KiB。单行前插造成大量索引移位时先整体省略index明细，仍保留紧凑完整行计数差异；后者仍超预算才整体省略，并明确`detailsComplete`/`effectiveRowDiffComplete`等false、真实总数与完整`rawDiffSha256`/长度，不截字符串冒充全量。规范化异常只返回阶段、类型和异常消息SHA，保持blocked。`rawDiffSha256`绑定加入自身SHA/长度字段前的脱敏前完整诊断树；Gateway会继续脱敏项目路径和URL，不能从脱敏后的JSON直接重算该原SHA，调用方应独立保存实际完整工具返回并记响应SHA。本诊断不写sidecar、不创建Operation、不授予执行/恢复许可；旧blocked返回不能据事后捕获补造原因。

正式Generate准备失败的字体缓存诊断现只使用**同一次原guard判定**的纯读取记录：两次原输入捕获分别返回局部out记录，不给Snapshot增加字段，不进入inputSHA、Store、Preview或准入比较。原9参输入入口、2参失败描述器及单参guard名称仍唯一；其他调用保持原路径。来源/importer/唯一owner/直接ID及完整零JSON条件保持。实证一次Texture ToJson可令同一导入缓存的Native尺寸从256²变为0²；当前guard有意将原唯一ToJson移到6项元数据及第二hideFlags之前，随后仍要求width/height=0、Alpha8、不可读、mip1、Tex2D及准确标志全部成立，不能把序列化前256²与序列化后正文混成一个状态。原6项内部&&顺序和异常边界保持，各既有getter调用点仍至多执行一次；顺序调整可使原先短路未读的后续门现在被读取，不能称两版实际Native读取总数相同。原生读取顺序已明确调整，不能称Native零变化；不新增ToJson、重捕获、字体加载、清dirty或预热，不接受256或任意TTF/hash白名单。

`fontCacheDecisions.countScope=collected-dirty-Texture2D-only`表示数量只覆盖原持久化dirty筛选中的Texture2D候选，不能称全部guard调用。记录仅保存原读取已返回的纯值；未执行点、已尝试但无返回、实际异常类型分别明确，最后尝试点不推断为异常原因。guard=false分支只绑定原Select已算出的完整dirty行；guard=true分支只有原已读path，最多作唯一path关联，并明确没有GUID/localID归属证明；缺匹配、多匹配或未请求采集不冒充通过/完整。变化行保留同一次actual输入SHA与原比较器拒绝，trace仅用于定位，不能授权排除脏资产。

完整记录以数量、UTF8长度和脱敏前SHA绑定；展示按阶段/首个拒绝门分组，提供实际整份样本并明确省略/完整性。分组展示受4KiB内部预算且与原失败说明共用15KiB总门；总量紧张时先整体省略trace样本/分组，优先保留完整变化行身份，所有省略标非完整。非纹理配置正文、Font完整JSON和凭据不进入诊断；Gateway仍可能脱敏路径/URL，实际工具返回须另归档并记响应SHA。原31纹理事后只读全通过只证明当时状态，不能回填旧Plan失败原因；新同次trace需经真实Unity回归及新的冷域正式Plan验证，纯测试不能代替该验收。

隔离自有LiberationSans真实阶段证据表明：首次显式ToJson前同ID为256²/Alpha8/不可读/mip1/Tex2D，唯一ToJson返回826字节完整零模板后同ID为0²，其余已测元数据、源/meta/dirty与inventory保持；本case随后Request前后均0，不能据此推断其它glyph行为。生产回归须在独立fresh Import/LoadOwnedFont后、任何fixture ToJson/前置helper前直接调用guard，分别测原bool及同次trace，核序列化后严格门、Source/meta/dirty归属及Material/Font inventory。分类纯负例仍全部保护；新候选的离线编译/纯测试不代替Root实际Unity或冷域正式Plan。异常分组仅凭实际非空异常类型字符串展示最后尝试点，不能将string-null的JValue.Type=String误报为异常；原历史null材料不改写。

上述inventory保持仅限定唯一ToJson紧邻前后。隔离实验原terminal的cleanup=false完整保留，随后专用资产工具清场另有独立证明：自有目录/meta与Font/Texture/Material均已移除；全局临时7个Material和4个Texture身份换出换入但数量相同，未清未知对象或RecentlyDeleted GUID缓存，不能称整个实验Native inventory完全不变。

Execute 已成功取得同次 current、原冻结比较返回 false 且没有具体 error 时，会返回至多 15KiB 的同调用纯 DTO 差异诊断。诊断只列外层及 Generate inputs/callbacks/SVS 字段指纹、长度/数量和 diff keys；dirty/TMP 对象行只显示受限类型、身份/路径 SHA、正文尾 SHA 与完整行 SHA，未知前缀仅摘要，不输出配置正文。重复项和顺序变化分别保留；超限整项省略并标记 incomplete、真实数量和完整差异指纹，不截字符串冒全量。恢复随机目录与未参与的 Request/lease 不被当作原比较判据。

这些是诊断 DTO 的事后纯比较，各原判据统一 notReevaluated；不会重新调用 SVS/TMP/Facebook/其他 BeforeEquals、弱 Target、Native getter、回调、捕获或重写已有材料。首查询压缩 observation 的差异不自动代表 SVS 门失败；DTO 全同仍可能是非序列化 managed/weak witness 门，保持 blocked/inconclusive。原 FrozenStatesEqual/GeneratePlansEqual 与成功路径不变；捕获失败或已有 error 原文保持，诊断不能作为许可或用于绕过匿名脏对象门。新的正式同次失败材料才能解释此次原因，分离快照不能回填为同次拒绝现场。

Generate 回调初始完整性门失败时，原拒绝条件、短路次序、typed after 校验及总 callback 合同摘要均保持。仅在该门已经实际失败后，使用该次调用已有的 before/after/current 回调 DTO 与 TMP 后态/错误存在标志追加 callbackDiagnostic；不重捕、不读 Native/getter/WeakReference.Target、不修改 DTO 或放宽合同。诊断先给出材料/chainReturned/unsupported/TMP 存在标志，只有存储摘要不一致时才按原摘要的十五个参与部分给出完整 SHA、计数与相邻阶段差异：八个有序数组部分（含 unsupported 与原 normal-reset 规范化）和七个既有 typed InvariantHash。行样本跨所有部分最多四条，只含索引、存在标记及完整行 SHA；原行、路径、配置、命令和密钥不输出。总错误含原 generic 前缀最多8KiB UTF8；超限整段省略并保完整诊断长度/SHA与明确 partial，单部分失败只标不完整，诊断异常不覆盖原失败原因。该消息只是失败定位，不是完整 final 或构建通过材料；旧 partial 不回填，需要全新 Plan/Execute 才能验收。

final 完整证据保存失败时，Execute 会把同一次 TryWriteFinal 返回的实际保存错误，同时保留于既有 Warnings 与原 Operation Message 的附加诊断；原生成/回调错误前缀不覆盖。附加段至多4KiB UTF8，保留原错误完整UTF8长度和SHA；超限只整份省略正文并明确标记，null或不可编码材料明确缺失/不完整。该诊断不重试 final、不重新序列化执行材料，也不改变8MiB Store、Operation预算、typed字段或编码。没有完整 final 的旧操作保持 partial，不能通过后来捕获、原 before 或现有产物补造丢失后态；必须使用全新操作取得完整验收材料。

Generate 仅对已审安装 DTO `BeeBuildProgramCommon.Data` 保存同次准确文件/加载前后态，允许正常构建首次加载这一行；其它程序集、全部原哈希材料和 typed after 保持。写前和同域复查严格；首个正式编译重载证明通过后才独立比较该准确 DTO 的自然存在性，不预加载、不删除原 Capture 行。未知安装指纹/路径、同名重复或其它漂移仍 blocked/partial；旧 partial 不因新合同升级。按需读既有 HybridCLR 文档，不新增 Action 或 Skill。
