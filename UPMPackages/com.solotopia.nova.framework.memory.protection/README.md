# Nova Framework - MemoryProtection

> 包名：`com.solotopia.nova.framework.memory.protection`
> 当前版本：`0.1.3`
> Unity：`6000.4.x`（最低 `6000.4.2f1`）

Nova 独立核心扩展包，提供 `ProtectedInt` / `ProtectedLong`、异常隔离、密钥轮换和脱敏埋点。主 Framework 不强制依赖本包；安装本包后无需增加 SDK、Component 或场景节点。

## 安装与调用

先安装 **Nova Framework - MemoryProtection**（`com.solotopia.nova.framework.memory.protection`）。NovaSpark 会默认安装本包；已有项目可打开 **Nova → Open PlugPals**，找到本包并安装。实际内存防篡改还需要兼容的 Anti-Cheat Toolkit，NovaSpark 不会自动安装该内网商业插件；安装方式见下文。

本包依赖 Framework `0.7.3` 及 Newtonsoft `3.2.2`。已发布的旧版 Framework `0.6.39` 不包含本包所需的通用隐私接口，不能作为消费端配对版本。

本包以 `nova.requiredFrameworkApiVersion = 0.7.0` 声明新接口的发布基线，本轮须与 Framework `0.7.3` 配对发布，或确认目标仓库已有该依赖版本；发布器会在打包写入前阻断错误配对。

```json
"dependencies": {
  "com.solotopia.nova.framework.memory.protection": "0.1.3"
}
```

公开命名空间保持 `NovaFramework.Runtime`，业务 asmdef 必须增加 `NovaFramework.MemoryProtection.Runtime` 引用；此前只引用 `NovaFramework.Runtime` 的调用方需要补充该引用。完整用法见 [内存数据防篡改](Nova/Docs/MemoryProtection.md)，文档索引见 [Nova/Docs/INDEX.md](Nova/Docs/INDEX.md)。

## Anti-Cheat Toolkit

本包不包含 ACTk 付费源码，也不把 ACTk 声明为强制 UPM 依赖。`Core/` 保留为空的第三方源码槽；插件检查不会替换或降级已安装版本。

外网用户从 [Unity 商店购买 Anti-Cheat Toolkit](https://marketplace.unity.com/packages/tools/utilities/anti-cheat-toolkit-202695)，再在 Package Manager 的 My Assets 下载并导入。内网获授权项目可安装自定义 UPM 包 `com.codestage.anti.cheat.tookit`，保留此指定拼写；不要同时保留 Assets 与 UPM 副本。

插件未安装或 API 不兼容时，配置保存、导出、Play 和普通数值兼容读写仍可使用，但没有内存防篡改能力。安装后在 `Nova/Open Config → 通用配置 → 隐私配置` 检查插件状态。

## 配置与示例

包内 Editor 通过主框架通用隐私面板扩展提供检测、换钥开关，默认均开启。设置保存在 `PrivacyConfigs.ExtensionConfigsJson` 的本包键内，沿用原三维配置导出链；旧配置已有的 `false` 自动迁移并保持关闭。

Package Manager 可导入本包 `MemoryProtectionDemo` Sample；主仓开发入口为 `Assets/Samples/MemoryProtectionDemo/MemoryProtectionDemo.unity`。MainDemo 已移除原 Core 1.10 演示。埋点表位于 `Nova/Tracks/Tracks.xlsx`，由主框架 TrackRegistry 自动汇总。

## 维护与协议

变更记录见 [CHANGELOG.md](CHANGELOG.md)，协议见 [LICENSE.md](LICENSE.md)。ACTk 商业许可由购买者单独取得，Nova 包许可不授予 ACTk 源码或分发权限。
