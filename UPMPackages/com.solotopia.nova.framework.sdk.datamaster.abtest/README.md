# Nova Framework - SDK - DataMaster - ABTest

> 包名：`com.solotopia.nova.framework.sdk.datamaster.abtest`
> 当前版本：`0.0.6`

Nova Framework 的 Starlus DataMaster 对接层，封装 `DataMasterPlugin`（继承 `SDKPluginBase`，由 `SDKManager` 统一编排），提供远程配置 / ABTest 读参、曝光打点与实验事件上报能力。

## 安装

本包只含 Nova 适配代码，**不含 DataMaster 原版**。安装本包后仍需安装原厂包：

- `com.starlus.sdk.datamaster`（Starlus DataMaster 原版）

内部成员可从 PlugPalsWindow 的「内部云仓库」安装原版包；原版包安装完成后，`NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime` 程序集会直接编译启用。

## 目录结构

- `Nova/`：Nova 自有适配代码与文档。
- `Core/`：第三方源码槽位。本包不分发 DataMaster 原版，因此当前为空。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 本包封装 Starlus 商业授权 SDK 的对接层，依赖不开源的原版包 `com.starlus.sdk.datamaster`，**不进入公开仓同步**。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- DataMaster 原版及其第三方组件（sqlite-net / SQLite / BouncyCastle）的许可随原版包 `com.starlus.sdk.datamaster` 分发。
