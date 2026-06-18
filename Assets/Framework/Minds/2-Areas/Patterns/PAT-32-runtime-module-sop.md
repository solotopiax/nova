---
id: PAT-32
title: 新增 Runtime 模块 SOP（含文件命名 + 9 步骤）
type: pattern
status: active
date: 2026-05-18
summary: Runtime模块SOP三层接口Base实现模板
category: arch
aliases:
  - runtime-module-sop
keywords: [PAT-32, runtime-module-sop, 新增 Runtime 模块 SOP（含文件命名 + 9 步骤）]
tags:
  - nova
  - framework
  - sop
  - runtime
  - file-naming
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-008-managerbase-internal-abstract]]"
  - "[[ADR-016-framework-vs-business-access]]"
  - "[[ADR-017-component-manager-isolation]]"
---

# PAT-32：新增 Runtime 模块 SOP（含文件命名 + 9 步骤）

## 适用场景

- 在 `Assets/Framework/Scripts/Runtime/Modules/` 下新增一个 Component+Manager 模块时。
- 评审 Runtime 模块 PR 是否符合目录、命名与 partial 切分规范。

## 核心做法

### 一、文件 / 目录命名惯例（partial 拆分约束）

| 后缀 / 目录 | 用途 |
|---|---|
| `XxxComponent.cs` | Component 主文件（类声明 + `Awake` / `Start`） |
| `XxxComponent.Visitors.cs` | Component 属性访问器（`[SerializeField]` 字段 + 属性） |
| `XxxComponent.Yyy.cs` | Component 内嵌配置数据类（如 `UIComponent.UIGroup.cs`） |
| `XxxManager.cs` | Manager 主文件（构造器 + 接口方法实现） |
| `XxxManager.Methods.cs` | Manager `private` / `protected` 辅助方法 |
| `XxxManager.Visitors.cs` | Manager 字段 + 属性访问器 |
| `XxxManagerBase.cs` | Manager 抽象基类（接口虚 / 抽象方法、收敛重载） |
| `IXxxManager.cs` | Manager 接口（放 `Interfaces/` 目录） |
| `Definitions/` | 枚举、配置数据类、纯数据载体（无业务逻辑） |
| `Interfaces/` | 接口文件 |
| `Implements/` | 接口具体实现 |

> partial 拆分铁律：每个文件**只**承担表头标注的职责。`Visitors`/`Methods` 之间不准互相塞代码。

### 二、新增 Runtime 模块 9 步骤

```text
1. 在 Runtime/Modules/Xxx/ 下建目录：
   Xxx/
     Managers/
       Interfaces/   IXxxManager.cs
       Definitions/  XxxManagerConfig.cs、数据类
       Implements/   XxxManagerBase.cs、XxxManager.cs、XxxManager.Methods.cs
   XxxComponent.cs
   XxxComponent.Visitors.cs

2. 创建 IXxxManager.cs（接口）：声明所有业务 API

3. 创建 XxxManagerBase.cs：
   - 继承 FrameworkManager
   - 实现 IXxxManager
   - 设置 Priority（参考 ARCHITECTURE.md 优先级表）
   - 声明 abstract 核心方法
   - 提供重载的 virtual 默认实现

4. 创建 XxxManager.cs / XxxManager.Methods.cs：实现所有 abstract

5. 创建 XxxComponent.cs：
   - 继承 FrameworkComponent
   - Start() 中 Util.TypeCreator.Create<IXxxManager>(typeName) 注入

6. Nova.Visitors.cs 添加：
     public static XxxComponent Xxx { get; private set; }

7. Nova.Start() 中：
     Xxx = FrameworkComponentsGroup.GetComponent<XxxComponent>()

8. Editor/Inspectors/XxxComponentInspector/ 下建 Inspector
   （继承 BaseComponentInspector，详 `PAT-31-inspector-sop`）

9. 创建对应类映射文档，跟代码版本走，不入长期知识层
```

### 三、约束补丁

- ManagerBase 必须 `internal abstract`（详 [[ADR-008-managerbase-internal-abstract]]），对外只暴露 `public interface IXxxManager`
- Component 必须对外完全隔绝它持有的 Manager（详 [[ADR-017-component-manager-isolation]]）—— 除接口外，业务侧拿不到 Manager 实例
- 跨模块访问只走「Component 静态属性 → 公开 API」 + `FrameworkManagersGroup.GetManager<I>()` 两条路径，禁止 Manager 互调对方的具体类（详 [[ADR-016-framework-vs-business-access]]）

## 反模式

- ❌ Manager 在 `Awake()` 内构造——其他 Component 未就绪
- ❌ `XxxManager : FrameworkManager` 而省略 `XxxManagerBase`——重载收敛与 Priority 设置丢失锚点
- ❌ Component 上把 Manager 字段开成 `public` 或属性裸暴露——破坏 [[ADR-017-component-manager-isolation|ADR-017]] 隔绝
- ❌ `Visitors.cs` 里写业务方法 / `Methods.cs` 里塞 SerializeField——partial 职责漂移
- ❌ 跳过 step 6/7（不挂 `Nova.Xxx` 静态属性）——业务层无法走 `Nova.Xxx.PublicAPI` 入口
