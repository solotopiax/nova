# 可选图集生产与恢复

只在评估图集生产、配置明确的批量规则或恢复本工具未完成事务时读取。先按 [是否需要这个工具](../../../../Docs/Editor/EditorUtil/EditorUtil.SpriteAtlasGeneration.md#先判断是否需要这个工具) 比较 Unity 原生文件夹引用；普通“一个文件夹一张图集”优先沿用原生能力，不能因用户提到“按目录”就自动创建本工具配置。确有多图集规则、排除、预演或受控流水线需求时才选择增强。完整字段、差异与归属以该文档和当前源码为准；旧项目不创建配置、不插入步骤、不迁移原图集流程。

当前生成器把来源目录展开为具体 Sprite 列表，成员增删移动后须重跑生成；原生文件夹引用无须这层维护。向项目组说明该成本和真实附加功能，不宣称有新的打包算法、额外合批或未经测量的速度收益。

## 输入与前置

从项目读取已保存 `SpriteAtlasGenerationSettingsSO` 路径/GUID、Enabled、Format、实际 Sprite Packer 模式、来源/排除路径、输出根及规则。配置不存在时，在已授权范围通过原生 `Assets/Create/Nova/Sprite Atlas Generation Settings` 和 Inspector/受支持 Editor 序列化操作创建保存；复杂 Rules 不由 configure-batch 的 ParamsJson 代写，也不手写 YAML。缺少该资产编辑通道时只阻断配置创建/调整，已有配置与已保存 Batch 可继续检查。

输出目录须已创建并导入；来源、源图/子图名、排除、打包参数及输出归属来自明确项目决策，不以“生成成功”为由猜目录或开启全局 Packer。新配置默认关闭且 Rules 为空，保持该状态直到本次明确启用范围。

## 复用实际 Action

1. `nova.project.pipify.inspect-settings` 用 `settingsGuid` 读取当前注册的 `spriteatlas.generate` / `spriteatlas.recover` 和真实字段。
2. `nova.project.pipify.configure-batch` 配置具名预演 Batch，`spriteatlas.generate` 的 `SettingsAssetPath` 为准确配置路径，`PreviewOnly=true`。优先单独预演 Batch，避免“只预演图集”运行其它有副作用 Step。
3. 本次授权包含运行时，用 `nova.project.pipify.run-batch(pipifySettingsGuid,batchName)` 的当前 Plan/Execute/Verify 执行，核对逐输出差异。字符串字段通过 `valueJson` 的 JSON 字符串 token，布尔通过 `true/false` token；不传任意 ParamsJson、类型名或代码。
4. 目标包含正式生成且预演范围无误时，明确设 `PreviewOnly=false`。需要与 Bundle 串联才在项目自己的 Batch 中把它放在 `bundlebuilder.build` 之前；不修改其它 Batch、不改 Collector，也不让直接 Bundle Action 自动生成。
5. 查看本次 Job 终态、实际生成路径/差异、GUID、归属清单与重复无变化。要交付可加载资源时继续既有 Bundle/Play/Player 验收，不能只用 Step 成功代替。

生成与恢复是底层同步工具；Runner 取消仍按原 Step 边界，不保证中途停止。保存配置/Batch 不表示已经生产、打包或运行通过。

## 恢复分支

发现本工具保留的配置 GUID 事务时停止生成，不删除备份、不重放旧 Batch。冻结同一已保存配置和事务归属后，通过只含 `spriteatlas.recover`、`SettingsAssetPath` 的明确恢复 Batch 执行一次。未完成事务只恢复已知的本工具图集/meta/归属清单写入；外部或导入回调未知改动、备份损坏、身份不一致会拒绝覆盖，保留现场。已提交/已恢复终态只核输出并继续清理备份，不回滚已提交输出，也不因部分备份已删而重新执行生成。

`Retained` 不是待自动删除列表；规则删除/改名的旧输出继续保留。恢复成功后重新预演当前配置，再另行决定生成，不将恢复当生成成功。
