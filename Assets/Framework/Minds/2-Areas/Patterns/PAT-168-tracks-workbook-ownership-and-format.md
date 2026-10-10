---
id: PAT-168
title: Tracks 集中归属与统一格式维护
summary: 集中归属与目标不变，源表生成器统一单色样式
category: module
type: pattern
status: active
date: 2026-10-09
source: 用户确认的Tracks维护规则与当前源表及生成器
aliases:
  - PAT-168-tracks-workbook-ownership-and-format
keywords:
  - Tracks.xlsx
  - TrackRegistry
  - 打点表-Network
  - 埋点表
  - 集中归属
  - 上报目标
  - 单色数据区
tags: [pattern, sdk, tracks, editor]
related:
  - "[[PAT-148-ad-revenue-track-callback-boundary|PAT-148]]"
  - "[[PAT-165-mobile-iap-pay-failure-track-boundary|PAT-165]]"
  - "[[PAT-167-thirdpay-payment-terminal-and-validation-tracking|PAT-167]]"
---

# PAT-168：Tracks 集中归属与统一格式维护

## 适用场景

为 Framework、SDK 或 Kit 查缺补漏、增加事件、更新属性或统一 Tracks Excel 格式，以及维护本地打点汇总生成器。

## 核心做法

- 先核对实际事件调用和属性构造，再补既有表；代码包边界不等于登记表边界。广告渠道与 MAX 集中在广告聚合表，IAP Mobile、ThirdPay、Voucher 集中在 IAP 核心表，TGA 既有固定属性集中在 Framework 说明页，不因审计而拆表。
- 上报目标保留原登记值；接口能力与统计目的地分开理解，不把全部目标机械改为 `ITrackPlugin`。
- 源表和生成器共同遵循 Framework 的 `打点表-Network` 模板；数据区统一浅绿，禁止交替颜色，仅按事件合并 A～C，不合并 G～I，不丢失后续非空说明。
- 标签页名称不带 `$`；单元格内原厂实际事件/属性名的 `$` 保留。页名标记和协议标识是不同层级。
- Tracks 登记真实事件与固定属性。动态命名占位、接口能力和采集范围说明属于 Docs，不放到“属性名称”列，不为其建立泛化说明页。
- 客户端、服务端约定和原厂自动采集分开核实；预留事件标注未触发，不把客户端价格、类型或 Debug 语义套到未知服务端行为。

## 为什么这样做

既有集中表面向完整能力的消费和后台分析，机械按源码子包拆分会破坏整体可读性并引入重复登记。保留统计目标和标识符才能避免把接口实现细节误当成后台协议。统一源表与生成器则保证后续新增模块或再次生成时不会恢复旧的交替颜色和合并行为。

## 反模式

- 为 MAX、TGA 或每个 IAP 子包新建重复表，或把 ThirdPay 独有事件从既有 IAP 表移走。
- 在格式修正中改变事件集合、属性类型、原有上报目标或服务端约定。
- 删除标签页 `$` 的同时删除 `$PayEvent`、`$pay_amount` 等实际协议名中的 `$`。
- 将“业务自定义事件”“原厂自动采集”等说明登记成具体属性。
- 直接把 XML `<summary>` 注释标签复制到枚举说明，或保留已撤回子表的引用。
- 只改单次源 Excel，生成器仍用交替色或跨事件合并；删页后保留失效筛选定义名。

## 来源、影响与验证

- 详细当前规则：`Assets/Framework/Docs/TRACKS.md`；工程门禁：根 `AGENTS.md` 与 `.nova/RULES.md`。
- 当前模板：`Assets/Framework/Tracks/Tracks.xlsx` 的 `打点表-Network`。
- 生成行为：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.cs` 与 `EditorUtil.TrackRegistry.XlsxWriter.cs`。
- 已确认源表文本与目标不因统一样式改变；实际 C# 写入器独立编译及输出的字体、颜色、排版、合并和冻结检查通过。该证据不证明 Unity 界面效果或后台收到事件。
- 影响所有 Tracks 维护与汇总任务；事件阶段边界仍以 [[PAT-148-ad-revenue-track-callback-boundary|广告收益回调]]、[[PAT-165-mobile-iap-pay-failure-track-boundary|Mobile失败边界]]、[[PAT-167-thirdpay-payment-terminal-and-validation-tracking|ThirdPay阶段分层]] 和当前源码为准，本规则不改变其运行时契约。
