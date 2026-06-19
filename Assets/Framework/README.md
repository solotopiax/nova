# Nova Framework

> 包名：`com.solotopia.nova.framework`
> 当前版本：`0.5.31`
> Unity：`6000.4`

Nova Framework 主包，Unity Component + Manager 架构，提供框架核心、各业务 Component 入口与默认 MainDemo Sample。

## 安装

通过 UPM Scoped Registry 以依赖形式接入（注册表地址与可用版本见发布说明 / 向维护团队索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework": "0.5.31"
}
```

> 商业 / 第三方原厂包不随主框架分发；可选商业依赖的接入方式见下文“可选商业依赖”。

## 静态访问器（Nova.*）

| 访问器 | 类型 | 说明 |
|---|---|---|
| `Nova.App` | `AppComponent` | 应用生命周期 / 全局上下文 |
| `Nova.Asset` | `AssetComponent` | 资源加载（YooAsset 封装）、Prefab / Scene / Asset |
| `Nova.Config` | `ConfigComponent` | 全局配置读取与运行时缓存 |
| `Nova.Prefab` | `PrefabComponent` | 预制体实例化与池化 |
| `Nova.Event` | `EventComponent` | 发布-订阅事件，`Fire` / `FireNow` 两种模式 |
| `Nova.Table` | `TableComponent` | Luban 配置表加载与查询 |
| `Nova.Localization` | `LocalizationComponent` | 多语言文本与字体表 |
| `Nova.UI` | `UIComponent` | UIView 生命周期、分组、对象池、分辨率适配 |
| `Nova.Network` | `NetworkComponent` | HTTP / WebSocket / DoH，AES 加密与重连 |
| `Nova.Procedure` | `ProcedureComponent` | 有限状态机驱动的流程编排 |
| `Nova.ObjectPool` | `ObjectPoolComponent` | 通用对象池 |
| `Nova.Persist` | `PersistComponent` | PlayerPrefs / FileFragment / SQLite 三后端 |
| `Nova.Sound` | `SoundComponent` | 音频播放与音轨管理 |
| `Nova.Vibrate` | `VibrateComponent` | NiceVibrations 接入 |
| `Nova.SDK` | `SDKComponent` | 第三方 SDK 接入 |
| `Nova.Debug` | `DebugComponent` | 运行时调试悬浮窗 |

> 热更：HybridCLR 代码热更 + YooAsset 资源热更，由 `ProcedureLoadDll` / `Nova.Asset` 联合驱动，无独立 `Hotfix` 静态访问器。

## 依赖

主包 `dependencies`（与 `package.json` 一致）：

| 包名 | 版本 |
|---|---|
| com.unity.textmeshpro | 3.0.9 |
| com.unity.nuget.newtonsoft-json | 3.2.2 |
| com.unity.inputsystem | 1.19.0 |
| com.solotopia.hybridclr | 10.0.6 |
| com.solotopia.unitask | 10.0.5 |
| com.solotopia.sqlcipher4unity3d | 10.0.5 |
| com.solotopia.simplediskutils | 1.0.7 |
| com.solotopia.excelio | 1.0.6 |
| com.solotopia.nicevibrations | 10.0.5 |
| com.solotopia.luban | 10.0.6 |
| com.solotopia.yooasset | 1.0.6 |

## 可选商业依赖

HTTP 可选 BestHTTP 后端已拆分为独立包 `com.solotopia.nova.framework.besthttp`。主框架只提供 HTTP Transport SPI 和缺省失败后端，不直接包含 BestHTTP 适配代码。

`com.solotopia.nova.framework.besthttp` 依赖原厂包 `com.tivadar.best.http` 与 `com.tivadar.best.tlssecurity`。使用者需自行购买原厂包，并通过其官方或自有 registry 安装（可在 PlugPalsWindow 工具栏填入 registry 地址，保存到 `ProjectSettings/Nova/PlugPalsRegistries.json`，该文件不入库）；TLS 包会依赖 HTTP 包。

外部使用者可自行购买并导入原厂包；若程序集名匹配 `com.Tivadar.Best.HTTP` / `com.Tivadar.Best.TLSSecurity`，独立适配包内的 `NovaFramework.BestHTTP.Runtime` 会直接编译启用。

## Samples

包内置 `Samples~/MainDemo`，可在 Unity Package Manager 面板按需导入，导入后会自动检测旧版本残留并提示清理、自动设置启动场景。

## 文档

详细的框架 API 与架构文档位于 `Assets/Framework/Docs/`：

- `ARCHITECTURE.md` — 架构总览
- `INDEX.md` — 类型索引
- `Runtime/` — 运行时 API 文档
- `Editor/` — 编辑器扩展文档

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 协议

MIT License，详见 [LICENSE.md](./LICENSE.md)。
