# PipifySteps

`PipifySteps` 是 Pipify 的内置步骤目录。  
这页不再试图充当“所有 Step 参数字典总表”，而是回答三个更关键的问题：

- Pipify 现在到底能编排哪些流程？
- 常见批处理应该怎么组合这些 Step？
- 每一类 Step 的前置条件和失败点在哪里？

## 什么时候先看这页

优先看这页的场景：

- 你准备组一个新的 Batch，但不知道先后顺序。
- 你要判断某个能力应该走 `Pipify`，还是直接走菜单 / 独立工具。
- 你要查某个 Step 的职责，而不是想看所有参数细节。

如果你已经确定具体参数怎么写，继续看：

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyContext.md](./PipifyContext.md)
- 具体工具页，比如 `EditorUtil.BundleBuilder`、`EditorUtil.HybridCLR`、各导出器文档

## Pipify 当前覆盖的能力

### 1. HybridCLR 流程

目标：

- 生成或补齐 HybridCLR 产物
- 编译业务 DLL
- 拷贝 AOT / Game DLL

高频 Step：

- `hybridclr.validate_linkxml`
- `hybridclr.compile_dll_active_build_target`
- `hybridclr.generate_all`
- `hybridclr.copy_aot_dll`
- `hybridclr.copy_game_dll`

适合场景：

- 热更 DLL 产物刷新
- HybridCLR 完整预构建

### 2. Android 依赖准备

目标：

- 在 HybridCLR 或打包前先跑一次 EDM4U Resolve

高频 Step：

- `edm4u.android_resolve`

适合场景：

- Android 构建链起点
- 避免 `GeneratedLocalRepo` 缺失导致后续构建失败

### 3. 资源导出

目标：

- 从当前工程状态导出 Config / Table / UI / Localization / Network / Sound / Vibrate

高频 Step 族：

- `export.config`
- `export.table.data` / `export.table.code`
- `export.ui.data` / `export.ui.code`
- `export.localization.*`
- `export.network.*`
- `export.sound.*`
- `export.vibrate.*`

适合场景：

- 构建前统一刷新资源导出物
- 只重导某一模块的数据或代码

### 4. Bundle 构建

目标：

- 跑 YooAsset ScriptableBuildPipeline 资源构建

高频 Step：

- `bundlebuilder.build`

适合场景：

- 资源包构建阶段

### 5. Player 打包

目标：

- 生成安装包或工程导出产物

高频 Step：

- `build.package`

适合场景：

- 资源导出与 Bundle 构建之后的最终打包

### 6. 系统外壳辅助

目标：

- 在批处理中打开目标目录，便于人工接力

高频 Step：

- `shell.open_folder`

适合场景：

- 构建产物落地后直接打开目录
- 导出完成后快速跳到目标位置检查结果

## 常见组合方式

### 1. HybridCLR 完整链

推荐顺序：

1. `edm4u.android_resolve`（Android 时）
2. `hybridclr.validate_linkxml`
3. `hybridclr.generate_all`
4. `hybridclr.copy_aot_dll`
5. `hybridclr.copy_game_dll`

适合：

- 要一次性拿到可用于运行时加载的完整 DLL 产物

### 2. 构建前资源刷新链

推荐顺序：

1. `export.config`
2. `export.table.data` / `export.table.code`
3. `export.ui.data` / `export.ui.code`
4. 按需追加 `localization / network / sound / vibrate`

适合：

- 正式构建前刷新所有高价值导出物

### 3. 完整打包链

典型顺序：

1. 资源导出 Step
2. `bundlebuilder.build`
3. `build.package`
4. `shell.open_folder`

适合：

- 一条龙产出资源包 + Player 包体

## 关键前置条件

### Config 导出

- 工程内必须能定位到唯一 `ConfigMasterSO`
- `ConfigMasterSO.ExportTarget` 不能为空

### 组件型导出 Step

`Table / UI / Localization / Network / Sound / Vibrate` 这些 Step 都依赖：

- 当前活动场景里能找到 `Nova`
- 对应组件挂在 `Nova` 层级上

这类 Step 不是纯资产导出器，它们依赖“当前场景上下文”。

### HybridCLR

- 相关菜单链和 `EditorUtil.HybridCLR` 必须可用
- `copy_aot_dll / copy_game_dll` 之前必须先有 DLL 产物

### Bundle / Build

- 前置导出物必须已就绪
- `build.package` 的产物命名还依赖当前激活 `ConfigRuntimeSO` 的 `DevelopMode`

## 常见失败点

- 当前场景没有 `Nova`：组件型导出 Step 会直接失败。
- `ConfigMasterSO.ExportTarget` 没配：`export.config` 会中断流水线。
- HybridCLR 只跑了拷贝，没先生成 DLL：拷贝类 Step 会失去输入产物。
- Android 没先 `edm4u.android_resolve`：后续构建链可能在依赖目录上失败。
- `build.package` 前没准备好 Config：文件名开发模式段会降级为 `Debug`，并输出 Warning。

## 关键源码入口

关键源码：

- [PipifySteps.HybridCLR.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.HybridCLR.cs)
- [PipifySteps.Export.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.cs)
- [PipifySteps.Export.Helpers.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.Helpers.cs)
- [PipifySteps.BundleBuilder.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.BundleBuilder.cs)
- [PipifySteps.Build.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Build.cs)
- [PipifySteps.Shell.cs](../../../../Scripts/Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Shell.cs)

关键入口：

- `[PipifyStep(...)]` 特性声明
- `Helpers.ResolveConfigMaster()`
- `Helpers.ResolveComponentOnNova<T>()`

## 相关文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifyContext.md](./PipifyContext.md)
- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [EditorUtil.BundleBuilder.md](../EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)
- [Editor.md](../../Editor.md)
