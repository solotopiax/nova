---
name: nova-project-diagnose-build
description: Use when Nova 消费项目的 Player 构建、平台工程导出或既有 Gradle、Xcode、WebGL 构建链失败，并需要只读定位最早直接失败阶段与证据时使用。
---

# Nova Player 构建故障诊断

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在已存在的失败证据指向对应阶段时按需读取下列随 Framework 发布的文档；不要在路由阶段加载全部平台、SDK 或构建文档。

| 证据指向的阶段 | 读取 |
|---|---|
| Unity Build、BuildReport、输出路径、YooAsset staging 或 HybridCLR ABI | `Docs/Onboarding/VALIDATION.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md` |
| Android 依赖解析、Manifest 或 Gradle 前置 | `Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md` |
| 失败入口明确为项目已有 Pipify Batch | `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md` |
| 已有日志命中 Nova 前/后处理器 | 先读上述 Build 文档，再在已解析 Framework 的 `Scripts/Editor/BuildProcessor/` 中只读核对对应 `NovaBuildPreprocessor`、`NovaSDKBuildProcessor`、`NovaBuildPostprocessor` 或 staging 回调 |

这是严格只读的诊断 Operation：只分析既有 `BuildReport`、Unity Console / `Editor.log`、构建入口记录、已生成的 Android / Xcode / WebGL 输出和对应源码。不得重新构建、触发编译、运行 EDM4U Resolve、清缓存 / 删除输出、修改源码或配置、重新导出 Gradle / Xcode / WebGL 工程，也不得用“验证一下”重跑失败命令。

## 冻结输入与诊断顺序

先冻结项目根、原始构建入口（Unity Build、`EditorUtil.Build`、Build Profile、Pipify 或 CI）、目标平台与精确输出路径、失败时间窗、用户看到的症状、已存在的 BuildReport / Console / `Editor.log` / 平台日志位置，以及能读取的现有产物。入口、目标平台或日志时间窗会改变归因而不明确时，返回 `blocked`，只提出最小澄清项。

按时间线定位，不把最显眼的 Warning 当根因：

1. 先区分构建启动前的脚本编译/项目解析错误，与真正进入 Player 构建后的错误。
2. 对已经开始的构建，以最早带有直接异常、失败结果或退出码的记录为准，依次判断是 BuildReport / Unity 管线、YooAsset staging、Nova BuildProcessor 前处理、Unity 平台转换、还是 Nova 后处理。
3. 仅当日志确实指向平台输出时细分：Android 的依赖 / Manifest / Gradle，iOS 的 Xcode / Pods / PBX / plist / entitlements，WebGL 的导出、模板或后处理。没有对应直接证据时只报告“未证实”，不根据平台惯例猜测。
4. 输出冻结上下文、最早直接失败位置、原文摘录或可定位位置、事实与推断的置信度、推荐的后续写入 Skill / 人工动作，以及没有取得的证据。`success` 仅表示诊断报告完成，不表示构建已经修复。

## 只读 Action Adapter

1. 使用已解析宿主的 Python 3.9+ 执行 `nova_skills.py resolve`，确认消费项目实际解析到的 Framework 包与文档；macOS/Linux 使用 `python3`，Windows 使用 `py -3`。
2. 只读收集当前或存档的 BuildReport、Console、`Editor.log`、构建入口参数、输出目录和平台工具日志；若需要 BuildProcessor 结构证据，只读取与最早失败 tag / stack 对应的已解析 Framework 源码。
3. 当前 Unity 已打开时，Unity Editor 自动化通道 只读取 Console、编译状态和已有构建信息；不得调用会触发刷新、编译、Play、Build、Resolve 或资产保存的工具。
4. 发现可能修复点时停止在报告，不编辑、不重跑。修复、依赖解析、打包或平台导出必须进入用户确认后的独立任务。

## Artifact → Evidence

交付一份只读诊断报告，说明原始入口与目标、最早失败阶段、日志 / BuildReport / 平台输出的准确位置、关键原文、根因置信度、下一个最小动作及未验证项。已有输出目录、旧 APK/AAB、Xcode 工程或 WebGL 文件本身不等于本次构建成功；缺失 BuildReport 也不能由旧产物补齐。

有完整冻结上下文和足以定位最早直接失败点的静态证据时返回 `success`；日志、BuildReport、平台日志或源码对照不足时返回 `partial`；关键上下文或读取范围不明确时返回 `blocked`；问题不属于 Player 构建 / 平台导出链时返回 `not_applicable`。不执行任何修复、重打包、配置改写或产物清理。
