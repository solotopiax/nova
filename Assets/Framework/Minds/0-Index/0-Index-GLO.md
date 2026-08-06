---
title: Nova GLO 索引（Layer 1）
auto_generated: true
---

# Nova Knowledge Base — GLO 索引

> **本文件由 `$nova-obs health --rebuild-index` 自动生成，请勿手工编辑。**
> 维护方法：修改对应入库文件的 frontmatter（`summary` / `category` / `title`），重跑命令。

加载策略：当用户问及 GLO 相关内容时再加载本文；命中具体编号后再 `obsidian_get_note` 拉全文。

## arch

- [GLO-02 Manager 三层继承链（FrameworkManager / ManagerBase / Manager）](../2-Areas/Glossary/GLO-02-framework-manager-tiers.md) — Manager 采用接口 + Base + 唯一实现的固定结构
- [GLO-03 Component / Procedure / Manager 职责边界](../2-Areas/Glossary/GLO-03-component-procedure-manager.md) — 三者职责分工
- [GLO-06 Nova 框架设计模式映射](../2-Areas/Glossary/GLO-06-design-patterns-map.md) — 用通用设计模式理解 Nova 的主要结构与机制
- [GLO-20 asmdef 与 namespace 的边界](../2-Areas/Glossary/GLO-20-asmdef-namespace-boundary.md) — asmdef 决定编译依赖，namespace 只组织代码名称

## asset

- [GLO-10 YooAsset 资源管理层](../2-Areas/Glossary/GLO-10-yooasset-asset-management.md) — YooAsset 是 Nova 的资源管理层
- [GLO-18 AssetBundle、Addressables 与 YooAsset 边界](../2-Areas/Glossary/GLO-18-assetbundle-addressables-yooasset-boundary.md) — Nova用YooAsset管理Bundle资源

## core

- [GLO-04 EditorUtil.Draw / Util.TypeCreator / Util.Json](../2-Areas/Glossary/GLO-04-utility-classes.md) — Nova 中优先使用的几个核心工具入口

## docs

- [GLO-05 三级文档体系（L0 / L1 / L2 + INDEX）](../2-Areas/Glossary/GLO-05-three-tier-docs.md) — Docs 由 L0、L1、L2 与 INDEX 组成

## editor

- [GLO-17 Unity Editor 的 Inspector 与 EditorWindow](../2-Areas/Glossary/GLO-17-unity-editor-inspector-window.md) — Inspector 编辑对象契约，Window 承载独立工具流程

## external

- [GLO-12 UniTask 异步基础设施](../2-Areas/Glossary/GLO-12-unitask-async-await.md) — UniTask 是零分配异步库

## hotfix

- [GLO-11 HybridCLR 业务 DLL 热更](../2-Areas/Glossary/GLO-11-hybridclr-hotfix-dll.md) — HybridCLR 承载业务 DLL 热更

## module

- [GLO-08 DataMaster 分流用户属性口径（app_version / install_time 必传）](../2-Areas/Glossary/GLO-08-datamaster-user-properties.md) — 两条必传分流属性的口径：版本号 + 安装时间
- [GLO-19 TMP_Text 文本组件](../2-Areas/Glossary/GLO-19-tmp-text-component.md) — TMP_Text是依赖字体材质链的文本基类

## naming

- [GLO-07 AssetLocation / Asset 地址](../2-Areas/Glossary/GLO-07-asset-location.md) — AssetLocation 统一称为 Asset 地址
- [GLO-09 运行平台、运营渠道与第三方登录提供方](../2-Areas/Glossary/GLO-09-channel-and-third-login-provider.md) — 区分运行平台、运营渠道与登录提供方

## runtime

- [GLO-13 ObjectPool 可复用对象池](../2-Areas/Glossary/GLO-13-objectpool-reusable-objects.md) — ObjectPool 池化带生命周期对象
- [GLO-14 ReferencePool 纯数据引用池](../2-Areas/Glossary/GLO-14-referencepool-data-refs.md) — ReferencePool 池化纯数据对象
- [GLO-15 Fsm 有限状态机与 Procedure](../2-Areas/Glossary/GLO-15-fsm-state-machine.md) — Fsm 驱动 Procedure 状态流转
- [GLO-16 Unity 对象与生命周期基元](../2-Areas/Glossary/GLO-16-unity-object-lifecycle-primitives.md) — 区分场景对象、组件、层级节点与数据资产


---
_共 19 条，分布于 10 个 category。_
