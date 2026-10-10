---
id: ADR-090
title: 资源密钥独立于资源化配置并在启动程序集提供
status: accepted
date: 2026-10-09
summary: 资源密钥按包名和编号管理由Config保存并导出为启动源码
category: arch
keywords: [ADR-090, YooAsset, 资源加解密, ResourceKeyRegistry, keyId, ConfigWindow]
tags: [adr, nova, asset, config, encrypt]
related:
  - "[[ADR-078-privacy-aes-and-app-aes-separation]]"
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
  - "[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]"
  - "[[MOC-Asset]]"
  - "[[MOC-Config]]"
---

# ADR-090：资源密钥独立于资源化配置并在启动程序集提供

## 背景

ConfigRuntime 自身通过资源系统加载。如果解密密钥也只能从该资源读取，就会形成启动循环依赖。各 YooAsset Package 还可能同时存在采用新旧密钥的缓存、首包和热更新资源。Package 是资源包归属，不等于其中每个 Bundle 文件。

## 决策

- Config 通用配置中的“资源加解密配置”只管理项目共享密钥与源码导出目录，各 Package 独立保留 keyId 与密钥，另指定当前构建编号；算法仍在 YooAsset Bundle Builder 和 Pipify 原有三个字段选择，两处共用候选与排序，不重复建设逐 Bundle 配置界面。
- 密钥在 Editor ConfigMaster 工作副本编辑，由顶部全局保存提交；保存只写配置，顶部导出或 Pipify Config 导出生成客户端源码。生成目录使用项目组选择的 Assets 相对路径，命名空间按 Config 解析，不生成固定 asmdef。按实际目录归属核验其程序集随 Player 编译且不依赖热更程序集，资源初始化前注册密钥提供器。
- 文件头包含模式、包名、编号等格式元数据，不包含真实密钥；解密器按包名与编号精确请求密钥。更换密钥新增编号，旧资源仍存在时保留旧密钥；同一编号不可原位换值。当前编号只决定后续构建，不决定旧资源的解密密钥。
- ConfigMaster 与所选目录的生成代码、link.xml 及相关 .meta 一起纳入项目私有仓库，不进入 ConfigRuntime；团队与 CI 只读取激活且已保存的 Master。旧独立 JSON 迁移后移除，不保留环境变量第二来源；Player 构建前验证源码、裁剪声明与输入一致。框架公开仓库和发布包不携带项目真实密钥。
- 资源密钥与隐私本地持久化、应用网络协议密钥分域，不复用已有 Config AES 配置。
- Config 页仍提供统一三维头部：ConfigMaster.ResourceEncryption 保存维度掩码与各组合的当前加密编号，全部保留密钥按包名和编号共用。解密不依赖当前环境，减维不删除解密密钥；不同环境需要不同密钥时使用不同编号，保持文件头和运行时提供器契约不变。
- Editor 构建从激活且已保存的 ConfigMaster 显式注入密钥提供器，AES/XOR 清单各自对应同格式构建解密器；Runtime 由生成代码注册提供器，AssetManager 自动接入通用识别解密器。共用密码学实现，保持两种配置来源与使用阶段独立，客户端不另选构建下拉中的类型。
- 同一 Unity 项目只能存在一份实际生成的提供器，防止启动注册互相覆盖；不同 Master 可预存不同路径，空配置不强制生成。多 Master 导出不自动合并密钥，每次采用当前 Master 的集合。
- 框架对外 Sample 默认采用不加密构建且无资源密钥。公开交付统一清除资源密钥配置和工具生成的提供器、裁剪文件；无法识别的生成物或密钥残留阻断发布，不以占位密钥替代，也不改写消费项目私有配置。

## 后果

消除配置资源与密钥的循环依赖，并支持新旧资源并存。项目组负责团队和 CI 密钥一致性，以及新客户端先携带新密钥的发布顺序。客户端代码中的密钥可被逆向提取；本方案不提供远端发放、签名或反回滚。

例如 Default 的首包使用 v1、热更使用 v2，客户端同时保留两把密钥。只有全部资源重新加密、切换新清单，且内置首包、缓存和回退不再使用 v1 后，才可在后续客户端删除 v1；重新编译客户端本身不改变资源密文。

## 被排除方案

- 密钥导出到 ConfigRuntime：加载该资源之前可能已需要密钥。
- 在 Config 再建算法选择：与官方构建页面和 Pipify 重复。
- 所有资源包只有一把可替换密钥：换值后无法消费仍存在的旧资源。
- Editor 构建依赖客户端启动注册：构建可能先于 Play/Player 启动，且注册来源不能代表已保存的当前 Master。
- 多份生成提供器各自注册：加载顺序会决定最终密钥集合，无法保证资源可解密。

## 来源与验证依据

- 用户确认的职责划分：Config 全局保存只保存配置，导出才生成客户端源码；密钥在 Config 管理，三个构建字段对齐官方 Bundle Builder。
- 配置与导出：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.ResourceEncryption/` 下的 `ResourceKeyStore.cs`、`ResourceKeyCodeGenerator.cs`；Player 门禁：`Assets/Framework/Scripts/Editor/BuildProcessor/Core/ResourceKeyBuildPreprocessor.cs`。
- 构建与客户端来源隔离：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.ResourceEncryption/ResourceEncryptors.cs`、`Assets/Framework/Scripts/Runtime/Modules/Asset/Definitions/ResourceEncryption/ResourceBundleDecryptors.cs`；默认加载注入：`Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/AssetManager/Implements/AssetManager.Methods.cs`。
- Docs：`Assets/Framework/Docs/Runtime/Modules/Asset/ResourceEncryption.md`。
- `Assets/Tests/Editor/ResourceEncryptionTests.cs` 覆盖四种格式、认证篡改、密钥错误、随机访问、八种掩码、加减维、旧配置兼容和构建平台解析；不据此声明 Android/iOS 真机验证完成。
- 长期 UI 契约遵循 [[PAT-161-configwindow-new-panel-dimension-header]]，保存归一遵循 [[ADR-058-per-panel-dimension-mask]]。

## 关联

- [[ADR-078-privacy-aes-and-app-aes-separation]]：资源、持久化与协议密钥分域。
- [[ADR-058-per-panel-dimension-mask|ADR-058]]、[[PAT-161-configwindow-new-panel-dimension-header|PAT-161]]：当前编号分维，全部解密密钥保留。
- [[MOC-Asset]]、[[MOC-Config]]：配置资源加载前的密钥来源与消费边界。
