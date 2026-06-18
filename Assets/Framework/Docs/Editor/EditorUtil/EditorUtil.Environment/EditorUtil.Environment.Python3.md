# EditorUtil.Environment.Python3Checker

**类签名**：`public static class Python3Checker`（嵌套于 `EditorUtil.Environment`）
**命名空间**：`NovaFramework.Editor`

Python3 运行环境多路径探测检查器：依次执行 5 种策略定位 python3 可执行文件，结果缓存至 SessionState。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.cs` | `EditorUtil.Environment.Python3Checker` | 检查器主体（含 `Python3CheckResult` readonly struct） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Environment (public static partial class)
        └── Python3Checker (public static class)
              └── Python3CheckResult (public readonly struct，嵌套)
```

---

## §4 关键字段表

### 私有常量

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_ProbeTimeoutMs` | `int` | `3000` | 单次命令探测超时（毫秒） |
| `c_SessionKeyCached` | `string` | `"Nova.Python3.EnvCheckCached"` | SessionState：是否已执行过检测 |
| `c_SessionKeyAvailable` | `string` | `"Nova.Python3.EnvCheckAvailable"` | SessionState：是否可用 |
| `c_SessionKeyVersion` | `string` | `"Nova.Python3.EnvCheckVersion"` | SessionState：版本字符串 |
| `c_SessionKeyDetectedPath` | `string` | `"Nova.Python3.EnvCheckDetectedPath"` | SessionState：命中的可执行路径 |
| `c_SessionKeyDetectedVia` | `string` | `"Nova.Python3.EnvCheckDetectedVia"` | SessionState：命中策略名 |

### 私有静态字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_Python3Regex` | `Regex` | 编译型正则 `^Python 3\.\d+`，匹配版本输出首行 |

### Python3CheckResult（公开 readonly struct，嵌套）

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsAvailable` | `readonly bool` | python3 命令是否可用 |
| `Version` | `readonly string` | 版本字符串（如 `"Python 3.11.6"`；不可用时为 null） |
| `DetectedPath` | `readonly string` | 命中的可执行路径（绝对路径或裸命令名；不可用时为 null） |
| `DetectedVia` | `readonly string` | 命中策略名（`ExplicitPath` / `PATH` / `PyLauncher` / `Where` / `PythonFallback` / `NotFound`） |

---

## §5 完整公开 API

```csharp
// 检查 python3 环境；结果缓存到 SessionState，同一编辑器会话不重复检测
public static Python3CheckResult Check()

// 强制重新检测，忽略 SessionState 缓存；返回最新结果
public static Python3CheckResult Recheck()
```

---

## §9 关键算法

### 五策略探测流程

```
DoCheck()
  策略 A — ExplicitPath（显式候选路径）
    foreach path in GetExplicitCandidates()
      File.Exists(path) && TryRunAndParse(path, "--version") → 成功 → return (true, version, path, "ExplicitPath")

  策略 B — PATH
    TryRunAndParse("python3", "--version") → 成功 → return (true, version, "python3", "PATH")

  策略 C — PyLauncher（仅 Windows）
    TryRunAndParse("py", "-3 --version") → 成功 → return (true, version, "py -3", "PyLauncher")

  策略 D — Where（仅 Windows）
    TryWhere("python3") → fullPath
    TryRunAndParse(fullPath, "--version") → 成功 → return (true, version, fullPath, "Where")

  策略 E — PythonFallback
    TryRunAndParse("python", "--version") && version.StartsWith("Python 3.") → 成功 → return (true, version, "python", "PythonFallback")

  全部失败 → return (false, null, null, "NotFound")
