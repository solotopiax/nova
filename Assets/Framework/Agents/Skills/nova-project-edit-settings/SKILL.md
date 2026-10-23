---
name: nova-project-edit-settings
description: Use when 项目组要查阅或修改已有对象的 Inspector 字段、内置组件或 UnityEvent，维护 App/Native APK 安装总门、调试与日志配置，或在项目自有业务入口接入更新文件下载、显式安装及 Nova 暂停、恢复、速度与退出；ConfigMaster、Pipify 与生成配置使用专用能力。
---

# 检查与修改对象配置

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

读取 [契约](references/contract.json)。仅在选择普通资产 Action 或确认其字段与保存边界时，读 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/SerializedTargetEditing.md`；涉及场景、内置组件、Prefab 变体/嵌套或 UnityEvent 时按需读 [共享 Unity MCP 编辑参考](references/unity-mcp-editing.md)；App 大版本更新、可选 Android 自身 APK 安装、游戏暂停/恢复/速度/退出业务、RuntimeDebugger、日志级别只读 [App、游戏运行与调试维护](references/app-and-debug-maintenance.md)；增删、替换或清空脚本宏读 [平台宏编辑](references/build-defines.md)。解释字段用途、生效时机或默认值时，只读实际目标类型对应 Docs 和当前版本声明。默认未知时继续核实声明，不能把当前值当默认值；不要为一个字段展开所有工具或文档。

## 先选择可复用入口

用户只需说明对象与希望得到的效果。Agent 从当前工程发现路径、组件及字段，解释用途与影响，再冻结精确变更，不让新手猜 GUID、序列化字段名或回调签名。

| 实际目标 | 执行入口 |
|---|---|
| 自有普通无嵌套 Prefab 的 MonoBehaviour、单对象 SO，满足无编辑期回调等资产契约 | 优先下方 `inspect-target / patch-target` |
| 已保存场景的已有组件，包括内置 Camera、Light、Collider、RectTransform 等 | `find_gameobjects` + 组件资源 + `manage_components(set_property)` + 精确活动场景 `manage_scene(save)` |
| 自有 Prefab 变体、包含嵌套实例的 Prefab、内置组件 | `manage_prefabs(get_info/get_hierarchy/modify_contents)`；冻结写当前变体/外层 override 或源资产的范围，同类重复组件另走明确 Stage 路线 |
| 已核实的 UnityEvent 持久绑定 | 按参考核实事件、目标组件、方法/参数与原监听列表，再经 `manage_components` 或 `modify_contents` 的 SerializedProperty 路线；保存后只读核对实际持久绑定 |
| 已有 UI Toolkit UXML/USS | `manage_ui(read/update)`；不把它当 UGUI/Button 回调工具 |
| 资产定位、Prefab 类型和实际字段核对 | `manage_asset(search/get_info/get_components)`；其 generic modify 不作为本Skill的保存捷径 |
| App 大版本更新、Native 显式 APK 安装总门、RuntimeDebugger 与框架日志 | 复用对应对象字段编辑；日志固定菜单与相关业务验证按维护参考核对 |
| 增删、替换或明确清空平台脚本宏 | `nova.project.settings.patch-build-defines`，完整最终数组支持 `[]`；按平台宏参考冻结真实 NamedBuildTarget、全部原宏与 ProjectSettings，单次修改、编译重载后 Verify |
| 暂停、恢复倍速、正常速度与退出业务 | 按维护参考在已确认项目自有正式业务入口调用 `Nova.Self` 公开 API，复用已有 UI/Procedure/Event/Persistence Operation；源接入、持久绑定与运行证据分别验收 |

这些共享 MCP 操作没有 Nova 的一次性 Plan/Receipt、完整资产漂移锁或自动脱敏。先核对当前工具 Schema、工程实例和草稿，展示目标、before/after 与确切保存范围，沿用已有授权；逐次写入并立即读回。缺少唯一定位、完整已有监听列表、持久保存或无关脏态证明时，报告具体缺项，不能静默改用任意代码。字段已满足时跳过写入；工具返回错误后核对实际部分变化，不自动重放。

## 输入与检查

进入普通资产 Action 分支后，从当前项目发现用户所指的已存在资产，冻结一个 `targetGuid`。普通 Prefab 组件必须同时绑定精确 `globalObjectId`，同名对象或同类多个组件不能猜。普通 SO 可以仅提供资产 GUID；检查返回规范对象标识后，后续使用该标识。资产本身必须在项目 `Assets` 自有目录；类型源码可来自项目自有脚本或实际解析的 Framework/Nova SDK/Kit 包自有 `Scripts`。包内容、vendor、Plugins、生成物和测试资产不能作为写入目标。场景与其它共享 MCP 分支按参考冻结真实 scenePath/prefabPath、完整对象路径、当前组件标识和所属保存文件，不能把 instanceID 当持久 GUID。

先调用 `nova_project_action(operation="describe", action_id="nova.project.settings.inspect-target")` 获取当前 Request Schema，再按 `plan → execute` 只读检查。结果提供真实字段全路径、Unity 类型、tooltip、当前值 JSON、是否可编辑及阻断原因。敏感字段和包含敏感子项的聚合对象不返回原值，只报告存在性；不要从其他坐标、Sample 或日志猜凭据。

## 修改与保存

有明确修改意图时，使用 `nova.project.settings.patch-target`，传相同目标和 1 至 64 个 `patches`。每项为检查返回的精确 `propertyPath` 与 `valueJson` 字符串；后者承载一个严格 JSON 值，真实字段类型决定是否合法。

- 字符串示例：`{"propertyPath":"m_Title","valueJson":"\"设置\""}`；空字符串使用 `"\"\""`。
- 布尔值示例：`{"propertyPath":"m_Enabled","valueJson":"false"}`。枚举使用检查返回的精确枚举名称字符串。
- 对象引用使用 `assetGuid/localId` JSON 对象，明确清空时使用 `"null"`；不接受 instanceID、对象名或类型名替代。
- `localId` 与 Int64/UInt64 字段值必须保留完整十进制整数。构造 `valueJson` 时保留原整数文本，或使用精确整数库；不要先转成 JavaScript Number 或浮点数再序列化，超过其精确范围会改变值。JSON 内仍传整数 token，不能改成带引号的数字字符串。
- 数组使用 JSON 数组，最多 1024 项；结构对象仅更新明确提供的真实直接子字段。重复/父子重叠路径拒绝；省略字段保留。

Plan 只在 `SerializedObject` 待应用缓存赋值并丢弃，未创建 clone、实例化组件、Apply 或保存。展示精确字段写集、唯一目标文件、源脚本和整体状态指纹；已有任务授权覆盖这些具体修改时继续执行，否则在真实写入前取得针对该计划的确认。Execute 必须绑定 `action_id/plan_id`，需确认时传 `confirmation_token=plan_id`；不要把自然语言确认和绑定计划 token 混为一事。

Execute 复核身份、文件、完整序列化状态和草稿，再只保存所属 Prefab 文件或单个普通 SO。字段已经满足时不保存。资产或 Inspector 有未保存草稿、同目标 PrefabStage 打开、自定义 Inspector 无草稿证明时先保持 `blocked`，让用户处理现存草稿，不能自动保存或丢弃其他工作。

## 适用边界与结果

`settings.patch-target` 自身只支持普通、没有嵌套实例的 Prefab 资产组件和单对象 SO。场景组件、Prefab 变体/嵌套、内置组件及 UnityEvent 使用上方已经存在的共享 MCP 分支，不能扩写 Action 能力或声称同等冻结/脱敏保证。子资产容器、动态管理引用类型替换以及带 `OnValidate`、`ISerializationCallbackReceiver` 或编辑期运行特性的业务类型仍先读对应领域契约，明确回调实际写集，不能用其它工具绕过草稿或回调保护。

`ConfigMasterSO` 及 SDK/Kit 维度配置走配置专用 Skill/Action，`PipifySettingsSO` 走 Pipify 专用编辑能力，`ConfigRuntimeSO` 从权威源正式导出。编辑普通组件字段不会替代 UI 表注册、Prefab 结构绑定、资源导出、代码生成、初始化或 Runtime 验证；用户要求完整功能时继续沿实际消费链补足这些层级。

普通资产 Action 保存后用 Receipt/recovery token 调 `verify`，只读核对整个资产状态和文件。共享 MCP 没有该收据，按参考读回目标组件、保存文件、Prefab 来源/override 或事件持久记录及草稿状态。写入开始后失败返回 `partial`，不能自动回滚、重放或把缺证据标为成功。字段维护的 `success` 只证明本轮字段及确切文件保存；业务控制接入还须实际可达入口、完整生命周期与允许范围内的运行分支证据。只登记工具、只读回内存、只得到工具 success 均不足。游戏交互、Player、真机和外部服务仍需任务对应证据；本轮新增共享分支的源审查不冒充实际项目执行验收。只读解释字段时无需强行进入写入。

同一目标和 Unity Editor 单写者串行。测试完成后清理本轮临时对象、脚本和日志并核对工作树；保留用户要求的正式资产修改，报告清场结果。
