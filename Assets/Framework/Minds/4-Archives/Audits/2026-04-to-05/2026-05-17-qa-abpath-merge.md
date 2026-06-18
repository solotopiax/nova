# QA 审计报告：ABPath/AssetName → AssetLocation 全模块合并改造

**日期**：2026-05-17  
**验证者**：qa-reviewer  
**测试脚本**：`Assets/Game/Test/Test.cs`（`AssetLocationMergeTest_20260517`）

---

## 总结

**[QA-PASS]** 三步验证全部通过。41 个测试用例，PASS=41，FAIL=0。

---

## Step 1 — 编译验证

**[通过]**

- `read_console` filter type=error → 0 条 CS 编译错误
- `read_console` filter type=warning + filter_text=AssetLocation → 0 条相关 warning
- 唯一 error 条目："type is not a supported pptr value"，来自 Unity 内部 `/Runtime/Export/Scripting/StackTrace.cs:35`，为 UnityMCP 工具调用产生的内部日志，与业务代码无关
- 编译状态：`isCompiling=false`，`isUpdating=false`

---

## Step 2 — Inspector 与序列化

**[通过]**

逐组件序列化字段检查结果：

| 组件 / 类型 | AssetLocation 新字段 | 旧 ABPath/AssetName 残留 |
|------------|---------------------|------------------------|
| ConfigComponent | `m_AssetLocation` 存在（String，value=''） | 无 |
| SoundUnitSetting | `AssetLocation` 存在 | 无 |
| UIUnitSetting | `AssetLocation` 存在 | 无 |
| HostKeyUnitSetting | `AssetLocation` 存在 | 无 |
| NetCmdUnitSetting | `AssetLocation` 存在 | 无 |
| VibrateUnitSetting | `AssetLocation` 存在 | 无 |
| TableUnitSetting | `AssetLocation` 存在 | 无 |
| LocalizationSettings | `TextUnitsSettings` / `FontUnitsSettings` 存在 | 无 |
| TableSettings | `TableUnitsSettings` 存在 | 无 |

TableComponent 字段名为 `m_Setting`（非 `m_TableSettings`），已通过反射确认正确。

---

## Step 3 — Play Mode 行为验证

**[通过]**

### 测试用例文件

`Assets/Game/Test/Test.cs`（类名 `AssetLocationMergeTest_20260517`，namespace `Game.Runtime`）

### 执行结果

| TC | 测试用例描述 | 结果 |
|----|------------|------|
| TC-01 | ConfigComponent `m_AssetLocation` 存在，旧字段已删除（3 个断言） | [PASS] ×3 |
| TC-02 | `ConfigComponent.AssetLocation` internal 属性可读（3 个断言） | [PASS] ×3 |
| TC-03 | `SoundComponent.PlaySound` 新签名存在，旧签名已删除（2 个断言） | [PASS] ×2 |
| TC-04 | `UIComponent.OpenUIViewSync` 4 个 assetLocation 重载，旧三参签名删除（2 个断言） | [PASS] ×2 |
| TC-05 | `UIComponent.OpenUIViewAsync` 4 个 assetLocation 重载，旧三参签名删除（2 个断言） | [PASS] ×2 |
| TC-06 | `UIComponent.GetUIView(string assetLocation)` 存在，旧双参删除（2 个断言） | [PASS] ×2 |
| TC-07 | `UIComponent.GetUIViews(string assetLocation)` 存在，旧双参删除（2 个断言） | [PASS] ×2 |
| TC-08 | `UIComponent.HasUIView(string assetLocation)` 存在，旧双参删除（2 个断言） | [PASS] ×2 |
| TC-09 | `UIComponent.IsLoadingUIView(string assetLocation)` 存在，旧双参删除（2 个断言） | [PASS] ×2 |
| TC-10 | `SoundUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-11 | `UIUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-12 | `HostKeyUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-13 | `NetCmdUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-14 | `VibrateUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-15 | `TableUnitSetting` AssetLocation 存在，ABPath/AssetName 已删（3 个断言） | [PASS] ×3 |
| TC-16 | `LocalizationSettings` TextUnitsSettings / FontUnitsSettings 存在（2 个断言） | [PASS] ×2 |
| TC-17 | `TableSettings` TableUnitsSettings 存在（1 个断言） | [PASS] ×1 |

**汇总：PASS=41，FAIL=0**

---

## 缺陷清单

无 P0～P4 缺陷。

---

## 战场清场确认

- Play Mode 内测试 GO `AssetLocationMergeTest_20260517` 已在 `Destroy(gameObject)` 自毁
- Play Mode 退出后 Unity 场景还原，Edit Mode 残留 GO 已通过 `DestroyImmediate` 手动清除
- 无新建 Prefab / Asset
- Console 无残留 [FAIL] 日志
- Test.cs 已覆盖为 `AssetLocationMergeTest_20260517`，旧 `LocalizationResolvePlayModeTest_20260516` 内容已替换

---

## 结论

[QA-PASS] 运行时验证通过，可交付 doc-writer。
