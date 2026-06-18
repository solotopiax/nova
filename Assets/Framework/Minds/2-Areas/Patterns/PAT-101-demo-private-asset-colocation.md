---
id: PAT-101
title: Demo 私有依赖与 view 同目录归位 + 命名脱敏
summary: 私有资产同目录归位+命名脱敏
category: naming
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-101-demo-private-asset-colocation
keywords:
  - PAT-101
  - Demo 私有依赖与 view 同目录归位 + 命名脱敏
  - PAT-101-demo-private-asset-colocation
tags:
  - pattern
  - naming
  - sample
  - demo
  - asset
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
  - "[[PAT-81-bundle-collector-address-rule|PAT-81]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
---

# PAT-101：Demo 私有依赖与 view 同目录归位 + 命名脱敏

## 适用场景（When）

- Sample/Demo 工程中某个演示 prefab、material、自旋脚本等仅被**单一** `DemoXxxView` 引用
- 演示资产命名带有跨模块通用字眼（如 `UI`、`Asset`、`Event`、`Sound`）容易与对应模块语义混淆时
- BundleCollectorSetting 中存在仅为该单一资产设立的 collector 子目录时

## 核心做法（What & How）

### 归位规则（colocation）

| 引用面 | 落点 |
|---|---|
| 仅 1 个 `DemoXxxView` 引用 | 与该 view prefab/script 同目录：`Prefabs/UIs/DemoXxxView/`、`Scripts/Runtime/UIs/DemoXxxView/` |
| ≥ 2 个 view 共享 | 落 `Prefabs/Demos/`（或同级共享目录），归专门的 collector |
| 全局通用资产（字体、Atlas） | 框架已有规范目录 |

### 命名脱敏规则

私有依赖资产的命名**必须**与所属 view 前缀对齐，**禁带**所属 view 之外的模块字眼。

| ✅ 推荐 | ❌ 反模式 | 说明 |
|---|---|---|
| `DemoPrefabBlock`（DemoPrefabView 私有） | `DemoUIBlock` | "UI" 是模块名，但该 prefab 不属于 UI 模块演示 |
| `DemoSoundBgmStub`（DemoSoundView 私有） | `DemoBGM` | 缺主体识别度，与全局 BGM 资产混淆 |
| `DemoAssetIconStub`（DemoAssetView 私有） | `DemoIcon` | 与全局 icon 资产混淆 |

### 移动 + 重命名 SOP

1. cs 文件：`mv <旧>.cs <新>.cs && mv <旧>.cs.meta <新>.cs.meta`（保 GUID，引用不丢）
2. prefab：`UnityMCP manage_asset move`，避免手搓 YAML
3. 类名重命名：`Edit replace_all`（namespace + 类名 + 文件头注释 + `m_EditorClassIdentifier`）
4. `m_PrefabLocation` 默认值同步：cs `[SerializeField] = "<新名>"` + view prefab YAML 字段值
5. 删除原 collector 条目（如 `Prefabs/Demos` 已空），同步 `BundleCollectorSetting.asset`
6. UnityMCP `refresh_unity` + `read_console` 验编译 0/0
7. grep 全仓 `<旧名>` 残留 == 0
8. 文档同步：`DESIGN.md` / `DEMO-INDEX.md` / 模块 L2 doc

## 为什么这么做（Why）

- **归属语义清晰**：与 view 同目录直接表达"这是 view 私有依赖"，新人不会误以为是共享资产
- **命名脱敏防混淆**：`DemoUIBlock` 出现在 Prefab 模块演示语境会让人以为属于 UI 模块演示
- **AssetLocation 自然对齐**：私有目录走 `Prefabs/UIs` collector 的 `AddressByFileName` 规则，地址即裸文件名，与代码 `m_PrefabLocation` 默认值天然对齐（PAT-81 已归档，保留该规则演化背景）
- **collector 条目清零**：私有依赖归位后空 collector 子目录可一并删除，减少 BundleCollectorSetting 维护成本

## 反模式（Anti-patterns）

- **私有 prefab 留在 `Prefabs/Demos/`**：归属模糊；多个 view 各放一份私有依赖到同目录会形成"伪共享"
- **命名直接带模块字眼**（`DemoUIBlock` / `DemoEventBus` / `DemoSoundBGM`）：与对应模块演示语义混淆
- **移动时只动 cs 不动 .meta**：GUID 改变 → 所有 prefab 引用变 Missing Reference
- **手搓 prefab YAML 移动**：违反「禁手搓 prefab YAML」铁律，需走 UnityMCP `manage_asset move`
- **不删 collector 空条目**：BundleCollectorSetting 残留无效路径，构建期 YooAsset 扫描浪费
- **直接保留 `m_EditorClassIdentifier` 旧类名**：Unity 下次保存时按 GUID 自修正，但 diff 噪音 + 易引发混淆

## 跨项目复用提示

- **完全可复用**到任何 Unity UPM Sample 或独立 demo 工程
- 其他类型 sample（如 SDK demo / Tool demo）私有依赖归位规则同样适用，只是路径前缀换成对应 view 目录

