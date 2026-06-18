---
id: PAT-DRAFT
title: Unity 包/asmdef 改名的硬编码串影响排查
summary: Unity 包与 asmdef 改名时优先排查硬编码字符串断裂点
status: archived
date: 2026-06-04
archived-date: 2026-06-08
type: pattern
category: governance
aliases:
  - PAT-DRAFT-2026-06-04-unity-asmdef-rename-hardcoded-scan
tags:
  - pattern
  - archive
  - package
  - asmdef
  - hybridclr
---

# PAT-DRAFT：Unity 包/asmdef 改名的硬编码串影响排查

## 归档说明

- 保留为历史草稿，供后续若需转正式 PAT 时复用结论。
- 已移除会话与 hook 残留，仅保留风险排查方法。

## 适用场景
重命名一个 Unity 包（目录名 / package.json name / asmdef name+rootNamespace / C# namespace）时使用。判断哪些引用会自动存活、哪些会静默断裂，必须在动手前把硬编码字符串的影响面摸清。

## 核心做法
- **物理改名优先用 `git mv`**：保住 .meta 的 GUID。依赖方 asmdef 之间是 **GUID 引用**，改名后 GUID 不变 → 引用方 asmdef 文件无需改。
- **GUID 安全，但硬编码命名空间/程序集字符串会断**，必须逐一核对：
  - SerializeReference 资产里的 `ns` / `asm` 字段（如 ConfigRuntime.asset），改 ns 会让字段变 `<Missing>`。
  - `ProjectSettings/HybridCLRSettings.asset` 的热更程序集名列表。
  - ConfigMaster 的 `EnabledKits` / `TypeName` 全名、DLL 的 AssetLocation 路径。
  - 热更字节文件 `*.dll.bytes` 的物理文件名（与程序集名绑定）。
  - `.proto` 的 `package` 声明：若无 `csharp_namespace` option，C# 命名空间从 `package` 推导——只改生成的 .cs 而不改 proto，下次 protoc 重生成会复原。改 package 对二进制 wire format 无影响（不含 message 全名），安全且必要。
- **替换规则要设边界防误伤**：保留类名（如 `Login` / `LoginKitConfig` / `Kit<Login>`），只替换命名空间/包名前缀，避免误改前缀碰巧相同的同族命名空间（如 Demo 自身 `...Login.Samples`）。
- **验证三件套**：编译 0 错 + Inspector 确认 SerializeReference 字段未变 `<Missing>` + Play Mode 关键 API 用例通过。

## 反模式
- 以为「asmdef 改名 = 全部引用要手改」→ 实际 GUID 引用自动存活，盲改反增风险。
- 只跑命名空间字符串替换脚本就收工，忽略 `.proto` / `*.dll.bytes` 文件名 / SO 资产字符串 → 编译过但运行时序列化断裂或 protoc 复原。
- hash 产物（如 BuiltinCatalog.json 的 bundle 名）手改 → 应由资源重新构建刷新，不手改。
- 改名提交时夹带工作区遗留的无关业务改动一起进 tag → 发版前必须核对会话起始 git status 与 diff 来源。
