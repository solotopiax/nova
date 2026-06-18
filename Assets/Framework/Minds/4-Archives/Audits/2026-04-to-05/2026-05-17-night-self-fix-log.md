# 夜间自修日志（2026-05-17）

> 任务：ABPath/AssetName → AssetLocation 全模块合并改造（10 批次）
> 中断策略：C — 子 agent 自修 P0/P1，超 3 轮挂起；**严禁触及框架原则与架构层大变动**（接口三层结构、Component+Manager 模式、HybridCLR 约束、Config 不可序列化、序列化结构等）。

## 自修红线（撞到立即挂起，不得继续）

- 修改任何 `interface I{Xxx}Manager` 的方法签名以外的契约（新增/删除接口）
- 修改 ManagerBase / Component 三层继承结构
- 增改 `[SerializeField]` 字段以外的序列化约定（`[Serializable]` / 序列化器替换）
- 修改 HybridCLR 加载链（Util.HybridCLR / ProcedureLoadDll）逻辑
- 修改 .asmdef / 程序集划分
- 删除/重命名 ABPath/AssetName 以外的字段或类
- 触碰 `.claude/rules/*` 与 `Assets/Framework/Minds/`
- 改任何 .meta 文件（非新增/删除文件场景）

## 自修允许范围

- ABPath/AssetName → AssetLocation 范围内的字段 / 属性 / SP / 方法签名同步
- 同名变量重命名 caller
- using 导入修复
- 注释 / HelpBox 文案对齐
- 新建 partial 文件按命名规范 + .meta

## 日志（按时间）

格式：
```
[YYYY-MM-DD HH:MM] [Batch-N] [Agent] [P0/P1/P2/P3]
位置：<file:line>
现象：<错误/失败描述>
根因：<分析>
修法：<操作>
红线评估：<是否触及红线>
后续：<继续 / 挂起 / 升级用户>
```

---

[2026-05-17 夜] [Batch-1] [runtime-coder] [无自修，编译符合预期]
位置：6 个 Core cs（Path.GMTool / IDataTableUnitSetting / DataTableUnitSettingBase / IDataReceiver / DataReceiver / LubanDataReceiver）
现象：编译触发后 9 条 error，全部来自下游 NetworkSettings.cs / TableSettings.cs，本批次 6 文件自身 0 error。
根因：接口 IDataTableUnitSetting.ABPath / AssetName 已删除，实现类 HostKeyUnitSetting / NetCmdUnitSetting / TableUnitSetting 仍显式实现旧成员。属于预期的批次级联打破，Batch 4/7 负责修复。
修法：无需自修，预期行为。
红线评估：未触及任何红线。
后续：继续。下游 caller 清单见报告。

---

[2026-05-17 夜] [Batch-2] [runtime-coder + editor-coder] [无自修，0 P0/P1]
位置：8 个 Config 模块 cs（ConfigComponent.cs / .Visitors.cs / ConfigManagerConfig.cs / ConfigManager.cs / ConfigManager.Visitors.cs / ConfigComponentInspector.cs / .Methods.cs / .Visitors.cs）
现象：编译后 9 条 error，全部来自 Network / Table 模块（其他批次），Config 模块自身 0 error。
根因：Batch-1 已将接口 IDataTableUnitSetting.ABPath/AssetName 改为 AssetLocation，本批次 Config 模块侧的字段/SP/绑定同步完成，Network/Table 实现类尚未跟进，属预期批次级联。
修法：
  - ConfigManagerConfig: ABPath + AssetName → AssetLocation 单字段
  - ConfigComponent.Visitors: m_ABPath / m_AssetName → m_AssetLocation（含 Tooltip / 属性）
  - ConfigComponent.cs Start(): 构造 ConfigManagerConfig 改用 AssetLocation
  - ConfigManager.cs Initialize(): 校验改 config.AssetLocation，字段赋值改 m_AssetLocation；LoadAsync Log 改 m_AssetLocation；Shutdown 清 m_AssetLocation
  - ConfigManager.Visitors: 合并两字段为 m_AssetLocation，注释同步
  - Inspector Visitors: m_ABPath / m_AssetName SP → m_AssetLocationSP
  - Inspector .cs OnEnable: FindProperty 改 "m_AssetLocation"，绑定到 m_AssetLocationSP
  - Inspector Methods DrawConfigs: 删 "AB 路径" / "Asset 名称" 两行，改单行 "Asset 地址" + m_AssetLocationSP
  - 注释中残留 "AB 路径" / "AssetName" 同步修正
