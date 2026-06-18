# EditorUtil.ProcessRunner

**类签名**：`public static class EditorUtil.ProcessRunner`
**命名空间**：`NovaFramework.Editor`

统一外部进程调用器，封装 stdout/stderr 全异步读取、超时 Kill、退出码判断，避免死锁和编辑器卡死。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.ProcessRunner.cs` | `EditorUtil.ProcessRunner` | RunSync（阻塞等待）、RunAsync（非阻塞流式）、ProcessResult 结构体 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── ProcessRunner (public static class)
```

---

## §4 关键字段

| 字段 | 类型 | 修饰符 | 默认值 | 说明 |
|------|------|--------|--------|------|
| `c_DefaultTimeoutMs` | `int` | `private const` | `120000` | 默认超时时间（毫秒，即 120 秒） |

### ProcessResult 结构体字段

| 字段 | 类型 | 修饰符 | 说明 |
|------|------|--------|------|
| `ExitCode` | `int` | `public readonly` | 进程退出码 |
| `Stdout` | `string` | `public readonly` | 标准输出内容 |
| `Stderr` | `string` | `public readonly` | 标准错误内容 |
| `TimedOut` | `bool` | `public readonly` | 是否因超时而终止 |
| `Success` | `bool` | `public` (computed) | 退出码为 0 且未超时时为 true |

---

## §5 完整公开 API

```csharp
/// <summary>
/// 执行外部进程并等待完成。
/// stdout 和 stderr 均使用异步事件驱动读取，避免缓冲区满导致的死锁。
/// 超时后自动 Kill 进程。
/// </summary>
/// <param name="fileName">可执行文件路径。</param>
/// <param name="arguments">命令行参数。</param>
/// <param name="timeoutMs">超时时间（毫秒），默认 120 秒。</param>
/// <returns>进程执行结果。</returns>
public static ProcessResult RunSync(string fileName, string arguments, int timeoutMs = c_DefaultTimeoutMs)

/// <summary>
/// 启动外部进程（非阻塞），输出实时流式重定向到指定回调。
/// 进程退出后自动 Dispose。
/// </summary>
/// <param name="fileName">可执行文件路径。</param>
/// <param name="arguments">命令行参数。</param>
/// <param name="onStdout">标准输出回调（每行一次，可选）。</param>
/// <param name="onStderr">标准错误回调（每行一次，可选）。</param>
/// <param name="workingDirectory">工作目录（可选）。</param>
/// <returns>启动成功返回 true。</returns>
public static bool RunAsync(string fileName, string arguments, Action<string> onStdout = null, Action<string> onStderr = null, string workingDirectory = null)
```

### ProcessResult 构造

```csharp
/// <summary>
/// 构造进程执行结果。
/// </summary>
public ProcessResult(int exitCode, string stdout, string stderr, bool timedOut)
```

---

## §7 线程模型

- `RunSync` 在调用线程同步阻塞，直到进程退出或超时；stdout/stderr 通过 `BeginOutputReadLine` / `BeginErrorReadLine` 在**线程池线程**异步读取，通过 `lock(gate)` 写入 `StringBuilder`，主线程 `WaitForExit()` 返回后统一读取
- `RunAsync` 立即返回，进程输出回调（`onStdout` / `onStderr`）在**线程池线程**触发，调用方需自行处理线程安全问题（不得在回调中直接调用 Unity API）
- `RunAsync` 的 `Exited` 事件先调 `p.WaitForExit()` 确保异步输出全部排干后再 `Dispose`，防止丢失最后几行或触发 `ObjectDisposedException`

---

## §9 关键算法

### RunSync 超时处理

1. 调用 `process.WaitForExit(timeoutMs)` 等待进程退出
2. 返回 `false`（超时）时调用 `process.Kill()`，再次 `WaitForExit()` 确保进程已终止
3. Kill 失败仅输出 Warning 日志，不抛出异常
4. 超时结果：`ExitCode = -1`、`TimedOut = true`，已收集的输出仍可读取

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 在 `RunAsync` 回调中调用 Unity API | 回调在线程池线程执行，不可直接调用 Unity API；需通过 `EditorApplication.delayCall` 转主线程 |
| 认为 `RunSync` 不会死锁 | 正确：stdout/stderr 均异步读取，避免了传统同步读取的缓冲区死锁 |
| `ProcessResult.Success = false` 但没有 `Stderr` | 可能是进程启动失败（`Stderr` 为空时查看 `ExitCode`） |

---

## §11 使用示例

```csharp
// 同步调用（阻塞等待结果）
EditorUtil.ProcessRunner.ProcessResult result = EditorUtil.ProcessRunner.RunSync("dotnet", "--version");
if (result.Success)
{
    Log.Debug(LogTag.Editor, "dotnet 版本：{0}", result.Stdout.Trim());
}
else if (result.TimedOut)
{
    Log.Error(LogTag.Editor, "dotnet --version 执行超时。");
}
else
{
    Log.Error(LogTag.Editor, "dotnet --version 失败（ExitCode={0}）：{1}", result.ExitCode, result.Stderr);
}

// 非阻塞调用（流式输出到 Console）
EditorUtil.ProcessRunner.RunAsync(
    "python",
    "\"install_aab.py\" \"aab_path\" \"apks_path\"",
    onStdout: line => { if (!string.IsNullOrEmpty(line)) Log.Debug(LogTag.Editor, line); },
    onStderr: line => { if (!string.IsNullOrEmpty(line)) Log.Warning(LogTag.Editor, line); });
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Luban.CliRunner.md](../EditorUtil.Luban/EditorUtil.Luban.CliRunner.md)
- [EditorUtil.Proto.CliRunner.md](../EditorUtil.Proto/EditorUtil.Proto.CliRunner.md)
- [EditorUtil.FileSystem.md](../EditorUtil.FileSystem/EditorUtil.FileSystem.md)
