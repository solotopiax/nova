---
id: ADR-089
title: iOS 应用权限用途说明由项目显式配置
status: accepted
date: 2026-09-29
summary: iOS 用途文案由项目填写，框架只提供配置与注入
category: arch
keywords: [ADR-089, iOS, Info.plist, UsageDescription, PrivacyInfoConfig, ConfigWindow, 隐私配置]
tags: [adr, nova, framework, config, ios, privacy]
related:
  - "[[ADR-078-privacy-aes-and-app-aes-separation]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header]]"
---

# ADR-089：iOS 应用权限用途说明由项目显式配置

## 背景

iOS 应用的 `Info.plist` 用途说明取决于项目实际调用的权限和面向用户的功能。同一 SDK 在不同项目中的用途也可能不同。仅凭框架或已安装 SDK，无法生成真实、完整的应用文案；缺项可能到平台构建或提交阶段才被发现。

`PrivacyInfo.xcprivacy` 的 Required Reason API 声明与 `Info.plist` 的权限用途说明是两份独立声明，不能互相代替。

## 决策

- 在现有 ConfigWindow「通用配置 → 隐私配置」详情页维护应用级 `InfoPlistUsageDescriptions` JSON；沿用 `PrivacyConfigs` 的 `Platform × Channel × DevelopMode` 保存、投影和单格导出链，仅 iOS 可编辑并参与构建。
- JSON 键使用对应的 `UsageDescription` 键，值由项目组填写面向用户的真实用途。默认 `{}`；框架不预填通用文案，不根据已安装或已启用 SDK 推断必需键，暂不为各 SDK 暴露独立配置界面。
- iOS 构建后处理从已导出的 `ConfigRuntimeSO.PrivacyConfigs` 写入应用 `Info.plist`。`PrivacyInfoConfig` 仍单独合并到应用级 `PrivacyInfo.xcprivacy`。
- 项目组在构建或上传时发现具体缺项后，补充当前 iOS 坐标的配置，重新导出 Config、构建并检查最终归档中的应用 `Info.plist`。键名格式校验不能证明 Apple 认可该键，也不能保证声明已覆盖项目实际使用的全部权限。

## 后果

- 框架提供统一入口和稳定导出路径，同时避免发布物携带虚构或与项目用途不符的默认文案。
- 权限清单的完整性和文案真实性由项目组核对；补充配置后需要重新构建应用，旧归档不会随 Config 改动更新。
- 非 iOS 配置不会触发用途说明校验或构建注入。

## 被排除方案

| 方案 | 原因 |
|---|---|
| 一次性预填所有已知权限用途说明 | 框架无法知道项目的真实用途，空泛文案不能代表应用行为。 |
| 按 SDK 自动推断并填入应用级文案 | SDK 的安装或启用不等于项目使用了每一项权限，且无法推断面向用户的用途。 |
| 在各 SDK 配置详情页分别暴露应用级 `Info.plist` 字段 | 当前需求只需应用级统一配置；分散填写会增加冲突与覆盖风险。 |

## 来源与验证

- 当前实现：`PrivacyConfigs.InfoPlistUsageDescriptions`、`ConfigWindow.RightPanel`、`EditorUtil.Config.Exporter`、`InfoPlistUsageDescriptionsParser`、`IOSPrivacyManifestBuildProcessor`。
- 当前说明：`Assets/Framework/Docs/Runtime/Modules/Config/PrivacyConfigs.md` 与 `Assets/Framework/Docs/Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md`。
- 验证：解析用例 6/6 通过，Editor 与测试程序集静态编译 0 错误；非 iOS 灰显态已查看。iOS 启用态与真实 Xcode 归档尚未验证，不能据此断言上传平台已接受全部用途说明。

## 关联

- 隐私配置中 AES 与应用协议 AES 的职责边界：[[ADR-078-privacy-aes-and-app-aes-separation|ADR-078]]
- ConfigWindow 三维配置面板的通用约束：[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]
