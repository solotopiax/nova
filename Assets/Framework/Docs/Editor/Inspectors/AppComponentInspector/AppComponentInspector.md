# AppComponentInspector

**类签名**：`internal sealed partial class AppComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`

App 组件编辑器面板定制，绘制 Manager 选择器、App 更新总开关与三组 Foldout 配置（版本检查 / 更新规则 / 更新下载）。
所有字段上方会先显示一条只读 `DevelopMode` 场景快照标签，由 `BaseComponentInspector` 统一绘制。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Editor/Inspectors/AppComponentInspector/AppComponentInspector.cs` | `AppComponentInspector` | OnEnable（属性绑定）+ OnInspectorGUI |
| `Editor/Inspectors/AppComponentInspector/AppComponentInspector.Visitors.cs` | `AppComponentInspector` | SerializedProperty 字段 + 类型列表 |
| `Editor/Inspectors/AppComponentInspector/AppComponentInspector.Methods.cs` | `AppComponentInspector` | DrawConfigs（三 Foldout 绘制） |

---

## §3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract)
        └── AppComponentInspector (internal sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_CurManagerTypeName` | `SerializedProperty` | IAppManager 实现类全名 |
| `m_EnableAppUpdate` | `SerializedProperty` | App 更新功能总开关，默认关闭 |
| `m_AppDownloadCheckUrlDebug` | `SerializedProperty` | Debug 主版本检查地址 |
| `m_AppDownloadCheckUrlFallbackDebug` | `SerializedProperty` | Debug 备用版本检查地址 |
| `m_AppDownloadCheckUrlRelease` | `SerializedProperty` | Release 主版本检查地址 |
| `m_AppDownloadCheckUrlFallbackRelease` | `SerializedProperty` | Release 备用版本检查地址 |
| `m_TimeoutSeconds` | `SerializedProperty` | 超时秒数（默认 5） |
| `m_DownloadRoute` | `SerializedProperty` | 更新下载路由（Store/Apk） |
| `m_AndroidStoreUrl` | `SerializedProperty` | Android 商店地址 |
| `m_AppStoreUrl` | `SerializedProperty` | iOS 商店地址 |
| `m_PrimaryDownloadUrl` | `SerializedProperty` | APK 主下载地址（当前启动期必填项） |
| `m_FallbackDownloadUrl` | `SerializedProperty` | APK 备用下载地址（可选，当前启动期不校验） |
| `m_UseRecommendedDownloadRule` | `SerializedProperty` | 推荐更新规则开关 |
| `m_UseForcedDownloadRule` | `SerializedProperty` | 强制更新规则开关 |
| `m_AppManagerTypeNames` | `List<string>` | OnEnable 时扫描 IAppManager 所有实现类名称 |

---

## §5 完整公开 API

```csharp
// 注册 CustomEditor 绑定到 AppComponent
[CustomEditor(typeof(AppComponent))]

// 启用：绑定所有 SerializedProperty + 扫描 IAppManager 实现类型
protected override void OnEnable()

// 绘制：base.OnInspectorGUI() → DrawConfigs() → FinalRefreshInspectorGUI()
public override void OnInspectorGUI()
```

---

## §6 Inspector 布局

```
[顶层] App 管理器（TypesSelector，GUILayout.Width(180f)）
       HelpBox：自定义 IAppManager 说明
       启用 App 更新（总开关）
       HelpBox：关闭后跳过 App 大版本检查；Asset 热更新不受影响
───────────────────────────────────────────────────────────
[DisabledScope: 总开关关闭时以下三组整体灰显]
  Foldout "版本检查"（SessionState key: AppVersionCheckGroup）
  ├── 版本检查-模板文件位置（DrawTemplatePathHintReadOnlyOpenFolderOnly，缩进 16f）
  ├── 版本检查URL-Debug（Property，缩进 16f）
  ├── 版本检查URL-Debug（备用）
  ├── 版本检查URL-Release
  ├── 版本检查URL-Release（备用）
  └── HelpBox：按模板生成 JSON 并上传 CDN；DevelopMode 决定用哪一组；支持四项 URL 占位符；主备都不可用时返回 NoDownload

  ├── 版本检查超时（秒）（Property，缩进 16f）
  └── HelpBox：弱网说明 + 推荐值 5

───────────────────────────────────────────────────────────
  Foldout "更新规则"（SessionState key: AppUpdateRuleGroup）
  ├── 启用推荐更新规则（Toggle，缩进 16f，各带 HelpBox）
  └── 启用强制更新规则（Toggle，缩进 16f，各带 HelpBox）

───────────────────────────────────────────────────────────
  Foldout "更新下载"（SessionState key: AppDownloadGroup）
  ├── 更新下载方式（Property EnumPopup）
  ├── [DisabledScope: Apk 时灰] Android/iOS 商店地址
  │   └── 仅当前平台对应的商店地址需要配置
  └── [DisabledScope: Store 时灰] 主/备下载地址
      └── APK 下载地址仍独立于版本检查主备 URL

```

