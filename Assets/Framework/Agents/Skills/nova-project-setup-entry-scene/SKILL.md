---
name: nova-project-setup-entry-scene
description: Use when 项目组要检查或配置 Nova 项目的启动场景、Build Settings、canonical Nova.prefab 与 Content 场景职责，并需要在既有运行拓扑中完成最小验证时使用。
---

# Nova 配置启动场景

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

再读取 [references/contract.json](references/contract.json)，先识别当前实际启动路径、启用 Build Scene、Nova 托管场景和 Content 角色。不得把 Starter、Sample、测试场景或仓库中未共同运行的替代入口当成产品拓扑。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或替项目选择场景架构。

| 条件 | 读取 |
|---|---|
| 要判断入口、canonical Prefab、单/多场景或 Content 责任 | [Docs/Onboarding/PROJECT_STRUCTURE.md](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) |
| 要确认任务验收或只读构建前检查的边界 | [Docs/Onboarding/VALIDATION.md](../../../Docs/Onboarding/VALIDATION.md) |
| 新项目或准备固定启动 Resources | [Docs/Onboarding/PROJECT_STRUCTURE.md](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) 的开工准备与 [Docs/Onboarding/RESOURCE_WORKFLOW.md](../../../Docs/Onboarding/RESOURCE_WORKFLOW.md) 的固定启动资源；字段按 [Docs/Runtime/Modules/Procedure/LauncherSettings.md](../../../Docs/Runtime/Modules/Procedure/LauncherSettings.md) |
| Content 通过 Nova 资源系统 Additive 加载 | [Docs/Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md](../../../Docs/Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md) |

## 冻结输入

确认入口场景、Build Settings 的精确增删/排序范围、共同运行的 Nova 拓扑、每个 Content 场景的加载者与卸载者、目标 BuildTarget 和允许的场景/业务代码写入集。入口或 Content 角色不明确、需要替换非生成 Prefab、或要改动现有 Build Settings 闭包时先请求确认。

新项目默认固定启动资源分支须在场景实施前冻结当前已解析版本的 MainDemo 来源、项目目标目录、全部必要依赖、资源键冲突处理、`ProcedureComponent.LauncherSettings` 原值/目标值与保存范围。使用已有同范围建项授权继续，不把可读取路径转交用户猜；已有项目只补已授权缺项，不引入整套 Sample。基础配置、平台环境或业务预加载尚缺时，按准备清单路由既有 Operation，不能用本 Skill 的场景保存替代整个开工门。

固定启动 Resources 的 Splash/Progress/Dialog 使用独立 CanvasScaler，须按产品声明的横屏或竖屏核对与调整；玩法 UIComponent 不自动适配启动面板。按[逐页适配](../../../Docs/Runtime/Modules/UI/UIComponent.md#从启动到玩法的逐页适配)覆盖启动实际显示，背景和关键内容分别布局；完整产品验收交接玩法、暂停/结算等后续页面，不默认要求另一方向，不把固定资源存在或单页截图当作全流程适配完成。

新游戏同时按 [Docs/Onboarding/PROJECT_STRUCTURE.md#标准产品预加载基线](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#标准产品预加载基线) 冻结并落实 MainDemo 八项标准动作；场景和三启动面板就绪不等于产品基线就绪。检查 `startupPreparationInventory` 中八节点的来源、配置与物料、成功条件和验证状态；不因单机或体量小只保留直接使用的模块，平台限制在节点内按真实能力处理。业务预加载代码交 integrate-procedure，缺项交对应 Operation，不能在场景步骤擅自省略或将未完成项标成不适用。

来源未正式导入时，先独立冻结 `sampleImportWriteSetAndEffects`：官方 MainDemo 来源、准确导入目录/文件、编译和自动导入辅助工具的已知写入及弹窗范围。按实际授权执行并读回，再进入资源复制；不能用“三面板复制”授权暗含整套 Sample 导入。无法冻结副作用时保持该来源缺口，不修改缓存或用 Sample 专用自动配置覆盖工程。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|
| 已确认的入口场景与 Nova 托管范围 | Unity Editor 自动化通道 | 使用完整 canonical [Nova.prefab（定位）](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) 的已保存场景 | 连接实例与场景层级检查 |
| 已确认的 Build Settings 变更 | Unity Editor 自动化通道 的 Build Settings 操作 | 精确的启用场景及顺序 | Build Settings 差异 |
| 已确认的 Content 加载/卸载责任 | 当前项目已有启动/场景代码；YooAsset 场景使用 `Nova.Asset.LoadSceneAsync` 与 `ISceneHandle.UnloadAsync` | 显式的 Content 所有者与生命周期 | 代码与运行时加载/卸载证据 |
| 目标场景与实际运行拓扑 | Unity Editor 自动化通道的只读层级与组件检查 | 本次场景、Nova 根及必需引用 | 对象路径、绑定与入口 Play 证据 |

## 实施与验证

1. 先只读检查启动路径和 Build Settings。首个启用场景没有 Nova 时先核实是否为自定义 Bootstrap，不自动改写。
2. 承载 Nova 的场景必须通过 Unity Editor / MCP 使用 Framework 提供的完整 canonical [Nova.prefab（定位）](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) connected instance；不得手工拼装组件或手写 Scene/Prefab YAML。已有残缺根节点不自动删除或替换。
   新项目按资源文档经 Unity 正式导入/复制 MainDemo 三启动面板、字体/材质/贴图及所需启动 JSON 到项目自有 Resources，检查全局键唯一与启动前依赖；保存并读回项目实例的 LauncherSettings 及 Prefab 引用。只复制 Prefab 或看到默认路径不算完成。资产复制仍经 Unity 工具，不手写 YAML、不修改包缓存；业务预加载代码复用 integrate-procedure，配置/正式导出复用对应 Operation。
3. 共同运行的拓扑只保留一个有效 Nova。明确为 Content 的场景不承载 Nova，且必须有项目显式的加载、失败处理与卸载责任；Nova 不会自动加载 Content。
4. 仅按已确认范围更新 Build Settings，不因发现 Sample、测试或插件 Scene 扩大闭包。
5. 通过当前 Unity 工具核对目标场景、Nova 根、必需引用和 Content 所有者；获授权后执行入口 Play smoke。只有涉及构建准备时才复用 `nova-project-preflight-build`，不以静态结果代替 Play。

验证已导入 Sample 时，先按 [Sample 导入边界](../../../Docs/Onboarding/PROJECT_STRUCTURE.md#sample-的导入辅助工具与页面边界) 核对清单声明的生成目录是否已被 AssetDatabase 识别。重写完成标记不能代替目录检查；自动导入器只恢复声明目录，不创建缺失的普通源资产。缺少平台 DLL 的自动同步跳过不阻断普通 Editor 演示，也不能作为正式 Bundle、Player 或设备已就绪的证据。不要手工修补已安装缓存包、复制开发机 DLL 或为通过采集检查修改工作区绑定。

达到本次场景保存、拓扑与绑定检查和入口 Play 证据才报告 `success`；新游戏还须验证固定启动资源实际加载/显示，并按标准产品预加载基线逐项核验，不能只拿部分模块、流程继续或进度完成代替完整基线。跨 Operation 仍有缺项时分别报告已完成的场景部分和待准备/阻塞的基线项，不扩张为整套产品已就绪。无法进入 Unity、Content 所有者不明确、必要资源/配置/环境未齐或仅取得静态结果时返回 `partial` 或 `blocked`。不自动 Build Player、发布、删除场景或提交 Git。
