# Nova Framework - Kit - Network - GameLogin

> 包名：`com.solotopia.nova.framework.kit.network.gamelogin`
> 当前版本：`0.1.8`

登录业务网络模块，封装登录协议及相关协议类型

登录成功会更新该 UID 的最新设备；旧设备后续访问受保护接口收到 `10400`。账号绑定与冲突裁决请使用独立 GameBind package，完整流程手册位于该包的 `Nova/Docs/AccountLoginAndThirdPartyBindFlow.md`。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.kit.network.gamelogin": "0.1.8"
}
```

## 示例工程

导入 Samples 时选择 `GameLoginDemo`，示例路径：`Assets/Samples/GameLoginDemo/`，命名空间 `NovaFramework.Kit.Network.GameLogin.Samples`。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。
