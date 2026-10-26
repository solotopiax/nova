# PlugPalsWindow

**类签名**：`public sealed partial class PlugPalsWindow : EditorWindow`  
**命名空间**：`NovaFramework.Editor`  
**菜单入口**：`Nova/Open PlugPals`

`PlugPalsWindow` 是 PlugPals 云插件服务中心窗口。窗口自身只负责 GUI 与交互；远程拉取、manifest 读写、安装卸载等业务逻辑委托给 `EditorUtil.PlugPals`。当前窗口同时承载两套仓库视图：

- 外网仓库：参与 `全部 / SDK / 业务核心 / 框架核心 / 其他 / 已安装`
- 内部云仓库：参与 `全部 / 内部云仓库 / 已安装`

仓库地址在工具栏配置、落盘 `ProjectSettings/Nova/PlugPalsRegistries.json`（该文件 `.gitignore` 不入库）：

- 配置文件不存在时，公网与内网输入框分别填入 `upm.solotopiax.com` 和 `4874` 默认地址。
- 配置文件已存在时，两个输入框都严格采用存档 URL；空值也会保留，空地址表示不请求对应仓库。

## 文件拆分

| 文件 | 说明 |
|---|---|
| `PlugPalsWindow.cs` | `Open()`、`Open(bool)`、`OnEnable`、`OnGUI`、`OnDestroy` |
| `PlugPalsWindow.Visitors.cs` | 仓库地址、菜单路径、分类/筛选/样式状态 |
| `PlugPalsWindow.Methods.cs` | 请求包列表、过滤、绘制工具栏、表头、卡片与操作按钮 |
| `PlugPalsWindow.Definitions.cs` | 当前为空；历史上放内部数据类，现已迁到 `EditorUtil.PlugPals` |
| `PlugPalsMissingRequiredLibrariesWindow.cs` | 安装前依赖预检发现缺失库时弹出的购买/内部云仓库引导提示窗口 |

## 当前公开入口

```csharp
[MenuItem("Nova/Open PlugPals")]
public static void Open();

public static void Open(bool showInstalledOnly);

public static void OpenInternalRegistry();
```

- `Open()`：打开窗口并立即刷新包列表。
- `Open(bool)`：打开窗口并指定初始视图是否只看已安装包。
- `OpenInternalRegistry()`：打开窗口并直接进入“内部云仓库”页签，供缺失商业库提示窗口跳转使用。

## 当前窗口职责

- 并发调用 `EditorUtil.PlugPals.FetchRemotePackagesAsync(...)` 拉取外网仓库与内部云仓库数据；任一仓库先返回时立即展示其列表，不等待另一侧完成。
- 另一侧仍在加载时允许浏览已返回列表，但暂时禁用安装、升级、卸载和一键升级，避免依赖预检或 registry 清理使用不完整的仓库数据。
- 调用 `EditorUtil.PlugPals.BuildDisplayEntries(...)` 组装本地/远端版本对比结果。
- 提供搜索、分类筛选、内部云仓库筛选、已安装筛选。
- 触发 `EditorUtil.PlugPals.InstallPackage(...)` / `UninstallPackage(...)`。
- 为 `com.solotopia.nova.framework` 前缀的已安装包展示 Samples 导入和更新日志入口。
- 接收 `EditorUtil.PlugPals` 的缺失必须三方库提示跳转请求（`OpenInternalRegistry()`），但不承载缺库判定或 package 元数据解析逻辑。

## 当前筛选语义

- `全部`：展示外网仓库 + 内部云仓库全部条目。
- `SDK / 业务核心 / 框架核心 / 其他`：仅展示外网仓库对应分类条目。
- `内部云仓库`：仅展示内部云仓库条目。
- `已安装`：展示两侧仓库中当前已安装的全部条目。

进入“已安装”页后，列表顶部显示横向拉满的绿色“一键升级”按钮。当前可见列表没有可升级包时按钮禁用；点击并确认后，窗口会对当前列表中全部可升级包依次调用既有安装入口，统一复用依赖预检与延迟 UPM Resolve 机制。

一键升级的所有已接受目标还会共用一个本地重要声明批次。实际安装核验结束后，从这些包的 CHANGELOG 汇总 Important 到一个 [强制确认窗口](ImportantUpdatesWindow.md)，并按包和版本展开。单包操作只显示该包；失败目标不写已读。该状态保存在工具层，窗口重开或 domain reload 不会丢失，不新增手动查看重要声明的菜单。

所有表格条目在进入当前视图后都会重新按**显示名**做字母升序排序，不依赖仓库拉取顺序，也不依赖单仓库内部排序结果。

## 视觉区分

