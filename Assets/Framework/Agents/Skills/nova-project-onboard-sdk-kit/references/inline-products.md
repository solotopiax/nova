# 从已有 Excel 批量替换支付商品

用户只需说明使用哪份项目商品表、目标商店与希望导入的坐标。Agent 从真实文件、已安装包及 Config `inspect-panel` 读取路径、Config FullName、列表字段和当前值；业务价格、商品及覆盖范围不明确时，只补这些决策。

## 同一个保存入口

执行复用 `nova-project-configure-runtime` 的 `nova.project.config.inspect-panel / patch-panels / export-runtime`。以下转换 CLI 只读取工作簿，不写源、不导入 Master、不生成 Runtime，也不是新 MCP Action：

```text
python3 -B <resolvedFramework>/Agents/Tools/nova_inline_products.py --project-root <projectRoot> --source <projectRelative.xlsx> --source-hash <currentWholeFileSha256> --sheet <confirmedSheet> --family iap
```

微信使用 `--family wechat`；购买表含 `GameCurrency` 时还需 `--coins-per-yuan <currentOrAuthorizedFinalValue>`，该值取目标坐标的 `m_GameCurrencyCoinsPerYuan`，不从别的项目或 Sample 猜测。

| 类型 | 当前 Config FullName | 列表字段相对 path |
|---|---|---|
| IAP | `NovaFramework.SDK.IAP.Runtime.IAPPluginConfig` | `m_Products.m_Items` |
| 微信 | `NovaFramework.SDK.WeChatMiniGame.Runtime.WeChatMiniGamePluginConfig` | `m_PurchaseList.m_Items` |

这些路径须与当前 live `inspect-panel` 的 `editable` 和结构一致；版本漂移或任一真实字段无法保存时停止，不省略商品名或改成反射赋值。IAP 的公开 `StoreConfigs` 属性对应另一份管理引用列表 `m_StoreConfigList.m_Items`，本转换工具不创建商店或更换类型。首次商店接入先复用 `nova.project.config.ensure-panel-references` 在已安装 Mobile/ThirdPay/Voucher 白名单中仅补缺项，核验全部同组写集与旧值保留；随后通过面板字段保存和正式 Runtime 导出完成配置，具体协议见 [面板编辑参考](../../nova-project-configure-runtime/references/panel-editing.md)。

## 表源与校验

- 仅已有项目自有 `.xlsx`，显式选择唯一 Sheet、冻结整文件 SHA256；不自动猜首张页。支持 IAP 固定九列及 `##var` 模板，微信固定表头及 Solar 的 ID/Count/Price/Desc 别名；Kind 必须显式填写，不从名称推断。
- 仅输出已知商品字段与原行号；其它页及其它列不回显。拒绝宏、外链，以及读取到的已知商品字段中的公式/明显凭据材料、重复主键、非法枚举/范围和源漂移；数字与 SKU 读取原文本，避免浮点中转丢精度。
- IAP TableId 为 `1..4294967295`，ProductType 为精确 `Consumable/NonConsumable/Subscription`。价格保持字符串，非空价格须为范围内非负十进制；允许源价格/平台 SKU 为空；空字符串直接进入新完整列表，不读取或回填旧 Master 值，必须在 Plan 的完整替换差异中确认，也不代表对应商店可用。该转换在价格格式、公式和空表方面比手工导入器更严格。
- 微信 TableId、Quantity、PriceFen 为正 int；Price 元必须精确到整数分，道具须有 ProductId。游戏币满足 `PriceFen × GameCurrencyCoinsPerYuan = Quantity × 100`。不把客户端价格当成可信实付，不创造奖励数量。
- 完整列表为 `1..1024` 项，`valueJson` 同时满足字段 `32768` 字符与 Gateway `16 KiB UTF-8` 字符串限制；中文等多字节内容按字节计。超过任一上限整体阻断，不输出无法保存的 prepared 请求；不能把完整替换拆成多次覆盖造成丢行。清空商品、编辑 Workbook 行、`.xls/.xlsm` 转换不由此工具完成。

## 配置 Plan 与后验

转换返回 `stage=prepared-only`、`sourceHash/sourceRows/configType/field`。只把 `field.path/valueJson` 放入当前坐标 SDK 面板的一项 `changes.fields`；Master GUID、平台、渠道、模式仍由 Config Action 真实 Schema 冻结。游戏币汇率若需修改，将已授权最终值与列表置于同一面板变更，不把检查用值漏在实际配置之外。

完整替换会删除当前列表中源表没有的条目。Plan 前按 TableId 对比现有列表与源，列出新增、变更、删除、SKU/类型/价格/币种差异，并核对维度共享导致的全部物理写集。执行前重新校验源 SHA；已冻结具体范围的授权持续有效。若源在后续保存窗口内漂移，读回当前 Master 与源并报告 `partial`，不能把旧转换冒充最新源导入，也不能自动重放。

依次 `describe → plan → execute → verify` 保存 Master，随后正式导出目标 Runtime、编译并核对 IAP `ProductTable` 或微信 `Purchases` 的实际消费。本地产物成功只证明本层；商店商品可售、签名、验单、幂等发货和平台资格仍分别验证。临时转换输出/脚本清场，不将商品以外的 SDK 配置或凭据放入记录。

转换器只使用显式页和已知列。微信别名按固定优先顺序选择：优先列存在但为空时不会再取后面的别名补值。文本/凭据检查是有限规则，不能证明整份工作簿没有敏感信息；未知列和其它页不输出。价格格式检查只验证本地非负十进制和范围，不证明商店价格、订单或服务端验单有效。
