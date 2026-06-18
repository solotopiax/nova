# PluginBase

**类签名**：`public abstract class PluginBase<TConfig> : SDKPluginBase where TConfig : class, ISDKPluginConfig`  
**命名空间**：`NovaFramework.Runtime`

`PluginBase<TConfig>` 是 `SDKPluginBase` 的强类型配置桥接基类，适用于“插件必须拿到某个具体配置类型”这一类实现。

## 当前行为

```csharp
protected sealed override Type ConfigType => typeof(TConfig);

protected sealed override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
{
    if (config is not TConfig typed)
        throw new InvalidCastException(...);
    return OnInitializeAsync(typed, ct);
}

protected abstract UniTask OnInitializeAsync(TConfig config, CancellationToken ct);
```

## 适用场景

- 插件需要某个确定的配置类型时，优先继承 `PluginBase<TConfig>`。
- 插件不需要配置时，直接继承 `SDKPluginBase` 更合适。

## 使用示例

```csharp
public sealed class ExampleSDKConfig : ISDKPluginConfig
{
    public string DisplayName => "示例 SDK";
    public string AppId;
}

public sealed class ExampleTrackPlugin : PluginBase<ExampleSDKConfig>, ITrackPlugin
{
    public override string Name => "ExampleTrack";

    protected override async UniTask OnInitializeAsync(ExampleSDKConfig config, CancellationToken ct)
    {
        await UniTask.SwitchToMainThread(ct);
    }

    protected override UniTask OnDisposeAsync(CancellationToken ct)
    {
        return UniTask.CompletedTask;
    }

    public void SetUserId(string userId) { }
    public void SetUserProperty(string key, string value) { }
    public void TrackEvent(TrackEvent evt) { }
    public void TrackEvent(string eventName, Dictionary<string, object> parameters) { }
}
```

## 关联文档

- [./SDKPluginBase.md](./SDKPluginBase.md)
- [./ISDKPluginConfig.md](./ISDKPluginConfig.md)
