# 环境检测入口（EnvironmentWindow）

环境检测位于 `ConfigWindow`，打开路径为 **Nova → Open Config → 环境检测**。当前没有 `EnvironmentWindow` 类或独立菜单；本文保留这个文档路径作为入口导航。

## 打开与重检

左树提供 Luban、Python3、HybridCLR 三个面板。首次默认选中 Luban，未绑定 `ConfigMasterSO` 时也能使用环境检测。

1. 查看对应面板的状态与安装指引。
2. 在系统或 Unity 中完成安装、配置。
3. 回到面板点击“重新检测”，读取新的结果。

`Check()` 优先读取 Unity `SessionState` 缓存，缓存跨程序集域重载保留，在 Editor 会话结束时清空。系统 PATH、安装文件或包状态发生变化不会自动使它失效；“重新检测”调用该检查器的 `Recheck()`，只清自己的缓存并同步探测，不修改 ConfigMaster 或安装环境。

Luban / Python3 的首次检测和重新检测会同步执行外部命令，不能视为后台任务。Python 单次候选超时为 3 秒，多候选等待会累加；dotnet 版本探测使用 `ProcessRunner` 的默认 120 秒超时。

## 三类状态的含义

| 面板 | 当前检查 | 未就绪时的处理与边界 |
|---|---|---|
| Luban | dotnet 可执行性、版本建议区间 `[8.0.127, 10.0.203]`，以及 Luban.dll 是否存在；SDK 和 DLL 独立显示 | 按面板指引安装后重检。仅 SDK 版本越界时，现有 Luban Pipeline 告警后继续；缺少 DLL、找不到或无法执行 dotnet 时阻断导出并打开 Luban 面板。DLL 存在不证明导出成功 |
| Python3 | 按固定候选路径、PATH、Windows `py -3` / `where python3` 和 `python` 回退逐项运行版本命令；只接受 Python 3.x | 安装 Python3 后重检。就绪只证明某个候选命令返回版本，没有 Python 3.9 下限，也不证明业务脚本依赖齐全 |
| HybridCLR | 固定 `Packages/com.solotopia.hybridclr/package.json` 与 `HybridCLRData/LocalIl2CppData-{Application.platform}/il2cpp/libil2cpp/hybridclr/` 目录存在 | 按面板指引检查包与 `HybridCLR → Installer...`。版本读取失败仍可能就绪；固定 Packages 路径可能漏掉 PackageCache 布局，存在性检查不验证 LocalIl2Cpp 版本、文件完整性或实际构建 |

Luban 的启动静默检查通过 `EditorApplication.delayCall` 调度，未就绪时只写 Warning；它不会安装环境或自动打开窗口。日志中的旧 `Nova/Luban 环境检查` 文案不是实际菜单，应使用上面的 `Nova/Open Config` 路径。

## 实现与调用入口

| 职责 | 当前位置 |
|---|---|
| 打开窗口、默认选中项、环境检测 UI | `Scripts/Editor/Windows/ConfigWindow/`；`ConfigWindow.Open()` 与 `OpenLubanSection(result)` |
| Luban 检测与启动静默检查 | `EditorUtil.Environment.LubanChecker`；该嵌套类为 internal，供框架 Editor 内部使用 |
| Python3 检测 | `EditorUtil.Environment.Python3Checker` |
| HybridCLR 检测 | `EditorUtil.Environment.HybridCLRChecker` |
| 导出前决定继续或阻断 | `EditorUtil.Luban.Pipeline.ValidateEnvironmentForExport` |

检查器的 `Check()` / `Recheck()` 只返回状态。是否打开窗口、允许告警继续或阻断操作，由调用方决定；不能把某个面板显示就绪当作 Bundle、Player 或设备验收。

可从 Editor 代码直接查询公开的 Python3 检查器：

```csharp
var result = EditorUtil.Environment.Python3Checker.Check();
if (!result.IsAvailable)
{
    // 提示安装或请用户在环境面板重新检测。
}
```

检测算法、结果字段与缓存细节由对应工具文档维护：

- [EditorUtil.Environment](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.md)
- [LubanChecker](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.md)
- [Python3Checker](../EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.md)
- [ProcessRunner](../EditorUtil/EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
- [ConfigWindow](ConfigWindow.md)
