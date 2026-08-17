---
id: ADR-078
title: 隐私 AES 默认密钥与应用协议 AES 密钥分域
status: accepted
summary: 隐私默认 AES 与应用协议 AES 分域
category: arch
date: 2026-08-13
aliases:
  - ADR-078-privacy-aes-and-app-aes-separation
  - AES 密钥分域
keywords:
  - ADR-078
  - PrivacyConfigs
  - AppConfigs
  - AppAesKey
  - AppAesIV
  - Util.Encrypt.AES
  - Persist
  - NetService
  - AES 密钥分域
tags:
  - nova
  - config
  - encrypt
  - network
  - persist
related:
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
  - "[[PAT-108-upm-kit-public-api-collapse|PAT-108]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]"
---

# ADR-078：隐私 AES 默认密钥与应用协议 AES 密钥分域

## 背景

Nova 同时存在两组 AES Key/IV，但它们的职责不同：

- `PrivacyConfigs.AESKey/AESIV` 是按 `Platform × Channel × DevelopMode` 导出的隐私配置，只为 `Util.Encrypt.AES` 的默认 Key/IV 提供运行期注入，并用于 Persist 等本地数据加解密。
- `AppConfigs.AppAesKey/AppAesIV` 是既有应用配置，属于服务端协议约定；Network、ThirdPay 等业务协议请求必须显式使用这组值。

若把两组字段迁移、复用或让协议请求回退到默认 AES，会把本地数据边界与服务端协议边界混为一谈。此前网络调试开关还允许跳过 AES，使 Debug 与 Release 的线上字节协议不一致。

## 决策

- `ConfigManager` 在 `Nova.Config.LoadAsync()` 加载 `ConfigRuntimeSO` 后，调用内部 `Util.Encrypt.AES.InitializeFromConfig(PrivacyConfigs)` 注入默认 Key/IV；全部 `FrameworkManager` 完成逆序 Shutdown 后，由 `FrameworkManagersGroup` 的 `finally` 清空该静态状态。
- 未显式传入 key/iv 的 `Util.Encrypt.AES` 调用只使用隐私配置默认值。配置未加载、字段缺失或 UTF-8 长度不是 16 字节时，调用失败；业务侧不得手动调用已废弃的 `Util.Encrypt.AES.Configure`。
- `AppConfigs.AppAesKey/AppAesIV` 保持在应用配置中，不迁移到隐私配置，也不得作为 `Util.Encrypt.AES` 默认值。
- `NetService` 固定从 `AppConfigs.AppAesKey/AppAesIV` 读取 Key/IV，并显式传给 `NetBuilder.Encrypt` 与 `NetParser.Decrypt`。ThirdPay 等经 `NetService` 发出的协议请求继承该约束，不能回退到隐私配置默认 AES。
- 网络传输不再暴露会切换明文的 `SetDebugMode` / `IsDebugMode` / 单次覆盖入口。Development Build 的诊断日志可以输出受控调试信息，但不改变 `Proto → AES → HTTP → AES 解密 → Proto` 的线上字节协议。
- Persist Inspector 读取 `WorkspaceActive` 当前 ConfigMaster 的合法坐标，并显式传入该格隐私 Key/IV；坐标校验不强制要求 Platform 与 Unity `activeBuildTarget` 一致。

## 后果

### 正面

- 本地 Persist 数据与服务端协议拥有清晰、独立的密钥来源，修改一方不会隐式改变另一方。
- Debug 与 Release 使用同一网络加解密链，联调结果可直接代表正式协议行为。
- 默认 AES 的生命周期绑定 Config 加载与 Shutdown，避免 Domain Reload 关闭时沿用旧配置。
- Editor Inspector 与 Runtime 使用同一份三维隐私配置，但 Editor 不依赖当前 Unity 构建目标。

### 代价

- 配置维护者需要分别维护隐私 AES 与应用协议 AES，并确保每个值按 UTF-8 编码均为 16 字节。
- 需要本地默认 AES 的调用方必须等待 `Nova.Config.LoadAsync()` 完成；协议调用方则必须通过 `AppConfigs` 提供有效的 Key/IV。
- 旧的手动默认 AES 初始化和网络明文调试用法不再可用，需要改为配置修复与受控日志诊断。

## 被排除方案

| 方案 | 否决理由 |
|---|---|
| 将 `AppAesKey/AppAesIV` 迁入或复用为隐私配置默认值 | 会把服务端协议密钥与本地数据密钥耦合，破坏两套配置的职责边界。 |
| 业务侧手动调用 `Util.Encrypt.AES.Configure` | 使默认密钥脱离 Config 生命周期和三维导出链，无法保证 Runtime 与 Inspector 一致。 |
| Debug 模式跳过 AES、发送明文 Proto | 造成 Debug 与 Release 字节协议分叉，掩盖真实联调问题，并扩大明文泄露面。 |
| Persist Inspector 强制匹配 `activeBuildTarget` | 当前 ConfigMaster 的合法三维坐标才是编辑配置的真相源；构建目标并非此处的额外约束。 |

## 验证依据

- Runtime：`ConfigManager` 在加载 `ConfigRuntimeSO` 后初始化 `Util.Encrypt.AES`；`Util.Encrypt.AES` 对默认与显式 Key/IV 分别校验。
- Network：`NetService` 从 `AppConfigs.AppAesKey/AppAesIV` 取值，并将其传入 `NetBuilder.Encrypt` 和 `NetParser.Decrypt`；ThirdPay 已删除其传输调试覆盖并通过 `NetService.SendAsync` 发请求。
- Editor：`PersistComponentInspector` 从 `WorkspaceActive` 解析当前隐私配置，并显式传入 AES Key/IV。
- Docs：`PrivacyConfigs.md`、`Util.Encrypt.md`、`NetService.md`。
- 测试：`PrivacyConfigAesContractTests` 覆盖字段分域、Editor 坐标约束和 Inspector 显式传参；`ThirdIapNetServiceContractTests` 覆盖 ThirdPay 调试传输入口已移除。

## 关联

- 三维配置面板与导出：[[ADR-058-per-panel-dimension-mask|ADR-058]]
- 仅暴露必要 Kit API：[[PAT-108-upm-kit-public-api-collapse|PAT-108]]
- 新配置面板三维头部：[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]
