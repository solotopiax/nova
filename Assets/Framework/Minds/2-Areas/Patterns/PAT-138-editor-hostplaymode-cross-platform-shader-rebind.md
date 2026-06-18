---
id: PAT-138
title: Editor HostPlayMode 跨平台真实包 Shader 重绑排障
type: pattern
summary: 真实包洋红块先验shader
category: asset
status: active
date: 2026-06-08
aliases:
  - PAT-138-editor-hostplaymode-cross-platform-shader-rebind
keywords:
  - PAT-138
  - Editor HostPlayMode 跨平台真实包 Shader 重绑排障
  - HostPlayMode
  - TMP Dynamic
  - TextMeshPro
  - Shader.Find
  - YooAsset
tags: [pattern, nova, asset, yooasset, tmp, debugging]
related:
  - "[[ADR-014-playmode-split-editor-runtime|ADR-014]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
  - "[[PAT-70-yooasset-packrule-selection|PAT-70]]"
  - "[[PAT-87-button-text-visibility-check|PAT-87]]"
---

# PAT-138：Editor HostPlayMode 跨平台真实包 Shader 重绑排障

## 适用场景（When）

- Unity Editor 中把 `AssetComponent.EditorPlayMode` 手动切到 `HostPlayMode` / `OfflinePlayMode` / `WebPlayMode`，用真实 AssetBundle 预览。
- 同一界面在 `EditorSimulateMode` 正常，但真实包模式下 TMP 文字或普通材质显示为洋红色块 / 大色块。
- 正在排查 TMP Dynamic 字体、YooAsset 真实包、内置 shader bundle、平台包之间的交叉问题。

## 核心结论（What）

这类现象要优先验证 **bundle 内 shader 对象是否适配当前 Editor 渲染端**，不要先把问题归因到 TMP Dynamic 字体。

本次 MainDemo 复盘中的关键事实：

- `EditorSimulateMode` 正常，切到 `HostPlayMode` 后 TMP 文字变成洋红 / 大色块。
- HostPlayMode 实际加载的是 `Default 2026-06-08-1039` 真实包，包路径为 `Bundles/Android/Default/...` / `Assets/StreamingAssets/yooasset/Default/...`。
- 运行时 TMP 使用的是 bundle 内实例，`AssetDatabase.GetAssetPath(font)` 为空；项目原始 `TMP_FontAsset` 不是直接参与渲染的那份对象。
- bundle 内 `TMP_FontAsset.sourceFontFile` 存在，`font.TryAddCharacters(...)` 能动态生成新 glyph，说明 Dynamic 生成链路成立。
- TMP 材质 `_MainTex` 指向 font atlas，FaceColor、atlas、材质引用均正常。
- 执行 `material.shader = Shader.Find(material.shader.name)` 后，洋红色块立即恢复为正常文字，根因锁定为真实包 shader 引用在当前 Editor 渲染端不可用。

## 排查顺序（How）

1. **先确认运行模式和包版本**：读取 `AssetComponent.m_EditorPlayMode`、YooAsset `PackageVersion`、已加载 bundle 列表，确认是否在 Editor 中加载真实包。
2. **区分原始资产和 bundle 实例**：对可见 `TextMeshProUGUI` 打印 `font.name`、`AssetDatabase.GetAssetPath(font)`、`sourceFontFile`、`atlasPopulationMode`、`characterTable.Count`、`glyphTable.Count`。
3. **验证 Dynamic 本身**：对 bundle 内 font 调 `TryAddCharacters`，检查 missing 字符、glyph 数变化、`HasCharacter` 前后变化。只要能加字，就不要继续把问题当成 Dynamic 失效。
4. **验证材质和 atlas**：检查 `fontSharedMaterial.shader.name`、`_MainTex`、`font.atlasTexture` 是否一致，以及 FaceColor / text color 是否合理。
5. **最小变量验证 shader**：只做一次同名 shader 重绑：

```csharp
var shader = Shader.Find(material.shader.name);
if (shader != null)
{
    material.shader = shader;
}
```

如果重绑后立即恢复，根因是 Editor 真实包预览的 shader 适配问题，不是字体资产、文字内容或 atlas。

## 修复边界（Fix Boundary）

- 修复应收敛在 Asset 模块加载出口，符合 [[PAT-37-no-yooasset-outside-asset-module|PAT-37]]：YooAsset 细节不扩散到 UI / Localization / 业务层。
- 只在 `UNITY_EDITOR` 且 `EditorPlayMode != EditorSimulateMode` 时执行同名 shader 重绑。
- Player 运行时不执行这段修复；真机应加载同平台 bundle，不应依赖 Editor 预览修正。
- 不要通过换字体、改 Static FontAsset、走静态资产来绕开问题。Dynamic 字体只要 `sourceFontFile` 存在且 `TryAddCharacters` 成功，就不是根因。

## 反模式（Anti-patterns）

- 看到 TMP 文字块状异常就直接要求换字体 / 改 Static，跳过 `sourceFontFile` 和 `TryAddCharacters` 取证。
- 只看 shader 名称存在就判定 shader 没问题；bundle 反序列化出的 shader 对象可能与当前 Editor 渲染端不匹配，同名重绑才是有效验证。
- 在 UI 或 Localization 层做 TMP 专用修复；这会把 AssetBundle 平台适配问题泄漏到上层模块。
- 把 Editor HostPlayMode 的真实包预览现象直接外推到真机；Android 设备加载 Android bundle 与 macOS Editor 加载 Android bundle 是不同条件。

## 关联

- PlayMode 拆分语义：[[ADR-014-playmode-split-editor-runtime|ADR-014]]
- YooAsset 边界：[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]
- PackRule 与字体资源组织：[[PAT-70-yooasset-packrule-selection|PAT-70]]
- TMP 可见性排查：[[PAT-87-button-text-visibility-check|PAT-87]]
