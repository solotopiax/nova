---
id: PAT-61
title: 版本一致性优先借助现有资源系统 manifest 而非自造校验
summary: manifest 已统管版本禁自加版本戳
category: hotfix
type: pattern
status: active
date: 2026-05-21
aliases:
  - PAT-61-version-consistency-prefer-manifest
keywords:
  - PAT-61
  - 版本一致性优先借助现有资源系统 manifest 而非自造校验
tags:
  - pattern
  - hybridclr
  - asset
  - yooasset
  - anti-overengineering
related: []
---

# PAT-61：版本一致性优先借助现有资源系统 manifest 而非自造校验

## 适用场景（When）

需要保证多份产物（如热更 dll + 游戏资源 AB + 配置 SO）在客户端运行时**版本同步**，避免"新 AB + 旧 dll"等错位组合导致 Missing Script / 反序列化失败。

第一直觉容易跳到"在每个产物头部插版本戳 + 启动期比对拒入"——这往往是**过度设计**。

## 核心做法（What & How）

1. **先问一句**：项目里是否已有资源系统（YooAsset / AssetBundle 原生 / Addressables）做整批 manifest？
2. **能塞进 manifest 的就塞进 manifest**：
   - dll 字节走 TextAsset 通道（`.dll.bytes`），拷贝到 `Assets/` 下作为普通资源参与构建
   - 与所有 AB 进入同一份 manifest 版本号
   - 发版按批次整体上传 CDN，manifest 版本即"全套版本"
3. **运行时只校验 manifest 版本**：资源系统自身有 manifest 版本机制（YooAsset 的 PackageVersion 等），客户端启动期取 manifest 版本即可，**不在 dll 字节流单独插版本戳，不在 ProcedureLoadDll 加 dll-vs-AB 比对**。
4. **构建管线绑死原子产出**：把 dll 编译/拷贝/AB 打包/打包出包串到同一次构建（Pipify 这类管线工具），杜绝单边发版可能性。

## 为什么这么做（Why）

- **资源系统 manifest 已经是单一真相源**：自造 dll 版本戳本质是**第二份**版本机制，与 manifest 形成两套不一致风险，反而比单一 manifest 更脆弱。
- **复杂度成本**：插版本戳要改 dll 字节流 / 改 `DllAssetEntry` / 改 `ConfigRuntimeSO` / 改 `ProcedureLoadDll` 加比对逻辑 / 加错位拒入 UI / 加灰度回滚策略——而 manifest 路径只需"按批次上传"这条工作流约定。
- **AI 容易跳到自造校验**：本会话 AI 第一版回答就把"dll/AB 版本强一致校验"列为"框架层必须扛住的事"，是典型过度设计；用户提醒"我们用 yooasset 同批次打包"后才意识到不需要。

## 反模式（Anti-patterns）

- 在 dll 字节流头部插版本戳 → manifest 已有版本，重复
- 在 `DllAssetEntry` / `ConfigRuntimeSO` 加 `Version` 字段供启动期比对 → 配置侧两套版本，必然漂移
- 在 `ProcedureLoadDll` 加 dll-vs-AB 版本比对逻辑 → 增加启动期失败点，没有真实收益
- 设计"灰度发版只发 dll 不发 AB"工作流 → 哪怕技术上可行，破坏 manifest 原子性，未来每次诊断 Missing Script 都要先排查"是不是当时只发了 dll"

## 跨项目复用提示

通用反过度设计原则，可推广到：
- 多语言资源 + 翻译表版本同步
- 配置表 + 代码版本同步
- 任意"产物 A + 产物 B 必须同版本"场景

判断口诀：**"已有 manifest 能管的，不要自己再管一层"**。

