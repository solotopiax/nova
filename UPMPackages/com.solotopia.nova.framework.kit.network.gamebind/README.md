# Nova Framework - Kit - Network - GameBind

> 包名：`com.solotopia.nova.framework.kit.network.gamebind`
> 当前版本：`0.2.0`

账号绑定业务网络模块，封装绑定状态查询、绑定、冲突查询与裁决协议及类型。

完整的登录、Header 身份、绑定冲突、顶号和存档编排说明见 [账号登录与三方绑定业务流程](./Nova/Docs/AccountLoginAndThirdPartyBindFlow.md)。

## 职责边界

本模块只负责账号归属查询与裁决：查询指定 OpenID 是否已绑定、为当前账号绑定三方 `openid`、查询绑定冲突详情、二选一裁决。`QueryBindingAsync` 是只读查询，不修改当前网络身份。**不处理存档数据覆盖**——数据流向（本地覆盖云端 / 云端覆盖本地）由业务层配合 `gamesave` 模块编排完成。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.kit.network.gamebind": "0.2.0"
}
```

## 示例工程

在 Package Manager 中导入 `GameBindDemo`，再从实际导入目录打开同名场景；命名空间为 `NovaFramework.Kit.Network.GameBind.Samples`。源码开发态的来源由本包 [示例清单](nova-samples.json) 的 `sourceDir` 标明，不作为消费项目的固定导入位置。

> **示例前置依赖**：`GameBindDemo` 演示「先登录取得当前账号 → 再绑定三方号」的完整流程，登录步骤使用 `com.solotopia.nova.framework.kit.network.gamelogin` 的 `Login` 服务。导入本示例前请确保工程已安装 `gamelogin` 包，否则示例脚本会因引用 `Login` 类型而编译失败。
>
> 注意：该依赖仅存在于**示例工程**层面（演示登录前提）；`gamebind` 运行时包本身**不依赖** `gamelogin`——绑定只做账号归属裁决，登录态由宿主的登录体系提供，二者职责分离。

## 维护

公开 API、配置与跨包流程入口见 [Nova/Docs/INDEX.md](./Nova/Docs/INDEX.md)。

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| `com.solotopia.nova.framework.kit.network.gamelogin` | `Nova/Docs/INDEX.md` | `Nova/Docs/Login.md` |
