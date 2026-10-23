# EditorUtil.TrackRegistry

**类签名**：`public static partial class TrackRegistry`（`EditorUtil` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.TrackRegistry`

打点 Excel 汇总工具。扫描 Framework 公共打点工作簿与各包内模块打点工作簿，合并生成一份本地汇总 xlsx，供 SDK Inspector 一键打开查看；只从源 Excel 读取登记数据，不扫描代码推断埋点；客户端实际发送行为仍需核对源码。全局登记和格式规则见 [Tracks 维护规范](../../../TRACKS.md)。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.cs` | `EditorUtil.TrackRegistry` | 汇总入口：扫描/读取/重名校验/规范化行/调度写盘 |
| `Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.AgentSnapshot.cs` | `EditorUtil.TrackRegistry`（internal Agent helpers） | 复用真实扫描的全源/meta/包快照、页内容与单色样式只读验证 |
| `Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.XlsxWriter.cs` | `EditorUtil.TrackRegistry.XlsxWriter`（private） | 最小 OpenXML xlsx 写入器：样式、列宽、行高、合并单元格 |

---

## §5 完整公开 API

```csharp
// 生成当前工程的打点汇总表
// 扫描 Framework 与各包 Nova/Tracks/Tracks.xlsx，合并写入 Library/Nova/Tracks/Tracks.generated.xlsx
// <param name="projectRoot">工程根目录；null 或空时抛 ArgumentException</param>
// <returns>生成的 xlsx 绝对路径</returns>
// 未找到任何打点 Excel 时抛 InvalidOperationException
// 同名 Sheet 出现在不同来源时抛 InvalidOperationException（携带来源 A/B 路径）
public static string Generate(string projectRoot);
```

内部 `XlsxWriter` 为 `private`，不对外公开。

---

## 关键行为

### 扫描来源（固定顺序）

1. Framework 本地表：`Assets/Framework/Tracks/Tracks.xlsx`。
2. Framework 包内表：`com.solotopia.nova.framework/Tracks/Tracks.xlsx`（由有效包根目录定位）。
3. 本地模块表：`UPMPackages/*/Nova/Tracks/Tracks.xlsx`，按包目录名 ordinal 排序。
4. Unity 模块包表：`Packages/*/Nova/Tracks/Tracks.xlsx`（包含 Unity 解析出的有效包根目录）。相同规范化路径只读取一次。

### 输出位置

`Library/Nova/Tracks/Tracks.generated.xlsx`（不入库，属本地缓存）

### Sheet 合并规则

- 读取每份工作簿的全部 Sheet，按来源追加进汇总列表
- 源事件页使用 `打点表-<模块名>`，新增或修正格式参考 Framework 的 `打点表-Network`；保留既有工作簿结构和归属。广告渠道统一在广告聚合包登记，IAP 子包统一在 IAP 表登记，TGA 固定属性保留在 Framework 说明页。汇总器仅保留源页名并追加，不负责拆表或重命名。
- 同名 Sheet 跨来源出现立即抛 `InvalidOperationException`，错误信息携带 Sheet 名与两个来源工作簿路径
- 每行末尾的空单元格在写出前裁剪；空引用统一转为空字符串

### 生成 xlsx 的样式规则（XlsxWriter）

- 单元格统一使用 `inlineStr` 内联字符串，不生成 sharedStrings
- 所有事件页采用 `Assets/Framework/Tracks/Tracks.xlsx` 的 `打点表-Network` 样式：表头深蓝（`FF1F4E78`）、白字、Calibri 11pt 加粗居中；数据区统一浅绿（`FFEAF4EC`），不使用交替颜色。
- 数据默认 Calibri 11pt；事件标识 A/B 左对齐、顶部对齐，事件说明 C 使用宋体-简 11pt、左对齐、顶部对齐并自动换行；D～I 沿用模板默认文字排版与细边框。
- 事件列宽 A～I 固定为 28、18、36、40、14、58、32、16、48；首行高 17、默认行高 16.8。说明页保留原字段布局，普通列宽 32，末列 58。
- 事件页只按事件边界合并 A～C；同列存在后续非空说明时分段合并，避免丢失内容。G～I 不合并。
- 冻结表头并启用筛选；带独立标题行的说明页冻结前两行，标题和字段表头采用同一表头样式。

### 触发方式

不在 Editor 启动/域重载时自动跑，由 `SDKComponentInspector` 上的“打开打点表”按钮或 `nova.project.tracks.generate-registry` 的受控 Execute 显式触发。Inspector 按钮失败时记录 `Log.Warning`，不向 UI 抛出；Action 则按是否已开始写入返回 `blocked` 或 `partial`，保留收据且不重放。

---

## Agent 入口

`nova.project.tracks.generate-registry` 只接受 `{"scope":"all"}`。Plan 不写源或汇总，冻结全部正式源和 `.meta`、manifest/lock、package.json、已解析包元数据、页内容及已有输出；`DataJson.registry` 供同领域结构 CLI 复用。Execute 只调用 `Generate`，旧输出备份路径在 Plan 精确列出；提交后取消或失败保持 `partial` 和一次性 Receipt，不重放。Verify 只读核对全来源、整输出 hash、页序、全部行和实际 Network 单色样式。公开 C# API 签名不变，新增 snapshot helpers 均为 internal。源编辑入口与消费态边界见 [Tracks 规范](../../../TRACKS.md)。

## §11 使用示例

```csharp
// 业务侧通常无需手动调用——SDK Inspector 面板上的按钮已封装 try/catch 与 OpenFile
// 仅在脚本化流水线里需要主动刷新汇总表时手动调用
string projectRoot = Directory.GetParent(Application.dataPath).FullName;
string generatedPath = EditorUtil.TrackRegistry.Generate(projectRoot);
EditorUtil.FileSystem.OpenFile(generatedPath);
```

---

## §13 关联文档

- [Editor.md](../../Editor.md)
- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Excel.md](../EditorUtil.Excel/EditorUtil.Excel.md)（读取源工作簿 `ReadAllSheets` 的底层入口）
- [SDKComponentInspector.md](../../Inspectors/SDKComponentInspector/SDKComponentInspector.md)（主要调用方：触发 `Generate` 并打开产物）

## 扫描、快照与缓存失效

物理根只枚举一级，不要求 package.json；只有扫描当前 Unity 项目时才补 PackageInfo.resolvedPath。Framework 根识别仍依据目录名去 @ 后缀，目录名不标准的 file 引用可能漏检，不能仅凭该扫描证明全部有效安装。工作簿路径按 FullPath、忽略大小写去重，不解析符号链接；普通 Generate 仅按 Ordinal 拒绝同名 Sheet，case-only 名称仍可能冲突，Agent 快照另拒绝忽略大小写重名和 `$` 页名。

规范行只裁尾空值、内部 null 转空，不 Trim、不删空行或判断事件正确性；原厂单元格 `$` 保留。TrackSheet/扫描 context 的只读属性仍持有可变列表。输出前先删除旧缓存再写 ZIP，异常可能丢旧文件并留下半份新 xlsx；这是可再生缓存，不是 OutputApplier 事务。

Writer 按内容首格识别事件页，framework 标志不决定样式。固定行高不测量长文本，空 Sheet/全空列、非法 XML 控制字符或未保留首尾空白可能影响实际 Excel；静态审查不是视觉验收。Agent Verify 核页序、全部规范行摘要、事件页全部颜色和 A-C 同列合并，不证明业务已触发或后台收到。CaptureAgentFile 拒绝祖先/目标链接且核完整 Hash，取消检查用于扫描/hash，CaptureAgentSheets 本身无 token 参数。
