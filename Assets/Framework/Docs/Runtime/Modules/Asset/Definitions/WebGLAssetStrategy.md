# WebGLAssetStrategy

**类签名**：`public enum WebGLAssetStrategy : byte`
**命名空间**：`NovaFramework.Runtime`

WebGL 启动资源策略。它只在浏览器 WebGL Player 生效，与 `AssetPlayMode`（`OfflinePlayMode` / `HostPlayMode`）和 `EnableHotfix` 独立。

| 值 | 数值 | 启动行为 |
|---|---:|---|
| `TagsOnLaunch` | `0` | 默认。按 `LaunchHotfixTags` 启动预热，成功后释放，并在启动 DLL 消费完成后清理未使用内存；无有效 Tag 时降级 `OnDemand`。 |
| `OnDemand` | `1` | 不创建启动预热，跳过 `ProcedureHotfix`，业务全程异步按需加载。 |
| `AllOnLaunch` | `2` | 启动预热默认 Package 的全部 Bundle，并由 AssetManager 持有到 Shutdown。 |

值 `0` 被刻意分配给 `TagsOnLaunch`，以保证已有场景在新增序列化字段后采用 Nova 的默认策略。

详情见 [WebGLAssetStrategies.md](../WebGLAssetStrategies.md)。

## 关联文档

- [AssetPlayMode.md](AssetPlayMode.md)
- [AssetManagerConfig.md](../AssetManager/Definitions/AssetManagerConfig.md)
- [AssetComponentInspector.md](../../../../Editor/Inspectors/AssetComponentInspector/AssetComponentInspector.md)
