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

## 目录与发现

| 位置 | 责任 |
|---|---|
| `Skills/<skill-id>/` | 可被 Codex 发现的 Skill 真源，保持一层平铺 |
| `catalog.json` | Profile、用户旅程、副作用、最低证据与机器可选项 |
| `Schemas/` | Catalog 的可校验结构 |
| `Tools/nova_skills.py` | 真源校验、消费项目投影、漂移诊断 |

`Assets/Framework/Agents/Skills` 本身不是 Codex 的默认仓库发现路径。消费项目应先把所需 Profile 投影到自己的 `.agents/skills`；该副本带有 `nova-skills.lock.json`，不会覆盖同名的用户 Skill 或已修改的受管 Skill。投影会先在隐藏 staging 中准备完整 Profile，再登记事务并逐项落盘；若进程中断，保留的事务会在下一次非 dry-run `sync` 时续传，而不是把半成品误认作用户 Skill。

```bash
# 先定位当前消费项目实际解析到的 Framework 包
python3 <framework>/Agents/Tools/nova_skills.py resolve --project-root .

# 预览，不写入任何文件
python3 <framework>/Agents/Tools/nova_skills.py sync --project-root . --profile core --dry-run

# 创建受管投影；仅在目标 Profile 与副作用已获授权时执行
python3 <framework>/Agents/Tools/nova_skills.py sync --project-root . --profile core

# 只读检查未初始化、中断事务、缺失、用户修改、源更新或包解析变化
python3 <framework>/Agents/Tools/nova_skills.py doctor --project-root .
```

## P0 Profile

| Profile | 内容 | 适用场景 |
|---|---|---|
| `core` | 任务路由、项目就绪检查 | 接手项目、范围不清、先诊断后执行 |
| `ui` | 路由、创建 UIView、数据驱动 UI 工作流 | 已明确的项目组 UI 开发 |
| `p0` | 当前全部四个样板 | 验证完整 P0 闭环 |

P0 用四个不同风险面验证分发与契约：只读路由、只读评估、可逆项目/Unity 写入、组合编排。后续按照用户旅程扩展 Operation，再增加少量高价值 Workflow；不把单一 API 或 Pipify Step 拆成顶层 Skill。

## 当前限制

- Skill 定义与 Framework UPM 同版本发布；P0 不维护独立 Skill 版本。
- `sync` 对已升级真源采取保守策略：先由 `doctor` 报告，再由后续升级策略显式处理，绝不静默覆盖。
- Profile 是精确能力集；若切换会留下不属于目标 Profile 的受管 Skill，P0 会停止而不自动删除，避免把实际写入权限伪装成更小 Profile。
- 静态检查、Unity 编译、Play 验证和真机/服务端证据彼此独立；报告必须标明实际达到的层级。
