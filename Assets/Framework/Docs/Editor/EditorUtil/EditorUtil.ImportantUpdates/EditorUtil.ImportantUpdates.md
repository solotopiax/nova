# EditorUtil.ImportantUpdates

`NovaFramework.Editor.EditorUtil.ImportantUpdates` 是重要升级声明的内部工具。唯一内容来源是每个实际安装包根目录的 `CHANGELOG.md`，不建立额外声明文件，也不联网下载旧包。

## 声明格式与发布

开发时在 `[Unreleased]` 下维护 `### Important`，正式发布时随其他分类归档至 `## [SemVer] - YYYY-MM-DD`。每版本最多一个 Important 分类，分类内可以用四级标题、段落和列表组织多项声明。空分类不提示；未发布草稿不进入消费端窗口。已发布正文保持不可变，补充和勘误放入下一版本。

```markdown
## [Unreleased]

### Important

#### 升级后需要执行的操作

影响：说明受影响的用户和具体变化。

需要操作：
1. 写清实际操作步骤。
2. 给出必要的迁移说明。
```

本仓维护声明复用 `changelog_tool.py add <target> Important --body-file <UTF-8文件>`；读取使用 `read-important`，修改或清除未发布草稿使用 `set-important --body-file` / `--clear`。上述工具属于框架发布维护端，消费项目只读取随包 CHANGELOG，不依赖主仓工具。发版 Skill 逐包展示已有声明或明确“无”，询问保留、修改或补充，并纳入最终发布确认；不会默认把 Breaking 全部转为强制弹窗。

## 自动提示与操作范围

- Editor 启动和包注册后安排检查，等待包解析、编译、资源更新及 Play Mode 切换结束。
- 单包操作由 `PlugPals.InstallPackage` 在写 manifest 前持久化一个目标；批量操作由 `PlugPalsWindow.UpgradeAll` 在工具层批次范围内提交，窗口只负责命令转发。
- 单包窗口只包含该包；批量窗口只包含本批次实际到达目标版本的包。自动带动的依赖与其他包的旧声明留到下次真正启动补查。
- 无显式批次的 Git 同步、原生 Package Manager 或外部宿主升级按同步升级处理，汇总当前已安装包的未确认声明；不会猜测外部用户点击的次数。
- 通过离线 `Client.List` 等待 UPM 查询终态，再核对注册包、包目录的 `package.json`、版本及 registry lock；提交安装请求或 lock 变化不代表成功。部分失败目标不展示、不写已读，其他已生效目标仍可汇总。
- 最新包保留完整 CHANGELOG。跨版本时收集所有不高于实际版本且尚未确认的 Important，同包版本按完整 SemVer 降序（从新到旧）；首次使用也不会把历史重要版本自动标为已读。

## 读取、内存与持久化

`实际包目录 → CHANGELOG 提取 → 按包范围/版本/确认键筛选 → 固定展示快照 → 用户确认 → 再核对安装版本与正文 → 原子保存`。

本地文件为工程内 `Library/Nova/ImportantUpdates/<本机用户哈希>/State.json`，包含 `SchemaVersion`、`Acknowledged`、`Observed`、`Pending`。它不进入 Git；消费项目也必须保留 Library 忽略规则。不同工程和本机用户分别保存；清理 Library 后允许重新提示。

确认键为 `包名@正式版本`，对应整个 Important 分类。`Observed` 只是升级跨度说明，观察到版本不代表确认。`Pending` 保存批次 ID、显式包名、原版本与目标版本，重载后只恢复核验与展示，绝不重放安装。

窗口关闭、失焦、Editor 退出或脚本重载都不写已读；只在用户勾选阅读并点击确认，且正文和安装版本未漂移、原子写入成功时完成确认。写入失败保留待确认内容并提示重试。只有本窗口展示的版本可写入，其他包和未来版本不能被顺带确认。

## 错误与限制

- 解析忽略 fenced code 和 HTML 注释内的伪标题，保留正文。重复 Important、错误大小写/层级、无版本归属、重要版本的非法 SemVer/日期或含 Important 的重复版本节均明确报错；没有重要声明的旧格式继续兼容。
- 本地状态损坏会在 Console 明确报告，保留原文件，不静默重置成已读。修复后继续检查。
- 模态窗口只在 Editor 稳定后打开；未确认关闭后会在安全时机恢复。batchmode 不弹窗、不确认，留给后续交互式 Editor。
- 提示工具随 Framework 分发。旧 Framework 不会自动具备本能力；工具因编译错误无法加载时，待恢复后再补查。本机制不替用户执行迁移，也不证明用户已完成声明中的操作。
- 不提供手动菜单。回查历史沿用 PlugPals 更新日志入口；Agent 不得通过反射、脚本、修改状态文件或自动勾选代替人的阅读确认。

## 关联

- [ImportantUpdatesWindow](../../Windows/ImportantUpdatesWindow.md)
- [PlugPals](../EditorUtil.PlugPals/EditorUtil.PlugPals.md)
- [普通更新检查](../EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md)

## 快照确认与持久化细节

展示摘要覆盖所有不高于当前实际版本的 Important 版本和原始正文，长度前缀拼接后摘要；确认时重读实际安装版本、正文和完整适用集合。即使只是补写旧版本声明，也会使旧展示失效，必须重新阅读。构建元数据只参与 SemVer 语法验证，不参与版本优先级；超大数字按长度比较，避免整数溢出。

BeginBatch 只维护嵌套内存范围，不是安装事务或资源锁。显式目标先写 Pending 再提交 manifest；提交失败只撤销准确目标，已提交的其他目标保留，Dispose 恢复外层范围并安排核验，不回滚或重放安装。SaveState 用唯一临时文件替换/移动，任何保存或临时清理异常均向上报，不能声称确认成功。调度错误按去重日志并隔 30 秒重试；未确认关闭约 2 秒后可再次提示。
