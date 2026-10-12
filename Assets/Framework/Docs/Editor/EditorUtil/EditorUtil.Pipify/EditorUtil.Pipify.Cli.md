# EditorUtil.Pipify.Cli

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public static class Cli` |
| 命名空间 | `NovaFramework.Editor` |
| 宿主类 | `EditorUtil.Pipify`（嵌套） |
| 功能描述 | Jenkins / CI batchmode CLI 入口，解析命令行参数并驱动 Batch 执行 |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/EditorUtil.Pipify.Cli.cs` | `EditorUtil.Pipify.Cli` | 完整实现 |

## §5 公开 API

| 方法 | 说明 |
|---|---|
| `public static async void Run()` | CLI 主入口；异步等待 Batch，成功 `EditorApplication.Exit(0)`，失败 `Exit(1)` |

### 私有辅助方法

| 方法 | 说明 |
|---|---|
| `private static string ReadArg(string name)` | 从 `Environment.GetCommandLineArgs()` 读取命名参数值；未命中返回 null |
| `private static PipifySettingsSO ResolveWorkspaceFromArgs()` | 成对解析显式 GUID；未提供时读取 Globals 当前工作区，不扫描首个资产 |
| `private static T LoadAssetByGuid<T>(string guid, string label)` | 校验显式 GUID 可解析且资产类型正确 |
| `private static IReadOnlyDictionary<string, string> ParseOverrides(string json)` | JSON 解析为覆盖字典（走 `Util.Json.Deserialize`）；json 为空则返回 null |

### 关键约束

- `-executeMethod` 需要 `public static void`，`Cli` 必须为 `public static class`
- `async void` 保留 executeMethod 所需的 void 返回类型；由 Runner 完成后自行退出。命令不得添加 `-quit`，否则异步步骤可能提前被终止。
- JSON 解析**必须**走 `Util.Json.Deserialize<Dictionary<string, string>>`，禁 JsonUtility
- `-configMasterGuid` 与 `-pipifySettingsGuid` 必须成对提供；显式工作区一次原子写入 Globals
- 未提供显式 GUID 时必须已有有效 Globals 工作区；缺失或漂移直接失败，不允许 `FindAssets()[0]` 降级
- `Log` 级别：Error（不可恢复）/ Debug（正常流程）；禁 Log.Info
- `c_LogPrefix` 复用 `EditorUtil.Pipify.Visitors.cs` 中的同名常量

## §11 使用示例

```bash
# Jenkins Pipeline 示例
unity \
  -batchmode \
  -projectPath "$UNITY_PROJECT" \
  -executeMethod NovaFramework.Editor.EditorUtil+Pipify+Cli.Run \
  -batchName "Release_Android" \
  -configMasterGuid "$CONFIG_MASTER_GUID" \
  -pipifySettingsGuid "$PIPIFY_SETTINGS_GUID" \
  -params '{"ExportStep.OutputPath":"/build/android","ExportStep.BuildTarget":"Android"}'
```

```csharp
// 仅供 CLI 调试：该入口最终会退出 Unity；编辑器自动化用 StartBatchJob。
EditorUtil.Pipify.Cli.Run();
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [EditorUtil.Pipify.Runner.md](./EditorUtil.Pipify.Runner.md)
- [EditorUtil.Pipify.CliReporter.md](./EditorUtil.Pipify.CliReporter.md)
- [PipifySettingsSO.md](./PipifySettingsSO.md)
- [Batch.md](./Batch.md)
