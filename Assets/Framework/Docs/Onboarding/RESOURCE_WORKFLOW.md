# 资源工作流

Nova 推荐业务资源采用 YooAsset/Bundle，但资源检查必须先识别所有权和使用场景，不能把所有 `Resources` 目录当成错误。

## 先分类，再决定加载方式

| 资源类型 | 常见加载方式 | 默认处理 |
|---|---|---|
| Nova 业务资源，需要版本、热更或可控卸载 | `Nova.Asset` / YooAsset Handle | 推荐路径；持有并正确释放 Handle |
| `Resources/BuiltIn/**` 内置资源 | `Resources.Load` 等 Resources 接口 | 合法的特殊用途 |
| UPM 包内第三方插件 Resources | 插件自己的 `Resources.Load` 或封装 API | 合法，不要求 Contract 白名单 |
| 导入到 `Assets/**` 的第三方插件 Resources | 插件自己的 `Resources.Load` 或封装 API | 合法，不要求 Contract 白名单 |
| 项目业务 Resources | Unity Resources 接口 | 可以运行；在 Nova 托管业务范围内通常给 Warning 并建议评估 Bundle |
| 视频、数据库、证书、离线表等原始文件 | 项目明确选择的 StreamingAssets/文件方案 | 按平台和发布需求评估，不作一刀切禁止 |

第三方插件包括其自带的 Demo、Editor 支撑资源和运行时资源。只要内容属于插件自身，位于 UPM 或 `Assets/**` 都不因使用 Resources 而非法，也不需要逐项加入 Nova 白名单。

## Nova 业务资源

需要下载、版本管理、增量更新、依赖追踪或确定卸载时，优先使用 YooAsset/Bundle：

1. 通过 Collector 把业务资源纳入对应 Package。
2. 构建 Bundle，并验证运行目标使用的 Package 与地址。
3. 通过 `Nova.Asset` 加载，保留返回的 Handle。
4. 在所有者生命周期结束时释放 Handle；场景 Handle 使用 `UnloadAsync()`。

API 细节见 [AssetComponent](../Runtime/Modules/Asset/AssetComponent.md)，Bundle 构建见 [EditorUtil.BundleBuilder](../Editor/EditorUtil/EditorUtil.BundleBuilder/EditorUtil.BundleBuilder.md)。

## Resources 规则的边界

- `Resources/BuiltIn/**` 可以正常通过 `Resources.Load`、`Resources.LoadAsync` 等接口加载。
- UPM 与 `Assets/**` 下第三方插件自己的 Resources 和加载代码合法。
- 不扫描未声明的整个 `Assets` 并据此阻断 Play 或 Build。
- 对无法确定归属的目录先报告信息或 Warning，由 Agent 向用户核实。
- 业务代码直接使用 `Resources.Load` 不等于项目损坏；它可能失去版本、依赖和释放能力，因此更适合作为迁移建议。
- 已有自定义 Addressables、StreamingAssets 或文件系统方案不应被静默改写。

ProjectGuard 若执行资源检查，只应在项目明确声明的 Nova 托管业务范围内给出建议；插件和 BuiltIn 豁免不依赖 Contract 存在。

## 构建前检查

普通开发构建确认当前资源能在目标平台加载即可。只有项目显式启用 Release Strict 时，才要求它声明的 Bundle fingerprint、代表资源或真实 Player Smoke 等发布证据。Pipify 可以编排这些步骤，但不是资源构建或 Player 构建的唯一合法入口。

