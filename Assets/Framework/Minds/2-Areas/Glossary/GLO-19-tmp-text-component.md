---
id: GLO-19
title: TMP_Text 文本组件
summary: TMP_Text是依赖字体材质链的文本基类
category: module
status: active
date: 2026-08-06
aliases:
  - GLO-19-tmp-text-component
  - TMP_Text
keywords:
  - GLO-19
  - TMP_Text
  - TextMeshPro
  - TMP字体
  - 字体材质
tags: [glossary, nova, unity, ui, tmp]
related:
  - "[[PAT-138-editor-hostplaymode-cross-platform-shader-rebind|PAT-138]]"
---

# GLO-19：TMP_Text 文本组件

## 定义

`TMP_Text` 是 TextMeshPro 文本组件的公共基类，统一承载文本内容、字体资产、材质、排版和渲染属性；常见具体实现包括 UI Canvas 下的 `TextMeshProUGUI` 与 3D 场景文本。

## Nova 边界

- 业务代码面向项目现有的文本组件或 UI 封装，不因只需要设置字符串就绕过既有 UI 层直接查找场景对象。
- TMP 字体资产不仅包含字符数据，还关联材质、Atlas 与 Shader；跨平台 Bundle 或 HostPlayMode 异常时应沿完整依赖链排查。
- TMP Default Font Asset 属于 TMP Settings 的启动基础依赖时，不应同时被错误地当作普通远端 Collector 资源。
- 动态字体、Fallback 字体和多语言字符覆盖是资源与本地化共同约束，不能只看 `TMP_Text.text` 是否赋值成功。

## 易混淆项

- `TMP_Text` 是基类，不等同于具体的 `TextMeshProUGUI` 组件。
- 文本显示为空、方块或粉色不一定是字符串问题，也可能是字体字符、材质或 Shader 平台不匹配。
- Editor 中能显示不证明 HostPlayMode 或目标平台 Bundle 中依赖完整。

## 示例

HostPlayMode 下 TMP 文字材质异常时，先确认实际加载的包版本、字体 Asset、材质与 Shader Bundle，再判断是否需要资源加载出口的跨平台重绑定。

## 来源

- [[PAT-138-editor-hostplaymode-cross-platform-shader-rebind|PAT-138]]：HostPlayMode 跨平台 Shader 重绑定排障模式。

---
