---
id: PAT-150
title: Editor 多仓库渐进加载与变更就绪门
type: pattern
status: active
date: 2026-08-05
summary: 多仓库并发渐进展示，变更操作等待数据集完整
category: editor
aliases:
  - PAT-150-editor-multi-registry-progressive-load-mutation-gate
  - Editor 多源渐进加载
keywords:
  - PAT-150
  - EditorWindow
  - 多仓库并发
  - 渐进展示
  - 变更就绪门
  - registry
  - 空地址禁用
tags:
  - pattern
  - editor
  - unity
  - async
  - registry
related:
  - "[[PAT-18-editor-window-vs-util-split|PAT-18]]"
  - "[[ADR-064-plugpals-dependency-detection|ADR-064]]"
  - "[[MOC-Inspector|MOC-Inspector]]"
---

# PAT-150：Editor 多仓库渐进加载与变更就绪门

## 适用场景

- EditorWindow 同时依赖两个或更多相互独立的远端数据源。
- 某个内网、私有或可选数据源在部分使用者环境中必然不可达。
- 列表允许先浏览，但安装、卸载、写配置等操作依赖所有数据源的合并结果。
- 仓库地址允许由使用者清空，以显式关闭对应数据源。

## 核心做法

### 1. 独立数据源同时发起

所有已配置数据源应同时开始请求，不要按“公网完成后再请求内网”的顺序串行等待。串行请求会把总等待时间放大为各数据源耗时之和；并发后，总等待上限由最慢数据源决定。

每个请求返回统一结果对象，至少包含：来源、数据、错误。窗口使用 `Task.WhenAny` 或等价机制逐个消费已完成结果，不用 `Task.WhenAll` 把首个可用列表继续挡在最慢请求之后。

### 2. 展示就绪与变更就绪分离

任一数据源成功后即可刷新筛选结果并展示列表；另一侧仍在加载时，界面明确提示“可浏览，暂不可变更”。

这是依赖全量数据时的默认规则。产品明确要求仅等待某个必需来源时，应统一该来源的窗口门禁，并在模块文档中明确其余来源未完成时的数据缺口；PlugPals 的特例见下节。

如果安装依赖检测、共享 registry 清理或批量升级依赖合并后的全量数据集，则在所有请求结束前必须禁用这些变更入口。仅禁用主按钮不够，展开卡片、折叠行和批量操作等所有等价入口都要使用同一就绪条件。

原因是“列表已可见”不代表“决策数据完整”：提前安装可能把尚未返回仓库中的依赖误判为缺失；提前卸载可能漏算其他已安装包仍使用的 scoped registry，进而误删共享配置。

### 3. 保存新地址必须取消旧请求

使用者可能在加载期间清空不可达仓库并点击保存。保存后应取消旧地址请求、废弃旧结果并立即用新配置重新拉取；旧请求即使稍后完成，也不能回写窗口状态。

### 4. 区分“没有存档”与“存档为空”

- 配置文件不存在或无法解析：使用默认地址，保证首次打开可用。
- 配置文件存在：以存档字段为准，空字符串是有效值，表示显式禁用对应仓库。
- 保存时不得把空字符串再次归一化为默认地址。
- 所有读取该配置的消费者都必须把空地址解释为“跳过请求”。

默认值是首次初始化策略，不是对用户显式空值的持续覆盖策略。

## PlugPals 落地

PlugPals 的公网与内部云列表请求同时开始，单仓库列表完成后立即更新窗口状态；共用 `HttpClient` 超时为 10 秒。按产品要求，已配置的公网包列表请求未完成时，仅在页签及包列表所在内容区显示居中等待提示；上方标题和工具栏保留，允许修改仓库地址并保存重试。公网列表请求成功或失败后恢复内容区。公网地址为空时跳过等待，仅内部云仓库仍在加载时不限制窗口及包操作。

发布时间属于展示补充信息，不进入列表就绪门：先展示已有信息，再在后台原位补齐缺失时间，不重建安装状态。补充请求绑定原来源与取消令牌，刷新、改址或关闭后废弃旧批次；不能让非必要详情查询拖住列表和包操作。

此特例允许在内部云数据尚未完整时操作已返回条目，不能据此推断依赖预检或共享 registry 清理已获得全部数据，也不改变 Agent 包操作独立的全来源计划要求。

`ProjectSettings/Nova/PlugPalsRegistries.json` 不存在时填充默认公网和内网地址；文件存在时保留实际 URL，包括空值。保存发生在加载期间时取消旧令牌并重新请求。PlugPals 与 CheckUpdate 都把空 URL 作为跳过对应仓库的信号。

## 失败边界

本模式解决的是“请求尚未完成时使用半份数据”的竞态，不自动消除“某仓库最终请求失败后数据不完整”的业务风险。PlugPals 依赖检测对 registry 内存列表的依赖及其失败降级债务仍由 [[ADR-064-plugpals-dependency-detection|ADR-064]] 约束，不能把“所有请求已结束”误写成“所有数据源均成功”。

## 反模式

- 多仓库逐个 `await`，让不可达内网仓库阻塞已成功的公网列表。
- 使用 `Task.WhenAll` 后才一次性提交 UI，名义并发但仍不渐进展示。
- 列表一出现就开放安装或卸载，忽略依赖决策仍缺少另一侧数据。
- 只禁用一个操作入口，遗漏折叠行、详情区或批量按钮。
- 保存空地址时自动回填默认地址，使“禁用仓库”无法持久化。
- 保存新地址后让旧请求继续回写，造成警告、列表与输入框状态互相矛盾。

## 验证依据

- 并发与渐进更新：`Assets/Framework/Scripts/Editor/Windows/PlugPalsWindow/PlugPalsWindow.Methods.cs`
- 加载态展示与变更入口门禁：`Assets/Framework/Scripts/Editor/Windows/PlugPalsWindow/PlugPalsWindow.cs`、`PlugPalsWindow.Methods.cs`
- URL 存档语义：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.Registries.cs`
- 10 秒超时：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.Visitors.cs`
- 空地址消费：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.CheckUpdate/EditorUtil.CheckUpdate.Methods.cs`
- 回归测试：`Assets/Tests/Editor/PlugPalsRegistriesTests.cs`
- 静态验证：`NovaFramework.Editor.csproj` 与 `NovaFramework.Runtime.Tests.Editor.csproj` 编译均为 0 error；`git diff --check HEAD` 通过。Nova2 未连接 Unity MCP，本次未实际执行 EditMode Test Runner。

## 关联

- [[PAT-18-editor-window-vs-util-split|PAT-18]]：窗口持有加载与交互状态，网络和 registry 业务能力继续归 `EditorUtil.PlugPals`。
- [[ADR-064-plugpals-dependency-detection|ADR-064]]：安装决策依赖完整的内存 registry 数据，加载中不能提前执行。
- [[MOC-Inspector|MOC-Inspector]]：EditorWindow 与 Editor 工具模式入口。
