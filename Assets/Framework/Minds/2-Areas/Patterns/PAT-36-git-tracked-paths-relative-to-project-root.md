---
id: PAT-36
title: 入 git 的路径配置字段强制项目根相对路径
type: pattern
status: active
date: 2026-05-18
summary: 入git路径字段强制项目根相对路径
category: core
aliases:
  - PAT-36-git-tracked-paths-relative-to-project-root
keywords:
  - PAT-36
  - 入 git 的路径配置字段强制项目根相对路径
tags:
  - pattern
  - methodology
  - path
  - config
  - portability
  - unity
related:
  - "[[PAT-27-config-no-serialize|PAT-27]]"
---

# PAT-36：入 git 的路径配置字段强制项目根相对路径

## 适用场景（When）

任何会被 **git 版本管理** 的"目录/路径/位置"配置字段，都适用本规范，包括但不限于：

- Unity Inspector 上的 `[SerializeField]` 路径字段（`string`、`UnityEditor.DefaultAsset` 序列化路径等）
- ScriptableObject 资产里的路径字段（如 `ConfigMasterSO`、`ConfigRuntimeSO` 中的源/目标目录、AssetLocation）
- JSON / YAML / asmdef 等以文本形式入库的配置中的目录/文件路径
- Pipify Step 参数、构建管线参数（拷贝源、拷贝目标、导出目标）
- 各类导出器、生成器、拷贝器在配置侧填的目录字段

**触发信号：**

- 我要新增一个"目录"或"位置"字段，且这个字段会随资产/代码进入 commit
- 同事反馈"我机器上跑不起来，路径找不到"
- 审查时看到 `[SerializeField] string m_XxxDir` 类字段需要重点确认
- 多人协作 / Mac × Win 混合 / CI 跑构建管线

## 核心做法（What & How）

### 默认规范

| 项 | 要求 |
|---|---|
| 字段存储格式 | **项目根（`SettingsUtil.ProjectDir` 即 Unity `Assets` 的上级目录）的相对路径** |
| 路径分隔符 | 统一正斜杠 `/`（跨平台兼容；落盘前由代码做归一化） |
| 是否含扩展名 | 按字段语义决定，但禁止"隐式追加后缀"——所见即所得 |
| 是否允许绝对路径 | **禁止入库**；运行/编辑期需要时由代码拼出 |

### 拼接惯例（编辑期/运行期需要绝对路径时）

```csharp
string absolute = Path.GetFullPath(
    Path.Combine(SettingsUtil.ProjectDir, ResolvePlaceholders(entry.RelativePath)));
```

- 拼接职责放代码侧，**不**让 Inspector 用户去填绝对路径
- `ProjectDir` 由 HybridCLR `SettingsUtil`（或等价的 Unity API）统一提供，单一来源
- 拼接前先做**占位符解析**（见下节），让一份字段同时支持多平台/多通道差异

### 路径占位符（编辑期相对路径里允许的变量）

| 占位符 | 替换值来源 | 解析时机 |
|---|---|---|
| `{ActiveBuildTarget}` | `EditorUserBuildSettings.activeBuildTarget.ToString()`（如 `Android` / `iOS` / `StandaloneOSX` / `StandaloneWindows64`） | 运行/编辑期需要拼绝对路径时（CopyDllEntries / 加载侧）由代码替换 |

**约束：**

- SO 字段里**直接存字面值**：例如 `HybridCLRData/AssembliesPostIl2CppStrip/{ActiveBuildTarget}`，**不**预先替换为 `Android` 后入库——预先替换就丧失了多平台共享的意义
- 解析点必须**统一封装**为单一函数（`ResolvePlaceholders` / 等价命名），所有读路径的入口都过同一个解析；禁止各处现场 `string.Replace`
- `AssetLocation`（YooAsset 逻辑 key）不做占位符替换；占位符仅作用于"路径"语义字段（`SourceLocation` / `TargetLocation` / 各种导出目录字段）
- HelpBox / Tooltip 必须给出占位符清单 + 解析说明（让填写人知道哪些字面词被代码识别）
- 未来扩展（如 `{Channel}` / `{ProjectName}`）必须在这张表里登记，禁止"代码偷偷支持但文档不写"的占位符

**为什么允许占位符：**

