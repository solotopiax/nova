# IDeviceIdProvider

**类签名**：`public interface IDeviceIdProvider : ISDKPlugin`
**命名空间**：`NovaFramework.Runtime`
**全局访问**：`Nova.SDK.Get<IDeviceIdProvider>()`

设备唯一标识提供者接口，SDK 插件实现此接口对外暴露设备 ID。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Device/IDeviceIdProvider.cs` | `IDeviceIdProvider` | 接口定义，继承 ISDKPlugin |

---

## § 3 继承关系

```
ISDKPlugin
  └── IDeviceIdProvider
```

---

## § 5 完整公开 API

```csharp
public interface IDeviceIdProvider : ISDKPlugin
{
    /// <summary>
    /// 获取设备唯一标识。
    /// </summary>
    /// <returns>设备 ID 字符串；未就绪或不可用时返回空串。</returns>
    string GetDeviceID();
}
```

---

## § 11 使用示例

```csharp
// 业务侧读取设备 ID：
if (Nova.SDK.TryGet<IDeviceIdProvider>(out var provider))
{
    string id = provider.GetDeviceID();
}
```

---

## § 13 关联文档

- [ISDKPlugin.md](../../Definitions/ISDKPlugin.md) — 根接口契约
- [SDKComponent.md](../../SDKComponent.md) — SDK 初始化入口（Get 解析路径）
