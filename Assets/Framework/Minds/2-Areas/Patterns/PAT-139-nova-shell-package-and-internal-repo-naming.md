---
id: PAT-139
title: Nova 包壳策略与内部云仓库命名规则
summary: Nova 包壳策略与内仓命名规则
category: upm
type: pattern
status: active
date: 2026-06-11
aliases:
  - PAT-139
  - PAT-139-nova-shell-package-and-internal-repo-naming
keywords:
  - PAT-139
  - Nova 包壳
  - 内部云仓库
  - 原厂包名
  - 商业 SDK
tags:
  - pattern
  - upm
  - package
  - sdk
related:
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]"
---

# PAT-139：Nova 包壳策略与内部云仓库命名规则

## 适用场景

- 规划或维护 `UPMPackages/` 下的 Nova 框架包
- 评估某个第三方依赖该以 Nova 包壳方式集成，还是直接保留原厂包名分发
- 设计 Plugin 云插件服务中心、内部云仓库与自动安装链路时统一包名和依赖口径

## 核心规则

### 1. Nova 框架包默认自行包一层壳

- Nova 框架下的 package，默认以 Nova 自维护包作为对外安装与集成单位。
- 该包壳负责承接 Nova 侧需要的目录布局、元数据、示例、接入说明与日常开发集成方式。
- 例外仅限商业性质 SDK；这类包不再额外改造成 Nova 命名包。

### 2. 商业 SDK 例外走内部云仓库

- 商业性质 SDK 需要部署在内部云仓库中使用，不作为对所有用户开放的公共包壳来处理。
- 这类包在内部云仓库中保留各自原厂包名，不改成 Nova 自定义命名。
- 相关展示、更新提示与安装逻辑，应按“内部云仓库条目”识别，而不是按 Nova 包壳条目识别。

### 3. 自动安装链路优先兼容 Unity 约束

- Unity 对依赖声明有硬约束：子 package 侧不能依赖“由下一级 package 再配置 git url 依赖库”这条链路来完成 Nova 期望的自动安装。
- 依赖入口必须由项目级依赖关系统一承接，不能把自动安装成功与否寄托在子 package 继续向下拉 git url。
- 因此，Nova 日常开发默认采用“依赖源码直接进入 package”的方式，保证 package 自身具备可安装、可联调、可日常开发的完整性。

## 为什么这样定

- Nova 需要稳定的自动安装体验，不能把成功条件建立在 Unity 不支持的子 package 依赖链上。
- 自行包壳后，Nova 可以统一控制包结构、接入方式、文档与开发态依赖组织。
- 商业 SDK 保留原厂包名，有利于对齐上游识别、采购授权、升级对照与内部仓库检索。
- 把“Nova 包壳”与“内部商业 SDK 原厂包”分开，能避免公共仓库语义与内网仓库语义混淆。

## 反模式

- 给所有第三方包一刀切改成 Nova 命名，导致商业 SDK 上游身份丢失
- 指望子 package 继续通过 git url 依赖链完成自动安装
- 包壳与原厂包混用同一命名口径，导致仓库侧、安装侧和展示侧无法稳定区分
- 明明属于商业 SDK，却按公共 Nova 包壳规则处理

## 跨项目复用提示

适用于任何“Unity 主工程 + 多 UPM 子包 + 自动安装”型工作区。关键不是是否叫 Nova，而是要先承认 Unity 的依赖边界，再决定哪些包应该做统一包壳，哪些包必须保留上游身份。

## 关联

- [[PAT-41-upm-package-layout-and-manifest|PAT-41]]
- [[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]
