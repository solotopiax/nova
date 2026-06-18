---
id: PAT-130
title: UPM Sample 只读 Prefab 路径靠 scene override + import 重写（修订版）
summary: 只读 prefab 路径走 scene override
category: workflow
type: pattern
status: active
date: 2026-05-31
aliases:
  - PAT-130-sample-readonly-prefab-path-override-revised
keywords:
  - PAT-130
  - UPM Sample 只读 Prefab 路径靠 scene override + import 重写（修订版）
  - PAT-130-sample-readonly-prefab-path-override-revised
tags: [pattern, upm, sample, prefab, publish]
supersedes:
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
related:
  - "[[PAT-121-publish-sample-rewrite-symmetric|PAT-121]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[ADR-048-nova-prefab-follow-framework|ADR-048]]"
---

# PAT-130：UPM Sample 只读 Prefab 路径靠 scene override + import 重写（修订版）

> 本稿修订并取代 [[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]。PAT-63 的 scene override 内核仍成立，但其「项目根 Docs/ 搬运三段链路」与反模式 1 已随「Excel 源数据迁入各 Demo 副本」（见对应 ADR）失效，必须修订。入库时旧 PAT-63 走 superseded。

## 适用场景（When）

UPM 包内 prefab 含**指向 sample 内资源副本的路径字段**（如 `SourceDirPath` / `ProtoSourceDirPath`），且该 prefab 在外部工程会落到 `Packages/<pkg>/...` 只读区。需要让 import sample 后，prefab 上的路径解析到外部工程里 sample 的真实落地路径。

典型字段：各 Component 的 `SourceDirPath` / `ProtoSourceDirPath` / `EmphasisSourceDirPath` / `CustomSourceDirPath` / `TextSourceDirPath` / `FontSourceDirPath` 等编辑期工具链消费的源目录路径。

## 核心做法（What & How）

资源副本已与 sample 同目录（如 `Assets/Samples/<XXXX>Demo/Excels/`），无需任何「搬运」。路径靠一条单链落地：

1. **prefab 默认值指主 Demo 副本**：开发工程内 prefab 字段默认值 = `Assets/Samples/MainDemo/Excels|Protos/...`。MainDemo 场景的 PrefabInstance 不加路径 override，靠默认值生效——开发期打开 MainDemo 即可正常用工具链（dev 期导出读「当前场景 Nova 实例值」，链路 B）。
2. **非主 Demo 用 scene override**：其他 Demo（如 LoginDemo）的 scene PrefabInstance.m_Modifications 注入路径字段，指向自己的副本。scene 在 sample 内可写，prefab 本体只读不动。
3. **发版期自动扫描 + 注入 override**：扫 prefab 收集 `(fileID, propertyPath, value)` 三元组（`endswith("SourceDirPath")` 覆盖全部变体，不硬编码字段清单），把值前缀从 `Assets/Samples/MainDemo` 按当前 sample 名重写为 `Assets/Samples/<sampleName>`，幂等去重后追加进 sample scene。主/子包走同一注入函数（遵循 [[PAT-121-publish-sample-rewrite-symmetric|PAT-121]]）。
4. **import 期 SamplePathRewriter 二次重写**：sample scene 此刻是开发工程逻辑路径 `Assets/Samples/<sampleName>/...`，由 `SamplePathRewriter` 在 `[InitializeOnLoad]` 把前缀替换为外部工程真实 sample 根。

收敛为一条生命周期链：**prefab 默认值(MainDemo) → 发版注入 scene override(按 sample 前缀) → import 重写真实路径**。资源副本随 sample 整目录拷贝进包（`_copy_sample_to_pkg`），不再有独立的 Docs 搬运步骤。

具体到 Nova：发布脚本中的 `SAMPLE_SOURCE_PATH_MAPPINGS` + `_collect_source_path_overrides_from_prefab()` + `_inject_overrides_into_sample_scene()`（含幂等去重）；`Assets/Samples/MainDemo/Scripts/Editor/SamplePathRewriter.cs`。

## 为什么这么做（Why）

- **prefab 在 Packages/ 只读**：外部工程唯一可写入口是 sample 内 scene 文件，override 是 Unity 原生覆盖语义。
- **资源副本与 Demo 同目录**：各 Demo 自闭包（ADR-033），不再依赖项目根中性目录，省掉一整套搬运 + 扁平化映射。
- **prefab 默认值指 MainDemo 而非中性路径**：因为 Excel 源数据已物理迁入 `Assets/Samples/MainDemo/Excels`，字面量与物理位置吻合，开发期打开 MainDemo 即可直接导出，无需 override。这与旧 PAT-63 的「保留项目根中性路径」相反——见反模式修订。
- **扫描而非硬编码**：framework 新增 `*SourceDirPath` 字段无需改 Python。

## 反模式（Anti-patterns）

1. **删除 override 注入、只靠 prefab 默认值**：SamplePathRewriter 碰不到 `Packages/` 下只读的 prefab，prefab 默认值前缀无人重写，import 后路径错位、工具链找不到源文件。override 注入是单链的必要环节，不可省。
2. **rewriter 改 `Packages/<pkg>/Prefabs/Nova.prefab`**：Packages 下只读，写入被 Unity 拒绝；即便绕过也污染上游包。
3. **硬编码 (fileID, propertyPath, value) 清单**：每次 prefab 增删字段都要同步 Python，迟早失守。
4. **资源副本进 Assets 却被 BundleCollector 收录**：Excels/Protos 不应进 `BundleCollectorSetting` 的 CollectPaths，否则 .xlsx 被误打进 AB。

> **PAT-63 原反模式 1（已推翻）**：旧 PAT-63 称「改 prefab 字面量为 `Assets/Samples/MainDemo/...` 是反模式」。在资源副本迁入各 Demo 后，prefab 字面量改指 Demo 副本路径反而是**正确做法**（字面量与物理位置吻合，开发期直接可用）。该禁令在「源数据仍在项目根 Docs/」的旧前提下成立，迁移后不再适用。

## 跨项目复用提示

适用于任何「UPM 包 prefab 引用 sample 内资源、需让 sample 自洽」的场景。关键：
- 资源副本与 sample 同目录，prefab 默认值直接指主 sample 副本
- scene override 是唯一可写入口，非主 sample 靠 override 区分
- override 值用 sample 内逻辑路径，import 期 rewriter 二次替换
- 自动扫描 prefab 字段 > 硬编码清单

