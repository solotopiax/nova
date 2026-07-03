# Nova Framework - Kit - Network - GameBind

> 包名：`com.solotopia.nova.framework.kit.network.gamebind`
> 当前版本：`0.0.1`

账号绑定业务网络模块，封装绑定、冲突查询与裁决协议及类型。

## 职责边界

本模块只负责账号归属裁决：为当前账号绑定三方 `open_id`、查询绑定冲突详情、二选一裁决。**不处理存档数据覆盖**——数据流向（本地覆盖云端 / 云端覆盖本地）由业务层配合 `gamesave` 模块编排完成。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.kit.network.gamebind": "0.0.1"
}
```

## 示例工程

导入 Samples 时选择 `GameBindDemo`，示例路径：`Assets/Samples/GameBindDemo/`，命名空间 `NovaFramework.Kit.Network.GameBind.Samples`。

> **示例前置依赖**：`GameBindDemo` 演示「先登录取得当前账号 → 再绑定三方号」的完整流程，登录步骤使用 `com.solotopia.nova.framework.kit.network.gamelogin` 的 `Login` 服务。导入本示例前请确保工程已安装 `gamelogin` 包，否则示例脚本会因引用 `Login` 类型而编译失败。
>
> 注意：该依赖仅存在于**示例工程**层面（演示登录前提）；`gamebind` 运行时包本身**不依赖** `gamelogin`——绑定只做账号归属裁决，登录态由宿主的登录体系提供，二者职责分离。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
