# Config 已有面板字段与维度

固定六字符串字段继续复用 `patch-coordinate`。其它已有面板字段和维度通过 `inspect-panel` / `patch-panels`，不由通用SO编辑入口修改ConfigMaster。

## 检查真实目标

先 live `describe`，再只读查询：

```json
{
  "masterGuid": "实际Master资产GUID",
  "target": {
    "platform": "Android",
    "channel": "Official",
    "developMode": "Debug",
    "panel": "Namespace"
  }
}
```

坐标示例必须替换为当前真实精确枚举名和已存坐标，不能照抄来源值。面板仅为 `AppConfigs / PrivacyConfigs / Namespace / HybridEditorConfigs / YooAssetEditorConfigs / CDNEditorConfigs / SDK / Kit / Custom / ResourceEncryption`。Custom 是全局本地键值列表，不接收 dimensions。ResourceEncryption 是顶层密钥字典和独立编号维度：维度只通过 `dimensions` 修改，不直接编辑 DimensionMask；旧密钥编号和原字节必须保留，轮换增加新编号。两者不做矩阵字段广播。SDK/Kit另需 `configType`，它是已保存Config实例的精确FullName，不是Plugin类型名；其它面板禁止类型参数。

检查返回实际字段相对path、显示名、tooltip、类型、`editable`、脱敏值、`defaultStatus=unknown`、维度掩码与完整同组物理坐标。Namespace只有 `Value` 路径，其它路径从inspect结果读取。缺失实例/掩码/坐标或非规范结构不能靠该入口猜测和补齐。

## 变更请求

首次 IAP 配置缺少商店实例时，先复用 `nova.project.config.ensure-panel-references`，再编辑现有字段。请求绑定 `masterGuid`、SDK `target`、真实 `collectionPath="m_StoreConfigList.m_Items"` 与已安装 Store 的 `typeFullNames`；首版仅允许 Mobile、ThirdPay、Voucher 的已登记 Config 类型，不接任意 CLR 类型或初始值。类型全名由 Agent 从当前已解析包核实。

Plan 在独立副本上仅补当前掩码逻辑组的缺项，保留所有旧 Store 值及顺序，并冻结完整 Master、meta、包源码、程序集与 MVID；Execute 重新核对来源、脏资产、窗口草稿和全部物理写集，只保存该 Master。Verify 只读且不重放。此操作不启用顶层 SDK、不改变维度、不导出 Runtime；后续分别完成 `patch-panels`、正式导出与编译。已有 Store 的再次 Ensure 为无写入幂等操作。

`patch-panels` 接收 `masterGuid` 与1至64个 `changes`。每项至少提供字段或维度修改，最多128个字段与3个维度轴，不接受重复字段/重复轴。

```json
{
  "masterGuid": "实际Master资产GUID",
  "changes": [
    {
      "target": {
        "platform": "Android",
        "channel": "Official",
        "developMode": "Debug",
        "panel": "Namespace"
      },
      "fields": [{ "path": "Value", "valueJson": "\"Game.Client\"" }]
    }
  ]
}
```

`fields` 缺省保留；`valueJson` 是严格JSON值的字符串，具体可用值按实际SerializedProperty类型。基础值、枚举精确名称、支持的Unity值对象和数组/对象以live `editable` 与受控转换为准，不宣称任意复杂类型均可编辑。不能写管理引用类型元数据、UnityEvent回调或任意代码。

`dimensions` 使用 `{"axis":"Platform|Channel|DevelopMode","enabled":true|false}`。先沿用既有维度分裂/合并，再按最终掩码广播字段。关闭轴时由 `target` 的坐标明确选择保留来源；同组同字段不同请求值拒绝。Plan列出全部物理差异，不能把一个逻辑坐标误认为只写一个物理格。

Privacy 初始 Key/IV 可逐步填写；原 Key/IV 按 UTF-8 各为16字节时，最终 clone 的同一物理坐标与 DevelopMode 必须保留两值。`patch-panels` 阻断更换、清空、维度合并及字段广播覆盖任何原有效凭据，包含从尚未初配的格广播到已有格。原同值请求和其它隐私字段可编辑；错误只显示固定 `privacy-key-rotation` 阶段，不回显凭据。保护限定 `PanelEditing.BuildPreview` 与该 Action；ConfigWindow 手工编辑和共享 `SaveWorkingCopy` 现行为保持。旧数据须保留时，路由同一存档 Skill 的[日常维护参考](../../nova-project-integrate-persistence/references/maintenance.md)中独立 Privacy 分支；限定当前macOS Editor三存储/当前Privacy逻辑组与正规保存/导出/独立恢复，不能用普通配置patch代替迁移。

## 保存与验收

预演只在独立Master副本；实际文件/全序列化状态/完整物理写集及窗口草稿反复核对。只保存目标Master，不改活动BuildTarget，不导出Runtime，不构建。相同目标保持文件不变；写后失败为partial，使用Receipt只读Verify，不回滚重放。

凭据不回显值或逐值摘要。现有授权继续有效，协议token仍绑定当前Plan。面板保存只证明设计态状态；本Skill若要求运行快照，继续单一实际坐标Runtime导出与编译。未验证设备、外部厂商配置或实际Player运行需保留边界。

## 非规范 Master 的独立整理

普通 `patch-coordinate` / `patch-panels` 拒绝非规范矩阵或请求外归一变化时，停止该计划并独立调用 `nova.project.config.normalize-master`，不让新手为此必须手动打开窗口。先 live describe 确认当前 Schema；请求只含目标与明确的存量分支锚点：

```json
{
  "masterGuid": "实际32位Master资产GUID",
  "platform": "Android",
  "channel": "Official",
  "developMode": "Debug"
}
```

示例坐标须替换为当前已确认的实际名称。Platform/Channel 不能为 None；DevelopMode 的名称以真实枚举及 live Schema 为准，不能用数字或宽松大小写。锚点决定被合并轴的存量保留来源，不能隐式选择窗口当前值或相邻格。已确认的任务坐标可以复用；多分支取舍不明确时，向用户说明值的用途与影响后确定保留来源，不让其猜字段名。

独立 Plan 在 clone 上执行既有完整归一与资源密钥校验，要求第二次归一不再改变状态；展示整份 Master 的全部实际差异地址与完整资产写集。凭据原值和逐密钥摘要不出现在 Plan/Receipt；脱敏不缩小需核对的物理范围。该范围可能超出最初单字段请求，必须与任务授权一致，不能把完整整理藏进字段 patch。

Execute 再次核对目标文件、meta、完整语义状态、草稿和精确差异，只调用单资产保存。已规范时不保存；不会切当前坐标或 BuildTarget，不导出 Runtime，不生成资源密钥源码，也不完成旧迁移文件清理。无法形成无歧义稳定状态、脏资产或窗口草稿阻塞时保留 blocked，不旁路写 YAML。保存开始后的失败为 partial，断线或重载后使用 recovery token 只读 Verify，不能重放 Execute。

Verify 仅核对目标身份、文件/meta 摘要和完整语义状态，不再次归一或保存。成功后重新 inspect 目标面板或 validate-coordinate，并重新 Plan 所需 patch；旧字段计划和旧确认不能沿用。整理成功只证明设计态保存；当前项目是否实际执行通过，必须以 live Execute/Verify 证据为准，源码登记或静态检查不能替代 Runtime 导出、Unity 编译或 Player 验收。
