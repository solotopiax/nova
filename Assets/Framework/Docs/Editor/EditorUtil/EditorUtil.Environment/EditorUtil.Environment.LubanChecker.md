# EditorUtil.Environment.LubanChecker

**类签名**：`[InitializeOnLoad] internal static class LubanChecker`（嵌套于 `EditorUtil.Environment`）
**命名空间**：`NovaFramework.Editor`

Luban 运行环境检查器，检测 dotnet-sdk 路径、版本（[8.0.127, 10.0.203] 闭区间）和 Luban.dll 是否存在，结果缓存到 `SessionState`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.cs` | `EditorUtil.Environment.LubanChecker` | 检查器主体（含 `EnvironmentIssue` 枚举 + `EnvironmentCheckResult` readonly struct） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Environment (public static partial class)
        └── LubanChecker (internal static class, [InitializeOnLoad])
              ├── EnvironmentIssue (public enum，嵌套)
              └── EnvironmentCheckResult (public readonly struct，嵌套)
```

---

## §4 关键字段表

### 私有常量

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `c_SessionKeyReady` | `string` | `"Nova.Luban.EnvCheckReady"` | SessionState 缓存键：是否就绪 |
| `c_SessionKeyDotnetPath` | `string` | `"Nova.Luban.EnvCheckDotnetPath"` | SessionState 缓存键：dotnet 路径 |
| `c_SessionKeyDotnetVersion` | `string` | `"Nova.Luban.EnvCheckDotnetVersion"` | SessionState 缓存键：dotnet 版本号 |
| `c_SessionKeyErrorMessage` | `string` | `"Nova.Luban.EnvCheckErrorMessage"` | SessionState 缓存键：错误信息 |
| `c_SessionKeyIssue` | `string` | `"Nova.Luban.EnvCheckIssue"` | SessionState 缓存键：问题类型（int） |
| `c_MinDotnetVersion` | `internal string` | `"8.0.127"` | dotnet 兼容版本下限（闭区间），低于此版本硬阻断 |
| `c_MaxDotnetVersion` | `internal string` | `"10.0.203"` | dotnet 兼容版本上限（闭区间），高于此版本硬阻断 |

### EnvironmentIssue 枚举值

| 值 | 含义 |
|----|------|
| `None` | 无问题，环境就绪 |
| `DotnetNotFound` | 未找到 dotnet 可执行文件 |
| `DotnetVersionTooLow` | dotnet 版本低于兼容下限 |
| `DotnetVersionTooHigh` | dotnet 版本高于兼容上限，超出 Luban 兼容区间 |
| `DotnetNotExecutable` | dotnet 无法正常执行（进程失败/超时） |
| `LubanDllNotFound` | 未找到 Luban.dll，UPM 包可能未安装 |

### EnvironmentCheckResult（公开 readonly struct，嵌套）

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsReady` | `readonly bool` | 环境是否就绪 |
| `DotnetPath` | `readonly string` | dotnet 可执行文件路径（未找到时为 null） |
| `DotnetVersion` | `readonly string` | dotnet 版本号字符串（如 `"8.0.100"`，失败时为 null） |
| `ErrorMessage` | `readonly string` | 错误信息（就绪时为 null） |
| `Issue` | `readonly EnvironmentIssue` | 环境问题类型 |

---

## §5 完整公开 API

```csharp
// 检查 Luban 运行环境；结果缓存到 SessionState，同一编辑器会话不重复检测
// 返回值：最新或缓存的环境检查结果
public static EnvironmentCheckResult Check()

// 强制重新检测，忽略 SessionState 缓存；返回最新结果
public static EnvironmentCheckResult Recheck()
```

---

## §6 生命周期状态机

```
[InitializeOnLoad] 静态构造
  └── EditorApplication.delayCall += RunSilentCheck（延迟到首次 update 后执行）

RunSilentCheck
  └── Check() → 未就绪时 Log.Warning 提示用户打开引导窗口

Check()
  ├── 读取 SessionState._cached = true → 直接 ReadFromSession()
  └── 未缓存 → RunCheck() → DoCheck() → WriteToSession()

Recheck()
  └── 清除 _cached → RunCheck()
```

---

## §8 初始化时序

`DoCheck` 执行顺序（四步串行）：

1. 调用 `EditorUtil.Luban.CliRunner.ResolveDotnetPath()` 获取 dotnet 可执行路径
2. 调用 `EditorUtil.ProcessRunner.RunSync(dotnetPath, "--version")` 获取版本字符串
3. 调用 `ParseVersion` 解析版本号，验证落在 `[c_MinDotnetVersion, c_MaxDotnetVersion]` 闭区间；解析失败返回 `DotnetNotExecutable`，版本过低返回 `DotnetVersionTooLow`，版本过高返回 `DotnetVersionTooHigh`
4. 调用 `EditorUtil.Luban.CliRunner.GetLubanDllPath()` 验证 Luban.dll 是否存在

---

## §11 使用示例

```csharp
// Pipeline 入口处进行环境 guard（ConfigWindow 或 Pipeline 内部调用）
EditorUtil.Environment.LubanChecker.EnvironmentCheckResult envResult =
    EditorUtil.Environment.LubanChecker.Check();
if (!envResult.IsReady)
{
    ConfigWindow.OpenLubanSection(envResult);
    return false;
}

// 环境窗口中手动触发重检（"重新检测"按钮回调）
EditorUtil.Environment.LubanChecker.EnvironmentCheckResult result =
    EditorUtil.Environment.LubanChecker.Recheck();
if (result.IsReady)
{
    Log.Debug(LogTag.Editor, "Luban 环境就绪，dotnet {0}", result.DotnetVersion);
}
else
{
    Log.Warning(LogTag.Editor, "Luban 环境问题：{0}", result.ErrorMessage);
}
```

---

## §12 注意事项

| 场景 | 说明 |
|------|------|
| 缓存有效期 | 缓存绑定 Unity SessionState，重启 Unity Editor 后自动清除，下次启动重新执行检测 |
| `internal` 可见性 | 本类对外不公开，由 `Pipeline` 和 `ConfigWindow` 内部调用 |
| dotnet 路径复用 | 检测 dotnet 路径调用 `EditorUtil.Luban.CliRunner.ResolveDotnetPath()`，避免重复实现探测逻辑 |
| SessionState 键与旧版保持不变 | 键值前缀仍为 `"Nova.Luban.EnvCheck*"`，迁移后跨会话缓存无缝继承 |

---

## §13 关联文档

- [EditorUtil.Environment.md](EditorUtil.Environment.md)
- [EditorUtil.Environment.Python3.md](EditorUtil.Environment.Python3.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.Luban.CliRunner.md](../EditorUtil.Luban/EditorUtil.Luban.CliRunner.md)
- [EditorUtil.ProcessRunner.md](../EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
