---
id: PAT-48
title: Unity Editor 引用计数 API 泄漏排查与排干
summary: 循环 Unlock/Stop 把泄漏计数一键归零
category: editor
type: pattern
status: active
date: 2026-05-20
aliases:
  - PAT-48-editor-refcount-api-leak-drain
keywords:
  - PAT-48
  - PAT-48-editor-refcount-api-leak-drain
  - Unity Editor 引用计数 API 泄漏排查与排干
tags:
  - pattern
  - editor
  - hybridclr
  - pipify
  - debugging
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
  - "[[ADR-027-rule-ban-editor-refcount-batch-apis|ADR-027]]"
  - "[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]"
  - "[[PAT-51-sbp-buildcache-vs-lock-reload-incompat|PAT-51]]"
---

# PAT-48：Unity Editor 引用计数 API 泄漏排查与排干

## 适用场景（When）

- 跑完某个 Editor 工具批处理后，**修改 cs 文件 Unity 不再触发编译**
- 跑完某个批处理后，**ImportAsset / AssetDatabase.Refresh 看似生效但产物无变化**
- Console 出现 "Cannot label ... properly" / "Resolution Failed" 这类 importer 排队报错
- 代码里出现过 `LockReloadAssemblies` / `StartAssetEditing` 调用且历史发生过异常路径

## 核心做法（What & How）

### 一、先确认是不是引用计数泄漏

| 现象 | 嫌疑 API |
|---|---|
| 修改 cs 不编译 | `LockReloadAssemblies` 计数 > 0 |
| Importer 看似不跑 / SBP cache 命中陈旧 hash | `StartAssetEditing` 计数 > 0 |

两者都是**只增不漏报错**的 API，泄漏后 Editor UI 不会有任何提示。

### 二、用 UnityMCP execute_code 一键排干

```csharp
for (int i = 0; i < 100; i++) UnityEditor.EditorApplication.UnlockReloadAssemblies();
for (int i = 0; i < 100; i++) UnityEditor.AssetDatabase.StopAssetEditing();
return "drained";
```

- Unlock / Stop 在计数已为 0 时是 no-op，多调 100 次完全安全
- 等本轮 Domain Reload 完成后再改 cs 验证编译触发
- 不要重启 Editor——重启会丢失 Console 日志和 Window 状态，无法定位真凶

### 三、找真凶并修代码

排干是急救，不是治根。必须 grep 全仓：

```bash
grep -rn "LockReloadAssemblies\|UnlockReloadAssemblies\|StartAssetEditing\|StopAssetEditing" --include="*.cs"
```

定位每一处调用，按 `csharp-code-style.md §4·1` 删除。Step / 工具不能假设别的代码持锁——本会话的 EDM4U Step 就是因为单边 finally 净 +1 而每跑一次全量批就泄漏一次。

## 为什么这么做（Why）

- **引用计数式 API 在异常 / 取消 / 反射调用 / Editor 强退路径下无法保证 finally 跑到**——一旦泄漏就是永久故障，重启 Editor 是唯一路径
- 排干脚本是"用魔法打败魔法"——这两组 API 都没有"读当前计数"的接口，只能盲目 Unlock / Stop 把计数压到 0
- 故障复现极难，发现一次必须当场修根因，否则下次还会再来

## 反模式（Anti-patterns）

- ❌ 重启 Editor 当唯一止血手段——丢失定位线索 + 用户工作流被打断
- ❌ "下次跑批前手动调一次 Unlock/Stop"——治标不治本，且违反规则层禁用清单
- ❌ Step 之间隐式持锁假设——见 `[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]`
- ❌ 在 try/finally 里写"豁免别人的锁"代码（Stop/Unlock 开头 + Lock/Start 收尾）——Runner 不锁时这个 finally 净增 1，每跑一次泄漏一次

## 跨项目复用提示

通用——任何使用 HybridCLR / YooAsset / 自研批流水线的 Unity 项目都可能踩到。Editor 引用计数 API 不限于这两组，凡是 `XxxBegin/XxxEnd` 配对、内部用 int 计数的 Editor API 都适用同一套排查方法。

