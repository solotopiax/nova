# SerializedTargetEditing

`SerializedTargetEditing` 是 `nova.project.settings.inspect-target`、`nova.project.settings.patch-target` 与存档维护事务共用的内部 Editor 服务。它复用 `SerializedPropertyValues` 的严格 JSON 转换，目标定位、保存、漂移和草稿检查只维护在本服务；消费侧流程见 `Agents/Skills/nova-project-edit-settings/SKILL.md`。

目标文件必须是 `Assets` 内自有普通 `.prefab` 或单对象 `.asset`；Prefab 组件用 `targetGuid + globalObjectId` 精确定位，SO 可先用 GUID 检查再复用返回标识。对象必须拥有真实匹配类型的 `MonoScript`；脚本可位于项目自有源码，或实际解析的 `com.solotopia.nova.framework` / Nova SDK、Kit 包自有 `Scripts`。脚本来源允许包内不代表包文件可作为编辑目标。

`Inspect` 枚举真实可见 `SerializedProperty`，返回完整 `path`、`displayName`、`tooltip`、Unity `type/propertyType`、`editable`、`valueJson`、`hasValue`、枚举名称和 `defaultStatus="unknown"`。数组与结构递归读取已有字段；敏感路径及含敏感子项的聚合值不返回原值。字段当前值不能作为声明默认值。完整对象/资产指纹包括隐藏字段，只有整体摘要对外输出。

`BuildPlan` 接收 1 至 64 项 `propertyPath/valueJson`。省略保留，路径必须来自当前可见字段，禁止重复及父子重叠。JSON 转换由真实字段类型决定，拒绝宽松字符串转换、未知结构成员、超范围数字和管理引用动态类型。数组最多 1024 项；对象引用必须指向真实 GUID/localId 及匹配类型。Plan 在现有 `SerializedObject` 待应用缓存中赋值，计算目标状态后 `Update()` 丢弃；不 `Instantiate`、`ApplyModifiedProperties` 或保存，不把真实改动当作预演。

`localId` 按 JSON Integer token 读取为 `long`；普通整数字段经 `decimal` 解析检查实际范围后写入 `longValue/ulongValue`。调用方构造 `valueJson` 时须保留原十进制整数文本，或使用精确整数库，不能先经 JavaScript Number 或浮点数舍入。大整数仍是 JSON 整数，带引号的数字字符串不被接受；引用 ID 已失真时会因无法精确匹配资产而阻断。

`SerializedPropertyValues.CanWrite` 只判断此序列化类型是否有转换实现，领域权限还须由目标、敏感路径、UnityEvent、管理引用及存档专属规则核对，不能据它开放任意字段。递归写入数组/结构的中途失败可能已改动待应用缓存，必须丢弃该预演缓存或重新 `Update()`；不能继续 Apply 剩余半份输入。它不调用业务 setter，也不承诺 Unity 序列化保存不会触发目标生命周期。

Execute 单次消费计划，重新核对 GUID、GlobalObjectId、目标类型、源脚本文件、meta、资产文件、完整状态及精确写集。已有脏态、Inspector 待应用字段、自定义 Inspector 未知草稿或同目标 PrefabStage 阻断。实际写入使用 `ApplyModifiedProperties`；普通 SO 只 `SaveAssetIfDirty`，Prefab 只 `SavePrefabAsset` 保存该文件。没有变化不保存。

本服务只支持普通无嵌套 Prefab 的自有 MonoBehaviour 和单对象 SO。场景、Prefab 变体/嵌套实例、子资产容器、内置组件、vendor/插件/生成目标、`ConfigMasterSO`、`ConfigRuntimeSO`、`PipifySettingsSO` 不在本服务的通用写入范围。目标类型有 `OnValidate`、`ISerializationCallbackReceiver`、`ExecuteAlways` 或 `ExecuteInEditMode` 时拒绝通用保存；这些行为可能扩大本轮写集，必须走领域契约，不能借其他通用工具绕过保护。