- **多平台一份配置：** 多端工程（Android / iOS / Mac / Win）的 HybridCLR AOT metadata 拷贝源目录通常以平台名作子目录，一份带占位符的 SourceLocation 可服务全平台，避免按平台维护 N 套 SO
- **字段语义清晰：** 字面 `Android` 看不出"是不是只在 Android 用"——填占位符即是"声明此字段平台敏感"
- **零序列化变化：** 字段类型仍是 `string`，git diff 仍是文本，不引入复杂类型/平台特化条件序列化

### 已建立范例

- `DllAssetEntry.AssetLocation`（运行期 YooAsset 加载地址 + 编辑期目标路径双语义）
- `EditorUtil.HybridCLR.CopyDllEntries`：源/目标全部基于 `projectRoot` + `entry.AssetLocation` 拼接
- `ConfigMasterSO` / `ConfigRuntimeSO` 路径字段族

### 落实手段

- 设计阶段：新建字段时直接按相对路径设计，**不再询问需求方**
- 实现阶段：Inspector 选择目录的 UI（`EditorUtility.OpenFolderPanel`）拿到绝对路径后，必须立即转相对路径再写入字段
- 审查阶段：code-reviewer 看到序列化字段写入绝对路径直接退回
- 文档同步：相关 L2 / `csharp-code-style.md` 在涉及路径字段时引用本 Pattern

## 为什么这么做（Why）

- **可移植性：** 多人协作 + 多机器（Mac/Win 盘符根目录差异 + 不同登录用户名），绝对路径一旦入库就不可移植，下一个同事拉代码就得改 SO
- **CI/构建友好：** 流水线机器路径与开发机不同；入库相对路径才能在 CI 上零改动跑通
- **Diff 噪声控制：** 否则每次切机器都会污染 commit，PR 审阅失焦
- **符合"配置外部化但应可复用"的精神：** 配置外部化（避免硬编码）≠ 配置个人化（让每人改不同路径）；相对路径让外部化字段对团队共享

## 反模式（Anti-patterns）

- **绝对路径直入 Inspector：** Inspector 字段填 `D:\WorkSpace\Nova\Output\Hotfix\Game.Runtime.dll` 或 `/Users/alice/Desktop/Nova/...`，提交后另一台机器打开 SO 全是红字，必须手改后才能跑
- **代码常量硬编码绝对路径：** 类似 `const string c_OutputDir = "C:/Users/xxx/Build/";` 这种放进代码常量，不仅入 git 还伴随了路径与作者绑定，离职/换机直接报废
- **`OpenFolderPanel` 取值后直接写字段：** 编辑器里点"选择文件夹"，回调拿到 `panel.openedPath` 就直接 `m_TargetDir = panel.openedPath`——这是绝对路径；正确做法是 `Path.GetRelativePath(SettingsUtil.ProjectDir, picked)` 后再赋值
- **Pipify Step 参数填磁盘绝对路径：** Step 参数本身随 PipifySettings 资产入库，绝对路径在他人机器上整条 pipeline 立刻失败
- **JSON 资产里写本地绝对路径：** 例如某个 `ExportSettings.json` 里 `"output": "/Users/<user>/..."` 提交了；该类资产会被 git 记录，立即跨不了机

## 跨项目复用提示

**完全可复用：** 本 Pattern 与 Unity / Nova Framework 无强绑定，是任何"工程仓库 + 多人协作 + 多平台"项目都该遵循的常识规范。

**移植到其他技术栈时的小适配：**

- Web/Node：基准点改为 `process.cwd()` 或 `path.resolve(__dirname, '..')` 到仓库根
- .NET 工程：基准点改为 `AppContext.BaseDirectory` 或解决方案根
- Python：基准点改为 `pathlib.Path(__file__).resolve().parents[N]` 到仓库根
- Unreal Engine：使用 `FPaths::ProjectDir()` 作为基准点
- C/C++ CMake：用 `${CMAKE_SOURCE_DIR}` 作为基准点

**核心思想不变：**"git 入库路径必须项目根相对" + "运行期由代码侧用统一基准拼接绝对路径"。

## 关联

- 规范落点：建议在统一工程约束或单独路径规范中引用本 Pattern
- 相关 Pattern：[[PAT-27-config-no-serialize|PAT-27]]（ManagerConfig 禁序列化的姊妹规范，同属"配置外部化但要可移植"主题）
- 相关铁律：入 git 的路径配置必须是项目根相对路径
