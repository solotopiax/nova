# EditorUtil.Pipify

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public static partial class EditorUtil.Pipify` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `EditorUtil.Pipify` |
| 功能描述 | Pipify 自动化流水线执行引擎入口，UI 与 CLI 共享同一 Runner |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/EditorUtil.Pipify.cs` | `EditorUtil.Pipify` | 主类骨架 |
| `EditorUtil.Pipify/EditorUtil.Pipify.Visitors.cs` | `EditorUtil.Pipify` | 常量（`c_LogPrefix`） |
| `EditorUtil.Pipify/EditorUtil.Pipify.Registry.cs` | `EditorUtil.Pipify.Registry` | TypeCache 扫描注册表 |
| `EditorUtil.Pipify/Definitions/PipifyStepAttribute.cs` | `PipifyStepAttribute` | Step 标注特性 |
| `EditorUtil.Pipify/Definitions/PipifyStepInfo.cs` | `PipifyStepInfo` | Step 元信息记录 |
| `EditorUtil.Pipify/Definitions/PipifyContext.cs` | `PipifyContext` | Runner 下行上下文 |
| `EditorUtil.Pipify/Definitions/IPipifyProgressReporter.cs` | `IPipifyProgressReporter` | 进度汇报接口 |
| `EditorUtil.Pipify/Definitions/Batch.cs` | `Batch` | 批次数据 |
| `EditorUtil.Pipify/Definitions/BatchItem.cs` | `BatchItem` | 批次条目 |
| `EditorUtil.Pipify/Definitions/PipifySettingsSO.cs` | `PipifySettingsSO` | 持久化 SO 存档 |
| `EditorUtil.Pipify/EditorUtil.Pipify.Methods.cs` | `EditorUtil.Pipify` | ApplyOverridesForItem / ConvertOverrideValue 私有工具方法 |
| `EditorUtil.Pipify/EditorUtil.Pipify.Runner.cs` | `EditorUtil.Pipify.Runner` | 纯执行引擎（internal static class） |
| `EditorUtil.Pipify/EditorUtil.Pipify.WindowReporter.cs` | `EditorUtil.Pipify.WindowReporter` | Window 宿主进度 Reporter（EditorUtility 模态进度条） |
| `EditorUtil.Pipify/EditorUtil.Pipify.CliReporter.cs` | `EditorUtil.Pipify.CliReporter` | CLI 宿主进度 Reporter（纯日志，恒返回 false） |
| `EditorUtil.Pipify/Steps/PipifySteps.HybridCLR.cs` | `PipifySteps` | 内置 Step：HybridCLR 分组 10 个 Step（3 个主流程：`hybridclr.validate_linkxml` / `hybridclr.copy_aot_dll` / `hybridclr.copy_game_dll`；仅编译 DLL 入口：`hybridclr.compile_dll_active_build_target`；对齐 HybridCLR/Generate 子菜单的 6 个细粒度入口：`hybridclr.generate_all` / `hybridclr.generate_linkxml` / `hybridclr.generate_method_bridge` / `hybridclr.generate_aot_generic_reference` / `hybridclr.generate_il2cpp_def` / `hybridclr.generate_aot_dlls`） |
| `EditorUtil.Pipify/Steps/PipifySteps.BundleBuilder.cs` | `PipifySteps` | 内置 Step：构建资源 分组 1 个 Step（`bundlebuilder.build` / DisplayName=Bundles：YooAsset ScriptableBuildPipeline 薄封装，参数类 `AssetBundleBuildArgs` 复用 EditorUtil.BundleBuilder） |
| `EditorUtil.Pipify/Definitions/PipifyDropdownAttribute.cs` | `PipifyDropdownAttribute` | string 字段渲染特性：以接口实现类下拉框形式编辑（存储 FullName） |
| `EditorUtil.Pipify/Definitions/PipifyDynamicDropdownAttribute.cs` | `PipifyDynamicDropdownAttribute` | string 字段渲染特性：动态选项下拉框（每帧调用 Provider 取 string[]） |
| `EditorUtil.Pipify/Definitions/PipifyDynamicDefaultAttribute.cs` | `PipifyDynamicDefaultAttribute` | string 字段渲染特性：值为空时显示动态默认值占位（不写回字段） |
| `EditorUtil.Pipify/Definitions/PipifyVisibleWhenAttribute.cs` | `PipifyVisibleWhenAttribute` | 字段显隐特性：依赖另一字段当前整型化值匹配 AnyOf 时才显示 |
| `EditorUtil.Pipify/Steps/PipifySteps.Build.cs` | `PipifySteps` | 内置 Step：打包分组 1 个 Step |
| `EditorUtil.Pipify/Steps/PipifySteps.Export.cs` | `PipifySteps` | 内置 Step：导出分组 2 个 Step（Config + 表格） |
| `EditorUtil.Pipify/Steps/PipifySteps.Definitions.cs` | `PipifySteps` | 内置 Step 嵌套参数类集中定义（AssetBundleParams / PackageParams / TableExportParams） |

## §5 完整公开 API

### EditorUtil.Pipify（主入口）

| 成员 | 签名 | 说明 |
|---|---|---|
| `RunBatchAsync` | `static UniTask RunBatchAsync(Batch, EditorWindow host)` | UI 宿主入口：使用模态进度条 WindowReporter 执行 Batch；host 用于 Batch 末尾 `ShowNotification` 弹结果浮窗，传 null 时只写日志 |
| `RunBatchForCliAsync` | `static UniTask RunBatchForCliAsync(Batch, IReadOnlyDictionary<string, string>)` | CLI 宿主入口：使用纯日志 CliReporter 执行 Batch，支持参数覆盖；overrides 为 null 表示不覆盖 |
| `Registry` | — | Step 元信息注册表（嵌套静态类） |

