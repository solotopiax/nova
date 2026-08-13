---
name: nova-project-data-driven-ui
description: Use when 项目组要把当前已接入并可加载的 Nova 数据表绑定到一个业务 UIView、从既有入口打开并完成分阶段验证，且任务跨越表、UI、Prefab、注册与导出时使用。
---

# Nova 数据驱动业务界面

先读取 `references/contract.json`，并使用 `nova-project-ui-create-view` 承担创建/注册页面的闭环。该 Workflow 只编排已有消费端能力，不直接改 Nova Manager、UIManager、表生成物或 Framework 包。

## 冻结业务契约

必须确认表类型、主键/选中规则、展示字段、缺行处理、语言与图标来源、目标 View/UIGroup、主页入口、返回行为和重复点击策略。禁止默认取“第一行”、硬编码表数据、在 UI 内重复加载表或替用户猜测导航语义。

确认表已经纳入当前项目的运行时加载链，且可经公开 `Nova.Table.HasTable<T>()` / `GetTable<T>()` 使用；若表源、Binding、加载时序或 UIGroup 不成立，返回 `blocked` 或转交尚未实现的表接入 Operation。表源没有变化时跳过表导出；本任务不因 ProjectGuard 的无关 Error 自动执行 Config 导出或修复。

## 依赖图

```text
冻结输入
 ├─ 只读：表类型、字段、加载时序
 ├─ 只读：UIGroup、注册源、主页入口
 └─ 只读：当前导出与项目约束
             ↓
          决策门
             ↓
  创建/注册 View ── 绑定表数据与主页入口
             ↓
     Unity/Prefab 写入（独占）
             ↓
       选定 UI 导出 → 编译 → Play smoke
```

三个只读节点可以并行；任何 Unity、AssetDatabase、活动场景或同一 UI 注册源写入必须串行。Pipify Batch 自身是顺序、失败即停的 Action Adapter，不能替代本图的并发与恢复判断。

## 验证与恢复

验证入口按钮只触发一次，目标行字段值准确，缺表/缺 ID 有明确处理，页面关闭返回正常，池化重开会刷新数据。Play smoke 未执行时仅返回 `partial`；外部服务、真机和 CDN 结果不能由本 Workflow 推断。

每个写入节点记录精确输入、前置 hash 和局部验证结果。普通源码冲突停止并交给人工；导出使用已有事务/恢复能力；外部写入不自动重试或补偿。任何节点失败后不继续后续节点，也不把部分结果报告为 `success`。
