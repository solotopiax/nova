# AppComponentInspector

**类签名**：`internal sealed partial class AppComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`

App 组件编辑器面板定制，绘制 Manager 选择器与三组 Foldout 配置（版本检查 / 更新规则 / 更新下载）。
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
───────────────────────────────────────────────────────────
Foldout "版本检查"（SessionState key: AppVersionCheckGroup）
  ├── 版本检查URL-Debug（Property，缩进 16f）
  ├── 版本检查URL-Debug（备用）
  ├── 版本检查URL-Release
  ├── 版本检查URL-Release（备用）
  └── HelpBox：当前节点 DevelopMode 决定用哪一组；主地址失败或空内容时切备用，主备都不可用时返回 NoDownload

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
