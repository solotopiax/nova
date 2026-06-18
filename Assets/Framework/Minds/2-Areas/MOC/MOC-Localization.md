---
id: MOC-Localization
title: 本地化系统图谱
summary: Localization 入口、语言切换与 TextLocalizing 边界速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Localization
  - 本地化系统图谱
  - i18n 图谱
  - 国际化图谱
tags: [moc, nova, localization, i18n, runtime]
keywords: [LocalizationComponent, ILocalizationManager, LocalizationManager, TextLocalizing, Language, SetLanguage, GetText, LocalizationRefreshEventData]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[PAT-09-inspector-config-i18n|PAT-09]]"
  - "[[PAT-28-luban-load-release-symmetric|PAT-28]]"
  - "[[MOC-Asset]]"
---

# MOC-Localization：本地化系统图谱

## 一句话

Localization 模块负责运行时语言解析、文本表切换和字体适配；`TextLocalizing` 是 UI 侧跟随语言刷新的桥，而不是本地化系统本体。

## 何时查这页

- 要改语言初始化、语言切换或文本获取
- 要理解 `LocalizationComponent` 和 `TextLocalizing` 的分工
- 要判断字体适配和文本刷新属于谁

## 当前结构

```text
Nova.Localization
  -> LocalizationComponent
  -> ILocalizationManager
  -> LocalizationManagerBase
  -> LocalizationManager

UI 侧：
TextLocalizing

启动期旁路：
LauncherLocalization / LauncherLocalizedText / LauncherDialogLocalizedText
```

## 当前高频入口

- `LoadSync / LoadAsync`
- `InitCurrentLanguageSync / Async`
- `SetLanguageSync / Async`
- `GetText(name)`
- `GetSupportedLanguages()`
- `ResolveLanguage()`
- `GetTexts<T>() / GetTexts(string)`
- `GetFontDatas(Language)`

## 当前边界

- 负责语言选择、语言持久化策略、文本表和字体数据管理
- `ResolveLanguage()` 的优先级是持久化记录、系统语言、回退语言
- 这是运行时主链，不覆盖启动期 `LauncherLocalization` 的轻量旁路
- `TextLocalizing` 挂在 `TextMeshProUGUI` 节点上，订阅 `LocalizationRefreshEventData`，负责刷新文本和按 `FontMark` 做字体/材质适配；`OnDisable()` 释放相关句柄

## 与其他模块的关系

- `Persist`：语言偏好会进入持久化选择链
- `Asset`：字体和材质加载依赖资源系统
- `Event`：UI 自动刷新依赖本地化刷新事件
- `Procedure/LauncherUI`：启动期文案走 `LauncherLocalization` 旁路，不依赖 `LocalizationManager`

## 导航提醒

- `TextLocalizing` 当前强依赖 `TextMeshProUGUI`，不是泛化到所有 `TMP_Text`
- `AutoFontAdapt` 为 false 时，`TextLocalizing` 不做字体适配
- 具体优先级与实现细节，以 `Docs` 和源码为准

## 常见误区

- 把 `TextLocalizing` 当成整个本地化系统
- 忽略启动期旁路，误以为 `Nova.Localization` 覆盖所有阶段文案
- 把字体适配逻辑塞进业务 UI，而不是复用 `TextLocalizing`
- 忽略 `OnDisable()` 的字体/材质释放
- 把语言切换只理解成“换文本”，忽略字体和事件刷新链

## 先往哪看

- 改结构：[[ADR-001-component-manager-three-layer]]
- 改资源句柄语义：[[ADR-042-assetmanager-load-api-all-return-handle]]
- 改 Inspector 多语言配置：[[PAT-09-inspector-config-i18n]]

## 关联

- 图谱：[[MOC-Asset]]、[[MOC-Manager]]
