---
id: PAT-78
title: Nova Sample Demo 七阶段交付流程
summary: Sample Demo 按七阶段交付
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-78-sample-demo-full-flow-sop
keywords:
  - Nova Sample Demo 七阶段交付流程
  - PAT-78
  - PAT-78-sample-demo-full-flow-sop
tags: [pattern, methodology, sample, demo, workflow]
related:
  - "[[PAT-65-demo-coverage-standard|PAT-65]]"
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
---

# PAT-78：Nova Sample Demo 七阶段交付流程

## 适用场景

- Nova framework / SDK / Kit 的 sample demo 批量产出
- 需要同时交付页面、数据、资源、Prefab 和说明文档
- 任何“代码只是其中一环”的示例工程工作

## 七阶段

### 1. 设计期

- 先用 [[PAT-65-demo-coverage-standard|PAT-65]] 收敛覆盖矩阵
- 再用 [[ADR-033-maindemo-isolated-topology|ADR-033]] 确认 sample 自闭包
- 最后用 [[PAT-77-base-demo-view-three-zone-template|PAT-77]] 明确页面骨架

产物：

- 覆盖矩阵
- 页面树
- `DemoXxxView` 列表

### 2. 源数据期

- 补 demo 数据行
- 命名统一 `Demo_` 前缀
- 触发对应导出链，确认类型与数据都能产出

### 3. 资源期

- 只按需搬运资源
- 资源命名要进入 Nova 自己的命名体系
- 地址、分组与收集规则要补齐

### 4. 代码期

- 按既有模板写 `DemoXxxView`
- 如需临时数据对象，按 `IReference / POCO` 规则选型
- 页面入口与注册链路必须补齐

### 5. Prefab 期

- 通过 Unity Editor 真实序列化路径制作或修改 Prefab
- 补齐标题、交互区、反馈区与引用绑定
- 不直接手改 Prefab 文本

### 6. 验证期

- 编译通过
- 菜单能打开页面
- 页面交互可操作
- 反馈区有结果
- 关闭与返回链路正常

### 7. 文档期

- 同步模块级 `Docs`
- 只有出现新的长期模式时，才再回写 `Minds`
- 不把本次实施过程当成长期知识直接沉淀

## 为什么这样定

- Demo 交付是“设计、数据、资源、代码、Prefab、验证、文档”的串联工程
- 少掉任何一个阶段，最后都容易变成“代码在，但示例不可用”
- 按阶段组织后，问题更容易定位到覆盖、注册、资源、Prefab 或验证层

## 常见误区

- 跳过覆盖设计，直接零散加页面
- 资源全量搬运，之后再清
- 代码写完，但没补注册与资源链路
- Prefab 做完不验证引用
- Demo 可运行了，但当前事实层文档没同步

## 最小检查清单

- 是否有清晰的 Demo 覆盖目标
- 是否有对应的数据与资源入口
- 是否已完成 View 注册
- 是否已完成 Prefab 绑定
- 是否已做过一次真实运行验证
- 是否已同步当前事实层文档

## 关联

- [[ADR-033-maindemo-isolated-topology|ADR-033]]
- [[PAT-65-demo-coverage-standard|PAT-65]]
- [[PAT-77-base-demo-view-three-zone-template|PAT-77]]
- [[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]
- [[PAT-42-naming-concrete-deduplicate|PAT-42]]
- [[PAT-64-business-ui-view-suffix|PAT-64]]
