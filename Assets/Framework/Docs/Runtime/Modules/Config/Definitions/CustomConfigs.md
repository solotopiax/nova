# CustomConfig

Custom 配置分为本地数据与运行时查询入口：

```csharp
[Serializable]
public sealed class CustomConfigEntry
{
    public string Key;
    public string Value;
}

[Serializable]
public sealed class CustomConfigData
{
    public List<CustomConfigEntry> Entries = new();
}

public sealed class CustomConfig
{
    public string GetString(string path, string defaultValue = null);
    public int GetInt(string path, int defaultValue = default);
    public float GetFloat(string path, float defaultValue = default);
    public bool GetBool(string path, bool defaultValue = default);
}
```

ConfigWindow 在“应用配置”中把 `Entries` 直接呈现为“本地自定义配置项键值对”，使用者不会看到 `Entries` 容器层级。Key 使用路径形式，例如 `User.Level`、`Rewards[0].Id`；Value 是本地默认字符串。

GM 返回值必须是以 object 为根的完整 JSON，可包含对象、数组以及本地未声明的任意字段。成功响应先尝试原子缓存到磁盘，再完整替换上一次云端快照；磁盘不可写时仍可更新内存。业务通过 `Nova.Config.Custom.GetString / GetInt / GetFloat / GetBool` 按 JSONPath 读取：优先云端，其次本地，最后使用调用方默认值；远端类型转换失败时也会尝试本地值。远端路径显式为 `null` 时直接返回调用方默认值，不回退本地。

`CustomConfig` 只持有内部查询管理器引用，不保存冻结快照，也不持有 SO 句柄。外部保留的旧 wrapper 会读取同一管理器的后续快照；`Shutdown()` 清空快照后返回调用方默认值。省略默认参数时，字符串为 `null`，int/float 为 `0`，bool 为 `false`；这些回退值不会写回配置。

本地 `AppConfigSnapshot` 在加载时按 Entries 顺序建树：忽略 null 条目和空 Key，Key 去首尾空白，null Value 转为空串，同一路径后项可覆盖前项。本地写入只解析属性段与非负数字数组下标，读取使用 Newtonsoft `SelectToken` 的 JSONPath，二者语法范围不同。冲突项会告警，但建树并非事务：失败前创建的容器或数组占位可能保留；数组下标也没有额外分配上限，配置源应限制规模。

远端先解析成临时 `JObject`，验证通过后 `DeepClone` 整树替换，不合并局部 patch，不保留调用者对象引用；解析失败保留旧远端。字符串 token 保留原文，可格式化的标量使用固定区域格式并转小写，对象/数组返回紧凑 JSON。非法或空 JSONPath 按未命中处理，未捕获的转换异常仍可传播。内部 `TryGetString` 用固定 `"\u0000__nova_custom_missing__"` 区分缺失，真实值若恰等于该标记也返回 `false` 和 null。

int 使用 `NumberStyles.Integer`，float 使用 `NumberStyles.Float`，均为 `InvariantCulture`；float 没有额外有限数校验。bool 先走标准 `bool.TryParse`，再接受精确 `"1"`/`"0"`，数字分支不 Trim。远端非 null 但转换失败时，只在本地字符串与有效字符串不同的情况下再解析本地；仍失败才用调用方默认值。缓存失败但内存成功、刷新取消与重新初始化的边界见 [ConfigManager](../ConfigManager.md)。
