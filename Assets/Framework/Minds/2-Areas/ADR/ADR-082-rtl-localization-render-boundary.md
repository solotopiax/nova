---
id: ADR-082
title: RTL 本地化转换固定在 TMP 渲染边界
summary: RTL 文本仅在 TMP 渲染前转换
category: module
status: accepted
date: 2026-08-27
aliases:
  - ADR-082-rtl-localization-render-boundary
keywords:
  - ADR-082
  - RTL 本地化
  - Arabic
  - TextLocalizing
  - ITextPreprocessor
  - Unicode 逻辑顺序
tags: [adr, nova, localization, rtl, tmp]
supersedes: []
superseded-by: []
related:
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
  - "[[PAT-67-no-ui-text-only-tmp|PAT-67]]"
  - "[[MOC-Localization]]"
---

# ADR-082：RTL 本地化转换固定在 TMP 渲染边界

## 背景（Context）

Arabic、Persian 和 Hebrew 使用从右到左书写方向；其中 Arabic、Persian 还需要按上下文连接字形。仅把翻译原文直接赋给 TMP，或只开启 `isRightToLeftText`，都不足以稳定处理字母连接、英文和数字混排、标点与富文本标签。

如果在 Excel、JSON、Binary 导出或 `GetText()` 层提前倒序，同一份文本会失去正常 Unicode 逻辑顺序，日志、格式化、搜索和非 UI 消费方都会得到渲染专用字符串。

## 决策（Decision）

1. Excel、JSON、Binary 和 `LocalizationManager.GetText()` 始终保存并返回正常 Unicode 逻辑顺序，不做倒序或字形替换。
2. `TextLocalizing` 在 TMP 的 `ITextPreprocessor` 边界执行 RTL 字形连接和双向混排整理，并按当前语言设置 `isRightToLeftText`。
3. RTL 语言集合由 `LanguageMetadata.IsRightToLeft(Language)` 统一定义；当前包括 Arabic、Persian 和 Hebrew。
4. 字体继续复用现有 `Language + FontMark` 配置链。具体字体由项目提供，并必须覆盖所需字符和 Arabic Presentation Forms。
5. 按钮顺序、返回箭头、Alignment、RectTransform、锚点和页面镜像属于业务 UI 设计，不由 Localization 框架自动修改。
6. 第一阶段只支持 `TextMeshProUGUI` 展示文本；`TMP_InputField` 的光标、选区和输入法行为不在本决策范围内。
7. shaping 实现保持 `internal`，第三方算法、类型和品牌不进入 Nova 对外 API；许可证与来源在实现目录内保留。

## 后果（Consequences）

### 正面

- 翻译人员和业务代码继续使用自然书写的原文，不需要手工倒序。
- `GetText()` 的既有语义不变，非 UI 调用方不会收到视觉顺序字符串。
- 语言切换时 `TextLocalizing` 自动在 LTR 与 RTL 之间切换，业务页面不需要重复接入 shaping。
- 页面镜像仍由业务按具体交互设计决定，避免框架擅自改变布局。

### 代价与限制

- 项目必须提供覆盖目标字符的 TMP FontAsset；只有文本处理而没有字体仍会显示缺字方框。
- shaping 会在 TMP 生成网格前增加一次字符串处理。
- 输入框仍需单独验证光标、选区、IME 和复制粘贴语义。

## 被排除方案（Rejected Alternatives）

- **导出阶段倒序或转换字形**：会污染数据契约，并让日志、格式化和非 UI 消费方拿到视觉字符串。
- **让翻译或业务代码手工倒序**：重复、易错，且英文数字混排与富文本无法稳定维护。
- **只设置 TMP `isRightToLeftText`**：只能改变排版推进方向，不能完整解决 Arabic/Persian 字母连接和混排。
- **框架自动镜像整页布局**：按钮、箭头和布局顺序取决于具体产品交互，超出 Localization 文本显示职责。
- **替换为专用 TMP 子类**：会要求改造现有 Prefab 组件类型；使用 `ITextPreprocessor` 可以保持 `TextMeshProUGUI + TextLocalizing` 契约。

## 验证依据（Verification）

- Unity 6000.4.2f1 编译通过，Console 无 C# Error。
- EditMode 定向测试 `ArabicLocalizationTextTests` 与 `LocalizationLanguageResolverTests` 合计通过 `17/17`。
- 用例覆盖 RTL 语言识别、LTR 原文保持、Arabic 字形连接、英文数字混排、TMP Rich Text 标签、RTL 到 LTR 回切和已有预处理器串联。
- 异步语言切换先校验请求版本再提交私有文本缓存；字体与材质加载也校验刷新版本和目标语言。

## 当前事实来源（Sources）

- `Assets/Framework/Scripts/Runtime/Core/Definitions/Language.cs`
- `Assets/Framework/Scripts/Runtime/Modules/Localization/TextLocalizing/`
- `Assets/Framework/Scripts/Runtime/Modules/Localization/TextShaping/`
- `Assets/Tests/Editor/ArabicLocalizationTextTests.cs`
- `Assets/Framework/Docs/Runtime/Modules/Localization/TextLocalizing.md`
