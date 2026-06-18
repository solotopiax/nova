# EditorUtil.Environment

**类签名**：`public static class EditorUtil.Environment`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Environment`

外部工具环境检测（Python3 / Luban）与 DataPipeline 环境数据工具：从场景读取 DevelopMode / Channel / Platform，供 Config 和 Network 预过滤器共用；同时托管 Python3Checker 与 LubanChecker 两个运行时环境检查器。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Environment/EditorUtil.Environment.cs` | `EditorUtil.Environment` | 全部定义：`EnvironmentData` struct、`ColumnIndices` struct、`GetEnvironmentData()`、`GetColumnIndices()` |
| `EditorUtil/EditorUtil.Environment/EditorUtil.Environment.Python3.cs` | `EditorUtil.Environment.Python3Checker` | Python3 运行环境多路径探测检查器（`Python3CheckResult` 结构体 + `Check()`/`Recheck()`） |
| `EditorUtil/EditorUtil.Environment/EditorUtil.Environment.LubanChecker.cs` | `EditorUtil.Environment.LubanChecker` | Luban 运行环境检查器（`EnvironmentIssue` 枚举 + `EnvironmentCheckResult` 结构体 + `Check()`/`Recheck()`） |

---

## §3 继承关系

```
EditorUtil (public static partial)
  └── EditorUtil.Environment (public static partial)
        ├── EnvironmentData  (public struct)         运行时环境数据
        ├── ColumnIndices    (public struct)          列索引缓存
        ├── Python3Checker   (public static class)   Python3 多路径探测检查器
        │     └── Python3CheckResult (public readonly struct)   检测结果
        └── LubanChecker     (internal static class, [InitializeOnLoad])   Luban 环境检查器
              ├── EnvironmentIssue (public enum)
              └── EnvironmentCheckResult (public readonly struct)
```

---

## §4 关键字段表

此类仅包含静态方法和公开 struct，无实例字段。

### EnvironmentData（公开 struct）

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsDevelopMode` | `bool` | 是否为开发模式（`Config.RuntimeProvider.GetDevelopMode() == DevelopMode.Debug`） |
| `CurrentChannel` | `ChannelType` | 当前业务渠道类型（来自 `Config.RuntimeProvider.GetChannel()`） |
| `CurrentPlatform` | `string` | 当前构建平台名称（`EditorUserBuildSettings.activeBuildTarget.ToString()`） |

### ColumnIndices（公开 struct）

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | `int` | Name 列索引，未找到为 -1 |
| `Channel` | `int` | Channel 列索引，未找到为 -1 |
| `Platform` | `int` | Platform 列索引，未找到为 -1 |
| `DevelopValue` | `int` | DevelopValue 列索引，未找到为 -1 |
| `PublishValue` | `int` | PublishValue 列索引，未找到为 -1 |

---

## §5 完整公开 API

```csharp
// 从 Config.RuntimeProvider 读取运行时环境数据（DevelopMode、Channel、Platform）
// Channel 与 DevelopMode 均来自 RuntimeProvider，保证单一数据源
// 场景中未找到 Nova 对象时 Log.Warning 并返回默认值（IsDevelopMode=false, Channel=None）
public static EnvironmentData GetEnvironmentData()

// 从列名行解析 Config/Network 共用的列索引（Name、Channel、Platform、DevelopValue、PublishValue）
// nameRow：列名行数据（rows[1]）
// stripHashPrefix：是否去除列名 '#' 前缀后再匹配（Config 模块需传 true）
// 未找到的列索引返回 -1
public static ColumnIndices GetColumnIndices(IReadOnlyList<string> nameRow, bool stripHashPrefix = false)
```

---

## §9 关键算法

### GetEnvironmentData 读取流程

```
GetEnvironmentData()
  ├── data = { CurrentPlatform = activeBuildTarget.ToString(), CurrentChannel = None, IsDevelopMode = false }
  ├── Serializer.FindInScene<Nova>("Nova") → novaSerObj
  │     为 null → Log.Warning → return data（默认值）
  ├── Config.RuntimeProvider.GetDevelopMode() == DevelopMode.Debug → data.IsDevelopMode
  └── Config.RuntimeProvider.GetChannel() → data.CurrentChannel
```

### GetColumnIndices 列名匹配规则

遍历 `nameRow`，逐列匹配以下固定列名（`stripHashPrefix=true` 时先 `TrimStart('#')`）：

| 匹配字符串 | 赋值目标 |
|---|---|
| `"Name"` | `indices.Name` |
| `"Channel"` | `indices.Channel` |
| `"Platform"` | `indices.Platform` |
| `"DevelopValue"` | `indices.DevelopValue` |
| `"PublishValue"` | `indices.PublishValue` |

---

## §10 常见误区

| 误区 | 说明 |
|---|---|
| 认为 `GetEnvironmentData` 从场景组件读 Channel | Channel 现已改为从 `Config.RuntimeProvider.GetChannel()` 读取，不再反射场景组件序列化字段 |
| `stripHashPrefix` 参数 | Config 列名行中列名可带 `#` 前缀（Luban 标记），须传 `true`；Network 列名行无前缀，传默认值 `false` 即可 |
| 场景依赖 | `GetEnvironmentData` 仍依赖**当前已打开场景**验证 Nova 对象存在；Scene 缺失时返回默认值（`IsDevelopMode=false, Channel=None`） |

---

## §11 使用示例

```csharp
// Config/Network 预过滤器中：读取运行时环境数据（Channel 来自 RuntimeProvider）
EditorUtil.Environment.EnvironmentData envData =
    EditorUtil.Environment.GetEnvironmentData();

// 解析列索引（Config：带 # 前缀；Network：不带前缀）
EditorUtil.Environment.ColumnIndices indices =
    EditorUtil.Environment.GetColumnIndices(rows[1], stripHashPrefix: true);

// 使用环境数据过滤行
if (!channelStr.Contains("All") && !channelStr.Contains(envData.CurrentChannel.ToString()))
{
    continue; // 渠道不匹配，跳过该行
}
```

---

## §13 关联文档

- [EditorUtil.Environment.Python3.md](EditorUtil.Environment.Python3.md)
- [EditorUtil.Environment.LubanChecker.md](EditorUtil.Environment.LubanChecker.md)
- [NetworkExcelPreFilter.md](../../DataPipeline/Implements/Networks/NetworkExcelPreFilter.md)
- [EditorUtil.Config.RuntimeProvider.md](../EditorUtil.Config/EditorUtil.Config.RuntimeProvider.md)
- [EditorUtil.md](../EditorUtil.md)
- [EditorUtil.Serializer.md](../EditorUtil.Serializer/EditorUtil.Serializer.md)
- [ChannelType.md](../../../Runtime/Core/Definitions/ChannelType.md)
