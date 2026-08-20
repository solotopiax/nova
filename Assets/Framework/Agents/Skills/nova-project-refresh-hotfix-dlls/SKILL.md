---
name: nova-project-refresh-hotfix-dlls
description: Use when 已配置 HybridCLR 的 Nova 消费项目需要在当前 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标下，仅刷新本地业务热更 DLL 并核对完整映射时使用。
---

# Nova 刷新本地业务热更 DLL

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

仅在当前决策分支需要时读取对应资料，不加载全量 HybridCLR、Config、Pipify、构建或运行时文档。

- L0：只用 Catalog、frontmatter 与共同底线判断是否命中；不要先加载 HybridCLR、构建和启动链的全部文档。
- L1：读取 `references/contract.json`，冻结输入、写入集、锁、确认门与本 Operation 的成功边界。
- L2：需要核对真实编译、拷贝和导入行为时，仅读取 `Docs/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md`、`Docs/Editor/Config/Definitions/HybridEditorConfigs.md` 与 `Docs/Editor/Config/Definitions/DllMasterAssetEntry.md`；只有选择项目已有 Pipify Batch 时再读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；运行时边界只在需要解释为什么本 Operation 不证明运行时成功时，才读 `Docs/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.md`。
- L3：确认后只执行声明的 compile -> copy Action Adapter，不临时拼接其他 HybridCLR、Bundle、Player 或 CDN Step。
- L4：只收集当前业务 DLL 编译、整批映射、目标导入与哈希证据；不执行、也不记录运行时或真机 smoke。

## 适用范围

该 Operation 只适用于已经完成 HybridCLR 安装、程序集与 ConfigMaster 配置的消费项目。它在以下单一坐标内刷新本地业务热更 DLL：

`当前 activeBuildTarget + 当前 DevelopmentBuild + 当前激活 ConfigMaster 的 Platform/Channel/DevelopMode`

它不执行 AOT 全预构建、`GenerateAll`、AOT metadata 拷贝、Bundle、Player、CDN、商店、Git、运行时加载或真机验证，也不修改 HybridCLR 版本、ASMDEF、Namespace、入口 Procedure、ConfigMaster 或 ConfigRuntime。

## Input 冻结

执行前必须完整冻结：

1. `projectRoot`：当前消费项目根目录。
2. `activeBuildTarget`：当前 `EditorUserBuildSettings.activeBuildTarget`。
3. `developmentBuild`：当前 `EditorUserBuildSettings.development`，不得用 Config 的 `DevelopMode` 猜测。
4. `activeConfigMaster`：`EditorUtil.Config.WorkspaceActive.Get()` 返回的激活资产及其稳定身份。
5. `platformChannelDevelopMode`：激活 ConfigMaster 当前 `Platform / Channel / DevelopMode` 三维坐标。
6. `resolvedGameDllEntries`：当前坐标下 `StartupGameDlls + RunningGameDlls` 的完整并集映射；逐项记录所属列表、程序集、解析 `{ActiveBuildTarget}` 后的 SourceLocation、TargetLocation 与 AssetLocation，不得只选其中一部分。其去重后的 `SourceLocation` 就是本 Operation 要验证的必需编译产物集合；不得另读或猜测 `HybridCLRSettings`。
7. `executionEntry`：固定为 MCP Action `nova.project.hotfix.refresh-game-dlls`；不得切换为直接 C#、Pipify 或手工复制。

任一必填输入缺失、当前值与冻结值不一致、映射存在空项/重复目标/越界目标，或确认后 Target、DevelopmentBuild、Master、三维坐标、完整映射、执行入口或覆盖范围发生变化时，停止执行并返回 `blocked`；旧确认失效。

## Action Adapter

唯一正式入口是 MCP Tool `nova_project_action` 的 `nova.project.hotfix.refresh-game-dlls`。先 `describe` 获取当前 Request Schema，再以冻结的 Master、坐标、activeBuildTarget 和 developmentBuild 调 `plan`；展示完整映射、写入集与证据后取得确认，再调用 `execute`，同时传 `action_id`、`plan_id` 与 `confirmation_token=plan_id`。Execute 返回后只用 `recovery_token` 调 `verify`，不得自动重放。

该 Action 内部固定复用 `CompileDllActiveBuildTarget -> CopyGameDlls` 闭环。禁止单独调用 `CopyGameDlls`。Skill 不再临时拼直接 C#、Pipify Batch、手工复制或单条目旁路；Tool/Action 未安装或未开放时返回 `blocked` 并报告缺少 `com.solotopia.nova.framework.mcp`，不退化为任意代码执行。

