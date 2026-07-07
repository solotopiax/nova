# Nova

![](Assets/Samples/AppIcons/Logo.png)

![license](https://img.shields.io/badge/license-MIT-blue.svg)
![release](https://img.shields.io/badge/release-v0.5.34-blue.svg)
![unity](https://img.shields.io/badge/unity-6000.4-blue.svg)
![PRs Welcome](https://img.shields.io/badge/PRs-welcome-blue.svg)

**Nova 是 Solotopia 基于 Unity 深度定制的一套游戏客户端通用解决方案**，把资源、UI、网络、热更、配置、存档、多语言、SDK 接入等通用底座统一收敛、持续维护，让业务团队只需聚焦玩法本身。

---

## 核心能力

Nova 以场景中的 `Nova` 根节点为统一入口，所有子系统经 `Nova.*` 静态门面暴露，调用契约一致、风格统一：

| 子系统 | 访问 | 职责 |
|---|---|---|
| 入口 | `Nova` | 框架全局根节点与静态访问器门面，挂载于场景即完成全部子系统的统一注册与生命周期接线，业务侧仅通过 `Nova.*` 调用。 |
| 事件 | `Nova.Event` | 支持泛型订阅 / 注销与按类型派发，事件参数基于引用池复用实现零 GC，跨模块通信的首选通道。 |
| 对象池 | `Nova.ObjectPool` | 对象池与引用池双轨：纯 C# 对象走 `IReference` 栈式分配回收，GameObject 走通用容器，覆盖高频实例化与销毁场景。 |
| 调试 | `Nova.Debug` | 运行时调试面板与诊断工具，提供运行期日志查看、性能指标监控与变量检视，便于真机与开发期问题定位。 |
| 配置 | `Nova.Config` | 运行时配置访问层，承载 SDK 插件配置与 Kit 套件配置的注入、查询与变更通知，业务侧不直接持有配置实例。 |
| 持久化 | `Nova.Persist` | 多后端本地存档，统一封装 `PlayerPrefs`、分片文件 `FileFragment` 与加密 `SQLite`，按数据规模与安全要求择优落盘。 |
| 资源管理 | `Nova.Asset` | 资源加载、下载、缓存与热更，基于 YooAsset 封装，提供同步 / 异步加载、引用计数与场景加载，释放由加载方负责。 |
| 预制体 | `Nova.Prefab` | 预制体实例化与回收，内部复用对象池，适合子弹、特效、列表项等高频生成销毁场景，避免运行期 GC 抖动。 |
| 数据表 | `Nova.Table` | 基于 Luban 导出的表格数据查询，按类型强类型取表与取行，支持运行期按需加载与表存在性判断。 |
| 本地化 | `Nova.Localization` | 多语言运行时切换，文本键取值、缺失检测与字体适配，支持运行期切换语言而无需重启。 |
| 音频 | `Nova.Sound` | 音频播放与声音组管理，按组独立调节音量与静音，区分 BGM、音效与 UI 音，支持播放暂停与释放。 |
| 触觉 | `Nova.Vibrate` | 触觉振动反馈，封装 Nice Vibrations，提供预设振动模式与自定义波形，适配 iOS / Android 差异。 |
| 界面 | `Nova.UI` | 界面生命周期与栈式管理，支持异步 / 同步打开关闭、界面分组与层级控制，自动复用实例池，覆盖弹窗、主界面与多层叠加场景。 |
| 网络 | `Nova.Network` | 网络通信层，封装 HTTP / WebSocket 请求与 DNS over HTTPS，支持连接复用、超时重试与统一错误处理，对接 BestHTTP 底层。 |
| SDK 接入 | `Nova.SDK` | 第三方 SDK 装配与初始化，统一管理广告（AdMob / MAX）、登录（Facebook / Apple / Google）、支付（IAP）、统计（AppsFlyer / Firebase）等插件。 |
| 流程 | `Nova.Procedure` | 基于有限状态机的启动流程编排，串联闪屏、版本检查、强更 / 热更、业务 DLL 加载等节点，节点间切换受控且可观测。 |
| 应用 | `Nova.App` | 应用入口，负责版本检查、强制更新与新安装包下载，协调各子系统按启动阶段顺序就绪，是运行期的顶层编排者。 |
| 检查器 (Editor) | `Inspectors` | 各 Component 的自定义 Inspector，配置字段在 Unity 面板内可视化编辑与校验，配合 HelpBox 提示约束，降低误配风险。 |
| 窗口 (Editor) | `Windows` | 配置、环境、流水线、包管理等编辑器工具面板，集中管理框架与项目级设置，提供可视化操作入口。 |
| 数据流水线 (Editor) | `DataPipeline` | 配置与资源的数据导出 / 处理流水线，串联 Luban 导出、Excel 读取与中间产物清理，保障表格数据从源到运行时的一致性。 |
| 构建处理 (Editor) | `BuildProcessor` | 构建预 / 后处理流程，覆盖 Android / iOS 平台、Manifest 规则、Gradle 模板与构建上下文，自动对齐发布设置。 |
| 编辑器工具 (Editor) | `EditorUtil` | 可复用编辑器基础设施，聚合 asmdef、Asset、Build、HybridCLR、Luban、Network、Pipify、PlugPals 等工具集，支撑上层工具开发。 |
| 工具 (Editor) | `Tools` | 更高层级的部署与一键工具入口，封装常用编辑器操作为可复用命令，便于团队定制与自动化。 |

---

## 安装

下载火种脚本 [NovaSpark2.0.cs](./NovaSpark2.0.cs)，拖入 Unity 工程，等待编译结束即可。

---

## 快速开始

```csharp
using NovaFramework.Runtime;
```

### 加载资源

```csharp
var handle = await Nova.Asset.LoadAsync<Sprite>("icons/hero");
image.sprite = handle.Asset;
handle.Release();
```

### 实例化 Prefab

```csharp
var instance = await Nova.Prefab.InstantiateAsync("prefabs/bullet", parent);
Nova.Prefab.Destroy(instance);
```

### 打开 UI

```csharp
await Nova.UI.OpenUIViewAsync<MyView>();
```

### 订阅与派发事件

```csharp
void OnEnable()  => Nova.Event.Subscribe<MyEvent>(OnEvent);
void OnDisable() => Nova.Event.Unsubscribe<MyEvent>(OnEvent);

var evt = ReferencePool.Get<MyEvent>();
Nova.Event.Fire(this, evt);
```

### 查询表格

```csharp
var row = Nova.Table.GetTable<TbItem>().Get(itemId);
```

---

## 目录结构

```
Assets/Framework/Scripts/Runtime/  # 运行时核心与业务模块
Assets/Framework/Scripts/Editor/   # 编辑器工具 / Inspector / 窗口 / 流水线
Assets/Framework/Docs/             # 事实层文档
Assets/Framework/Minds/            # 知识层文档（Obsidian Vault）
Assets/Samples/                    # 示例与演示（MainDemo 等）
UPMPackages/com.solotopia.*/       # 配套基础库 / Kit / SDK 包
Packages/manifest.json             # 工程依赖入口
```

---

## 文档体系

Nova 采用双层文档体系，团队多年沉淀持续累积：

```
Assets/Framework/
├── Docs/                # 事实层：当前 API、字段、调用步骤（随代码同步）
│   ├── INDEX.md         # 按任务类型导航
│   ├── ARCHITECTURE.md  # 总体架构
│   ├── Runtime/         # 运行时模块文档
│   └── Editor/          # 编辑器扩展文档
└── Minds/               # 知识层：架构决策、术语、模式、踩坑结论（Obsidian Vault）
```

- **Docs（事实层）回答"如何使用"** —— 日常实现任务与业务开发的优先入口。
- **Minds（知识层）回答"为何如此设计"** —— 调整 public API / 命名 / 架构边界时查阅。
- **冲突时以 Docs + 源码为基线**；未命中时如实说明，禁止凭印象编造。

将两层文档提供给 AI Agent，助手即可同时具备"会用 API + 知晓边界 + 沿用既有模式 + 避开已知坑"——这是最快上手的路径，也是项目长期可持续维护的保障。

---

## 许可证

本项目基于 [MIT License](https://opensource.org/licenses/MIT) 开源。

Copyright © 2026 Solotopia
