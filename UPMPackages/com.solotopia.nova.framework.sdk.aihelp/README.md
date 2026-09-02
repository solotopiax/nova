# Nova Framework - SDK - AIHelp

> 包名：`com.solotopia.nova.framework.sdk.aihelp`
> 当前版本：`0.0.13`

Nova Framework 的 AIHelp 智能客服 / 帮助中心对接层，封装 `AIHelpPlugin`（继承 `SDKPluginBase`，由 `SDKManager` 统一编排），提供智能客服会话、帮助中心 FAQ、未读消息 / 工单数、用户信息同步等能力。

## 安装

本包**随包 bundle AIHelp Unity SDK 6.0**（managed C# 与 iOS 原生库均已内置于 `Core/`），无需额外安装原厂包。

- Android 端 AAR 依赖由本包 `AIHelpBuildProcessor`（`IPostGenerateGradleAndroidProject`）在构建期自动注入到导出后的 Android gradle 工程（`unityLibrary/build.gradle`），声明 Maven 坐标 `net.aihelp:android-aihelp-aar:6.0.+`；无需 EDM4U，包内也没有 Dependencies.xml。
- iOS 端原生库（`AIHelpSupportSDK.framework` 等）随包直接落地在 `Core/Plugins/iOS/AIHelpSDK/`，构建 iOS 时按 Unity 插件导入规则自动链接。

## 快速上手

初始化由 `SDKManager` 在启动流程中读取 `AIHelpPluginConfig`（ServerCmdName / AppId 在 ConfigMaster 中配置；ServerCmdName 为 netcmd 指令名，运行时由框架 `INetworkManager` 解析出完整 URL 并取域名传给 AIHelp，AppId 从 AIHelp 后台获取）后自动完成，业务无需也不能手动调用 `Initialize`：

```csharp
// 打开某个入口的智能客服会话
if (Nova.SDK.TryGet(out AIHelpPlugin plugin))
{
    plugin.Show("entranceId");
}
```

详细公开方法清单、事件签名、自动登录说明见 [Nova/Doc/AIHelpPlugin.md](./Nova/Doc/AIHelpPlugin.md)。

## 目录结构

- `Nova/`：Nova 自有适配代码与文档。
- `Core/`：AIHelp Unity SDK 6.0 原样落地（managed C# `Core/AIHelp/` + iOS 原生 `Core/Plugins/iOS/AIHelpSDK/`），只读，不做业务改动。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- AIHelp SDK 及其随包分发内容的许可边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
