# Nova Framework - Kit - Network - GameSave

> 包名：`com.solotopia.nova.framework.kit.network.gamesave`
> 当前版本：`0.2.0`

游戏存档业务网络模块，封装云存档上传与拉取协议及类型

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.kit.network.gamesave": "0.2.0"
}
```

## 维护

公开 API 与配置入口见 [Nova/Docs/INDEX.md](./Nova/Docs/INDEX.md)。

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
