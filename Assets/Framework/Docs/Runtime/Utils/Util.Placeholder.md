# Util.Placeholder

`Util.Placeholder` 是 Editor、Runtime 与导出链共用的纯文本占位符解析器。解析器只消费显式传入的
`PlaceholderContext`，不会自行查找 `ConfigMasterSO`、`ConfigRuntimeSO` 或当前场景，因此可安全用于不同生命周期。

## 标准占位符

| 占位符 | 值 |
|---|---|
| `{Platform}` | `PlatformType` 枚举名 |
| `{Channel}` | `ChannelType` 枚举名 |
| `{Package}` | 调用方提供的资源包名 |
| `{Version}` | 调用方提供的应用版本，常规入口使用 `Application.version` |
| `{Time}` | 解析时刻，固定格式 `yyyy-MM-dd-HH-mm-ss`（24 小时制） |

占位符大小写敏感；未知占位符保持原样；上下文中的空字符串值替换为空文本。

## 公共 API

```csharp
var context = new PlaceholderContext(
    PlatformType.Android,
    ChannelType.Google,
    "DefaultPackage",
    Application.version,
    DateTime.Now);

string result = Util.Placeholder.Resolve(
    "{Platform}/{Channel}/{Package}/{Version}/{Time}",
    context);
```

Runtime 在 `ConfigRuntimeSO` 已加载后可使用：

```csharp
PlaceholderContext context = Util.Placeholder.FromRuntimeConfig(
    configRuntime,
    packageName,
    Application.version,
    DateTime.Now);
```

Editor 使用 `EditorUtil.Placeholder.FromConfigMaster` / `Resolve` 从当前 `ConfigMasterSO` 坐标取
Platform 与 Channel。导出器应传入目标 Platform/Channel 的显式重载，只处理明确需要固化的字段，
不要遍历并改写全部字符串配置。启动早期尚未加载 `ConfigRuntimeSO` 的调用方，应直接构造
`PlaceholderContext`，避免引入配置加载循环依赖。

## 关联文档

- [ConfigRuntimeSO.md](../Modules/Config/ConfigRuntimeSO.md)
- [PipifySteps.md](../../Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md)
