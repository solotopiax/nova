---
id: PAT-81
title: BundleCollector 寻址规则与 AssetLocation 前缀对齐
summary: 已作废 — MainDemo 回退至 AddressByFileName，自定义 AddressByCollectorRelativePath 类已删除
category: asset
type: pattern
status: superseded
archived_date: 2026-05-28
archived_reason: AddressByCollectorRelativePath.cs 已从 Assets/Samples/MainDemo/Scripts/Editor/ 删除；现行 BundleCollectorSetting 全部使用 AddressByFileName，AssetLocation 也回退为裸文件名
date: 2026-05-24
aliases:
  - PAT-81-bundle-collector-address-by-collector-relative-path
keywords:
  - PAT-81
  - BundleCollector 寻址规则与 AssetLocation 前缀对齐
  - PAT-81-bundle-collector-address-by-collector-relative-path
tags:
  - pattern
  - asset
  - bundle
  - collector
  - address
  - yooasset
  - sample
  - demo
related:
  - "[[PAT-77-base-demo-view-three-zone-template|PAT-77]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[GLO-07-asset-location|GLO-07]]"
  - Asset 地址中文统一术语
superseded-by: []
---

# PAT-81：BundleCollector 寻址规则与 AssetLocation 前缀对齐

> ⚠️ **已作废（2026-05-28）**：MainDemo 已不再使用 `AddressByCollectorRelativePath` 自定义 AddressRule，对应实现脚本 `AddressByCollectorRelativePath.cs` 也已从 `Assets/Samples/MainDemo/Scripts/Editor/` 删除。
>
> 当前事实：
> - `BundleCollectorSetting.asset` 中所有 Collector 的 `AddressRuleName` 统一为 `AddressByFileName`
> - `Jsons/UIs.json` / `Sounds.json` 中 `AssetLocation` 字段为裸文件名（如 `DemoNavTreeView` / `bgm_main`），不含目录前缀
> - 同名冲突防御改由 Sample 内部命名约定承担：业务 UI 一律 `DemoXxxView` 后缀（PAT-64），Sound 资源加 `bgm_/sfx_` 前缀，事实层面避免裸文件名碰撞
>
> 本文档保留作历史归档，禁止照此实施；新建 Sample 请直接使用默认的 `AddressByFileName`。

---

## 适用场景（When）

- 为 MainDemo 或任何 Sample 工程配置 BundleCollectorSetting
- 多个 Collector 下可能存在同名文件（如不同模块下各有一个 `DemoXxxView.prefab`）
- AssetLocation 字符串需要与 BundleCollector 生成地址完全匹配时

## 核心做法（What & How）

### AddressByCollectorRelativePath 规则

**地址格式：`{CollectPath 末段目录名}/{文件名（无扩展）}`**

```text
CollectPath = Assets/Samples/MainDemo/Prefabs/UIs
AssetPath   = Assets/Samples/MainDemo/Prefabs/UIs/DemoExtensionsView/DemoExtensionsView.prefab
输出地址     = UIs/DemoExtensionsView
```

对比 `AddressByFileName`（默认规则）：

| 规则 | 生成地址 | 问题 |
|---|---|---|
| AddressByFileName | `DemoExtensionsView` | 多个 Collector 下同名文件地址冲突 |
| AddressByCollectorRelativePath | `UIs/DemoExtensionsView` | 加了目录名前缀，全局唯一 |

### 自定义规则实现模板（IAddressRule）

```csharp
using System.IO;
using YooAsset.Editor;

[DisplayName("定位地址: 收集目录名/文件名")]
public class AddressByCollectorRelativePath : IAddressRule
{
    string IAddressRule.GetAssetAddress(AddressRuleData data)
    {
        // 取 CollectPath 最后一段目录名，作为地址前缀（命名空间）
        string collectDirName = Path.GetFileName(data.CollectPath);
        // 取文件名，去掉扩展名
        string fileName = Path.GetFileNameWithoutExtension(data.AssetPath);
        return $"{collectDirName}/{fileName}";
    }
}
```

文件须放在 `Scripts/Editor/` 下（Editor 程序集），命名空间可自定。

### BundleCollectorSetting 配置要点

| Collector | AddressRuleName | 生成地址示例 |
|---|---|---|
| Prefabs/UIs | AddressByCollectorRelativePath | `UIs/DemoNavTreeView` |
| Sounds | AddressByCollectorRelativePath | `Sounds/BGM_Main` |
| 不跨 Collector 同名的集合 | AddressByFileName（默认） | `BGM_Main` |

### AssetLocation 与 address 必须对齐（铁律）

- UIs.json 导出的 `AssetLocation` 字段（如 `UIs/DemoNavTreeView`）**必须**与 BundleCollector 实际生成的地址完全一致
- 改变 AddressRuleName 后必须同步更新所有 AssetLocation 引用（UIs.json 行、DemoNavTreeView.cs 等硬编码地址）
- **禁止**直接使用 `AddressByFileName` 生成的裸文件名作为地址，除非该 Collector 下无同名文件冲突

### 已覆盖的 Demo 配置（R7）

| 变更 | 旧值 | 新值 |
|---|---|---|
| Prefabs/UIs 的 AddressRuleName | AddressByFileName | AddressByCollectorRelativePath |
| Sounds 的 AddressRuleName | AddressByFileName | AddressByCollectorRelativePath |
| DemoNavTreeView 的 AssetLocation | `DemoNavTreeView` | `UIs/DemoNavTreeView` |

## 为什么这么做（Why）

- **地址冲突防御**：不同 Collector 下同名文件（如 `Button.prefab` / `Config.json`）在 `AddressByFileName` 规则下地址相同，后者覆盖前者，产生运行时加载错误
- **AssetLocation 语义明确**：带目录前缀的地址（`UIs/DemoNavTreeView`）比裸文件名更清楚地表达"来自哪个资源集合"
- **与 Luban 导出对齐**：UIs.xlsx 的 AssetLocation 列格式即 `{CollectDirName}/{FileName}`，Luban 导出 UIs.json 后地址格式与 BundleCollector 地址天然一致，无需手工映射

## 反模式（Anti-patterns）

- **只改 AddressRuleName 不同步 AssetLocation**：运行时 `Nova.Asset.LoadAsync("DemoNavTreeView")` 找不到资源（地址已变为 `UIs/DemoNavTreeView`）
- **只改 AssetLocation 不改 AddressRuleName**：bundle 实际地址仍是裸文件名，字符串不匹配
- **在业务代码里硬编码裸文件名地址**：Collector 增减时地址格式可能变化，应统一从导出 JSON 读取

## 关联

- 资源地址术语：[[GLO-07-asset-location|GLO-07]]（Asset 地址中文统一称"Asset 地址"，禁"资源地址"）
- Demo 拓扑：[[ADR-033-maindemo-isolated-topology|ADR-033]]（sample 独立闭包，各自管理自己的 BundleCollectorSetting）
- 模板总述：[[PAT-77-base-demo-view-three-zone-template|PAT-77]]（PAT-81 是 PAT-77 Prefab 制作约束的 Asset 地址细节）
