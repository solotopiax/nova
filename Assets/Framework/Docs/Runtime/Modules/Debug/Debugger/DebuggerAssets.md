# Debugger Assets

内置调试器资源随 `Assets/Framework` 主包交付，不再来自独立 UPM 包。

| 类型 | 路径 |
|---|---|
| Runtime Resources | `Assets/Framework/Resources/Debug`（`Settings.asset` 与 `Prefabs/**` 位于该目录下） |
| Prefab / UI | `Assets/Framework/Prefabs/Debug` |
| Animation | `Assets/Framework/Animations/Debug` |
| Runtime Texture / Sprite | `Assets/Framework/Textures/Runtime/Debug` |
| 示例 Scene | `Assets/Framework/Scenes/Debug` |

移动或重命名资源时必须保留 `.meta`，否则 Prefab 脚本绑定和资源引用会丢失。

## 当前边界

- `Resources/Debug/Settings.asset` 是运行时调试器设置资产，不再依赖自定义 Editor 绘制。
- 运行时 UI Prefab 直接位于 `Resources/Debug/Prefabs/**`，不再额外挂一层 `UI` 或 `RuntimeDebugger` 目录。
- 调试器运行时图片、Sprite、Logo 位于 `Textures/Runtime/Debug/**`。
- 当前不再保留调试器专属 Editor 代码和 Editor 图标依赖；若未来重新引入 Editor 可视化工具，必须重新同步本页与 Editor 文档。

## 变更同步规则

移动 `Resources/Debug`、`Prefabs/Debug`、`Animations/Debug` 或 `Textures/Runtime/Debug` 任一路径时，属于 Debug 资源契约变化，必须同轮更新：

- 本页资源表。
- Prefab / Settings 依赖说明。
- 全局 `Docs/INDEX.md` 的 Debug 入口描述。
- 如涉及脚本目录或程序集归属，同时触发 `nova-prelookup` 并复核 `Minds` 中 Debug 图谱与程序集边界。
