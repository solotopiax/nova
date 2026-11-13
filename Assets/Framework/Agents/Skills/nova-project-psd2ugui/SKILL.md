---
name: nova-project-psd2ugui
description: 在 Nova 项目中使用已安装的 PSD2UGUI 将 PSD/PSB 制作成 uGUI，查询配置与图层来源、同步或调整编辑树、生成界面与导出图片，以及运行或应用 AI 识别时使用；业务 UIView 注册与运行接入转交现有页面 Skill。
---

# 从 PSD 制作和维护 uGUI

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

本 Skill 管理设计源、插件编辑树与生成界面之间的转换。生成的 uGUI 与 Nova 业务 `UIView` 是不同交付层；需要页面注册、导航、事件或运行验证时继续进入已有页面 Skill。

## 渐进式披露

- L0：从用户目标判断是查询、编辑树、生成/导图还是 AI 识别，不要求用户提供能从项目读取的路径、GUID 或类型名。
- L1：读取 [references/contract.json](references/contract.json)，通过 live `describe` 核对 `nova.project.psd2ugui.inspect` 与本次需要的 `nova.project.psd2ugui.run`。
- L2：仅在相应分支读取 [编辑树与来源](references/edit-tree.md)、[生成与图片](references/generate-and-images.md) 或 [AI 识别](references/ai.md)。实现细节按已安装 `cn.efunstudio.psd2ugui` 的 README 与 [Nova/Docs/（包内定位）](../../../Docs/Runtime/Modules/SDK/INDEX.md#sdk--kit-包文档发现) 入口读取；不访问维护仓、隐藏源码或未安装包的缓存副本。
- L3：只向已开放 Action 提交当前 Schema 允许的固定操作与结构化参数；资产保存、来源冻结和恢复交给 Action，不临时拼装 C# 或反射调用。
- L4：按当前模式核对已保存资产、实际产物和只读 Verify；只有用户目标包含业务页面时，才继续页面接入与 Play 验收。

## 先取得当前事实

1. 通过 `inspect` 读取当前工程、已安装包与桥接状态、配置来源、目标 PSD/PSB、编辑树和已有输出。查询保持只读；“查看能做什么”不自动创建配置、建立树或运行 AI。
2. 目标不明确时先查用户选中资源、现有编辑树及其来源、邻近配置和输出记录。多个候选确实影响结果时才询问最少的业务选择；示例资源名不能成为项目默认值。
3. 配置、PSD、编辑树、节点与生成物分别冻结身份。节点名称不等于来源标识，Unity 对象的临时 ID 不跨重载复用；实际参数以当前 inspect 和 Schema 为准。
4. 缺包时说明准确包名 `cn.efunstudio.psd2ugui`、PlugPals 的内部云仓库入口和商业授权前提。只有已授权安装且内部源实际可用时才转交包管理 Skill；不从公开源猜找替代包，不绕过授权。已装损坏、来源冲突或桥接不兼容与未安装分别报告。
5. 创建或调整布局前沿用共同入口冻结屏幕适配方案。实际配置属于项目 [Assets/（项目位置）](../../../Docs/Onboarding/PROJECT_STRUCTURE.md)；包内模板保持只读。配置、九宫格或清理操作若未出现在当前支持集合，只完成查阅和精确缺口说明，不用通用字段修改绕过插件保护。

## 执行与衔接

| 用户目标 | `run.operation` 与按需资料 |
|---|---|
| 建立可编辑树、同步更新设计源、改节点 | `prepare` / `sync` / `patch-nodes`；[编辑树与来源](references/edit-tree.md) |
| 修改项目配置、复制缺少的 AI 提示模板 | `configure`；[编辑树与来源](references/edit-tree.md)，只修改明确字段 |
| 生成或更新界面、导出选中图层图片、处理九宫格 | `generate` / `export-images` / `nine-slice`；[生成与图片](references/generate-and-images.md) |
| AI 识别后应用、单独重放已确认的缓存结果 | `run-ai` / `apply-ai`；[AI 识别](references/ai.md)，应用须绑定本轮 `jobId` |

读清分支后按 `describe → plan → execute → verify` 执行。Plan 必须展示实际源、目标、操作、输出及潜在覆盖/删除；已有授权覆盖时直接绑定本次计划，不重复询问。范围变化或输入漂移重新计划；重载、取消、失败后依据回执只读核验，不自动重放写入。

请求中的资产与目录均为 [Assets/（项目位置）](../../../Docs/Onboarding/PROJECT_STRUCTURE.md) 下的项目相对路径；每个操作只传它允许的字段，不能把查询响应或完整配置对象原样回传。`configure` 要求准确 `configAssetPath`，`nine-slice` 要求准确 `spriteAssetPath`，其他写操作要求已发现的 `psdAssetPath`。布尔/数值及对应 `has` 标志同时提供且 `has=true`；不修改时一起省略，保留显式 `false` 与零的含义。

建立树、同步、改节点、生成、导图和 AI 是不同操作。只执行用户目标所需步骤，不把“导出图片”扩大成生成页面，也不把“重新生成”变成另存为或清理旧资源。

需要进入 Nova 业务页面时：新建或注册页面转 `nova-project-ui-create-view`，已有页面调整转 `nova-project-update-ui-view`；按 [生成与图片](references/generate-and-images.md) 保留插件输出与业务脚本边界。不要创建第二套 UI 注册、资源加载或缩放流程。

## 完成边界

- 查询成功只证明已取得当前状态。树编辑成功需保存并读回目标节点及来源，生成/导图成功需核对实际路径、资源与引用；AI 成功需有本次结果及已应用状态。
- 页面外观、文本/字体、九宫格和资源引用按本次改动验收；生成产物存在不能代替视觉或业务运行。涉及业务脚本须编译，业务打开/交互和移动端适配继续满足页面 Skill 的条件证据。
- 缺许可证、真实设计源、字体资源、已登录 AI CLI、受控操作或验收证据时，仅阻断依赖它的分支，准确返回 `partial` / `blocked` / `not_applicable`。
- 保留 PSD/PSB、编辑资产及 `.meta`、正式输出和生成记录；只清理本轮可归属的临时验证内容，按共同入口关闭本轮辅助窗口并报告清场。
