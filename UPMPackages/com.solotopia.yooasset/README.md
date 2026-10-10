# YooAsset

> 包名：`com.solotopia.yooasset`
> 当前版本：`1.2.0`

Unity 资源管理与热更新解决方案。Core 基线为官方 YooAsset `3.0.6`（commit `3b4cfb36cc2e81b9558ab74d65fee2cf2eac2804`），私包版本为 `1.2.0`。

保留 Nova 的 Settings 显式注入、配置路径提供器、BundleCollector 缓存重置及 `NovaFramework.Editor` 友元扩展；原生下载在缓存校验最终结果后通知 URL 策略，保持内容校验失败时的主备重试。Scriptable Build Pipeline 使用上游自动版本适配，当前依赖为 `2.6.1`。

升级到 3.0.6 的自定义异步操作须将 `InternalAbort()` 清理逻辑迁移到 `InternalDispose()`。

Bundle Builder 的三个加解密下拉通过 `EditorAssemblyUtility.GetBuildServiceTypes` 只列出可公开无参实例化的具体类型，None 优先，其余按全类型名排序；Nova/Pipify 共用该入口。构建直接实例化所选类型，不提供旧类名映射或附加筛选注册，不依赖 Nova 类型。该 Editor 本地补丁升级 Core 时须保留。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.yooasset": "1.2.0"
}
```

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留上游 Apache-2.0 许可材料 + 包根说明文件”的方式进入公开仓。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- `Core/` 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。
