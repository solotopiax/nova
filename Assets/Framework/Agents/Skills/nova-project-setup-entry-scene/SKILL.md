---
name: nova-project-setup-entry-scene
description: Use when 项目组要检查或配置 Nova 项目的启动场景、Build Settings、canonical Nova.prefab 与 Content 场景职责，并需要在既有运行拓扑中完成最小验证时使用。
---

# Nova 配置启动场景

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，先识别当前实际启动路径、启用 Build Scene、Nova 托管场景和 Content 角色。不得把 Starter、Sample、测试场景或仓库中未共同运行的替代入口当成产品拓扑。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或替项目选择场景架构。

| 条件 | 读取 |
|---|---|
| 要判断入口、canonical Prefab、单/多场景或 Content 责任 | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| 要确认任务验收或只读构建前检查的边界 | `Docs/Onboarding/VALIDATION.md` |
| Content 通过 Nova 资源系统 Additive 加载 | `Docs/Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md` |

## 冻结输入

确认入口场景、Build Settings 的精确增删/排序范围、共同运行的 Nova 拓扑、每个 Content 场景的加载者与卸载者、目标 BuildTarget 和允许的场景/业务代码写入集。入口或 Content 角色不明确、需要替换非生成 Prefab、或要改动现有 Build Settings 闭包时先请求确认。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|
| 已确认的入口场景与 Nova 托管范围 | Unity Editor 自动化通道 | 使用完整 canonical `Nova.prefab` 的已保存场景 | 连接实例与场景层级检查 |
| 已确认的 Build Settings 变更 | Unity Editor 自动化通道 的 Build Settings 操作 | 精确的启用场景及顺序 | Build Settings 差异 |
| 已确认的 Content 加载/卸载责任 | 当前项目已有启动/场景代码；YooAsset 场景使用 `Nova.Asset.LoadSceneAsync` 与 `ISceneHandle.UnloadAsync` | 显式的 Content 所有者与生命周期 | 代码与运行时加载/卸载证据 |
| 目标场景与实际运行拓扑 | Unity Editor 自动化通道的只读层级与组件检查 | 本次场景、Nova 根及必需引用 | 对象路径、绑定与入口 Play 证据 |

## 实施与验证

1. 先只读检查启动路径和 Build Settings。首个启用场景没有 Nova 时先核实是否为自定义 Bootstrap，不自动改写。
2. 承载 Nova 的场景必须通过 Unity Editor / MCP 使用 Framework 提供的完整 canonical `Nova.prefab` connected instance；不得手工拼装组件或手写 Scene/Prefab YAML。已有残缺根节点不自动删除或替换。
3. 共同运行的拓扑只保留一个有效 Nova。明确为 Content 的场景不承载 Nova，且必须有项目显式的加载、失败处理与卸载责任；Nova 不会自动加载 Content。
4. 仅按已确认范围更新 Build Settings，不因发现 Sample、测试或插件 Scene 扩大闭包。
5. 通过当前 Unity 工具核对目标场景、Nova 根、必需引用和 Content 所有者；获授权后执行入口 Play smoke。只有涉及构建准备时才复用 `nova-project-preflight-build`，不以静态结果代替 Play。

达到本次场景保存、拓扑与绑定检查和入口 Play 证据才报告 `success`；无法进入 Unity、Content 所有者不明确或仅取得静态结果时返回 `partial` 或 `blocked`。不自动 Build Player、发布、删除场景或提交 Git。