---

## §8 CDN 热更版本检查流程

App 模块的大版本检查依赖 CDN 上的一份 JSON 配置，整个链路在 Inspector 内完成配置：

```
[1] 模板：DrawTemplatePathHintReadOnlyOpenFolderOnly(c_AppDownloadRulesTemplateFileName)
    模板文件名 = AppDownloadRulesTemplate.json
    业务侧按模板生成版本检查 JSON（含推荐版本号 / 强制版本号 / 下载地址等字段）

[2] 上传：将生成的 JSON 上传到 CDN，分别得到 Debug / Release 两组可访问 URL；可通过「Config全局配置中心 - CDN 内容分发网络部署」或「Pipify 自动化管线编排中心 - 添加步骤」自动上传
    填入 m_AppDownloadCheckUrl{Debug,Release}（主）与 m_AppDownloadCheckUrlFallback{Debug,Release}（备）

[3] 启动：运行时按当前 DevelopMode 选择对应组的主地址发起检查
    主备 URL 均支持 `{Platform}` / `{Channel}` / `{Package}` / `{Version}`
    四项语义与 Asset 主机服务器 URL 一致：运行平台 / Config 导出渠道 / 默认资源包名 / Application.version
    主地址失败或返回空内容时自动切到备用地址
    主备均不可用时本次大版本检查直接返回 NoDownload

[4] 超时：m_TimeoutSeconds（秒）作用于版本检查请求
    推荐值 5；过短易在弱网环境下误判失败

[5] 规则命中：比对本地版本号与 CDN JSON 中的推荐 / 强制版本号
    启用推荐更新规则（m_UseRecommendedDownloadRule）
      本地版本号 < CDN 推荐版本号 → 弹推荐更新提示，用户取消后继续热更检查与后续启动
    启用强制更新规则（m_UseForcedDownloadRule）
      本地版本号 < CDN 强制版本号 → 弹强制更新提示，用户操作被锁定无法跳过
    两规则可任意组合启用，同时命中时优先级：强制 > 推荐

[6] 下载：命中任一规则后统一走 m_DownloadRoute 指定的下载方式
    Store：跳转应用商店（m_AndroidStoreUrl / m_AppStoreUrl，仅当前平台对应字段必填）
    Apk：App 内下载 APK 文件（m_PrimaryDownloadUrl 启动期必填，m_FallbackDownloadUrl 可选）
    Apk 模式下版本检查命中规则时会校验 m_PrimaryDownloadUrl 非空
```

---

## §11 使用示例

```csharp
// 无需手动操作，[CustomEditor] 属性自动挂载到 AppComponent Inspector
// Inspector 中选择 Manager 实现类，填写配置即可
```

---

## §13 关联文档

- [../BaseComponentInspector.md](../BaseComponentInspector.md)
- [../../../Runtime/Modules/App/AppComponent.md](../../../Runtime/Modules/App/AppComponent.md)
- [../../../Runtime/Modules/App/AppManager/IAppManager.md](../../../Runtime/Modules/App/AppManager/IAppManager.md)
- [../../../Runtime/Modules/App/Definitions/AppDownloadRoute.md](../../../Runtime/Modules/App/Definitions/AppDownloadRoute.md)