真实 `PersistComponent` 的 `m_UseAESForPlayerPrefs`、`m_UseAESForFileFragment`、`m_UseAESForSQLite` 与 `m_SQLiteCipherPassword` 改变持久数据格式或读取密码，通用 Plan/Execute 硬性拒绝其字段及父聚合结构，Inspect 标记 `editable=false` 并提示存档迁移。单纯保存开关不会转换旧数据。存档维护通过内部 `BuildPersistenceAESPlan(Target, bool, CancellationToken)` 创建唯一 FileFragment AES 布尔计划，值维护通过 `BuildPersistenceValueAESPlan(Target, string, bool, CancellationToken)` 仅选择 PlayerPrefs 或 SQLite 对应 AES 布尔字段，沿同一完整冻结/草稿/保存实现执行；非序列化的内部 Plan intent 在提交时再次检查真实类型、唯一字段与布尔值。请求及 Receipt 不增加 allow 标记或领域权限，Verify 不能恢复写入。PlayerPrefs/SQLite 值 AES 先完成各自受控数据维护再保存字段；SQLite Cipher 通过内部 `BuildPersistenceCipherPlan(Target, string, CancellationToken)` 只预演唯一字符串字段，Cipher intent 提交时再次核真实类型/路径/值类型，敏感值只在内部短时计划，收据仅保存整体资产摘要；外层必须先完成安全封存和同库轮换再保存。Privacy AES Key/IV 跨存储轮换使用存档领域的独立 schema4 协调契约，正常保存 Master 与正规导出 Runtime；本通用服务仍不开放 Config 目标，不能借通用 JSON 或共享 MCP 改字段替代数据迁移。

存档独立恢复使用内部 `AssertAesRecoveryTarget` / `RestoreAesRecoveryTarget` 与 `AssertCipherRecoveryTarget` / `RestoreCipherRecoveryTarget`，必须先由对应维护服务绑定原迁移令牌。仅本操作完整原/新内存状态可豁免资产 dirty 检查；磁盘来源由受保护原 Prefab 全文和原收据的整文件 SHA 认证，仅规整精确组件的对应单布尔值或密码标量，其余全文须相同。只读、PrefabStage、Inspector 草稿及回调检查仍生效；通用字段编辑始终拒绝已有脏态。恢复字段已为旧值但仍有本操作脏态时会实际保存，因此恢复后验使用领域的完整 clean 状态与真实保存收据，不沿用通用零变化不保存假设。旧 AES 日志缺少 `before-aes-asset.bin` 时，只有磁盘仍精确匹配原计划文件 SHA 才能恢复；历史已保存新值的文件不会凭裸 `savedSettings` 摘要获得恢复权限。

Receipt 只保存身份、源脚本/meta/整体文件与状态摘要和字段地址，不保存字段输入值。Verify 只重新加载并检查整个资产状态及已观察的保存文件，不回滚、不重放。Apply 开始后失败返回 `partial`；成功只证明字段及文件保存，不证明 UI 事件、运行配置导出、场景启动、Player 或真机行为。本服务没有场景保存契约，不能把其 Asset Receipt 宣传为场景验收。

## 同一 Skill 复用现有 Unity MCP

场景组件、内置组件、Prefab 变体/嵌套和已核实的 UnityEvent 配置由同一个 `nova-project-edit-settings` 分支复用现有 Unity MCP；准确请求、草稿、保存和证据边界见 [共享编辑参考](../../../../Agents/Skills/nova-project-edit-settings/references/unity-mcp-editing.md)。不会为每个组件或参数创建新 Skill，也不会扩大 `SerializedTargetEditing` 自身的输入范围。

当前审查依据是项目解析的 `com.coplaydev.unity-mcp` v10.3.0 `Editor/Tools/ManageComponents.cs`、`Prefabs/ManagePrefabs.cs`、`ManageScene.cs`、`ManageUI.cs`、`ManageAsset.cs` 和 `Editor/Helpers/ComponentOps.cs`，以及当前 Provider Tool Schema；包升级后须复核。`manage_components(set_property)` 支持精确组件索引，使用反射字段/属性或 SerializedProperty，并标脏但不保存；`manage_prefabs(modify_contents)` 在隔离上下文加载并 `SaveAsPrefabAsset` 指定路径，组件字典只选择第一个同类；`manage_scene(save)` 保存活动场景，传不同路径会 Save As。

`ComponentOps` 对 UnityEvent 强制走 SerializedProperty，不调用绑定方法；已有监听列表和目标/方法/参数仍须先核实。Prefab override 是否持久必须保存后核对，不能把未显式调用 `RecordPrefabInstancePropertyModifications` 的通用反射路线推断为已保证。`save_prefab_stage`、`manage_ui(update_panel_settings)` 和 `manage_asset(modify)` 会调用全局 `SaveAssets`，没有无关资产脏态证明时不能冒充单文件保存。共享 MCP 没有本服务的整体指纹、一次性收据和自动脱敏；源码审查及发现工具不证明当前项目已执行通过。
