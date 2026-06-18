---
id: ADR-005
title: HybridCLR 程序集与命名空间唯一写入路径
status: accepted
date: 2026-05-14
summary: HybridCLR命名空间单写入路径同步
category: hotfix
aliases:
  - ADR-005
keywords: [ADR-005, HybridCLR 程序集与命名空间唯一写入路径]
tags: [adr, nova, hybridclr, hotupdate, assembly]
supersedes: []
superseded-by: []
related:
  - "[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]"
  - "[[ADR-007-procedure-tier-split|ADR-007]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]"
  - "[[GLO-01-novabehaviour]]"
---

# ADR-005：HybridCLR 程序集与命名空间唯一写入路径

## 背景（Context）

Nova × HybridCLR 改造引入双层程序集：

- **AOT 层**：`NovaFramework.Runtime`（IL2CPP 提前编译，发包后不可改）
- **业务 DLL 层**：`Game.Runtime`（运行时 `Assembly.Load(bytes)` 热更）

历史问题：

- namespace 与 .asmdef name 不一致，反射加载时类型查找失败。
- `Assembly.Load` / `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly` 散落在多处调用，热更失败时难以定位。
- 业务命名空间（如 `Game.Runtime`）配置在多份配置 SO 中，手动同步易错，编辑器/导出/运行时三方读到不同值。

## 决策（Decision）

锁定三条铁律，违反任一即编译期/审查期 REJECT：

### 1. namespace == asmdef name（强一致）

- AOT 层程序集 `NovaFramework.Runtime`，命名空间也必须是 `NovaFramework.Runtime`
- 业务 DLL 层程序集 `Game.Runtime`（示例），命名空间必须是 `Game.Runtime`
- **禁止**跨程序集共用 namespace

### 2. Namespace 唯一写入路径

`ConfigRuntimeSO.Namespace` 字段（原 `ProcedureLoadDllSettings.m_BusinessAssemblyName`）的写入路径**唯一**：

```
ConfigWindow（用户改 Namespace）
   └→ 写入 ConfigMasterSO.Namespace（编辑器主配置）
        └→ 导出流程 → 写入 ConfigRuntimeSO.Namespace（运行时配置）
              └→ 运行时读取（Util.HybridCLR.LoadGameAssemblyAsync）
```

禁止：

- 任何代码手动 `configRuntimeSO.Namespace = "..."` 赋值
- 业务层硬编码字符串字面量绕过 ConfigRuntimeSO
- 通过反射或 AssetDatabase 直接修改 SO 文件


> **ADR-058 扩展注**（2026-06-02）：本决策描述的写入路径在 ADR-058 per-panel 维度化后有**部分扩展**：Namespace 仍只能通过 ConfigWindow 写入，但写入目标从顶层单字段 `ConfigMasterSO.Namespace` 扩展为"顶层字段（全局默认值）+ `NamespaceOverrides` 列表（维度差异值）"。两层合并后经 `DimensionalResolver.ResolveNamespace` 解析出最终值再写入 `ConfigRuntimeSO.Namespace`。原有禁止直接修改 `ConfigRuntimeSO.Namespace` 的约束不变。详见 [[ADR-058-per-panel-dimension-mask|ADR-058]]。

### 3. Assembly.Load / LoadMetadataForAOTAssembly 单一调用点

| API | 唯一调用点 |
|---|---|
| `System.Reflection.Assembly.Load(bytes)` | `Util.HybridCLR.LoadGameAssemblyAsync` |
| `HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly` | `Util.HybridCLR.LoadAotMetadataAsync` |

任何其他位置直接调用上述两 API 即视为违规。

### 4. HybridCLR 配套约束

- HomologousImageMode 默认 `SuperSet`，无明确理由禁止修改。
- 升级 HybridCLR 版本前必须同步 `ConfigRuntimeSO.AotMetadataDlls` 列表（通过 ConfigWindow → HybridCLR 配置面板编辑并导出）。
- 版本变更需同步 `link.xml` 保留规则。

## 后果（Consequences）

### 正面

- 反射加载类型时 `Type.GetType($"{namespace}.{type}, {assemblyName}")` 直接拼接成功，namespace == asmdef 是反射定位的稳定前提。
- Namespace 单一写入路径使 ConfigWindow 成为唯一来源，减少三方配置不同步事故。
- `Util.HybridCLR.LoadGameAssemblyAsync` / `LoadAotMetadataAsync` 作为唯一通道，热更日志、超时处理、加密解密都集中维护。
- HybridCLR 版本升级有明确清单（AOT 元数据 DLL + link.xml），降低盲区。

### 负面

- 业务方修改 Namespace 必须经 ConfigWindow，没有"快速覆盖"通道（设计上是优点，使用上是负担）。
- `Util.HybridCLR` 工具类成为热更关键路径单点，重构需谨慎。
- AOT 元数据 DLL 列表维护在配置 SO 内，需要 HybridCLR 升级时编辑器手动同步。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 命名空间与 asmdef 解耦（按业务领域划分 namespace） | 反射定位类型时无法仅凭 asmdef name + 类型短名拼接，逐 type 配置查找成本大 |
| Namespace 多源写入（编辑器 SO + 运行时 SO 各自填写） | 三方同步靠人工，已在历史中出过事 |
| Assembly.Load 各 Procedure / Manager 自由调用 | 重复实现解密 / 超时 / 日志，难以审计 |
| 全部业务代码塞 AOT 层避开 HybridCLR | 失去热更能力，违背改造初衷 |

## 验证依据（Verification）

- 规范落点：程序集与命名空间一致性约束
- Grep 关键词：`Assembly.Load(`、`LoadMetadataForAOTAssembly(`、`ConfigRuntimeSO.Namespace`
- 唯一调用点验证：除 `Util.HybridCLR.cs` 外搜索上述两 API 应零命中
- 配置面板：`Assets/Framework/Scripts/Editor/ConfigWindow/HybridCLR/*`

## 关联

- 规范落点：HybridCLR / 程序集命名一致性约束
- 相关 ADR：[[ADR-006-novabehaviour-ibaselife-replace-monobehaviour|ADR-006]]（NovaBehaviour 替代业务 MB）、[[ADR-007-procedure-tier-split|ADR-007]]（Procedure 分档）、[[ADR-008-managerbase-internal-abstract|ADR-008]]（ManagerBase 修饰符）、[[ADR-009-uimanager-no-addcomponent-fallback|ADR-009]]（UIManager 取消兜底）
- 相关 Glossary：[[GLO-01-novabehaviour]]
