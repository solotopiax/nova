---
id: PAT-40
title: 新增字段按所属定位插入既有顺序
type: pattern
status: active
date: 2026-05-19
summary: 新增字段按语义段插入联动文件同序
category: inspector
aliases:
  - PAT-40-field-insertion-by-semantic-grouping
keywords:
  - PAT-40
  - 新增字段按所属定位插入既有顺序
tags:
  - pattern
  - conventions
  - inspector
  - config
related:
  - "[[PAT-27-config-no-serialize|PAT-27]]"
  - "[[PAT-31-inspector-sop|PAT-31]]"
---

# PAT-40：新增字段按所属定位插入既有顺序

## 适用场景（When）

- 向既有 class（含 Component、ManagerConfig DTO、Inspector 三文件）新增字段时
- 字段已有明确语义簇（如"资源包配置"、"服务器分发"、"热更开关"等）
- 需要保持 Runtime 字段定义 / DTO 透传 / Inspector 声明绑定绘制 多文件联动一致

## 核心做法（What & How）

1. **先识别语义段**：把现有字段按语义聚类（例如 AssetComponent 划分六段：Manager 选择 / 加载模式 / 资源包配置 / 热更开关与下载行为 / 服务器分发与解密 / 运行时实例）。
2. **新增字段按归属插入**：而不是简单追加到文件末尾。例如 `m_Packages` 应进入"资源包配置"段，`m_HostServerTemplate` / `m_FallbackHostTemplate` / `m_DecryptorType` 应进入"服务器分发与解密"段。
3. **联动文件 1:1 同顺序**：Runtime 的 `Visitors.cs` 字段定义、Config DTO 公有字段、`Start()` 透传赋值顺序、Inspector 三文件（Visitors 声明 / OnEnable 绑定 / Methods 绘制）必须严格对齐，无错位。
4. **Foldout 命名跟随语义段**：例如服务器 Foldout 命名为"服务器分发与解密"，仅包含该段三项字段。
5. **核验手段**：六段顺序在所有联动文件中完全一致后，方可视为完成。

## 反模式（Anti-patterns）

- ❌ 把新字段直接 append 到 `Visitors.cs` 末尾，破坏既有语义簇连续性
- ❌ Runtime 排好序但 Inspector 三文件仍按旧顺序绑定/绘制，造成跨文件错位
- ❌ Foldout 既包含旧字段又包含新追加字段，但语义已不匹配 Foldout 标题
- ❌ 仅改字段位置不同步 DTO 透传赋值顺序，留下隐性可读性债

## 为什么这么做（Why）

- **可读性即维护性**：语义聚类的字段顺序让读者一眼看到模块边界；散乱排列等于把每次定位都摊到 reader
- **跨文件错位是隐性 bug 温床**：Inspector 绘制顺序与 Runtime 字段顺序不一致时，调试会反复对照"这个字段在 Inspector 第几个 / Runtime 第几行"，认知负担放大
- **Foldout 是语义合同**：Foldout 标题承诺了内含字段的范畴，加塞外段字段等于破坏合同

## 跨项目复用提示

- 任何"Runtime 定义 + DTO 透传 + UI 绘制"三文件联动的架构都适用
- 抽象原则：**字段顺序是数据结构的"非显式 schema"**，新增字段视同 schema 变更，需同步更新所有"消费此 schema 顺序"的文件

## 关联

- 触发案例：2026-05-19 AssetComponent 六段语义重排（`m_Packages` / `m_HostServerTemplate` / `m_FallbackHostTemplate` / `m_DecryptorType` 按段归位），核验六文件（AssetComponent.Visitors.cs / AssetManagerConfig.cs / AssetComponent.cs Start() / Inspector 三文件）顺序完全一致
- 相关 Pattern：[[PAT-27-config-no-serialize|PAT-27]]（ManagerConfig 散字段透传，本 Pattern 是其顺序约定）、[[PAT-31-inspector-sop|PAT-31]]（Inspector SOP，本 Pattern 是其字段插入细则）
