# EditorUtil.CDN

**类签名**：`public static partial class CDN`（`EditorUtil` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.CDN`

CDN 内容部署与缓存清理工具；把 ConfigMasterSO 中的 `CDNEditorConfigs` 作为输入，将本地目录经阿里云 OSS SDK 顺序上传到配置的 Bucket / 前缀，并可调用 Cloudflare purge API 按批清理缓存 URL。编排层（路径解析、上传计划、批次拆分、失败即停、脱敏）与传输适配器（OSS、HTTP）分离，便于测试注入。主要调用方是 ConfigWindow 的「CDN 内容分发网络部署」面板与 Pipify 的 `cdn.deploy` / `cdn.purge` Step。

阿里云 OSS 传输是可选的 Editor 能力：Framework 不再强依赖 `com.solotopia.alibabacloud.oss`，由 `NovaFramework.Editor.asmdef` 的 `NOVA_ALIBABACLOUD_OSS` version define 控制强类型调用。未安装该包时，`DeployAsync` / `DeployAssetCheckWhitelistAsync` 返回明确的安装错误；ConfigWindow 显示安装引导并只禁用两项 OSS 部署操作，`PurgeAsync` 与 Cloudflare 区仍可用。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.cs` | `EditorUtil.CDN` | 编排层：OSS 位置 / Region 解析、路径占位符替换、Object Key 组装、上传计划构建、缓存 URL 解析与分批、`DeployAsync` / `PurgeAsync` 核心循环、配置校验、Secret 脱敏 |
| `Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.LatestVersion.cs` | `EditorUtil.CDN` | YooAsset 有效版本目录识别与最新本地版本解析 |
| `Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.AlibabaCloud.cs` | `EditorUtil.CDN` | 可选阿里云 OSS 适配器：包存在时构造 OSS Client 并逐文件 `PutObject`；缺包时保留同一入口并返回安装错误 |
| `Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.Cloudflare.cs` | `EditorUtil.CDN` | Cloudflare 适配器：公开 `PurgeAsync` 重载，复用静态 `HttpClient`（30s 超时）逐批 POST |
| `Editor/EditorUtil/EditorUtil.CDN/EditorUtil.CDN.Models.cs` | `EditorUtil.CDN` | 不可变路径 / 结果模型：`OssLocation`、`OssUploadItem`、`CloudflareHttpResult`（均 internal） |

---

## §5 完整公开 API

> 对外暴露的真实入口只有两个重载：`DeployAsync` 与 `PurgeAsync`。其余解析 / 组装 / 分批方法均为 `internal`，仅供编排层与测试共用，业务侧不可见。

```csharp
// 使用阿里云 OSS SDK 将本地目录顺序部署到配置的 Bucket 与前缀
// 先静态校验 config（Endpoint / PresetOSSPath / AccessKeyID / AccessKeySecret），再按 BuildUploadPlan 逐文件上传
// config 为 null 抛 ArgumentNullException；静态字段非法抛 ArgumentException；单文件上传失败抛 InvalidOperationException（首个失败即停）
// onProgress 参数依次为完成数、总数、当前本地文件；上传前回调一次 (0, total, 首文件)，每文件成功后回调 (index+1, total, 该文件)
// <returns>成功上传文件数（等于计划总数）</returns>
internal static UniTask<int> DeployAsync(
    CDNEditorConfigs config,
    string projectRoot,
    Action<int, int, string> onProgress);

// 使用 Cloudflare API 按批清理配置中的缓存 URL
// 先静态校验 config（ZoneID 有效、API Token 非空），再按 100 条 / 批逐批 POST
// 请求地址固定构造为 https://api.cloudflare.com/client/v4/zones/{ZONE_ID}/purge_cache；旧 PurgeURL 仅作兼容迁移
// config 为 null 抛 ArgumentNullException；静态字段非法抛 ArgumentException；任一批失败抛 InvalidOperationException（首个失败即停）
// onProgress 参数依次为完成批数、总批数；发送前回调一次 (0, 总批数)，每批成功后回调 (index+1, 总批数)
// <returns>成功清理 URL 数量（等于去重后总条数）</returns>
internal static UniTask<int> PurgeAsync(
    CDNEditorConfigs config,
    Action<int, int> onProgress);
```

> 注：两个重载为 `internal`。程序集内（如 ConfigWindow）可直接调用；程序集外业务如需触发，应经 ConfigWindow 面板按钮而非直接引用。

---

## 关键行为与坑

