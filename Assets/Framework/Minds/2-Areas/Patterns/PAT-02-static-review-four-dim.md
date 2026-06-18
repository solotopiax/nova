---
id: PAT-02
title: 静态审查四维度框架
type: pattern
status: active
date: 2026-05-14
summary: 静态审查四维度逻辑风格架构安全性能
category: review
aliases:
  - PAT-02
keywords:
  - PAT-02
  - 静态审查四维度框架
tags:
  - pattern
  - methodology
  - code-review
  - quality
related:
  - "[[PAT-01-defect-severity|PAT-01]]"
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-05-l0-l1-l2-docs|PAT-05]]"
  - "[[ADR-002-manager-priority-system|ADR-002]]"
---

# PAT-02：静态审查四维度框架

## 适用场景（When）

- 代码合入前需要一道"不跑代码也能挡住大部分 bug"的关卡
- 团队里逻辑审查、风格审查、安全审查、性能审查分散在不同人/不同时机，难以追责
- 需要把"什么算审过"标准化，让审查报告可对账、可复审
- 想让运行时验证（QA）只关心"跑得对不对"，不再被风格 / 架构问题污染

## 核心做法（What & How）

把静态审查拆成**四个独立维度**，缺一不可。审查输入是 `git diff + 上下文 md/源码`。

### 维度一：逻辑正确性（diff 级 bug 猎杀）

| 检查面 | 关键问题 |
|--------|---------|
| 删除/修改 | 被删代码是否含副作用？被改分支是否改变原执行路径？被移除的校验调用方是否依赖？ |
| 边界条件 | null / 空集合 / 极端值 / 字符串 / 循环边界 / switch default |
| 接口契约 | 方法签名变更后所有调用方同步？接口实现类同步？序列化兼容？事件订阅方适配？ |
| 状态机 / 生命周期 | 转换覆盖完整？OnEnable/OnDisable 对称？Shutdown 路径释放完整？ |
| 异步 / 协程 | CancellationToken？await 后 this 是否已销毁？fire-and-forget 异常吞没？async void 滥用？ |
| 集合 / 并发 | foreach 中 Add/Remove？共享集合并发？池配对？ |
| 静默失败 | 空 catch / 危险 fallback / 丢失堆栈 / log-and-forget |

### 维度二：规范与架构合规

- 命名、注释、单行长度、文件拆分、目录结构是否符合团队风格规范
- 继承链 / 分层依赖 / 工具类优先级是否合规
- **文档同步**铁律：新建类必有 md，公开 API 变更同步 md，索引文件同步

> [!warning] 文档未同步 = 任务未完成
> 文档同步是维度二的强制子项，不是可选建议。

### 维度三：安全（CRITICAL，一票否决）

发现任一项直接 REJECT：命令注入 / 路径遍历 / 不安全反序列化 / 硬编码密钥 / 空 catch 吞异常。

### 维度四：性能（MEDIUM，建议修复）

热路径分配、LINQ 滥用、IEnumerable 多次枚举、Dictionary 双查、sync-over-async、未 sealed 等。

### 执行规则

- **顺序**：先逻辑后规范，发现 CRITICAL/P0 立即 REJECT，不再继续后续维度
- **修复边界**：reviewer 可直接修复确定性缺陷（明显 null 遗漏、命名前缀错），不确定的只标 ISSUE 不替写
- **迭代**：coder 修改后只审增量 diff，避免重复劳动
- **输出**：通过即出 `[CHECK-PASS]` 报告，按维度列出已确认无问题的文件

## 为什么这么做（Why）

- **职责分离**：把审查拆维度后，每个维度都有明确的 checklist，避免"凭感觉过审"
- **减少 QA 负担**：静态层把 80% 的 bug 拦在跑代码之前，运行时验证只需关注行为正确性，链路更短
- **CRITICAL 一票否决**：安全问题不可妥协，分维度后给安全维度独立的否决权，不会被"性能 vs 风格"的讨论稀释
- **可对账**：四维度 → 每条 ISSUE 标注维度 → 审查报告按维度归档，事后追溯哪一维度漏了
- **审查者更专注**：知道自己在看哪一维度，不会"看了一半被风格吸引走"

## 反模式（Anti-patterns）

- **混合维度**：一份审查报告里逻辑、风格、性能混在一起，看完不知道是不是真的看全了
- **风格优先**：reviewer 第一句"这里命名不规范"，逻辑 bug 留到最后，coder 修了风格已经累，逻辑修复不彻底
- **安全维度缺席**：只看逻辑和风格，不看注入/反序列化/密钥，把安全审查推给"以后专项做"
- **运行时验证替代静态审查**：寄希望于"跑跑看就知道有没有问题"，但静态层能查的契约不一致 / 资源泄露很多 case 跑不出来
- **不修复就标 P4**：reviewer 不愿担风险，把所有 ISSUE 都标 P4 让 coder 选择性忽略
- **reviewer 替 coder 大段改**：超出"确定性缺陷"边界，把业务逻辑也改了，coder 失去对代码的所有权

## 跨项目复用提示

- **维度框架完全通用**：任何静态语言项目（Java/C#/Go/Rust/TS）都可套用四维度
- 维度一"逻辑正确性"具体清单需按语言特化：Rust 项目要加"unsafe / unwrap / Send-Sync"；JS 要加"this 绑定 / 异步竞态 / 类型动态化"
- 维度二"规范"必须配套**已存在的**风格规范文档（如 `.editorconfig` / `style guide`），否则 reviewer 没有判据
- 维度三"安全"清单按业务调整：Web 项目加 CSRF/XSS；移动端加证书校验；服务端加输入校验/限流
- 维度四"性能"在不同领域权重不同：游戏客户端是热路径优先；企业 SaaS 可降级为非阻塞建议
- **配套必备**：缺陷分级（[[PAT-01-defect-severity|PAT-01]]）+ 文档同步规则（[[PAT-04-read-what-you-change|PAT-04]] / [[PAT-05-l0-l1-l2-docs|PAT-05]]）；缺一执行不下去

## 关联

- 配套：[[PAT-01-defect-severity|PAT-01]] 缺陷严重度 / [[PAT-03-runtime-verify-three-step|PAT-03]] 运行时验证 / [[PAT-05-l0-l1-l2-docs|PAT-05]] 文档体系
- 落地要求：静态审查不能脱离代码与当前 `Docs` 事实层