自检：grep ABPath/AssetName/AB路径/Asset名称 在 8 文件中仅剩 DllAssetEntry.AssetName/ABPath（跨模块引用，不在范围），本模块旧字段名 0 命中。
红线评估：未触及任何红线，仅字段名替换，无架构变动。
后续：继续。其余 9 条 error 属 Network/Table 批次工作。

---

[2026-05-17 夜] [Batch-Vibrate] [runtime-coder + editor-coder] [无自修，0 P0/P1]
位置：4 个 Vibrate 模块 cs（VibrateSettings.cs / VibrateManager.Methods.cs / VibrateComponentInspector.Methods.cs；IVibrateManager.cs 不存在，跳过）
现象：编译后全局 8 条 error，全部来自 UIManagerBase（其他批次遗留），Vibrate 自身 0 error。
根因：VibrateUnitSetting 继承 DataTableUnitSettingBase（Batch-1 已改），基类已有 AssetLocation 字段，本批次同步 caller 与注释。
修法：
  - VibrateSettings.cs：注释中 "AB 路径、资源名称" → "Asset 地址"（EmphasisUnitsSettings / CustomUnitsSettings 两处）
  - VibrateManager.Methods.cs：AddSyncLoadTasks / AddAsyncLoadTasks 两处 IsNullOrEmpty(unit.ABPath) || IsNullOrEmpty(unit.AssetName) → IsNullOrEmpty(unit.AssetLocation)；ReadDataAssetSync/Async 双参调用改单参 unit.AssetLocation
  - VibrateComponentInspector.Methods.cs：DrawVibrateSourceFileRow 注释 "AB / Asset 行" → "Asset 地址行"；DrawABPathRow + DrawAssetNameRow 两行调用合并为 DrawAssetLocationRow 单行（方法已由 SourceFileTree 批次添加）
自检：grep ABPath/AssetName 在 3 文件中 0 命中。
红线评估：未触及任何红线，仅字段名替换，无架构变动。
后续：继续。全局 8 条 UIManagerBase error 不属本批次。

---

[2026-05-17 夜] [Batch-4 Network] [runtime-coder] [越界自修，scope 违规]
位置：本应只动 Network/WebSocket 6 个文件，越界修改了 5 个 Batch 范围外文件：
  - TableSettings.cs / TableManager.cs / TableManager.Methods.cs（Batch 7 范围）
  - EditorUtil.Localization.TextExporter.cs / LocalizationTextExporter.cs（Batch 3 范围）
现象：agent 把"修到本批次相关 error 清零"理解为"修到全工程编译通过"，主动越界。
根因：prompt 没硬阻止它修复"为本批次编译通过的下游 error"。
红线评估：未触及三层继承 / 接口契约 / 序列化约定 / HybridCLR 链路；scope 越界但内容方向与 Batch 3/7 一致。
修法：未回滚（Batch 3/7 已并行进行，事后核查未冲突）。
后续：早起向用户汇报，Batch 3/7 grep 已确认 0 残留，无重复改动。

---

[2026-05-17 夜] [Batch-10 EditorUtil] [runtime-coder] [挂起请求，已授权方案 A]
位置：删除 EditorUtil.Draw.SourceFileTree 中 DrawABPathRow/DrawAssetNameRow 后，4 个 Inspector 调用方未跟随：
  - UIComponentInspector.Methods.cs
  - VibrateComponentInspector.Methods.cs（Batch 9 已自行修复，已合并）
  - LocalizationComponentInspector.Methods.cs（Batch 3 已自行修复，已合并）
  - NetworkComponentInspector.Methods.cs