- **路径占位符**：`VersionCheckLocalFilePath`、`VersionCheckRemoteFilePath`、`LocalDirectory` 与 `RemotePathSuffix` 支持大小写敏感的 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`。此 Editor 部署链分别取 Unity 当前 Active BuildTarget 映射的 `PlatformType`、`ConfigMasterSO.CurrentChannel`、Nova.prefab 上 AssetComponent 的默认资源包名（空时回退包列表首项）、`Application.version`；配置保存模板原文，构建上传计划时统一解析，未知占位符保持原样。它不改变 Runtime `AssetRemoteService` 启动期由编译宏解析 `{Platform}` 的契约。
- **OSS Object Key 组装规则**：`PresetOSSPath`（`oss://bucket-name/fixed/prefix`）解析出 Bucket 与固定前缀。热更资源拼接已解析的 `RemotePathSuffix` 与本地相对路径；版本检查文件在本地与云端位置均非空时，以 `VersionCheckRemoteFilePath` 作为完整远端文件位置并合并进同一上传计划。各段经 `NormalizeObjectKeyPart` 规整（反斜杠转正斜杠、去首尾分隔符、合并重复分隔符），空段被剔除。
- **自动关联最新版本**：`AutoLinkLatestVersion` 默认开启。`LocalDirectory` 可以指向包根或任一版本目录；候选目录必须具备内容匹配目录名的 `.version` 文件及对应 `.bytes`、`.hash`、`.report`，report 引用的全部 bundle 文件也必须存在，以排除复制阶段失败的半成品。YooAsset 不规定 `PackageVersion` 的比较语义，因此按 `.version` 的 `LastWriteTimeUtc` 选择；多个候选时间完全相同时明确报歧义，不使用版本字符串决胜。窗口展示和部署前分别解析，确保新构建完成后无需修改配置。
- **白名单三文件自动关联**：`AutoLinkLatestAssetCheckVersionFiles` 默认开启且独立于热更资源开关。以已配置 `.bytes` 文件的父目录为锚点复用完整版本选择规则，再调用 `YooAssetConfiguration.GetManifestBinaryFileName`、`GetPackageHashFileName`、`GetPackageVersionFileName` 生成当前 `PackageFilePrefix`、包名和资源版本匹配的三个路径；窗口只读展示，部署前重新解析。ConfigWindow 与 Pipify 在解析前均注入当前 ConfigMaster 的 YooAssetSettings。
- **本地目录边界**：`LocalDirectory` 先解析占位符，再视为项目根相对路径；经 `GetFullPath` 后必须仍位于项目根内（防越界到根外目录），目录不存在或无任何文件直接抛 `ArgumentException`。自动和手动部署都会拒绝路径组件或目录树中的 symlink/junction，防止词法上位于项目内的链接指向外部。所有此类错误都包含解析后的实际文件或目录路径，ConfigWindow 会把同一信息写入日志并显示在失败对话框。
- **递归枚举 + 稳定排序**：`Directory.GetFiles(..., AllDirectories)` 全量递归，按相对路径（`StringComparer.Ordinal`）升序排序，保证多次部署的计划顺序稳定一致。
- **可选部署前清理**：默认仍只用 `PutObject` 同 Key 覆盖。调用方显式开启后，先构建并校验完整上传计划，再删除计划内所有精确 Object Key，并分页列举、批量删除本次远端目录前缀下的遗留对象，最后上传。目录前缀强制以 `/` 结尾，避免 `release/1` 匹配 `release/10`；远端目录为空时直接拒绝，禁止清空整个 `PresetOSSPath`。
- **Cloudflare URL 解析**：`CachePaths` 按英文逗号 `,`、英文分号 `;` 或换行（`\r` / `\n`）分隔，逐条 Trim 后必须是绝对 HTTP/HTTPS URL，按首次出现顺序去重（`StringComparer.Ordinal`）；全空或含非法 URL 抛 `ArgumentException`。
- **分批上限**：每批最多 100 条，保持原顺序切批。
- **首个失败即停**：列举、删除、上传或清缓存均在首个失败处抛出 `InvalidOperationException` 并中止；清理失败时不会上传任何文件，进度条停留在失败点。
- **Cloudflare 成功判定**：HTTP 2xx 之外还解析响应正文 `success` 字段；空正文或非法 JSON 一律按失败处理。失败时从响应正文截取最多 1024 字符作为错误摘要。
- **Secret / Token 脱敏**：所有对外抛出的错误文本与响应摘要都会把非空的 `AccessKeySecret`、`Token` 原文替换为 `***`，避免对话框与日志泄露。注意 `CDNEditorConfigs` 在 ConfigMasterSO 资产中仍以明文序列化，脱敏只针对输出不代表存储加密。
- **静态校验前置**：`ValidateOssConfig` / `ValidateCloudflareConfig` 在发起任何网络请求前集中校验所有静态字段，让格式类错误（Endpoint 非标准地域域名、PresetOSSPath 非 oss:// 格式、Zone ID 非法等）在首个请求前暴露。Cloudflare API Token 需要 `Zone -> Cache Purge` 权限。
- **UniTask 异步**：两个入口均返回 `UniTask<int>`，在 Editor 上以 `async UniTask` / `.Forget()` 驱动；ConfigWindow 侧用 `m_IsCdnDeploying` / `m_IsCdnPurging` 标志在按钮入口处做**重复点击保护**（进行中直接忽略），该保护在调用方而非 `EditorUtil.CDN` 内部。
- **执行期配置快照**：ConfigWindow 在点击时通过 `DimensionalResolver.ResolveCDNEditorConfigs` 按当前维度坐标 Resolve 出独立 `CDNEditorConfigs` 快照再传入；该坐标的 Platform 实时映射 Unity Active BuildTarget，执行期间继续编辑面板不影响本次请求。
- **白名单分路径部署**：`VersionsCheckWhiteList.json` 使用包含 `.json` 文件名的完整 `AssetCheckWhitelistRemoteFilePath`，三个 YooAsset 版本文件使用 `AssetCheckVersionRemoteDirectory`。配置文件位置为空、不是 JSON 文件、使用绝对 URI、父级路径或含查询/片段时跳过 JSON，不回退到版本文件目录，也不阻断三个版本文件。
- **Pipify 路径覆盖**：`cdn.deploy` 同样按当前维度 Resolve 独立快照，其中 Platform 实时映射 Unity Active BuildTarget；用 Step 参数覆盖版本检查文件与热更资源目录四个路径，并用默认开启的 `AutoLinkLatestVersion` 控制本次执行是否从目录锚点关联最新完整版本；`CleanRemoteFilesAndDirectories` 也只对本次执行生效，均不回写 `ConfigMasterSO`。OSS 凭据、Endpoint 与 `PresetOSSPath` 始终来自 Config。`cdn.whitelist.deploy` 提供同名自动关联开关并映射到白名单三文件的 `AutoLinkLatestAssetCheckVersionFiles`，同样只覆盖单次快照。
- **Pipify 缓存清理覆盖**：`cdn.purge` 按当前维度 Resolve 独立快照，其中 Platform 实时映射 Unity Active BuildTarget；用 Step 参数覆盖 `ZoneID`、`Token` 与 `CachePaths`，不回写 `ConfigMasterSO`；随后复用同一 `PurgeAsync` 校验、分批、失败即停和脱敏链路。

