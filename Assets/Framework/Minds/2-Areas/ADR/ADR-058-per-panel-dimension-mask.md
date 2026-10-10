---
id: ADR-058
title: ConfigWindow per-panel 可勾选维度（PanelDimensionMask + DimensionProjector + Override 旁路）
summary: 面板独立分维且保存按当前掩码自动整理无效数据
category: module
status: accepted
date: 2026-06-02
aliases:
  - ADR-058-per-panel-dimension-mask
  - ADR-058
keywords:
  - ADR-058
  - ConfigWindow per-panel 可勾选维度（PanelDimensionMask + DimensionProjector + Override 旁路）
  - ADR-058-per-panel-dimension-mask
tags:
  - adr
  - nova
  - config
  - configwindow
  - dimension
  - configmaster
supersedes: []
superseded-by: []
related:
  - "[[ADR-054-kit-config-three-dim-matrix|ADR-054]]"
  - "[[ADR-053-kit-config-templating|ADR-053]]"
  - "[[ADR-005-hybridclr-namespace-single-write-path|ADR-005]]"
  - "[[ADR-047-editor-active-master-anchor|ADR-047]]"
  - "[[ADR-049-yooasset-settings-via-configmaster|ADR-049]]"
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[MOC-Config|MOC-Config]]"
  - "[[ADR-090-resource-keys-before-config-loading|ADR-090]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]"
---

# ADR-058：ConfigWindow per-panel 可勾选维度

## 背景

ADR-054 建好了三维矩阵底座，但不是所有面板都需要按三维分别填写。有些字段应保持全局唯一，有些只需要按部分维度区分。

## 决策

- 给每个面板加 `PanelDimensionMask`，决定它是否按平台 / 渠道 / 开发模式分格。
- 顶层面板用 `XxxOverrides` 承载维度化后的差异值，矩阵面板直接沿用 `m_Entries`。
- 普通字段编辑只以当前坐标为输入：已勾选轴保持独立，未勾选轴立即广播到同一逻辑组的全部物理格。
- 维度勾选或取消由 `DimensionProjector` 对整个矩阵原子投影：勾选时拆分全部旧逻辑组；取消时每个剩余逻辑组分别保留顶部当前轴取值，不能用一个完整坐标覆盖全矩阵。
- 保存按当前掩码自动整理 WorkingCopy：未勾选轴以显式编辑坐标为来源，已勾选轴逐组保留，裁剪失效分支并校验后用 `CopySerialized` 写回；不再要求整理确认。不能确定有效来源的真实结构损坏仍原子回滚。Exporter 保持只读门禁，不隐式修正或保存 Master。
- 取数由 `DimensionalResolver` 统一处理，运行时 `ConfigRuntimeSO` 仍保持单格快照，不感知掩码。
- YooAsset 与其它面板一样只编辑 WorkingCopy；路径或维度变化后用 WorkingCopy 刷新预览注入，点击保存后才写入 ConfigMaster 资产。
- CDN 部署面板使用 `CDNEditorConfigsMask + CDNEditorConfigsOverrides`，整套 `CDNEditorConfigs` 为一份快照，切坐标即整套切换；走 WorkingCopy 延迟落盘，仅 Editor 期消费，不导出 Runtime。

## 保存整理边界

- 当前掩码是有效维度的依据。全局表示所有平台、渠道和开发模式共用，历史分维残留由保存自动清理，不能要求用户手动修复后才能正常保存。
- 矩阵面板保留完整有效网格并同步共用组，不能把“清理无效维度”误解为删除非当前坐标。顶层默认值加 Override 的面板则删除失效分支；部分分维按剩余逻辑组分别保留。
- SDK/Kit 暂未启用的存量同样保留；当前来源缺失时，仅在同组已有值唯一时继承，不静默丢弃稀疏配置。
- 资源加解密只整理当前加密编号的 Override，不删除包内密钥字典。历史密文按包名/keyId 解密，与当前掩码无关。
- 自动整理不改变维度开关，重复执行结果稳定；真正重复有效记录或来源歧义必须失败并回滚。正常整理不弹确认，维度切换的操作确认与关窗保存提醒仍保留。

## 影响

- 全局唯一和维度化配置可以并存，避免无意义的重复填写。
- 读写职责分离，Exporter / ConfigWindow 共享同一套取数规则。
- 运行时结构不变，风险主要集中在编辑器侧。
- 用户确认维度切换后，内存副本会一次完成全矩阵转换；保存进一步自动清理旧维度残留，防止外部合并或历史配置使有效开关与实际数据不一致。投影或保存归一失败时保持原副本不变。

## 被排除方案

- 因失效旧分支拦住正常保存：用户已经选择了有效维度，不应再次手动修文件。
- 清空所有非当前格：会丢失仍勾选维度的独立内容，也破坏完整物理矩阵。
- 在导出时修正 Master：把读取产物与配置写入混为一体，难以判断实际保存内容。

## 来源与验证依据

- 用户明确约定：保存自动清理并矫正当前掩码下的无效数据，无需整理弹窗。
- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.cs` 的 `NormalizeForSave` 与 `Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.Methods.cs` 的保存入口。
- `Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.cs` 保持只读维度校验。
- 规则、来源选择、回滚与幂等边界见 `Assets/Framework/Docs/Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.DimensionProjector.md`；存储分类见 `Assets/Framework/Docs/Editor/Config/ConfigMasterSO.md`。
- `Assets/Tests/Editor/ConfigLayerContractTests.cs` 覆盖保存来源、已选轴保留、无效 Override、稀疏存量和原子回滚；`Assets/Tests/Editor/ConfigWindowMissingRefFlowTests.cs` 覆盖工作副本保存、重建与关窗恢复链。

## 关联

- 相关 ADR：`ADR-054`、`ADR-053`、`ADR-047`、`ADR-049`
- [[ADR-090-resource-keys-before-config-loading|ADR-090]]：密钥完整保留。
- [[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]：所有配置页接入统一三维头部和保存链。
