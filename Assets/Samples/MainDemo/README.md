# Nova Framework — MainDemo

本工程是 Nova Framework 主框架对外接口的**可运行参考**。不是项目模板，但对新项目有非常有价值的参考意义——你可以自由复制粘贴到自己的工程。

> 设计决策锚点：[PAT-77](../Framework/Minds/2-Areas/Patterns/PAT-77-base-demo-view-three-zone-template.md)（三段式模板）/ [PAT-78](../Framework/Minds/2-Areas/Patterns/PAT-78-sample-demo-full-flow-sop.md)（18 步 SOP）/ ADR-033（sample 自闭包）

---

## 整体拓扑

```
DemoNavTreeView（树形导航主菜单）
├─ 1.Core 核心库（9 个叶子）
│   ├─ 1.1 DemoFrameworkComponentView
│   ├─ 1.2 DemoFrameworkManagerView
│   ├─ 1.3 DemoFsmView
│   ├─ 1.4 DemoReferenceView
│   ├─ 1.5 DemoLogView
│   ├─ 1.6 DemoUtilView
│   ├─ 1.7 DemoCollectionsView
│   ├─ 1.8 DemoExtensionsView
│   └─ 1.9 DemoEdgeCasesView
├─ 2.Modules 模块库（16 个叶子）
│   ├─ 2.1  DemoAppView
│   ├─ 2.2  DemoAssetView
│   ├─ 2.3  DemoPrefabView
│   ├─ 2.4  DemoConfigView
│   ├─ 2.5  DemoEventView
│   ├─ 2.6  DemoTableView
│   ├─ 2.7  DemoLocalizationView
│   ├─ 2.8  DemoUIView
│   ├─ 2.9  DemoNetworkView
│   ├─ 2.10 DemoProcedureView
│   ├─ 2.11 DemoObjectPoolView
│   ├─ 2.12 DemoPersistView
│   ├─ 2.13 DemoSoundView
│   ├─ 2.14 DemoVibrateView
│   ├─ 2.15 DemoSDKView
│   └─ 2.16 DemoDebugView
├─ 3.HybridCLR 运行时热更新（3 个叶子）
│   ├─ 3.1 DemoHybridClrAotMetadataView
│   ├─ 3.2 DemoHybridClrGameDllView
│   └─ 3.3 DemoHybridClrProcedureRegisterView
└─ 4.Integration 跨模块联动（5 个叶子）
    ├─ 4.1 DemoIntegrationUiLocalizationView
    ├─ 4.2 DemoIntegrationUiAssetView
    ├─ 4.3 DemoIntegrationProcedureAssetView
    ├─ 4.4 DemoIntegrationEventNetworkView
    └─ 4.5 DemoIntegrationConfigHybridClrView
```

共 33 个演示子页面，均继承 `BaseDemoView`（三段式：标题栏 + 交互区 + 反馈区），通过 `Nova.UI.OpenUIViewAsync<T>()` 打开，`PauseCoveredUIView=false` 保持导航菜单始终可见。

另有 2 个辅助子页面（不计入树形导航 33 叶）：
- `DemoToastView`：DemoUIView / DemoIntegrationUiLocalizationView / DemoIntegrationUiAssetView 内部 spawn 的轻量子页面。
- `DemoDialogView`：DemoUIView 内部 spawn 的带按钮对话框子页面。

---

## 目录结构

| 路径 | 说明 |
|---|---|
| `Scripts/Runtime/UIs/` | 33 个 DemoXxxView 脚本 + BaseDemoView 基类 + DemoNavTree |
| `Scripts/Runtime/DataTypes/` | Tables / Sounds / Vibrates / Localizations / Networks 数据类型 |
| `Scripts/Runtime/Procedures/` | 业务侧 Procedure 实现 |
| `Scripts/Editor/` | 业务侧 Inspector / EditorWindow |
| `Prefabs/UIs/` | DemoNavTreeView prefab（33 个 DemoXxxView prefab 待 Prefab 期落地） |
| `Configs/` | 框架 ScriptableObject 配置示例 |
| `Jsons/` | 多语言 / 表格 JSON 资源 |
| `Resources/BuiltIn/Prefabs/` | Launcher 启动期 UI Prefab（SplashPanel / ProgressPanel / DialogPanel） |
| `Fonts/` | 演示用字体资源 |

详细 demo 索引（编号 / 类名 / 变体 / API / 资源依赖）见 [`DEMO-INDEX.md`](DEMO-INDEX.md)。

---

## 运行方式

### 自动（推荐）

导入 Demo 后，Editor 会自动弹窗：

1. **检测旧版本** — 若工程已有其他版本的 Nova Framework Demo，提示一键清理。
2. **设置启动场景** — 询问是否将 `MainDemo.unity` 设为 Build Settings 第一项。

按提示一路确认即可，然后点 Unity Editor Play 按钮。

### 手动

1. `File > Build Settings...`，把 `Assets/Samples/Nova Framework/<version>/Demo/MainDemo.unity` 拖到 `Scenes In Build` 第一项。
2. 点 Play。

---

## 贡献流程

新增 demo 子页面须严格遵循 PAT-78 全流程 18 步 SOP：

1. **设计期（步 1-3）**：读 PAT-65 覆盖矩阵 → 确认 ADR-033 自闭包 → 按 PAT-77 模板产出 DemoXxxView 设计稿，更新 [`DESIGN.md`](DESIGN.md)。
2. **源数据期（步 4-6）**：在对应模块 xlsx 追加 `Demo_` 前缀数据行 → Pipify 导出 → read_console 确认编译。
3. **资源期（步 7-9）**：确认 UI 一律纯色块 + TMP，无需搬运 Sprite → 若有音频搬运到 `Sounds/` → 登记 BundleCollectorSetting（Asset 地址格式 `{CollectDirName}/{FileName}`，详 PAT-80）。
4. **代码期（步 10-13）**：按 PAT-77 派生 DemoXxxView → UI Excel 追加注册行 → DemoTreeData.cs 追加叶子节点。
5. **Prefab 期（步 14-16）**：UnityMCP 串行 clone BaseDemoView.prefab → 绑定字段 → read_console 确认 Missing Reference 为零。
6. **验证期（步 17）**：Play Mode 全链路验证（树形菜单点叶子 → 子页面打开 → 反馈区出日志 → X 关闭）。
7. **文档期（步 18）**：更新对应模块 L2 md 末尾「Sample 演示位置」节 + 更新本 README 索引表。

---

## 升级注意

UPM 标准行为：升级 Nova Framework 后，Package Manager 重新导入 Demo 会**新建** `Assets/Samples/Nova Framework/<新版本>/Demo/`，旧版本目录**不会**自动删除。

- 旧版 asmdef 名称与新版冲突，工程会编译报红。
- SampleVersionGuard 会在 Editor 启动时检测并提示一键清理。
- 也可手动 `rm -rf "Assets/Samples/Nova Framework/<旧版本>/"`。

---

## 反馈

如果 Demo 内某个接口示例有错或不易理解，请反馈到 Nova Framework 主仓库 issue。
