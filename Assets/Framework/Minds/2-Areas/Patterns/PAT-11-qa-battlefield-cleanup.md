---
id: PAT-11
title: qa 测试结束清场铁律与测试脚本命名规范
type: pattern
status: active
date: 2026-05-16
summary: qa测试结束必须清场场景与Inspector复原
category: review
aliases:
  - PAT-11-qa-battlefield-cleanup
keywords:
  - PAT-11
  - PAT-11-qa-battlefield-cleanup
  - qa 测试结束清场铁律与测试脚本命名规范
tags:
  - pattern
  - qa
  - testing
  - nova
  - workflow
related:
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[ADR-004-static-runtime-review-split|ADR-004]]"
---

# PAT-11：qa 测试结束清场铁律与测试脚本命名规范

## 适用场景（When）

- qa-reviewer 完成「编译 + Inspector + Play Mode `[PASS]`/`[FAIL]`」三步验证后
- 任何在场景内挂临时 GameObject、修改 Inspector 字段、写 PlayerPrefs/EditorPrefs 的运行时验证
- 在被测模块附近就近创建测试脚本时（命名带「关键词+YYYYMMDD」）

## 核心做法（What & How）

### 一、清场清单（必须项）

qa 出 `[PASS]` 报告前**强制**给出清场清单，逐项确认已恢复：

| 维度 | 检查项 |
|---|---|
| 场景 GameObject | 测试期间临时挂的 `__TestRunner__` / 临时调试节点 / 测试 GO 全部删除 |
| Inspector 数据 | 所有被 SerializedObject 改动过的字段一一改回默认值或测试前快照 |
| Editor 菜单状态 | 临时切换的构建路径、配置开关、运行模式回滚 |
| PlayerPrefs/EditorPrefs | 测试期间 `Set*` 的键值对全部 `DeleteKey` |
| 临时文件 | StreamingAssets/PersistentDataPath/Library 下测试产生的文件清理 |
| 场景保存 | 清场后**保存场景**（防止 Editor 关掉前 lose 改动） |

### 二、保留项

- 工程目录中的**测试资源**（Prefab / SO / Texture / Audio）保留
- 工程目录中的**测试脚本本身**保留
- 这两类是"测试基础设施"，跨会话复用，不属于"脏数据"

### 三、测试脚本命名规范（零容忍）

class 名必须**同时**含两个要素：

1. **测试内容关键词**（PascalCase 英文，体现验证主题）
2. **YYYYMMDD 日期**（运行 qa 验证的日期）

#### 命名范式

```
Test{ContentKeyword}_{YYYYMMDD}
{Module}TestOn{YYYYMMDD}_{Topic}
```

#### 示例

| 命名 | 评价 | 理由 |
|---|---|---|
| `TestPlayModeSplitAndLinkage_20260515` | ✅ | 内容（PlayMode 拆分 + 联动）+ 日期齐 |
| `AssetTestOn20260515_PlayModeSplit` | ✅ | 模块 + 日期 + 主题齐 |
| `Test` | ❌ | 既无内容也无日期 |
| `TestEnableHotfix` | ❌ | 缺日期 |
| `Test20260515` | ❌ | 缺内容 |
| `TestNew` / `TestTemp` / `MyTest` | ❌ | 内容毫无信息 |

#### 拆文件规则

- 历史遗留的通用 `Test.cs` 不强制重命名（拆历史 TC 是大工程）
- 但**新增 TC 必须拆出独立文件**，不允许追加进通用 `Test.cs`
- 一份文件聚焦一个主题，一个日期；同主题次日复跑可以追加 TC，不强制每天新文件

### 四、qa 清场失败的处理

- qa 报告里清场清单缺项 → 主会话直接 `[REJECT]`，要求复跑清场
- 主会话发现场景 / Inspector 有脏数据 → 主动派 qa 二次清场，不允许"反正下次会注意"

## 为什么这么做（Why）

- **避免脏数据污染下次会话**：测试期间 Inspector 字段被改、忘了改回，下次同事打开工程看到的是"测试态默认值"，做错决策
- **避免误把测试态当生产态提交**：场景里残留 `__TestRunner__`、Inspector 数值偏移，git diff 一不小心就 commit 进去
- **避免多人协作翻车**：拉到不可用的工程状态，浪费排查时间
- **命名带日期 + 内容**：事后回溯"哪天验过哪条规则"一目了然，避免测试堆积变历史考古；删测试时也能基于"这条规则现在还需要验证吗"做判断
- **拆出独立测试文件**：通用 `Test.cs` 越塞越大，类内动辄上千行 + 多个独立模块的 TC 混在一起；拆出单文件后，用例的"模块归属"和"验证主题"通过文件名直接表达

## 反模式（Anti-patterns）

- **静默清场反模式**：qa 报告只写 `[PASS]` 不列清场清单。问题：主会话/用户无法核对清场是否真做了，下次发现脏数据时责任不清
- **可保留 = 不收拾反模式**：把"测试资源/脚本可保留"误读为"修改完不用收拾"。问题：可保留的是脚本和资源**文件**，不是脏数据；Inspector 数据不是文件，是序列化在场景/Asset 里的状态
- **追加进 Test.cs 反模式**：新模块 TC 直接 append 到通用 `Test.cs`。问题：文件膨胀，主题混淆，删除时不敢删（怕误伤其他 TC）
- **`TestSomething` 无日期反模式**：class 名只写内容不写日期。问题：半年后想知道"这条规则什么时候验过"只能 git blame 找文件首次提交时间，效率低
- **`Test20260515` 无内容反模式**：class 名只写日期不写内容。问题：同一天可能验多条规则，日期+空白内容名根本无法区分
- **临时 GO 不删反模式**：场景里挂个 `__TestRunner_W6__` 跑完不删，依赖"下次发现再说"。问题：用户提交前 git status 看到一堆场景修改，反复回滚浪费时间
- **Inspector 改完不存场景反模式**：清场恢复了 Inspector 默认值，但忘了 Ctrl+S 保存场景，关 Editor 时 Unity 提示保存却选 Discard。问题：清场动作白做

## 跨项目复用提示

- 通用规范——任何 Unity 项目接入运行时验证流程都适用
- 非 Unity 项目（WPF/Avalonia/Web）的 e2e 测试同理：测试结束清测试库数据、清 cookie、清缓存、关闭测试创建的进程；class 命名规范（Topic + YYYYMMDD）通用
- 关键判据：「测试结束后，工程的运行态是否与测试开始前一致」——一致即清场到位

