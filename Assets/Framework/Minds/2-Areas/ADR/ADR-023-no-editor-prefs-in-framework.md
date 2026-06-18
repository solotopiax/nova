---
id: ADR-023
title: 框架范围禁用 EditorPrefs（状态恢复走项目内资产）
status: accepted
date: 2026-05-19
summary: 框架范围禁用EditorPrefs改走项目资产
category: editor
aliases:
  - ADR-023-no-editor-prefs-in-framework
keywords: [ADR-023, ADR-023-no-editor-prefs-in-framework, 框架范围禁用 EditorPrefs（状态恢复走项目内资产）]
tags:
  - nova
  - editor
  - pipify
  - architecture
  - persistence
  - upm
related:
  - "[[PAT-30-framework-usage-redlines|PAT-30]]"
  - "[[PAT-34-minds-scope-nova-only|PAT-34]]"
  - "[[PAT-35-editor-draw-only|PAT-35]]"
---

# ADR-023：框架范围禁用 EditorPrefs（状态恢复走项目内资产）

## 背景

PipifyWindow 此前用 `EditorPrefs` 持久化绑定 GUID，导致新用户空白、跨项目串味和无法 git 追踪。

## 决策

**框架（NovaFramework.*）范围内不允许出现任何对 `UnityEditor.EditorPrefs` 的读写。**

替代方式：用 `AssetDatabase.FindAssets` 扫项目内资产自动绑定；需要持久化的配置落到 ScriptableObject 资产；业务侧自决不约束。

落地实现：`PipifyWindow.OnEnable → TryAutoBindSettings()`，删除 `RestoreSettingsFromPrefs / SaveSettingsToPrefs / c_SettingsGUIDKey`。

## 替代方案

- 保留 EditorPrefs 但加扫描兜底：仍会串味。
- 用 SessionState：重启即丢。
- 写 ProjectSettings json：与 SO 体系重复。
- `AssetDatabase.FindAssets` 扫资产：采纳。

## 后果

- 全框架编辑器代码需审视 `EditorPrefs` 引用，逐步替换为资产/SO 方案。
- 多份 PipifySettingsSO 时只自动绑首个，需在窗口顶部「编辑器存档文件」选择框手动切换 —— 已加 Warning 日志提示。
- PipifyWindow.md §5·1「存档绑定恢复」段需重写并标注铁律。

---

## 落实模式（Pattern 体例）

### 适用场景（When）

- 在 `Assets/Framework/Scripts/` 下编写任何 Editor 工具（EditorWindow / Inspector / 导出器 / 自定义抽屉）。
- 工具需要"上次选中 / 上次绑定 / 上次打开 / 折叠态记忆 / 滚动位置"等跨会话恢复的状态。
- 框架以 UPM 包形式分发到不同项目，新用户首次安装、换机器、换项目都属覆盖范围。

**触发信号：** 你正打算敲下 `EditorPrefs.SetString` / `EditorPrefs.GetInt` / `EditorPrefs.HasKey` —— 立刻停手。

### 状态承载选型表

| 状态类别 | 推荐承载方式 |
|---|---|
| 工具默认绑定（"上次打开哪个 Settings"） | `AssetDatabase.FindAssets("t:XxxSettingsSO")` 扫项目内资产；零个 → 留空提示用户；一个 → 自动绑；多个 → 绑首个并 `Log.Warning` 让用户走顶栏选择 |
| 真要持久化的工具配置 | 落到项目内 `.asset`（`ScriptableObject`），随项目走、随包分发可控 |
| 仅当次会话有效的临时态（滚动位置、折叠集合、过滤词） | 留在 EditorWindow 实例字段即可，关窗即清不需要恢复 |

### 修正后参考实现（PipifyWindow）

```csharp
private void OnEnable()
{
    TryAutoBindSettings();
}

private void TryAutoBindSettings()
{
    string[] guids = AssetDatabase.FindAssets("t:" + nameof(PipifySettingsSO));
    if (guids == null || guids.Length == 0) return;
    if (guids.Length > 1)
    {
        Log.Warning(LogTag.Editor, "{0} 项目内存在 {1} 份 PipifySettingsSO 资产，自动绑定首个；如需切换请使用顶部「编辑器存档文件」选择框。", c_LogTag, guids.Length);
    }
    foreach (string guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) continue;
        PipifySettingsSO loaded = EditorUtil.Asset.Operator.LoadAt<PipifySettingsSO>(path);
        if (loaded == null) continue;
        m_Settings = loaded;
        m_SettingsSO = new SerializedObject(loaded);
        m_IsDirty = false;
        return;
    }
}
```

### 反模式（Anti-patterns）

- **EditorPrefs 写资产 GUID 当默认绑定**：`EditorPrefs.SetString("Tool.LastSettingsGUID", guid)` —— 换项目即失效；扫项目资产才是 UPM 友好的方案。本次 PipifyWindow 修复就是这个反例。
- **EditorPrefs 写工具配置**："Tool.AutoExport"、"Tool.OutputDir" 写 EditorPrefs —— 应改为项目内 SO 资产，配置随项目走，团队成员/CI 共享同一份。
- **作用域越界**：本铁律**仅约束** `NovaFramework.*` 程序集；业务 DLL 层（`Game.*` 等）的 Editor 工具自己看着办，不强行管，避免框架越权。

### 跨项目复用提示

- 这条铁律是 **Unity Editor 工具 + UPM 分发**场景下的强约束，搬到非 Unity 项目不直接适用。
- 类比扩展：任何 IDE 插件 / 编辑器扩展工具的"全局偏好 vs 项目状态"分离原则都成立。跨项目共享的状态走 global，项目内状态走 workspace/资产。

### 深层成因（Why）

- EditorPrefs 是 user-level 持久化，不分项目。
- 跨项目串味会把工具状态读空。
- UPM 包随项目走，但 EditorPrefs 不随项目走。
- 框架不该污染用户机器全局状态。
