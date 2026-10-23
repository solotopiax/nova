# EditorUtil.Config.Validator

**类签名**：`public static class Validator`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

当前坐标的 Namespace、App / Privacy、SDK / Kit 配置校验工具，另提供矩阵维度一致性检查。支持三维（Platform×Channel×DevelopMode）定位，返回问题列表供调用方判断是否阻断；含 `Severity` 枚举与 `ValidationIssue` 只读结构体。

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
| `Path` | `readonly string` | 问题所在字段路径（如 `"AppConfigs.AppID"` / `"PrivacyConfigs.AESKey"`） |
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

// 独立检查现有矩阵行、模式容器、共享逻辑组及 Override 键；不会自动修复
public static IReadOnlyList<ValidationIssue> ValidateDimensionInvariants(ConfigMasterSO master);

// 技术诊断文本，只供异常、Console、Editor.log 使用；调用方应先判断确有问题
public static string BuildDimensionInvariantMessage(IReadOnlyList<ValidationIssue> issues);
```

`Validate` 校验范围：

1. `master` 空值检查（Error）
2. Platform / Channel 必须是已定义的非 `None` 值（Error）
3. `DimensionalResolver.ResolveNamespace(master, platform, channel, mode)` 非空；`master.GetAppConfigs(platform, channel, mode)` 的 AppID / AppAesKey / AppAesIV 三个字段非空（Error）
4. `master.GetPrivacyConfigs(platform, channel, mode)` 的 AESKey / AESIV 均需为 16 个 UTF-8 字节；iOS 下解析 PrivacyInfoConfig / InfoPlistUsageDescriptions，检查 JSON 结构（空文本由各解析器作为默认空配置接受；非法内容为 Error）
5. 目标 Platform×Channel 矩阵行存在性；SDK / Kit 启用名单中的类型必须有实例（Error）
6. SDK / Kit null 占位：能唯一恢复类型且该类型未启用的残留为 Warning；类型无法确定或属于已启用类型时为 Error（已报告缺实例的问题不重复报告）

`Validate` 本体不标脏、不保存，但调用的 Master / 矩阵 getter 可能补建缺少的结构，不能视为恒定纯只读。它不自动调用 `ValidateDimensionInvariants`；后者只读检查现有行与模式容器、App / Privacy 共享组值、已启用 SDK / Kit 类型的共享组值和重复实例、四类 Override 的裁剪键，不能替代目标坐标的缺行或缺实例检查。

`BuildDimensionInvariantMessage` 不检查问题列表是否为空；空列表也返回固定的“本次操作已停止”前言。问题非空时才应调用，且不要直接把含内部字段路径的文本用于用户弹窗。

---

## §11 使用示例

```csharp
// ConfigWindow.OnClickExport 中
IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues =
    EditorUtil.Config.Validator.Validate(
        m_Master,
        m_Master.CurrentPlatform, // 实时映射 Unity Active BuildTarget
        m_Master.CurrentChannel,
        m_Master.CurrentDevelopMode);

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

- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)
- [AppConfigs.md](../../../Runtime/Modules/Config/AppConfigs.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)

## 校验不等于纯查询或完整就绪

Validate 会调用可补矩阵行/模式的 Getter，虽然不保存、不 SetDirty，内存仍可能变化；它不自动调用 SchemaGuard 或维度不变量检查。维度不变量只读已有结构，允许未预建全部枚举组合，顶层 Override 主要核逻辑键，不比较所有正文；未启用历史 SDK/Kit 不参加当前启用值一致性比较。深层损坏容器仍可直接抛异常。

App 必填检查主要 IsNullOrEmpty，部分空白可过，App AES 与 Privacy AES 的长度规则不同；平台、渠道、模式完整门由调用链组合。类型存在按 FullName，不绑定程序集；缺引用按元数据和基数只能唯一关联时才给候选，不猜多缺失项。Scanner 会构造配置读取 DisplayName，异常回退短名但仍登记类型；SafeGetTypes 只兜 ReflectionTypeLoadException，菜单扩展扫描只登记，不验证菜单真实存在或可安全执行。隐私 JSON parser 只核结构/键形状，false 的 out 可已含部分结果，不证明 Apple 接受。
