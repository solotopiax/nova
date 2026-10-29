---
name: nova-project-build-bundles
description: Use when Nova 项目组需要为已配置的 YooAsset Package 按明确平台、版本、缓存与首包拷贝模式生成本地 Bundle 产物，并核验实际构建结果时使用。
---

# Nova 构建本地 Bundle

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`、`Docs/Onboarding/RESOURCE_WORKFLOW.md` 与 `Docs/Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md`。仅在发现适合本次任务的已有 Pipify Batch 时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；不要为直接构建加载全部 Pipify Step 说明。用户要求完整三平台或 WebGL 离线 Player 验收时，仅按需读取 [三平台验收参考](../nova-project-build-player/references/platform-validation.md) 的准备与 WebGL 段落。

该 Operation 只生成本地 YooAsset Bundle 及其构建报告，不部署 CDN、不清理远端、不上传商店，不执行 Git 或 UPM 发布。

## Input

冻结项目根、目标平台、PackageName、BuildVersion、实际输出根、执行入口，以及缓存、依赖数据库、压缩、文件名、加密器和 BundledCopy 选项。实际输出根必须解析为当前适配器固定的项目 `/Bundles`；不要虚构自定义输出参数。目标平台、输出根、Package、版本策略或构建/拷贝模式不明确时返回 `blocked`。

检查目标 Package 与 Collector 配置已经存在，所需导出物由项目提前准备。不要因为要构建 Bundle 就自动执行 Config、Table、UI 或全量 Excel 导出；缺少输入时报告具体前置缺口。

当前 Sample 的标准资源构建还会检查所选 Package 默认采集规则覆盖的已配置 DLL 目标：文件须非空且已导入为 TextAsset。导入器准备的空目录只用于 Editor 演示，自动同步因缺少源产物而跳过也不等于 DLL 已就绪。缺失时按既有 HybridCLR 编译/生成与复制流程补齐，不创建占位 DLL、不将此检查提前套到首次 Player 的 AOT 生成上；自定义规则仍按原管线与当前 Action 契约验收。

Package、版本策略、构建选项与首包模式先从当前配置、已保存 Batch 和工具实际状态读取，展示来源与有效值后冻结，不把可读参数转嫁给新手。完整构建授权下由组合任务复用既有配置/导出/HybridCLR Operation，先完成适用的生成代码与 DLL 目标导入，再构建 Bundle；保持本 Operation 的独立写入边界。不同平台共用 StreamingAssets 或输出目录时串行执行，分别记录本次产物，不把上一平台的首包带入下一平台。

WebGL `OfflinePlayMode` 必须配套 `ClearAndCopyAll`；`HostPlayMode` 可用 `None / ClearAndCopyByTags / ClearAndCopyAll`，`None` 依赖已部署的完整 CDN 资源。`LaunchHotfixTags` 和 `WebGLAssetStrategy` 不决定首包拷贝集合。核对实际 Package、Manifest/Catalog/Bundle 与后续 Player 中的 `nova-webgl-layout.txt`，不能用 Bundle 成功推断浏览器可运行。

## 已有 Action Adapter

Framework 已注册并开放 `nova.project.bundle.build-asset` 与 `nova.project.bundle.build-raw-file`。先用 `describe` 读取目标 Action 的当前 Schema，再按冻结的 Package、Target、版本、缓存和拷贝参数执行 `plan -> execute -> verify`；删除或重建输出目录仍必须使用当前一次性 PlanId 确认。不得退化到 `execute_code`、反射或临时 C#。

两个 Action 的 `encryptionPolicy` 必填，只接受 `none / offset / xor / aes-memory / aes-stream`；可选 `manifestEncryptionPolicy` 只接受 `none / aes / xor`，省略或 `null` 使用 `none`。两组方案独立，均固定映射当前 Framework 正式类型，AES/XOR 清单自动使用各自配套构建解密器；不传 C# 类型名、旧通用类名或别名。确认计划时核对两项逻辑值与展示的三个正式类型名。需要密钥的方案沿用已保存 ConfigMaster 的资源加解密配置，不把密钥写入请求或计划，不自动修改配置或导出客户端密钥源码。

确认 `sharedWrites`：关闭清缓存仍写共享 OutputCache；AssetBundle 另写脚本/临时输出与 SBP/Unity 共享缓存，依赖数据库只在启用时盘写。清缓存实际删除整个选中 Package 根及全部版本，不能把它解释为仅删除当前版本。Plan 显示未知回调、已有脏资产或 SBP 临时输出残留时，给出最小缺口；不执行临时代码、不清他人残留、不扩大 Assets 写集绕过阻断。RawFile 必须是有效组仅使用主 Collector 与 `PackRawFile` 的独立 Package；管线本身不自动过滤混合采集，没有 Raw 主 Collector 时 `not_applicable`。

AssetBundle 还要求当前场景已保存、无脏数据，冻结 setup、有效 Collector 根的递归依赖及源文件/meta/关键构建设置。Action 已审回调使用精确实现和最窄分支条件；Analytics/provider 下游未审、Shader 内部 stripper/事件未审、ProBuilder 设置尚未初始化、对象或输入依赖存在时按 Plan 的具体原因处理，不临时改设置或开场景绕过。SBP 的已审回调不能用于证明 Player 安全。执行后核对源文件与场景恢复状态，失败 Receipt 的核查结果也要保留；不替用户恢复资产。

Android EDM 场景无需解析分支还冻结完整依赖图、解析状态与产物、工具和主线程调度。首次必须有本次四条有序场景事件及计数增长，后续仅接受无解析事件的正常计数增长；不重置计数或主动执行解析。为了观察被 Console 过滤的详细日志，作用域临时订阅已审公开 Logger 尾部事件，只保留固定 token/失败类别，精确退订自身并核对原委托；日志级别不变。正规 Jetifier 前置仅接纳项目已有 True 且 canonical XML 与原字节相同的分支，计划声明精确 Gvh 文件和 `Unity EditorPrefs:GooglePlayServices.UseJetifier` 同值写，其他设置保持。Verify 不预读 XML、不重放回调；缓存命中可走独立证据分支：只有本次写出的精确报告和完整 SBP trace、场景 GUID 与冻结依赖闭包、全部场景/写操作缓存标记和计数一致，且场景计数、事件、EDM/Firebase 完整状态和写入集合均未变化，才接受真实未重跑场景回调的缓存结果。缺少 trace、混合未缓存场景或域身份改变保持 partial，不升级旧 Receipt。初始与最终各做一次完整 Version Handler 只读采集，中间仅双读绑定实际 DLL/MVID、域、owner/scheduler 身份及原调度不变量的廉价只读 pending 探针。首完整采集与探针绑定后启动唯一固定十秒的自然 Editor update 等待预算，至少观察一次真实 update；探针完成或到期后只做一次最终完整后验，采集可越过截止点但必须完整匹配且自然完成，仍 pending 保留 partial。回执分别保留真实 update 与成功探针计数，二者须相等且只含首末两份完整快照；旧无探针字段回执仍按每次 update 一份完整快照的原规则读取。未知探针或稳定输入变化立即失败，不重开窗口，不推进、调用或清空官方队列。

Firebase 正常 Android 生成复用同一 Action：Plan 不触发 Measurement 初始化，精确冻结四资源与 EDM metadata/manifest 行闭包；Execute 回执记录实际写入、标签、`Generated` 和普通日志级别的失败。只有本次两个资源成功行、合法 XML 和标签/meta 保持规则齐备才通过；已生成跳过分支保持原文件，不重置标志或重放。统计同意选择尚未完成时交由用户完成原选择，Enabled 保持原设置。IAP 已有配置按原枚举与 importer 规则核对已满足值，当前不足由 Plan 明确展示，不用临时代码修改插件或关闭设置。

VersionHandler 日志只接纳已审 Console 输出、空文件名和无外部委托分支，Plan 冻结其输出标志和级别。完整恢复回执在内部有界压缩并核对版本、原文长度与 SHA-256，保持公共存储上限和旧明文兼容；损坏或超限明确失败，不裁剪来源、metadata 或回调前后证据。

## Artifact → Evidence

本地交付验收按契约 `externalCompletionPolicy=incidental-build-telemetry` 区分正式构建产物与附带统计外发：不要求以远端统计收件证明本地产物完成。原 `external-write`、凭据、确认、回调及失败检查全部保留；不推断统计送达，不扩大到上传、支付、部署等外部业务任务。显式追加的外部验收与同一 Workflow 内其他外部任务仍独立必需，详见 [本地构建验收](../../../Docs/Onboarding/LOCAL_BUILD_EVIDENCE.md)。

Artifact 是 `BuildResult.OutputPackageDirectory` 指向的本地 Package 目录及本次 YooAsset 报告。只有 `BuildResult.Success` 为真、输出目录存在，并能对应冻结的 Target、PackageName 与 BuildVersion 时才返回 `success`；编译通过或目录里存在旧文件都不算 Bundle 构建证据。

报告实际 Adapter、冻结参数、输出目录、构建结果和未验证项。构建失败、产物无法归属本次输入或恢复状态不确定时返回 `partial` 或 `blocked`，不得把旧 Bundle、远端可用性或 Player 可运行性推断为成功。

使用本次 finalized Receipt，只读核对正式报告的目标/包/版本/管线/类型、引用 Bundle 文件，以及 OutputCache、脚本输出和可选数据库的完整辅助集合与 Hash。旧 Receipt 缺共享证据、后续构建改变共享产物、临时输出未清理或执行失败均为 `partial`；共享缓存的后台裁剪不属于完整产物验证承诺。

本次构建完成后清理可归属的临时目录与测试态配置，按冻结基线恢复临时首包/平台状态；组合任务中的首包须待后续 Player 消费和验收完成后再恢复。明确保留哪些正式 Bundle，不删除用户原有产物或其它 Agent 的验收环境。

计划中的 `callbacks.knownEffects` 会列明内存统计与已核原回调外发条件和字段；原 Analytics/telemetry 使用当前已保存设置和正式 ExternalWrite 确认，未审下游仍阻断，不临时关闭设置造可执行结果。Bundle 产物验收不证明统计送达。
