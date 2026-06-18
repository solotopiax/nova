# EditorUtil.Config.Validator

**类签名**：`public static class Validator`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

`CommonConfig` 与 `SDKConfigs` 必填字段校验工具，支持三维（Platform×Channel×DevelopMode）定位，返回问题列表供 `ConfigWindow` 弹窗展示；含 `Severity` 枚举与 `ValidationIssue` 只读结构体。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Validator.cs` | `EditorUtil.Config.Validator` | 校验工具类（含 Severity 枚举、ValidationIssue 结构体） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── Validator (public static class)
              ├── Severity (public enum，嵌套)
              └── ValidationIssue (public readonly struct，嵌套)
```

---

## §4 关键字段表

`ValidationIssue` 嵌套结构体字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Path` | `readonly string` | 问题所在字段路径（如 `"Common.AppID_Dev"` / `"SDKConfigs[0]"`） |
| `Message` | `readonly string` | 面向用户的问题描述文本 |
| `Level` | `readonly Severity` | 问题严重度（Warning / Error） |

`Severity` 枚举值：

| 值 | 说明 |
|----|------|
| `Warning` | 建议修正，不阻断流程 |
| `Error` | 必须修正，配置存在致命缺陷 |

---

## §5 完整公开 API

```csharp
// 对指定 Platform×Channel×DevelopMode 三维组合执行全量校验，返回所有问题列表
// master 为 null 时直接返回含根 Error 的列表
public static IReadOnlyList<ValidationIssue> Validate(
    ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode);
```

校验范围：
1. `master` 空值检查（Error）
2. `master.GetCommon(mode)` 返回的 `CommonConfig` 全部 4 个必填字段（AppID / AppAesKey / AppAesIV / Namespace，任意为空 → Error）
3. 目标 Platform×Channel 矩阵行存在性（不存在 → Error）
4. 该行 `GetSDKConfigs(mode)` 中 null 占位检查（每个 null → Error）

---

## §11 使用示例

```csharp
// ConfigWindow.OnClickExport 中
IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues =
    EditorUtil.Config.Validator.Validate(
        m_Master, m_Master.CurrentPlatform, m_Master.CurrentChannel, m_Master.CurrentDevelopMode);

if (HasAnyError(issues))
{
    ShowValidationDialog(issues);
    return;
}

// 判断是否有 Error 级问题
private static bool HasAnyError(IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues)
{
    for (int i = 0; i < issues.Count; i++)
        if (issues[i].Level == EditorUtil.Config.Validator.Severity.Error) return true;
    return false;
}
```

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [CommonConfig.md](../../../Runtime/Modules/Config/CommonConfig.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