```

### 候选路径表（策略 A）

**macOS（5 条）**

| 路径 | 说明 |
|------|------|
| `/opt/homebrew/bin/python3` | Homebrew（Apple Silicon） |
| `/usr/local/bin/python3` | Homebrew（Intel） |
| `/usr/bin/python3` | 系统自带 |
| `/Library/Frameworks/Python.framework/Versions/Current/bin/python3` | python.org 安装包 |
| `~/.pyenv/shims/python3` | pyenv shim |

**Windows（25 条，版本 3.9–3.13 × 5 路径）**

| 路径模板 | 说明 |
|---------|------|
| `C:\Python{ver}\python.exe` | 传统根目录安装 |
| `C:\Program Files\Python{ver}\python.exe` | 系统级安装 |
| `C:\Program Files (x86)\Python{ver}\python.exe` | 32 位系统级安装 |
| `%LOCALAPPDATA%\Programs\Python\Python{ver}\python.exe` | 用户级安装（LocalAppData） |
| `%USERPROFILE%\AppData\Local\Programs\Python\Python{ver}\python.exe` | 用户级安装（UserProfile 展开） |

版本变量 `{ver}`：`39` / `310` / `311` / `312` / `313`，共 25 条。

**Linux（3 条）**

| 路径 | 说明 |
|------|------|
| `/usr/bin/python3` | 系统自带 |
| `/usr/local/bin/python3` | 手动安装 |
| `~/.pyenv/shims/python3` | pyenv shim |

### SessionState 缓存机制

```
Check()
  SessionState.GetBool(c_SessionKeyCached, false) == true → ReadFromSession() → return
  否则 → RunCheck()
      → DoCheck() → WriteToSession(result)
      写入 5 个 key：Available / Version / DetectedPath / DetectedVia / Cached=true

Recheck()
  SessionState.SetBool(c_SessionKeyCached, false)
  → RunCheck()（与 Check 路径一致）
```

域重载后 SessionState 自动清空，`Recheck()` 通常无需手动调用。

---

## §10 常见误区

| 误区 | 说明 |
|---|---|
| 认为 `Check()` 每次都重新检测 | 同一编辑器会话（域重载前）第二次调用直接返回缓存，不重新执行 |
| 策略 C / D 在 macOS 有效 | `PyLauncher`（`py` 命令）和 `Where` 命令均**仅限 Windows**；macOS/Linux 直接跳过 |
| `Version` 为空字符串表示不可用 | `IsAvailable=false` 时 `Version` / `DetectedPath` / `DetectedVia` 均为 `null`，不是空字符串（SessionState 存储时用空字符串，读取时恢复为 null） |
| 检测超时会抛异常 | `TryRunAndParse` 内部捕获超时，超过 3000ms 静默返回 `false` |

---

## §11 使用示例

```csharp
// ConfigWindow.OnEnable 中异步触发检测（RunPython3Check）
m_Python3CheckResult = EditorUtil.Environment.Python3Checker.Check();

// UI 中展示结果
if (m_Python3CheckResult.IsAvailable)
{
    EditorGUILayout.LabelField($"Python3: {m_Python3CheckResult.Version} ({m_Python3CheckResult.DetectedPath})");
}
else
{
    EditorGUILayout.HelpBox("未检测到 Python 3，Luban 导出将无法运行。", MessageType.Warning);
}

// 手动触发重新检测（"重新检测"按钮回调）
private void OnClickRecheck()
{
    m_Python3CheckResult = EditorUtil.Environment.Python3Checker.Recheck();
    Repaint();
}
```

---

## §12 注意事项

- `Check()` 依赖 `ProcessRunner.RunSync`；Editor 主线程调用时若候选路径较多，首次检测可能阻塞约 1–3 秒（取决于超时策略）
- `c_ProbeTimeoutMs = 3000`：每条候选路径最多等待 3 秒，策略 A 中存在的路径会跳过探测直接尝试执行，不存在的路径（`File.Exists` 返回 false）直接跳过
- 工程域重载（脚本重新编译）后 SessionState 自动清空，下次打开 ConfigWindow 会重新检测

---

## §13 关联文档

- [EditorUtil.Environment.md](EditorUtil.Environment.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
