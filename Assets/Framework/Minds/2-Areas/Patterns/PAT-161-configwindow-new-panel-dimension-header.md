---
id: PAT-161
title: ConfigWindow 新增配置面板必须携带三维配置头部
type: pattern
status: active
date: 2026-08-13
summary: Config新面板必须接入独立三维头部工作副本保存与消费链
category: editor
aliases:
  - PAT-161-configwindow-new-panel-dimension-header
  - Config 全局配置中心新面板三维头部
keywords:
  - PAT-161
  - ConfigWindow 新面板
  - DrawPanelTitleWithMask
  - Platform Channel DevelopMode
  - 三维配置头部
tags:
  - pattern
  - editor
  - config
  - inspector
related:
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
  - "[[ADR-089-ios-privacy-usage-descriptions-project-owned|ADR-089]]"
  - "[[PAT-20-editor-panel-title-indent|PAT-20]]"
  - "[[ADR-090-resource-keys-before-config-loading|ADR-090]]"
---

# PAT-161：ConfigWindow 新增配置面板必须携带三维配置头部

## 适用场景

- 在 Nova `ConfigWindow` 的全局配置中心新增任意配置面板。
- 现有面板从全局单份配置升级为可按 Platform、Channel 或 DevelopMode 分份保存。

## 核心做法

1. 每个新增配置面板都必须在标题区调用 `DrawPanelTitleWithMask`，展示平台类型、渠道类型、开发模式三个维度开关及当前分组说明。
2. 面板必须拥有独立的 `PanelDimensionMask`，不得复用其他面板的掩码或数据结构。
3. 数据写入、维度加减和同组广播统一接入 `EditorUtil.Config.DimensionProjector`；保存按窗口显式编辑坐标及当前掩码自动整理。需要 Runtime 配置的面板按明确导出坐标裁剪为 ConfigRuntime 单格快照；Editor-only 面板由各消费入口解析已保存的维度数据。
4. 维度头部是 Config 全局配置中心的固定契约，新增面板时默认实施，不再等待单独需求补充。
5. Editor-only 配置同样复用 `DrawTitleWithMaskCore` 三维头部，独立掩码与逻辑组选择保存在 ConfigMaster。不能因不导出 ConfigRuntime 而省略头部；必须核对投影、保存和实际消费的坐标一致性。资源密钥只按维度选择当前加密编号，全部保留解密密钥共用，并通过 `DimensionProjector` 的资源工作副本入口衔接；不得将真实密钥放入 ConfigRuntime。
6. 保存链必须自动清理失效维度分支，不能只更新 UI 开关而保留会阻断导出的旧配置。矩阵同步与 Override 裁剪按存储类型分别处理；正常保存无需整理确认，真实来源歧义回滚，详见 [[ADR-058-per-panel-dimension-mask|ADR-058]]。

## 原因

- 缺少统一头部会让使用者无法判断当前数据是全局共用还是按坐标独立保存。
- 只增加 UI 字段而不接入独立 Mask 与 Projector，会产生“界面可切换、底层仍共享”或跨格数据污染。
- 所有面板使用同一交互模型，可以让坐标切换、维度操作确认、自动保存整理和导出语义保持一致。

## 反模式

- 新面板只绘制标题和字段，不提供 Platform、Channel、DevelopMode 三维头部。
- 复用 `AppConfigsMask` 等其他面板掩码，导致两个面板被迫同步分维。
- 直接修改当前格数据，却未调用 `DimensionProjector.BroadcastWithinGroup` 维持未勾选维度的一致性。
- 只在 ConfigWindow 加开关，不同步 ConfigMaster 存储、投影器、校验器与 Exporter。
- Editor-only 配置只增加三维开关，却不维护 WorkingCopy、顶部全局保存、构建坐标和真实消费链。
- 把 Editor-only 配置或全部资源密钥塞入 ConfigRuntime，或让正常保存要求用户先清理旧维度文件。

## 验证依据

- 既有维度决策：[[ADR-058-per-panel-dimension-mask|ADR-058]]。
- 公共标题入口：`Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.RightPanel.cs` 中的 `DrawPanelTitleWithMask`。
- 投影实现：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.cs`。
- 2026-08-13 隐私配置面板接入了独立 `PrivacyConfigsMask`、三维标题、同组广播与单格导出，并由 `PrivacyConfigAesContractTests` 覆盖。
- 资源密钥页复用统一头部，ConfigMaster.ResourceEncryption 保存当前编号维度选择；`Assets/Tests/Editor/ResourceEncryptionTests.cs` 覆盖八种掩码、加减维保留组、旧文件兼容、路径平台与缺失平台的清单阻断。

## 关联

- 每面板独立维度掩码：[[ADR-058-per-panel-dimension-mask|ADR-058]]
- 配置详情页标题与缩进：[[PAT-20-editor-panel-title-indent|PAT-20]]
- iOS 用途说明沿用隐私配置三维链：[[ADR-089-ios-privacy-usage-descriptions-project-owned|ADR-089]]
- 资源密钥的启动与配置来源：[[ADR-090-resource-keys-before-config-loading|ADR-090]]
