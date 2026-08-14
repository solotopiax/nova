# Nova Project Skills

这是 Nova Framework 随 UPM 包发布的、面向项目组的 AI Skill 系统。它帮助 Agent 完成项目接入、业务 UI、数据配置、构建和诊断；不用于开发或改造 Nova 框架本身。

## 执行模型

```text
用户意图（不是 Skill）
  → Router Skill
  → Workflow Skill（按依赖图编排，可选）
  → Operation Skill（可单独验收）
  → Action Adapter（Unity MCP / Pipify / 脚本 / API）
```

这不是固定顺序：用户提出一个独立操作时可直接进入 Operation；Workflow 只在多个操作存在依赖时生成 DAG。Unity Editor、AssetDatabase、活动场景和同一导出目录均视为单写者资源，不能因 DAG 而盲目并行。

## 目录、真源与自动发现

| 位置 | 责任 |
|---|---|
| `PackageInfo.resolvedPath/Agents/Skills/<skill-id>/` | 已安装 Framework 包内的只读真源快照；开发仓 Git 对应位置为 `Assets/Framework/Agents/Skills/<skill-id>/`，保持一层平铺 |
| `<consumer project>/.agents/skills/<skill-id>/` | 首次打开 Unity 时自动投影的全量受管副本，仅供宿主和 Agent 发现，不是第二真源 |
| `catalog.json` | 全量 Skill 清单、能力分组、用户旅程、副作用、最低证据与机器可选项 |
| `Schemas/` | Catalog 的可校验结构 |

用户安装或升级 Nova 后，首次打开 Unity 时 Editor 会将包内全部 Catalog Skill 自动投影到项目根 `.agents/skills/`；不需要执行 `sync` 或手工复制。该目录是受管副本，不能反向编辑真源。

遇到同名用户目录、已修改或缺失的受管副本时，自动投影 fail-closed：不会静默覆盖或删除，并将结果标为 `partial`；其他可安全完成的 Skill 仍继续处理。真源不可用时自动 bridge 只报告错误，不写入项目。项目本地状态和 Git 忽略边界统一见 [Agent 快速入口](../Docs/START_HERE.md)。能力分组只帮助 Router、导航和展示当前能力，不控制投影范围。Skill 也不是固定执行序列：Agent 只调用当前自然语言任务匹配的 Router、Workflow 或 Operation；多个有依赖的操作才由 Workflow 形成 DAG，且 Unity Editor、AssetDatabase、活动场景和同一导出目录仍是单写者资源。

## 当前 Skill 范围

| Skill | 类型 | 当前作用 |
|---|---|---|
| `nova-project-router` | Router | 将项目组自然语言任务路由到匹配的 Skill 或 Docs |
| `nova-project-check-readiness` | Operation | 只读检查项目接手与就绪条件 |
| `nova-project-ui-create-view` | Operation | 在确认边界内创建业务 UIView |
| `nova-project-data-driven-ui` | Workflow | 编排数据驱动 UI 的多个受控操作 |

当前包内已定义这四个 Skill，覆盖四个不同风险面：只读路由、只读评估、可逆项目/Unity 写入、组合编排。它不代表已覆盖所有日常 Nova 任务；后续按照用户旅程扩展 Operation，再增加少量高价值 Workflow，不把单一 API 或 Pipify Step 拆成顶层 Skill。

## 当前限制

- Skill 定义与 Framework UPM 同版本发布，不维护独立 Skill 版本。
- 自动投影遇到冲突或来源问题时只做安全项，并以 `partial` 提示项目组处理；不要把受管副本当作可反向编辑的来源。
- 当前只包含四个实验性 Skill；新增 Skills 前应先按用户旅程和风险面确认其是否应成为顶层 Skill。
- 静态检查、Unity 编译、Play 验证和真机/服务端证据彼此独立；报告必须标明实际达到的层级。
