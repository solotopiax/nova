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
| `{Time}` | 上下文显式传入的时间，固定格式 `yyyy-MM-dd-HH-mm-ss`（24 小时制） |

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
Platform 与 Channel；其中 `ConfigMasterSO.CurrentPlatform` 实时映射 Unity Active BuildTarget。导出器应传入目标 Platform/Channel 的显式重载，只处理明确需要固化的字段，
不要遍历并改写全部字符串配置。启动早期尚未加载 `ConfigRuntimeSO` 的调用方，应直接构造
`PlaceholderContext`，避免引入配置加载循环依赖。

Pipify Runner 是运行时参数快照的统一入口：完成 ParamsJson 反序列化、CLI 覆盖和 Active BuildTarget
同步后，会在调用 Step 前递归解析全部公开字符串字段及列表元素。该行为只修改本次调用参数，
不会遍历或回写 ConfigMasterSO、ParamsJson 等持久化配置。

## 替换边界与启动期 URL

`PlaceholderContext` 是只读值容器，不校验枚举或字符串；默认值的时间是 `DateTime.MinValue`。`Resolve(null, context)` 返回 null，Package/Version 为 null 时替换为空。替换依次执行 Platform → Channel → Package → Version → Time，因此前面值中插入的后续占位符还会再次展开；时间使用 InvariantCulture，不自动读取当前时间或转换时区。

内部 `Util.UrlTemplate` 使用同样的前四步而不处理 Time；它只替换文字，不编码 URL、不验证协议或未知占位符。启动期平台判断依编译条件依次选择 Android、iOS、WebGL，否则返回 `PlatformType.None`，不能与其他路径工具的平台目录名推断混用。

## 关联文档

- [ConfigRuntimeSO.md](../Modules/Config/ConfigRuntimeSO.md)
- [PipifySteps.md](../../Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md)
