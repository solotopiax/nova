# EditorUtil.Config.ActivePlatform

**类签名**：`public static class EditorUtil.Config.ActivePlatform`
**命名空间**：`NovaFramework.Editor`

Nova 编辑期执行平台的真相入口。它实时读取 Unity `EditorUserBuildSettings.activeBuildTarget`，供 ConfigWindow 的导出/YooAsset 生效门禁、Pipify、ProjectGuard 与 Agent Action 使用；不读取或回写 ConfigMaster 中升级前遗留的平台序列化值。ConfigWindow 可另选仅用于编辑和 CDN 操作的平台坐标。

## 平台映射

| Unity BuildTarget | Nova PlatformType |
|---|---|
| `Android` | `Android` |
| `iOS` | `iOS` |
| `WebGL` | `WebGL` |
| 其他 | `None`，由生产入口阻止操作 |

## 公开 API

```csharp
public static PlatformType Current { get; }
public static PlatformType FromBuildTarget(BuildTarget target)
public static PlatformType RequireCurrent(string operation)
public static void EnsureActiveBuildTarget(BuildTarget target, string operation)
```

- `Current` 每次访问均重新读取 Active BuildTarget，不缓存。
- `RequireCurrent` 用于必须具有 Nova 平台映射的 Editor 操作。
- `EnsureActiveBuildTarget` 用于带显式 `BuildTarget` 的入口，阻止请求目标与 Unity 当前目标分叉。
- Runtime URL 的 `{Platform}` 仍由 Player 编译宏决定，不使用此 Editor 工具。

## 关联文档

- [ConfigMasterSO.md](../../Config/ConfigMasterSO.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md)
