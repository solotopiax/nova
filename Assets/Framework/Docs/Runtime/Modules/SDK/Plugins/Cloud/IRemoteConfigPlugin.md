# IRemoteConfigPlugin

**类签名**：`public interface IRemoteConfigPlugin : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**源码文件**：`Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Cloud/IRemoteConfigPlugin.cs`

远程配置接口，抽象 Firebase Remote Config 等平台的 Key-Value 下发能力。

当前仓内没有自有 `IRemoteConfigPlugin` 实现；下列缓存、异常和事件描述是接口要求，文档示例不代表已启用可执行插件。实际键大小写、默认值、取消和平台缓存行为须由安装的实现兑现，不能从接口或 Inspector 分类推断。`TryGetBool` 返回 false 与“成功读取到布尔 false”应按方法返回值区分。

---

## §1 文件头

| 属性 | 值 |
|---|---|
| 类签名 | `public interface IRemoteConfigPlugin : ISDKPlugin` |
| 命名空间 | `NovaFramework.Runtime` |
| 职责 | 定义远程配置契约：拉取激活、强类型读取、配置更新事件 |

---

## §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Plugins/Cloud/IRemoteConfigPlugin.cs` | `IRemoteConfigPlugin` | 接口全部定义 |

---

## §3 继承关系

```
ISDKPlugin
  └── IRemoteConfigPlugin
```

---

## §5 完整公开 API

```csharp
public interface IRemoteConfigPlugin : ISDKPlugin
{
    // === 拉取激活 ===

    /// 异步拉取并激活最新远程配置。
    /// cacheExpiration：本地缓存有效期，null 使用平台默认值；缓存未过期时直接激活，不发请求。
    /// 网络异常时激活上一次缓存值并记录 Warning，不向调用方抛异常。
    UniTask FetchAsync(TimeSpan? cacheExpiration = null, CancellationToken ct = default);

    // === 强类型读取 ===

    /// 尝试获取 string 值；key 不存在或类型不匹配时返回 false，value 为 null。
    bool TryGetString(string key, out string value);

    /// 尝试获取 long 值；失败时 value 为 0。
    bool TryGetLong(string key, out long value);

    /// 尝试获取 double 值；失败时 value 为 0.0。
    bool TryGetDouble(string key, out double value);

    /// 尝试获取 bool 值；失败时 value 为 false。
    bool TryGetBool(string key, out bool value);

    // === 事件 ===

    /// 配置激活事件，在 FetchAsync 成功拉取并激活新配置后于主线程触发。
    event Action OnConfigActivated;
}
```

---

## §12 注意事项

- 使用前先确认存在可用实现，再依据该实现决定初始读取与 Fetch 时序。
- `cacheExpiration = null` 表达使用平台默认值，不是框架统一超时；接口没有提供默认等待时限。
- `OnConfigActivated` 表达激活通知，不能独立证明网络已获取新配置或服务端数据已经改变。

---

## §11 使用示例

```csharp
if (Nova.SDK.TryGet<IRemoteConfigPlugin>(out var rcPlugin))
{
    // 拉取配置（可在启动时调用一次）
    await rcPlugin.FetchAsync(cacheExpiration: TimeSpan.FromHours(1), ct);

    // 读取配置值
    if (rcPlugin.TryGetString("banner_ad_placement", out var placement))
        Debug.Log($"Banner 广告位：{placement}");

    if (rcPlugin.TryGetBool("feature_new_ui_enabled", out var enabled) && enabled)
        EnableNewUI();

    // 监听配置更新
    rcPlugin.OnConfigActivated += () =>
    {
        // 重新读取关心的配置项
        if (rcPlugin.TryGetLong("max_retry_count", out var maxRetry))
            ApplyRetryConfig((int)maxRetry);
    };
}
```

---

## §13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md) — 根接口
