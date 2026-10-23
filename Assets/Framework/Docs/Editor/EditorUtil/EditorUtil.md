# EditorUtil

**类签名**：`public static partial class EditorUtil`
**命名空间**：`NovaFramework.Editor`

编辑器工具集聚合入口，通过 `static partial class` 拆分为多个嵌套工具类，统一以 `EditorUtil.*` 访问；这些名称不是独立命名空间。

---

## 文件

| 文件 | 子类 | 说明 |
|------|------|------|
| `EditorUtil.cs` | `EditorUtil.Initializer` | `[InitializeOnLoad]`：注入 TxtHelper + LogHelper |
| [`EditorUtil.Draw/`](EditorUtil.Draw/EditorUtil.Draw.md) | `EditorUtil.Draw.*` | Inspector GUI 绘制工具集（Label/Field/Button/Toggle/Slider/Foldout/HelpBox/Toolbar/Layout） |
| [`EditorUtil.FileSystem/`](EditorUtil.FileSystem/EditorUtil.FileSystem.md) | `EditorUtil.FileSystem` | 文件系统封装（路径转换、目录创建、AssetDatabase 刷新） |
| [`EditorUtil.Serializer/`](EditorUtil.Serializer/EditorUtil.Serializer.md) | `EditorUtil.Serializer` | Unity 序列化属性读写与场景定位辅助 |
| [`EditorUtil.ScriptingDefineSymbols/`](EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md) | `EditorUtil.ScriptingDefineSymbols` | 脚本宏定义增删查工具（跨平台批量操作，`NamedBuildTarget` API） |
| [`EditorUtil.TypeCache/`](EditorUtil.TypeCache/EditorUtil.TypeCache.md) | `EditorUtil.TypeCache` | 编辑器类型缓存（反射收集实现类名称，编译后自动刷新） |
| [`EditorUtil.PlugPals/`](EditorUtil.PlugPals/EditorUtil.PlugPals.md) | `EditorUtil.PlugPals` | 私有 Verdaccio 仓库 UPM 包管理工具层（远程包拉取、manifest 读写、包安装/卸载、CHANGELOG 拉取） |
| [`EditorUtil.CheckUpdate/`](EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.md) | `EditorUtil.CheckUpdate` | UPM 包版本检查工具（启动自动检查 + MarkSkip 持久化，复用 PlugPals 网络层） |
| [`EditorUtil.AndroidResolver/`](EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md) | `EditorUtil.AndroidResolver` | Android 依赖解析工具（反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Assets/GeneratedLocalRepo/**） |
| [`EditorUtil.FileWatcher/`](EditorUtil.FileWatcher/EditorUtil.FileWatcher.md) | `EditorUtil.FileWatcher` | 编辑器侧目录监控工具，供各 Inspector 监听 `_configs/` 或源目录变更 |
| [`EditorUtil.CsvExporter/`](EditorUtil.CsvExporter/EditorUtil.CsvExporter.md) | `EditorUtil.CsvExporter` | 将结构化数据导出为 CSV 文件 |
| [`EditorUtil.Asmdef/`](EditorUtil.Asmdef/EditorUtil.Asmdef.md) | `EditorUtil.Asmdef` | Assembly Definition 命名空间解析（向上查找 .asmdef，提取 rootNamespace/name） |
| [`EditorUtil.Excel/`](EditorUtil.Excel/EditorUtil.Excel.md) | `EditorUtil.Excel` | Excel 读写工具（ExcelDataReader 读取 + EPPlus 写入） |
| [`EditorUtil.Luban/`](EditorUtil.Luban/EditorUtil.Luban.Pipeline.md) | `EditorUtil.Luban.*` | Luban 导出基础设施（Pipeline、SchemaManifest、GeneratedOutput 所有权等） |
| [`EditorUtil.Config/`](EditorUtil.Config/EditorUtil.Config.Exporter.md) | `EditorUtil.Config.*` | Config 导出工具集（Exporter/Validator/SDKPluginScanner/KitConfigScanner/StructureGuard/RuntimeProvider） |
| [`EditorUtil.Table/`](EditorUtil.Table/EditorUtil.Table.Exporter.md) | `EditorUtil.Table.*` | Table 模块 Luban 导出入口（ExportAll/ExportCode/ExportData） |
| [`EditorUtil.UI/`](EditorUtil.UI/EditorUtil.UI.Exporter.md) | `EditorUtil.UI.*` | UI 导出公共门面；内部编排与产物应用位于 DataPipeline/UIs |
| [`EditorUtil.Localization/`](EditorUtil.Localization/EditorUtil.Localization.TextExporter.md) | `EditorUtil.Localization.*` | 本地化导出工具集（TextExporter 多语言投影 + FontExporter 暂存事务发布） |
| [`EditorUtil.Network/`](EditorUtil.Network/EditorUtil.Network.Http.md) | `EditorUtil.Network.*` | Network 导出工具集及 Editor JSON 固定五字段默认 Head / HTTP 门面 |
| [`EditorUtil.Sound/`](EditorUtil.Sound/EditorUtil.Sound.Exporter.md) | `EditorUtil.Sound.*` | Sound 模块 Luban 导出流水线薄封装（ExportAll/ExportData/ExportCode） |
| [`EditorUtil.Vibrate/`](EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md) | `EditorUtil.Vibrate.*` | Vibrate 模块 Luban 导出工具（Emphasis + Custom 双轨，各提供 Data/Code/All） |
| [`EditorUtil.BundleBuilder/`](EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md) | `EditorUtil.BundleBuilder` | YooAsset 标准 AssetBundle 与 RawFile 构建入口 |
| [`EditorUtil.TrackRegistry/`](EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.md) | `EditorUtil.TrackRegistry` | 打点 Excel 汇总工具（Framework Sheet 优先，Package Sheet 沿用源 Sheet 名，生成 `Library/Nova/Tracks/Tracks.generated.xlsx`） |
| [`EditorUtil.Environment/`](EditorUtil.Environment/EditorUtil.Environment.md) | `EditorUtil.Environment.*` | Luban、Python3 与 HybridCLR 检测及会话缓存 |
| [`EditorUtil.ProcessRunner/`](EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md) | `EditorUtil.ProcessRunner` | 外部进程、超时与输出捕获 |
| [`EditorUtil.CDN/`](EditorUtil.CDN/EditorUtil.CDN.md) | `EditorUtil.CDN` | OSS 部署与 Cloudflare 缓存清理 |
| [`EditorUtil.Pipify/`](EditorUtil.Pipify/EditorUtil.Pipify.md) | `EditorUtil.Pipify.*` | 保存的 Batch、窗口/CLI 执行与配置编辑 |
| [`EditorUtil.HybridCLR/`](EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) | `EditorUtil.HybridCLR` | 热更 DLL、AOT 与 link.xml 工具 |
| [`EditorUtil.ImportantUpdates/`](EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md) | `EditorUtil.ImportantUpdates` | 重要更新声明解析与展示状态 |
| [`EditorUtil.Asset/`](EditorUtil.Asset/EditorUtil.Asset.Operator.md) | `EditorUtil.Asset.Operator` / `Cache` | 编辑器资源操作与热更缓存边界 |
| [`EditorUtil.Proto/`](EditorUtil.Proto/EditorUtil.Proto.CliRunner.md) | `EditorUtil.Proto.CliRunner` | Proto 编译器调用 |
| [`EditorUtil.Reflect/`](EditorUtil.Reflect/EditorUtil.Reflect.Tooltip.md) | `EditorUtil.Reflect` | 反射读取字段提示信息 |
| [`EditorUtil.AgentActions/`](EditorUtil.AgentActions/EditorUtil.AgentActions.md) | `EditorUtil.AgentActions.*` | 受控 Action 协议、执行与恢复 |
| [项目组 Skills](../../../Agents/INDEX.md) | `EditorUtil.AgentSkills` | 随包 Skills 的项目投影与发现入口 |

---

## Initializer 初始化

```csharp
[InitializeOnLoad]
public static class Initializer
{
    static Initializer()
    {
        // 注入 TxtHelper → Txt.Format 在编辑器模式下可用
        var txtHelper = Util.TypeCreator.Create<ITxtHelper>("NovaFramework.Runtime.TxtHelper");
        txtHelper.Initialize();
        Txt.SetHelper(txtHelper);

        // 注入 LogHelper → Log.* 在编辑器模式下可用
        var logHelper = Util.TypeCreator.Create<ILogHelper>("NovaFramework.Runtime.LogHelper");
        logHelper.Initialize();
        Log.SetHelper(logHelper);
    }
}
```

> 如果 Inspector 中出现 `Txt.Format` NullReferenceException，检查 `EditorUtil.Initializer` 是否正常加载（确认程序集引用正确）。

---

## 关联文档

- [Editor.md](../Editor.md)
- [BaseComponentInspector.md](../Inspectors/BaseComponentInspector.md)
- [EditorUtil.BundleBuilder.md](EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)

## Placeholder 的上下文来源

`EditorUtil.Placeholder` 连接纯占位符解析器与编辑器配置。显式 platform/channel 的 FromConfigMaster 重载仍要求非空 master，但使用传入坐标；便捷重载才读取 master 当前坐标。它们不验证 None 坐标或包是否存在。Resolve 按显式上下文解析，已知 token 区分大小写，未知 token 原样保留。

ResolveDefaultPackageName 从 canonical Nova.prefab 的序列化默认包名读取，缺失时取第一个包，再没有则空串，不查询场景实例或 Collector。ResolveFromActiveConfig 使用 WorkspaceActive、Application.version 和本次 DateTime.Now，不写资产；同一上下文内多个时间 token 一致，不同调用可能变化。最终路径仍需消费者核验范围与存在性。

## 场景路由与帮助器初始化

SceneRoute 只处理非构建中的 Single 场景打开事件，先 ReconcileScene，失败停止；成功后依次同步开发 Sample 的 BuildSettings、记录 Master、注入 YooAsset、记录 Pipify。后续异常直接传播，无跨步骤回滚，所以场景打开可能已改 BuildSettings 再注入失败。Additive 不切路由。

唯一入口同步仅在已核 Framework 实际解析自本工程 Assets/Framework、场景和 Master 属于同个 Assets/Samples/<sample>、Master 位于该 Sample 的 Editor 目录且约定 `<sample>.unity` 存在时，覆盖为唯一 enabled 场景，不保留原多场景列表。已注册包若解析在别处立即 false；只有未注册才用框架包名、仓库标记和 manifest 精确本地引用共同回退。路径比较不解析符号链接。Master 的日志只核有效 AssetPath/GUID，不要求 Sample 位置，也不证明保存或注入成功。

EditorUtil 静态初始化按准确类型名创建并 Initialize TxtHelper、LogHelper，逐项设置全局帮助器；异常不统一捕获，可能前项已设置、后项失败，不能当作 Runtime 初始化完整完成。

## 资源密钥的编辑器链路

ResourceKeyStore 从激活 Master 的已保存 ResourceEncryption 读取可变原引用并完整 Validate，不读取窗口草稿；WorkspaceActive 还可能修复绑定。包/keyId 大小写精确唯一，标识 1–64 字符且无首尾空白或控制字符，Base64 恰好 32 字节，维度坐标与 ActiveKeyOverrides 必须引用现存密钥。未勾选轴归固定哨兵；投影扩维复制旧逻辑组、减维按当前轴合并，保留全部解密密钥。SetActiveKeyId 只改内存，不自行校编号存在，最终保存 Validate；旧 Save 入口明确拒绝绕过 Config 顶层保存。

旧 JSON 仅迁入工作副本，正式保存后重新同步导入并逐值核一致才删除旧文件；失败保留。生成器明确使用 Master/coord，实际 Player owner assembly、Runtime 直接引用和 HybridCLR 热更依赖闭包决定能否启动加载，不从 namespace 或目录猜。Preview 只构造内存代码/精确删除集合，不写、保存或 Refresh；正式 Generate 先写新源码/link，再清精确旧工具产物，异常尝试恢复文件与 legacy asmdef/meta。公共 GenerateForCI 仍会全局 SaveAssets，受控 Action 不调用它。Base64 标识编码不保护密钥；正式私有生成源码包含保留密钥，不进入公开日志/收据。

构建上下文按正常同步嵌套 using 绑定包和实际目标，非线程局部且 Dispose 无幂等/当前身份检查，乱序或重复释放会覆盖上下文。官方 manifest 接口缺平台时，按平台分维必须由 Nova 构建上下文提供，不猜窗口。Bundle 路径要求 YooAsset OutputCache 结构；清单头的包名在认证前不可信。AES/XOR/Offset 及解密器配对按具体类型核验，Offset 不提供密码学保密或完整性。

Encode 先核模式/包/keyId 并认证解密旧缓存，与明文相同才复用密文，否则重新生成随机 IV。当前所有 Bundle Encrypt 都全量读原文件；AES stream 的 Runtime 随机读取不代表构建期流式加密。通常 finally 清零临时 key，但既有缓存读取遇到未过滤的 IO 异常时发生在最终 try 之前，不能宣称所有异常路径都清零。构建解密器使用独立 Editor provider，不注册客户端 provider；真实加载、内存和设备表现见 [资源加解密](../../Runtime/Modules/Asset/ResourceEncryption.md)。

## 项目组 Skill 投影的事务边界

EditorUtil.AgentSkills 从实际已注册 Framework 包身份/版本/resolvedPath 解析 Agents 真源；注册项不合法时阻断，不扫描其他 checkout 或随意开发回退。完整 Catalog/contract 先核依赖、弃用、循环、扁平目录与任务画像：画像不能覆盖原权限、锁、确认和写域，Workflow 只能选原子依赖子集，不能借未选子入口或删外部写入/凭据/构建风险。入口描述和渐进加载标记的结构校验仍不证明函数存在、真实执行权限或完整语义依赖。

Reconcile 的 dryRun 不锁不写，遇未完 journal 阻断；非 dryRun 先恢复旧 journal，再解析新源，旧恢复不依赖当前包源。新计划只取得未冲突项目的所有权，修改过、缺失或无主同名目录保留冲突，安全项仍可继续。Hash 覆盖普通文件（含 meta），空目录不贡献，路径按 UTF-8 排序；复制前后另核源/暂存 Hash，拒绝链接，仍不是抵御任意并发文件变更的原子快照。

同域 Monitor 加跨进程字节锁串行化，锁文件诊断文本不是所有权证明，释放不删除 inode。事务按 staging→journal→逐动作移动旧目标到 backup/新目标→最终 state→删 journal→清 staging 续传；不自动回滚或从新包源重拷。异常保留可恢复现场，完成 state/journal 后清理失败可留下无 journal 的孤立 staging，不自动删除。JSON 使用同目录唯一临时文件原子替换，但不等于断电 fsync 保证，清理异常也可遮原错。

自动同步等编译/更新稳定后执行，失败仅告警且不自排重试；下一启动/包事件/显式入口才再触发。CLI 仅供 headless/CI，异常退出 2；成功投影不证明编译、Play、MCP 发现刷新或任务可调用。DTO 的只读属性仍可能共享 JObject/List，不能代替冻结。