## 执行与整批验证

1. 获取 `unity-editor`、`asset-database`、`build-settings`、`active-config-master`、`hybridclr-hot-update-output` 与 `game-dll-targets` 单写者锁。
2. 在写入前重新读取 Target、DevelopmentBuild、WorkspaceActive Master 与三维坐标；与冻结值不一致时不执行。
3. 执行编译入口，记录当前 Target 的完整 HybridCLR 编译输出根目录及其中全部编译器产物；从冻结 `resolvedGameDllEntries` 派生去重后的 SourceLocation，并确认全部已产出。编译失败时不得继续 Copy。
4. 用冻结的 `resolvedGameDllEntries` 对编译后所有 SourceLocation 做整批预检，记录每个源文件的 SHA-256。任何源缺失或映射漂移都停止 Copy。
5. 一次性执行 Action 内的 `CopyGameDlls`。该入口先校验 Startup/Running 不跨列表重复，再校验完整并集源集合并覆盖目标；不得把一部分成功描述为整批完成。
6. 对每个目标重新计算 SHA-256，逐项确认与对应源文件一致。TargetLocation 位于 `Assets/` 时，还必须记录 `AssetDatabase.ImportAsset(..., ForceSynchronousImport)` 已完成的 Unity 导入证据；非 `Assets/` 目标不得伪造 AssetDatabase 导入结果。
7. 报告冻结坐标、实际 Adapter、编译输出、完整 source -> target -> SHA-256 映射、Assets 目标导入结果和未验证边界。

## 写入边界与确认门

允许写入仅包括：当前 `activeBuildTarget` 的完整 HybridCLR 编译输出根目录（含编译器生成的全部脚本 DLL/PDB）、冻结 `StartupGameDlls + RunningGameDlls` 并集映射的目标文件、目标位于 `Assets/` 时由既有入口触发的 Unity import，以及本次本地证据。

禁止写入或扩大到：AOT metadata、`link.xml`、`GenerateAll`、`GeneratedCpp`、`Il2CppDef`、ConfigMaster、ConfigRuntime、Bundle、Player、CDN、商店、Git、Framework、`Library/**`、其他 Target、其他三维坐标或映射外条目。

确认必须绑定 `activeBuildTarget`、`developmentBuild`、`activeConfigMaster`、当前 `Platform/Channel/DevelopMode`、完整 HybridCLR 编译输出根目录（含编译器生成的全部脚本 DLL/PDB）、完整 DLL 映射、`executionEntry` 与覆盖范围。任一值变化都必须重新确认；覆盖已有目标仍属于本次确认内容，不能从旧 Batch 名称推断授权。

## 结果状态

- `success`：当前 Target 的完整 HybridCLR 编译输出已生成；冻结 `resolvedGameDllEntries` 派生的每个 source 与 target 映射一致；`CopyGameDlls` 整批完成；所有 `Assets/` 目标已完成同步导入；每对源/目标 SHA-256 一致，并且所有确认值在执行期间未漂移。
- `partial`：已经产生本 Operation 允许的编译或目标写入，但整批 Copy、导入、哈希核对或执行后坐标复核未完成；必须列出已写入项与不可据此得出的结论。
- `blocked`：必填输入、锁、确认、源文件、完整映射或已确认执行入口不足，且尚未产生本次允许写入；保留现场并给出最小缺口。
- `not_applicable`：项目未配置 HybridCLR、没有业务热更程序集，或请求的核心目标属于 AOT/配置/Bundle/Player/CDN/发布/运行时诊断而不是本地业务 DLL 刷新。

不得把编译或本地 DLL 刷新称为 Bundle、Player、CDN、IL2CPP 运行时或真机成功。本 Operation 不执行、也不记录 runtime smoke；它不能替代运行时、设备或远端验证。

## 超出范围的路由

HybridCLR/AOT 版本变化、ASMDEF、Namespace、入口 Procedure、ConfigMaster/ConfigRuntime 改动、full GenerateAll、AOT 全预构建、Bundle、Player、CDN 或 Runtime 诊断必须路由到对应既有或未来 Operation；当前 Catalog 无合适闭环时返回 `not_applicable`，不得扩大本 Skill。已有最窄路径中，Bundle 使用 `nova-project-build-bundles`，Player 使用 `nova-project-build-player`，启动与运行时加载问题使用 `nova-project-diagnose-startup`，配置变更使用 `nova-project-configure-runtime`。
