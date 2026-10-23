# 平台脚本宏的精确编辑

用户要新增、移除、替换或清空项目脚本宏时读本页与随包 `Docs/Editor/EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md`。优先复用 `nova.project.settings.patch-build-defines`，不用共享 `manage_build` 的非空字符串限制处理清空，也不写 ProjectSettings YAML 或临时 C#。

先发现当前 Active BuildTarget 和需要的业务宏。Agent 读取完整当前集合后，保留无关项并构造最终 `symbols` 数组，展示 before/after、added/removed 与唯一目标文件 `ProjectSettings/ProjectSettings.asset`。目标名称由当前平台和真实 API 核实，不让新手猜：

| 请求 namedBuildTarget | 对应 Unity 组/真实 TargetName |
|---|---|
| Standalone | Windows、macOS、Linux 共用 `Standalone` |
| iOS | `iPhone` |
| Android | `Android` |
| WindowsStoreApps | `Windows Store Apps` |
| WebGL | `WebGL` |

请求严格为 `{"namedBuildTarget":"Android","symbols":["MY_FEATURE"]}`。这是目标完整集合，数组未包含的原宏会移除；`symbols: []` 明确清空。省略/null、空元素、重复、分号、空白、非法标识及未知字段拒绝。当前只允许这五个公共工具目标，并要求与 Unity Active BuildTarget 所属组一致；不自动切平台，不操作 Dedicated Server 或其他组。

按 live `describe → plan → execute → verify` 使用。Plan 只读冻结全部原宏与最终集合、真实 PlayerSettings 完整状态、目标文件 SHA、其他第一层 Unity ProjectSettings 文件以及准确平台。活动 Build Profile 会被 Unity 的宏 Set 同步修改，因此本 Action 只支持 `BuildProfile.GetActiveBuildProfile()==null` 的传统平台设置，存在活动 Profile 时明确阻断，不自动切换或改 Profile。Project Settings 窗口、PlayerSettings Inspector、未保存窗口或 PlayerSettings 脏态、正在编译/导入/Play、既有编译错误均阻断。关闭或处理已有草稿后重新 Plan，不能自动替用户保存/放弃。

已有任务授权覆盖所展示的完整宏差异时可继续，否则先取得针对该范围的授权；计划/坐标/原宏漂移后旧授权不能绑定新的写集。Execute 一次调用公共 `SetScriptingDefineSymbols`，它会改变内存设置并可能触发编译；随后以 Unity 公有 `Object.Instantiate` 取得唯一 PlayerSettings 的非持久副本，核完整快照，再用 `InternalEditorUtility.SaveToSerializedFileAndForget` 仅保存该副本至 `ProjectSettings/ProjectSettings.asset`，独立加载文件核全状态并销毁全部副本，不全局 SaveAssets。PlayerSettings 不是普通 AssetDatabase 资产，不能用 `SaveAssetIfDirty` 代替该保存；直接向原生序列化器传持久 PlayerSettings 也可能被跳过。真实变更必须使目标文件 SHA 改变，完整快照与范围外文件核对通过后才清除该对象脏标志。核对最终完整宏、其他平台宏及全部其他设置保持一致；写入开始后 partial 禁止重放。等待本次真实编译、无错误与脚本重载，再凭已绑定 Receipt Verify。Verify 不保存、不清脏；重载后仅有脏标志时仍要求当前完整快照、目标文件及范围外状态精确匹配。Set 早触发重载导致未取得编译令牌时保守 partial，不冒称成功。

已满足集合不写入、不申请编译，按静态/文件只读验收；实际变更必须取得本次编译/重载证据。Verify 只读核对准确平台、完整集合、范围外状态与保存文件。Action 自身恢复存储位于 ProjectSettings 内子目录，不参与第一层 Unity 设置文件保护；不会借此删除或覆盖该目录。

宏存在只影响编译分支，不证明依赖包、asmdef versionDefines、SDK/Kit 配置或运行能力。不要手工注入由 asmdef `versionDefines` 管理的可选库宏来代替依赖；日志/RuntimeDebugger 仍按 [App 与调试维护](app-and-debug-maintenance.md) 检查真实业务效果。仅当任务要求时运行对应业务分支、Player 或设备验证。测试不直接清空生产宏；真实验收冻结原集合后，由 Unity 单写者按授权修改并恢复，最后清理本轮测试物、日志和状态，保留正式变更。