### EditorUtil.Pipify.Registry（注册表）

| 成员 | 签名 | 说明 |
|---|---|---|
| `Rebuild()` | `static void` | 重新扫描全工程 `[PipifyStep]` 静态方法，刷新注册表；域重载时静态构造自动调用 |
| `GetAll()` | `static IReadOnlyCollection<PipifyStepInfo>` | 获取全部已注册 Step |
| `FindById(id)` | `static PipifyStepInfo` | 按 ID 查找 Step，未命中或 id 为空返回 null |
| `GroupByCategory()` | `static IEnumerable<IGrouping<string, PipifyStepInfo>>` | 按 Category 分组，组内按 DisplayName 升序 |

### EditorUtil.Pipify.Runner（执行引擎，internal）

| 成员 | 签名 | 说明 |
|---|---|---|
| `RunBatchAsync` | `static async UniTask RunBatchAsync(Batch, IPipifyProgressReporter, IReadOnlyDictionary<string, string>, CancellationToken)` | 顺序执行 Batch 所有 Item；batch/reporter 为 null 抛 ArgumentNullException；任一步 throw 即中断并向 reporter 汇报失败 |

### 关键常量（内部）

| 常量 | 值 | 说明 |
|---|---|---|
| `c_LogPrefix` | `"[Pipify]"` | 所有日志消息的统一前缀 |

## §9 关键算法

### Registry 签名校验（ValidateSignature）

合法签名必须同时满足：
1. 方法为 `static`
2. 返回类型为 `UniTask`
3. 首参为 `PipifyContext`
4. 参数个数为 1（无参 Step）或 2（有参 Step，第二参须为 `[Serializable] class`）

任一条件不满足均 `Log.Error` 并跳过，不影响其他 Step 的注册。

### Runner 执行循环（RunBatchAsync）

1. batch / reporter 为 null 立即抛 `ArgumentNullException`
2. `reporter.BeginBatch(name, count)` 通知开始
3. 对每个 `BatchItem`：
   - `ct.ThrowIfCancellationRequested()` 响应外部取消
   - `Registry.FindById(stepId)` 查找元信息，未命中抛 `InvalidOperationException`
   - 若 `info.ParamsType != null`：ParamsJson 非空则 `Util.Json.Deserialize(json, type)` 反序列化，否则 `Activator.CreateInstance`；再调 `ApplyOverridesForItem` 覆盖 CLI 参数
   - 构造 `PipifyContext` 并下发
   - `reporter.ReportStep(i, name, 0f)` 返回 true 时抛 `OperationCanceledException`（Window 取消按钮）
   - `info.Method.Invoke(null, args)` 反射调用，得 `UniTask` 后 `await`
   - `TargetInvocationException` 解包为 `InnerException`，经 `ExceptionDispatchInfo.Capture().Throw()` 保留原始栈后重新抛出
   - 任何异常均先调 `reporter.EndStep(i, false, elapsed, ex)` 再 throw
4. 全部成功后 `reporter.EndBatch(true, total)`；异常在 `finally` 中以 `success=false` 调用

### ApplyOverridesForItem 覆盖规则

key 格式优先级（高 > 低）：
- `stepId[itemIndex].fieldName`：精确匹配当前 Item 索引
- `stepId.fieldName`（不含 `[`）：匹配该 Step 所有 Item

字段类型转换顺序：`string` → enum（`Enum.Parse` 大小写敏感）→ `Convert.ChangeType`（数字 / bool）。字段不存在时抛 `InvalidOperationException`。

## §11 使用示例

```csharp
// 查询所有 Step 并按分组展示
foreach (var group in EditorUtil.Pipify.Registry.GroupByCategory())
{
    Debug.Log($"[{group.Key}]");
    foreach (PipifyStepInfo info in group)
        Debug.Log($"  {info.Id} -> {info.DisplayName}");
}

// 按 ID 查找
PipifyStepInfo step = EditorUtil.Pipify.Registry.FindById("export_config");
if (step != null)
    Debug.Log($"找到 Step：{step.DisplayName}");

// 强制刷新（热重载后）
EditorUtil.Pipify.Registry.Rebuild();
```

Runner 由 public 入口间接调用，不对外直接暴露。

```csharp
// UI 宿主：模态进度条 + 宿主窗口右下角浮窗
await EditorUtil.Pipify.RunBatchAsync(batch, hostWindow);

// CLI 宿主：纯日志，带参数覆盖
var overrides = new Dictionary<string, string> { ["export_config.outputPath"] = "/tmp/out" };
await EditorUtil.Pipify.RunBatchForCliAsync(batch, overrides);

// CLI 宿主：不覆盖参数
await EditorUtil.Pipify.RunBatchForCliAsync(batch, null);
```

## §13 关联文档

- [PipifyStepAttribute.md](./PipifyStepAttribute.md)
- [PipifyStepInfo.md](./PipifyStepInfo.md)
- [PipifyContext.md](./PipifyContext.md)
- [PipifyDropdownAttribute.md](./PipifyDropdownAttribute.md)
- [PipifyDynamicDropdownAttribute.md](./PipifyDynamicDropdownAttribute.md)
- [PipifyDynamicDefaultAttribute.md](./PipifyDynamicDefaultAttribute.md)
- [PipifyVisibleWhenAttribute.md](./PipifyVisibleWhenAttribute.md)
- [IPipifyProgressReporter.md](./IPipifyProgressReporter.md)
- [Batch.md](./Batch.md)
- [BatchItem.md](./BatchItem.md)
- [PipifySettingsSO.md](./PipifySettingsSO.md)
- [EditorUtil.Pipify.Runner.md](./EditorUtil.Pipify.Runner.md)
- [Editor.md](../../Editor.md)
