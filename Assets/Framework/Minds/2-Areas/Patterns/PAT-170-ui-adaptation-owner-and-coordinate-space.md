---
id: PAT-170
title: UI 适配先定拥有者与坐标空间
summary: 根画布统一缩放，安全区查询与内容布局分开接线
category: runtime
type: pattern
status: active
date: 2026-10-29
aliases:
  - PAT-170-ui-adaptation-owner-and-coordinate-space
keywords:
  - PAT-170
  - 屏幕适配
  - 设计分辨率
  - 安全区
  - CanvasScaler
tags:
  - pattern
  - ui
  - layout
  - safe-area
related:
  - "[[PAT-79-demo-view-portrait-layout|PAT-79]]"
---

# PAT-170：UI 适配先定拥有者与坐标空间

## 适用场景

Nova 项目开始制作 UI，或调整屏幕方向、根画布和安全区布局时；尤其是把某个 Demo 的布局推广到正式业务时。

## 核心做法

先选定方向、设计尺寸与支持比例，再确定根缩放和安全区各自唯一的拥有者。当前实现与实施、验收步骤以 [UIComponent 文档](../../../Docs/Runtime/Modules/UI/UIComponent.md#屏幕适配与安全区) 为准，不在知识库维护第二份参数表。

根画布已有框架配置时复用它；保持棋盘或图片比例属于局部内容职责。安全区查询只给出设备空间的数据，内容布局须按实际父级空间换算；框架提供查询不代表页面已经接入。子容器承担避让，背景是否延伸到边缘单独决定。适配需要覆盖尺寸变化与对象复用，不能只看首次显示。

## 原因

父画布与页面各自缩放，会让尺寸和输入映射相互影响；把设备像素当作画布单位，会随缩放比例产生错误边距。重复避让则无端缩小可用区域。先明确拥有者和空间关系，才能判断该改框架配置还是业务内容，避免页面做好后不断补丁式调整。

## 反模式

- 用每页根缩放脚本覆盖已有画布策略，或只改 Prefab 缩放器而忽略框架初始化写入。
- 把安全区查询结果当成自动布局，直接把物理像素写入已缩放画布的 offset。
- 从单张截图、单台设备或 Demo 参数推导所有业务页面的默认适配方案。
- 首次显示正确就认为旋转、边界比例和池化重开也正确。

## 来源、影响与验证边界

- 当前事实来自 `UIComponent.cs`、`UIComponent.Visitors.cs` 的根画布配置与安全区门面，以及 `UIView.cs`、`UIView.Methods.cs` 的初始化/复用语义；路径由上述模块文档进入。
- 本条约束跨框架根画布与业务内容的职责分配，不改变运行时代码，也不证明某个项目已通过设备适配验收。
- [[PAT-79-demo-view-portrait-layout|PAT-79]] 保留 BaseDemoView 的特定竖屏方案，不能推广为 Nova 业务默认值。
