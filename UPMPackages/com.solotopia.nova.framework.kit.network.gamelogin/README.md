# Nova Framework - Kit - Network - GameLogin

> 包名：`com.solotopia.nova.framework.kit.network.gamelogin`
> 当前版本：`0.2.0`

登录业务网络模块，封装登录协议及相关协议类型

登录成功会更新该 UID 的最新设备；旧设备后续访问受保护接口收到 `10400`。账号绑定与冲突裁决请使用独立 GameBind package，完整流程手册位于该包的 [Nova/Docs/AccountLoginAndThirdPartyBindFlow.md](README.md#跨包文档定位)（`com.solotopia.nova.framework.kit.network.gamebind` 包内 `Nova/Docs/AccountLoginAndThirdPartyBindFlow.md`）。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.kit.network.gamelogin": "0.2.0"
}
```

## 示例工程

在 Package Manager 中导入 `GameLoginDemo`，再从实际导入目录打开同名场景；命名空间为 `NovaFramework.Kit.Network.GameLogin.Samples`。源码开发态的来源由本包 [示例清单](nova-samples.json) 的 `sourceDir` 标明，不作为消费项目的固定导入位置。

## 维护

公开 API、配置与示例入口见 [Nova/Docs/INDEX.md](./Nova/Docs/INDEX.md)。

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| `com.solotopia.nova.framework.kit.network.gamebind` | `Nova/Docs/INDEX.md` | `Nova/Docs/AccountLoginAndThirdPartyBindFlow.md`<br>`Nova/Docs/Bind.md` |