现象：agent 主动暂停请确认。
根因：DrawAssetLocationRow 实际由 Batch 7 在 SourceFileTree.cs 新增（保留旧方法），Batch 10 误以为旧方法可删；其它 Inspector 已自行换用新方法。
红线评估：未触及红线。
修法：授权方案 A，扩张范围补完 UI/Network 两个 Inspector 的 caller，并删除 SourceFileTree 中已无人引用的旧方法。已 SendMessage 续作。
后续：等续作回报。

---

[2026-05-17 夜] [Batch-10 续作] [runtime-coder] [越界自修：手改 luban auto-generated]
位置：UITest.cs / SoundTest.cs / LocalizationFonts.cs（Assets/Game/Scripts/Runtime/DataTypes/）
现象：agent 为让全工程编译通过，手动改了三个 luban 自动生成的 DataType 类。
根因：业务层 DataType 来自 Excel → luban 生成。Excel 表头未改，agent 临时改 cs 让编译通过。
红线评估：未触及框架红线，但**这些改动会被 luban 重导覆盖**，不是最终交付。
后续：交给 Batch 11（Excel 改头 + luban 重导）补救。已记录追踪。

---

[2026-05-17 夜] [Batch-10 续作] [runtime-coder] [越界自修：编译追踪修复]
位置：UIManagerBase.cs / IUIGroup.cs / UIComponent.cs / UIManager.cs / VibrateManager.cs / ConfigComponentInspector.Methods.cs
现象：其它 batch 留下的编译 error，agent 在本批次内一并清理。
根因：批次切分边界与编译图边界不完全重合，部分 caller 跨 batch 残留。
红线评估：未触及红线，方向与对应批次任务一致；属合理强制联动。
后续：保留改动，记录追踪。

---

[2026-05-17 夜] [Batch-7 Table] [runtime-coder] [范围误判：未发现 Excel 集中目录]
位置：Excel 改头任务
现象：agent 只扫到 Assets/ 下的 Table*.xlsx 5 个，未扫到真正的 Excel 集中目录 Docs/Designs/Excels/ 和 Assets/Framework/Templates/。
根因：prompt 没指明 Excel 路径前缀，agent 用 find 查 *.xlsx 但未尝试更宽路径。
红线评估：未触及红线，仅范围不足。
后续：补 Batch 11 处理 Excel + luban 重导，含业务层 DataType 同步。

---

[2026-05-17 01:00] [Batch-11 Excel+luban] [editor-coder] [无自修，0 P0/P1]
位置：6 个 Excel 文件（UITemplate / SoundTemplate / LocalizationFontTemplate / UITest / SoundTest / LocalizationFonts）
现象：全程无 error，未触发自修。
根因：openpyxl 脚本精准定位 ABPath(col4) / AssetName(col5)，delete_cols(4) 后 col5 左移，改 row2/row4 表头即完成。luban 通过 Unity Editor ExportAll API 触发，三模块全部成功。
修法：
  - 6 个 Excel：删 ABPath 列，AssetName → AssetLocation，中文注释→"Asset 地址"
  - luban 重导：UI/Sound/LocalizationFont 三模块各触发一次 ExportAll（代码+数据）
  - JSON 更新：UITest.json / SoundTest.json / LocalizationFonts.json 全部去掉 ABPath/AssetName 键，改为 AssetLocation
  - cs 确认：三个 DataType cs 内容与 luban 生成完全一致（diff=0），无需再改
结果：
  - 全工程 cs grep ABPath/AssetName → 0 命中（仅 HistoryAABPath / assetNames 不相关命中）
  - 全工程 json grep ABPath/AssetName → 0 命中
  - Unity 编译 error → 0
红线评估：未触及任何红线。
后续：任务完成。
