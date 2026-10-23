# Config 面板编辑

`EditorUtil.Config.PanelEditing` 是内部 Editor 服务，复用 `SerializedPropertyValues` 的严格值转换和 `DesignTimeEditing` 的统一保存。`ConfigInspectPanelAction`、`ConfigPatchPanelsAction` 分别封装查询与编辑；窗口不作为执行引擎。消费请求见 [面板参考](../../../../Agents/Skills/nova-project-configure-runtime/references/panel-editing.md)。

## 目标与值

目标由 `masterGuid`、精确 `platform/channel/developMode` 及 `panel` 冻结，平台和渠道不能为 None。支持 `AppConfigs/PrivacyConfigs/SDK/Kit/Namespace/HybridEditorConfigs/YooAssetEditorConfigs/CDNEditorConfigs/Custom/ResourceEncryption`。SDK/Kit 必须另有已保存 Config 实例的 `configType` FullName；其它面板拒绝它。内部面板枚举不扩写公共 `DimensionProjector.PanelKind`。

`Inspect` 返回当前真实字段 path、类型、tooltip、editable、枚举名、脱敏当前值及 `defaultStatus=unknown`，并返回掩码和同组物理坐标。路径属于实际面板 SerializedProperty，Namespace 唯一字段为 `Value`；不根据用户术语构造序列化路径，不把资产当前值当声明默认值。敏感叶及含敏感子项的聚合值不返回原值。

`BuildPreview` 接收 1 至 64 个 change，每项含 target 和至少一项 fields 或 dimensions。fields 最多 128 项，各含真实 path 和承载严格 JSON 值的 valueJson；dimensions 最多三轴，axis 为 `Platform/Channel/DevelopMode`、enabled 为布尔。拒绝重复轴、重复字段、父子重叠及逻辑共享组的冲突写入；不能写管理引用类型元数据、回调或代码。数组最多 1024 项，数组整替换可改变该数组结构；对象省略的成员保留，只允许请求实际提供的子叶变化。

## 维度与完整写集

预演创建独立 Master 工作副本。保存前后完整归一必须没有请求外变化；非规范结构先经独立 `normalize-master` 以明确坐标锚点整理，再重新 Inspect/Plan。普通面板先执行既有分裂/合并算法，再按最终掩码广播字段；Plan 展示全部实际物理差异。关闭轴时保留来源由 target 决定，不能隐式使用窗口当前格。

Custom 是全局键值列表，拒绝 dimensions，不做矩阵广播。ResourceEncryption 是顶层密钥字典，编号维度通过独立投影与 dimensions 修改，DimensionMask 不直接暴露为字段写入。新增 Package/Key 或轮换编号可预演；全部旧包、旧 keyId 与原 Base64 字节必须保留，删除旧编号或同编号替换字节拒绝。保存不生成密钥源码、不更新 Runtime、不构建；后续链见 [资源加密](../../../Runtime/Modules/Asset/ResourceEncryption.md)。

Privacy 初始 Key/IV 仍可逐步填写；原配置尚未同时具有 UTF-8 各 16 字节的有效凭据时，允许补齐，正式导出继续按现 Validator 校验。已有有效 Key/IV 的任一更换或清空通过 `patch-panels` 阻断，要求显式存档迁移契约。检查比较预演完成后的全部物理坐标，因此字段广播、合并维度和从空格初始化也不能覆盖其它坐标的既有有效凭据。相同 Key/IV、`PrivacyInfoConfig`、`InfoPlistUsageDescriptions` 等其它隐私字段保持可编辑；错误只报告固定 `privacy-key-rotation` 阶段，不回显凭据。

该保护限定 `PanelEditing.BuildPreview` 与 `nova.project.config.patch-panels`；ConfigWindow 手动编辑及共享 `SaveWorkingCopy` 的现有行为保持。需要保留旧数据的 Key/IV 轮换使用同一存档 Skill 的独立[Privacy协调分支](../EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)，限定当前macOS Editor标准三存储与当前Privacy组、正规Master保存/Runtime导出及独立恢复，不使用普通配置入口替代迁移。通用 settings Action 已拒绝整个 ConfigMasterSO/ConfigRuntimeSO，不能旁路编辑 Runtime Privacy。

## 已登记内嵌 Store 引用

`PanelEditing.References` 与 `nova.project.config.ensure-panel-references` 复用上述目标、掩码、完整快照和统一保存；首版仅在已有 `IAPPluginConfig` 的真实 `m_StoreConfigList.m_Items` 中追加缺失 Mobile/ThirdPay/Voucher Store Config。类型全名由已解析包核实，冻结包版本、完整 Runtime 源、asmdef、程序集和 MVID，拒符号链接来源及任意 CLR 类型。当前登记类型的已序列化字段限标量、枚举、字符串与一维数组/列表；复杂新类型须先扩展受控契约。

同掩码逻辑组逐格构造独立实例，保留旧 Store 字段、顺序及请求外状态；已存在项为幂等无写入。Plan 不保存；Execute 复核来源和草稿后只保存目标 Master；Verify 仅核完整状态及稳定来源，不重放。它不启用顶层 SDK、不改变维度、不导出 Runtime，后续字段 patch 和正式导出独立执行。

## 保存及后验

Plan 冻结文件、meta、完整序列化状态及精确物理写集，输入值只留内存执行态，不写公开计划、Receipt 或逐密钥摘要。Execute 复核身份、全部指纹、重做副本预演及草稿，调用统一单资产保存，不切活动平台或当前坐标。相同目标不保存。已有窗口草稿、资产脏态、来源漂移或请求外变化阻断；实际提交开始后的失败为 partial，不能重放或自动回滚。

Verify 只读核对目标文件/meta 与完整语义状态；不归一、保存或导出。Master 保存成功仅证明设计态状态，运行快照、编译、实际业务、平台及厂商结果分别验收。Config 导出依当前 Active BuildTarget，面板设计态编辑可指向其它真实平台坐标。

源码：[PanelEditing](../../../../Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.PanelEditing.cs)、[Inspect Handler](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Inspect/ConfigInspectPanelAction.cs)、[Patch Handler](../../../../Scripts/Editor/EditorUtil/EditorUtil.AgentActions/Handlers/Ensure/ConfigPatchPanelsAction.cs)。
