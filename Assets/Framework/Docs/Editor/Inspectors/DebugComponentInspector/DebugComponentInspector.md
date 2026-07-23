# DebugComponentInspector

**类签名**：`[CustomEditor(typeof(DebugComponent))] internal sealed partial class DebugComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.DebugComponent`

当前 `DebugComponentInspector` 只负责三块内容：

- `IDebugManager` 实现类选择
- `DebuggerActiveType` 与 `MaximumConsoleEntries` 配置
- 磁盘监控 + Android 构建安装工具

旧版文档里的“悬窗皮肤”“日志过滤贴图”“设备性能分级”已经不属于当前实现。

Debug Inspector 仍只配置 `DebugComponent` 的激活策略、日志条数、Manager 和磁盘检测。内置调试器的 Editor 辅助工具已随 Framework 内置，不再要求安装独立调试器 UPM 包。

## 当前文件表

| 文件 | 说明 |
|---|---|
| `DebugComponentInspector.cs` | `OnEnable` 绑定属性，`OnInspectorGUI` 依次绘制配置、磁盘监控、Android 构建安装 |
| `DebugComponentInspector.Visitors.cs` | `m_DebuggerActiveType`、`m_MaximumConsoleEntries`、`m_CurManagerTypeName`、`m_DiskCheckingConfigs` 及 Android 本地缓存字段 |
| `DebugComponentInspector.Methods.cs` | `DrawConfigs`、`DrawDiskMonitoring`、`DrawAndroidBuild`、`DrawTextFieldWithWrite`、`DrawToolPathRow`、`ReadConfig`、`WriteConfig`、`ResetConfigToDefault`、`BuildAAB` / `BuildAPK`、`InstallAABToDevice` / `InstallAPKToDevice`、`RunPython` |

## 当前 Inspector 结构

### 基础配置

- `Debug 管理器`：从 `IDebugManager` 实现类型列表中选择
- `Debugger 激活类型`：绑定 `m_DebuggerActiveType`
- `Console 最大日志条数`：绑定 `m_MaximumConsoleEntries`

### 磁盘监控

- 绑定 `m_DiskCheckingConfigs`
- 按平台配置 `Enabled`
- 配置剩余空间阈值与对应轮询间隔

### 构建安装（Android）

- 签名文件与密码（`SignaturePath` / `SignaturePass` / `SignatureAlias` / `SignatureAliasPass`）
- `bundletool` 路径（Unity 安装目录 `Editor/Data/PlaybackEngines/AndroidPlayer/Tools` 下）
- `adb` 路径（Android SDK `platform-tools` 目录下）
- `编译时清空缓存` Toggle（写入 `BuildOptions.CleanBuildCache`）
- 构建 `AAB` / `APK`（默认输出至 `<ProjectRoot>/Build/Android/{AAB|APK}/`，文件名为 `{productName}_{version}.{aab|apk}`）
- 安装现有包到已连接设备（调用 Python 脚本 `install_aab.py` / `install_apk.py`）
- 构建并安装到设备（先 Build，成功后立即调用对应安装脚本）
- AAB 安装链路通过 `bundletool` 将 `.aab` 转成 `.apks` 后用 `adb` 安装
- 当签名四元组或 `bundletool` / `adb` 路径任一缺失时，构建/安装按钮整体 DisabledGroup 灰显

### 路径与缓存

- AAB 默认输出目录：`<ProjectRoot>/Build/Android/AAB`
- APK 默认输出目录：`<ProjectRoot>/Build/Android/APK`
- 安装脚本路径：`Packages/com.solotopia.nova.framework/Scripts/Editor/Tools/Deploys/install_aab.py` 与 `install_apk.py`
- `HistoryAABPath` / `HistoryAPKPath` 仅作为“打开文件面板”时的初始目录，选择其他目录后立即写回缓存

## 本地配置存储

Android 构建辅助配置不会序列化到组件，而是写入：

`Library/Nova/DebugConfig.json`

默认键包括：

- `BundleToolPath`
- `ADBPath`
- `HistoryAABPath`
- `HistoryAPKPath`
- `SignaturePath`
- `SignaturePass`
- `SignatureAlias`
- `SignatureAliasPass`
- `ClearCache`

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [DebugComponent.md](../../../Runtime/Modules/Debug/DebugComponent.md)
- [EditorUtil.ProcessRunner.md](../../EditorUtil/EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
- [EditorUtil.FileSystem.md](../../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
