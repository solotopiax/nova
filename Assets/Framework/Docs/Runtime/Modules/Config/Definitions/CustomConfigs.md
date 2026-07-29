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

GM 返回值必须是以 object 为根的完整 JSON，可包含对象、数组以及本地未声明的任意字段。成功响应完整替换上一次云端快照并原子缓存到磁盘。业务通过 `Nova.Config.Custom.GetString / GetInt / GetFloat / GetBool` 按 JSONPath 读取：优先云端，其次本地，最后使用调用方默认值；远端类型转换失败时也会尝试本地值。远端路径显式为 `null` 时直接返回调用方默认值，不回退本地。