---

## §11 使用示例

```csharp
// 以 ConfigWindow 「CDN 内容分发网络部署」面板为参考的真实调用方式：
// 从当前激活 master 按当前维度坐标 Resolve 出配置快照；CurrentPlatform 实时映射 Unity Active BuildTarget，再交 EditorUtil.CDN 执行
ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
CDNEditorConfigs config = EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
    master,
    master.CurrentPlatform,
    master.CurrentChannel,
    master.CurrentDevelopMode);
string projectRoot = Directory.GetParent(Application.dataPath).FullName;

// 1) 部署本地目录到阿里云 OSS
int uploaded = await EditorUtil.CDN.DeployAsync(
    config,
    projectRoot,
    master.CurrentPlatform,
    master.CurrentChannel,
    (completed, total, path) => EditorUtility.DisplayProgressBar(
        "批量部署到 CDN",
        $"{completed}/{total}  {path}",
        total > 0 ? completed / (float)total : 0f));
EditorUtility.ClearProgressBar();
Debug.Log($"已上传 {uploaded} 个文件。");

// 2) 清理 Cloudflare 缓存
int purged = await EditorUtil.CDN.PurgeAsync(
    config,
    (completed, total) => EditorUtility.DisplayProgressBar(
        "批量清除 CDN 缓存",
        $"{completed}/{total}",
        total > 0 ? completed / (float)total : 0f));
EditorUtility.ClearProgressBar();
Debug.Log($"已清理 {purged} 条缓存路径。");
```

---

## §13 关联文档

- [ConfigWindow.md](../../Windows/ConfigWindow.md)（主要调用方：「CDN 内容分发网络部署」面板，含重复点击保护与进度条接入）
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)（`CDNEditorConfigs` 字段来源与保存语义；`AccessKeySecret` / `Token` 明文存储说明）
- [EditorUtil.Config.DimensionalResolver.md](../EditorUtil.Config/EditorUtil.Config.DimensionalResolver.md)（`ResolveCDNEditorConfigs`：按维度坐标 Resolve 出本次执行生效的 `CDNEditorConfigs` 快照）
- [Cloudflare Purge Cached Content](https://developers.cloudflare.com/api/resources/cache/methods/purge/)（purge API 与单次请求规则）
- [Cloudflare Find account and zone IDs](https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/)（Zone ID 查询）
- [Cloudflare Create API token](https://developers.cloudflare.com/fundamentals/api/get-started/create-token/)（API Token 创建）
