# Nova Framework 全模块深度架构审计 — 设计规格

> 日期：2026-04-12
> 状态：已批准

---

## 1. 审计目标

以 12 年+ Unity 商业项目顶级架构师视角，对 Nova Framework 全部 16 个业务模块 + 基础层 + 工具层 + Editor 层进行**逐文件、逐方法级**深度审计，产出可直接落地的架构复盘、风险清单与优化方案。

## 2. 审计范围

### 2.1 输入

| 类别 | 路径 | 文件数 |
|------|------|--------|
| Runtime C# | `Assets/Framework/Scripts/Runtime/` | 438 |
| Editor C# | `Assets/Framework/Scripts/Editor/` | 131 |
| 技术文档 | `Assets/Framework/Docs/` | 358 |
| 规范文件 | `.claude/rules/*.md` | 5 |

**总计：569 个 C# 文件 + 358 个文档**

### 2.2 排除

- `Assets/Game/` 业务层代码
- 第三方插件、Unity 内置包
- `.meta` 文件

## 3. 审计视角（5 维度）

| 维度 | 关注点 |
|------|--------|
| 架构合规性 | 三层继承链、Component+Manager 分离、接口解耦、目录结构 |
| 逻辑正确性 | 空引用、边界条件、竞态、生命周期、异步时序 |
| 性能隐患 | GC 分配、Update 滥用、资源泄露、对象池有效性 |
| 代码风格 | 命名前缀(m_/c_/s_)、XML 注释、Partial 拆分、Log 级别 |
| 文档同步率 | 代码 vs 文档一致性、默认值核对、方法名核对 |

## 4. 分批执行策略

### T1 高危批（6 模块，并行）

| 模块 | 文件范围 | 审计重点 |
|------|----------|----------|
| **Network** | `Components/Networks/` | 4 子 Manager(DoH/Http/Network/WebSocket)、异步时序、跨线程消息分发、AES 加解密、WebSocket 重连状态机 |
| **Event** | `Components/Events/` | EventPool 泛型实现、EventTypeID 注册表、订阅/发布生命周期、EventData 引用池回收 |
| **Asset** | `Components/Assets/` | 3 子 Manager(AB/Asset/Prefab)、引用计数、弱引用缓存、异步加载回调链、WebGL 适配 |
| **UI** | `Components/UIs/` | UIManager+UIViewManager+UIGroupHelper 三层、深度排序算法、UIView 生命周期、泛型 Open API |
| **Hotfix** | `Components/Hotfixs/` | 版本比对算法、多协程并发下载、资源矫正、增量策略、10 个事件子类 |
| **Localization** | `Components/Localizations/` | 语言切换状态机、ResolveLanguage 回退算法、TextLocalizing TMP 刷新链、字体适配加载 |

### T2 中危批（5 模块，并行）

| 模块 | 文件范围 | 审计重点 |
|------|----------|----------|
| **Persist** | `Components/Persists/` | 三独立 Manager(PlayerPrefs/FileFragment/SQLite)、脏标记/事务批量/WAL、WebGL 禁用逻辑 |
| **Procedure** | `Components/Procedures/` | FSM 驱动流程、状态切换时序、Launcher UI 生命周期、内置 7 流程 |
| **ObjectPool** | `Components/Objects/` | 池化策略、释放筛选器、自动过期、容量管理、ObjectBase 生命周期 |
| **Table** | `Components/Tables/` | 表格数据加载/缓存、ITableData 接口一致性 |
| **Config** | `Components/Configs/` | JSON 配置加载/反序列化、ConfigSettings 序列化 |

### T3 低危批（5 模块 + 基础层，并行）+ 全局汇总

| 模块 | 文件范围 | 审计重点 |
|------|----------|----------|
| **Debug** | `Components/Debugs/` | IMGUI 窗口系统、磁盘检测、GM 工具、性能计数器 |
| **SDK** | `Components/SDKs/` | 插件扫描机制、异步初始化顺序、MonoBehaviour 基类 |
| **Sound** | `Components/Sounds/` | 声音组优先级抢占、Agent 池化、CTS 取消 |
| **Vibrate** | `Components/Vibrates/` | 链式播放、CTS 管理 |
| **Nova + Bases + Utils** | `Components/Nova/` + `Bases/` + `Utils/` | 根节点生命周期、数据结构正确性、工具类边界 |

### 执行顺序

```
T1 并行(6 agent) → T1 汇总
    ↓
T2 并行(5 agent) → T2 汇总
    ↓
T3 并行(5 agent) → T3 汇总
    ↓
全局架构复盘 + 终极优化方案
```

## 5. 缺陷分级标准

| 级别 | 定义 | 处理要求 |
|------|------|----------|
| P0 | 崩溃级（NullRef、越界、死锁、编译错误） | 必须定位到文件:行号，给出修复代码 |
| P1 | 数据错误（Off-by-one、条件逻辑反转、状态丢失） | 必须定位，给出修复方向 |
| P2 | 资源泄露（未释放、未取消订阅、引用计数不平衡） | 必须定位，给出修复方向 |
| P3 | 竞态与并发（异步安全、集合迭代中修改、跨线程） | 定位 + 风险评估 |
| P4 | 边界条件（空集合、null 字符串、极端输入） | 定位 + 建议 |

## 6. 代码风格审查项

对照 `.claude/rules/csharp-code-style.md`：

- [ ] 命名前缀（m_/c_/s_）
- [ ] 单语句单行（≤120 必须单行）
- [ ] 禁止对齐空格
- [ ] XML 注释完备性（多行 summary、param、returns）
- [ ] Partial class 四文件拆分合规
- [ ] Log 级别合规（禁用 Log.Info）
- [ ] 禁止分组分隔注释

## 7. 产出物

| 产出 | 格式 | 位置 |
|------|------|------|
| 模块审计报告 x16 | Markdown | `.claude/plans/audit-reports/` |
| 全局架构复盘 | Markdown | `.claude/plans/audit-reports/00-architecture-review.md` |
| 风险总清单 | Markdown 表格 | `.claude/plans/audit-reports/00-risk-registry.md` |
| 终极优化方案 | Markdown | `.claude/plans/audit-reports/00-optimization-plan.md` |

### 单模块报告结构

```markdown
# {ModuleName} 模块审计报告

## 1. 架构评价
- 继承链合规性
- 依赖关系
- 职责边界评分（1-5）

## 2. 风险清单
| 级别 | 文件:行号 | 问题描述 | 修复建议 |
|------|-----------|----------|----------|

## 3. 代码风格违规项
| 文件:行号 | 违规项 | 说明 |

## 4. 文档同步偏差
| 代码事实 | 文档描述 | 偏差 |

## 5. 模块级优化建议
```

## 8. 约束与假设

- 审计基于静态代码分析，不执行运行时测试
- 以当前 develop 分支 HEAD 为审计快照
- Editor 侧代码（Inspector/DataPipeline）随对应 Runtime 模块一并审计
- 文档偏差仅标记，不在本次审计中修复