- 内部云仓库条目的包名使用浅橙色。
- 其余条目维持默认白色。
- 真正可升级的仓库包，其“最新版本”版本号以缓慢、平滑的彩虹色循环提示；未安装包和非仓库引用不会播放该动效。
- 详情区布局、按钮区结构、展开卡片格式与普通条目保持一致。
- 展开详情后，`Local Version` 与 `Latest Version` 只显示版本号。`Release Time` 独立显示最新版本的发布时间，位于 `Core Version` 下方、`Description` 上方；没有 `Core Version` 时紧跟 `Latest Version`。时间采用编辑器本地时区、`yyyy/MM/dd HH:mm:ss` 格式和 24 小时制。
- 优先读取最新版本 `package.json` 的 `nova.releaseTime`。旧包没有有效记录时，回退到所属仓库 `time[最新版本号]` 的上传时间；无法确定时显示 `未知`，不采用其他版本的时间。本地版本不展示时间，未安装、解析中、解析失败继续显示原有状态。
- 公网和内部云仓库使用同一套规则。时间查询在异步刷新阶段完成，单仓库最多并发 4 个元数据请求；单包时间读取失败不影响列表，也不参与安装和升级判断。

## 版本操作规则

- 仅当 `remote > local` 且当前包是 Verdaccio 仓库安装态时，按钮才显示 `升级`。
- `local == remote` 与 `local > remote` 都统一显示 `已安装`，不再因为版本字符串“不相等”误亮升级按钮。
- `file:` / `git:` / `http` 这类非仓库引用统一视为 `NonRegistry`：窗口允许 `卸载` / `UPM`，但不提供一键切换到仓库版的 `安装/升级`。
- lock 版本不等于安装成功。只有 Unity 已注册包、注册信息无错误、`resolvedPath/package.json` 有效且实际版本与 lock 一致时才显示 `已安装`。
- manifest 直接声明的 `file:` 开发包会校验真实目录及其 `package.json`；通过后按本地开发源显示，避免 Nova 主仓的 `Assets/Framework` 被误报为解析失败。
- manifest 或 lock 已记录目标包、但实际注册包缺失或版本不一致时显示红色 `解析失败`；registry 包提供 `重试`，且不进入一键升级集合。
- 任一仓库列表存在解析失败条目时，列表顶部显示包含包名的错误提示，避免只看“一键升级”按钮时误判为全部完成。
- 点击 `安装 / 升级 / 卸载` 后先显示 `解析中`；窗口只委托 `EditorUtil.PlugPals` 写入 `manifest.json`，真正的 UPM Resolve 会排队到下一帧执行并合并重复请求。只有后续刷新核验通过才转为 `已安装`。

## 关键边界

- `PlugPalsWindow` 是窗口层，不持有 Verdaccio 数据模型定义。
- 双仓库来源判断继续停留在窗口层；`EditorUtil.PlugPals` 仍保持单仓库工具层，不增加仓库来源字段。
- 包管理逻辑不应回流到窗口类，窗口只做调用与展示。
- 安装前依赖预检（`CheckDependencies`）、购买/内仓引导归 `EditorUtil.PlugPals`；窗口只负责打开”内部云仓库”页签（`OpenInternalRegistry()`）和展示 `PlugPalsMissingRequiredLibrariesWindow` 提示窗口。
- 因为代码位于 `Scripts/Editor/Windows/PlugPalsWindow/`，文档归类也应在 `Docs/Editor/Windows/`，而不是 `Tools/`。

## 关联文档

- [../Editor.md](../Editor.md)
- [../EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md](../EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md)
- [./CheckUpdateWindow.md](./CheckUpdateWindow.md)

## 窗口状态和操作边界

“已安装”筛选仅排 NotInstalled，仍可包含 Pending/Failed；解析失败警告当前按显示名优先去重，同显示名不同包可能折叠。一键升级仅当前搜索可见项，不去重双仓同名，false继续、异常停止，已提交前项不回滚；数量是提交数，不是最终安装数。单侧请求失败后另一侧列表仍可开放操作，known映射不完整，不等于 Agent Plan 的双侧完整门。

窗口 URL 可在已有 fetched 列表时编辑，安装/日志再读当前 URL，旧列表不绑定原来源。只按引用认内部条目，复制或旧条目默认外网；两侧都空也可能被当请求失败。取消仅处理列表 fetch，日志用 None token 且关闭不取消；async完成没有全部窗口/请求身份保护。

按钮忽略 Install 的 bool，工具实际写 Pending 后无自动 refetch/Resolve后验；错误可能只存 m_ErrorMessage 而已有列表区域不显示。折叠行手动赋 GUI.enabled，未完全合并父禁用态，与展开动作保护不同；没有整体 Play 禁用。文档中的状态应结合真实刷新结果，不把 UI灰显或无错误文案当安装保证。

依赖缺失窗口只展示元数据购买/打开仓库入口，不自动购买或安装。日志入口当前不限 Framework，取远端 latest优先；Samples 仍通过原 Sample API覆盖导入，未重新冻结来源与目录写集。
