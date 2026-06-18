---
title: Nova PAT 索引（Layer 1）
auto_generated: true
---

# Nova Knowledge Base — PAT 索引

> **本文件由 `$nova-obs health --rebuild-index` 自动生成，请勿手工编辑。**
> 维护方法：修改对应入库文件的 frontmatter（`summary` / `category` / `title`），重跑命令。

加载策略：当用户问及 PAT 相关内容时再加载本文；命中具体编号后再 `obsidian_get_note` 拉全文。

## arch

- [PAT-08 架构反模式红旗清单](../2-Areas/Patterns/PAT-08-architecture-antipatterns.md) — 用红旗清单快速识别 Nova 架构偏航
- [PAT-107 禁 Proto 协议字段与外层包装容器字段重复](../2-Areas/Patterns/PAT-107-no-redundant-wrapper-with-proto-field.md) — Proto 已自带的协议字段禁再封装容器外层字段重复持有
- [PAT-108 UPM Kit 包对外 API 收口模式](../2-Areas/Patterns/PAT-108-upm-kit-public-api-collapse.md) — 跨 asmdef public API 从 IDE 补全隐藏的解法
- [PAT-112 ManagerConfig 宿主 Inspector → 子结构透传链路](../2-Areas/Patterns/PAT-112-managerconfig-host-to-leaf-passthrough.md) — Inspector 字段经 Config 透传到子结构
- [PAT-30 Nova 框架使用红线（陷阱清单）](../2-Areas/Patterns/PAT-30-framework-usage-redlines.md) — 框架使用红线汇总命名访问绕封装清单
- [PAT-32 新增 Runtime 模块 SOP（含文件命名 + 9 步骤）](../2-Areas/Patterns/PAT-32-runtime-module-sop.md) — Runtime模块SOP三层接口Base实现模板

## asset

- [PAT-125 运行时资源增量更新三步走与 YooAsset 缓存寻址认知](../2-Areas/Patterns/PAT-125-runtime-incremental-three-steps.md) — 刷清单按tag下载运行三步；tag不落盘无需维护列表
- [PAT-138 Editor HostPlayMode 跨平台真实包 Shader 重绑排障](../2-Areas/Patterns/PAT-138-editor-hostplaymode-cross-platform-shader-rebind.md) — 真实包洋红块先验shader
- [PAT-28 Luban DataReceiver 资源对称释放：Build*Delegates + assetLocationMap 反查](../2-Areas/Patterns/PAT-28-luban-load-release-symmetric.md) — Luban Load与Release必须对称释放
- [PAT-37 Runtime 侧禁止在 Asset 模块外直接依赖 YooAsset](../2-Areas/Patterns/PAT-37-no-yooasset-outside-asset-module.md) — YooAsset 细节只留在 Asset 模块
- [PAT-70 YooAsset PackRule 选型与 PackTopDirectory 陷阱](../2-Areas/Patterns/PAT-70-yooasset-packrule-selection.md) — PackRule 按粒度选；TopDir 需子目录

## config

- [PAT-27 Config 类型保持数据化，不承载序列化行为](../2-Areas/Patterns/PAT-27-config-no-serialize.md) — Config 只承载数据不承载行为

## core

- [PAT-36 入 git 的路径配置字段强制项目根相对路径](../2-Areas/Patterns/PAT-36-git-tracked-paths-relative-to-project-root.md) — 入git路径字段强制项目根相对路径

## demo

- [PAT-102 按钮内副提示叠加布局：主文字 stretch+Center 副提示锚点贴底](../2-Areas/Patterns/PAT-102-button-overlay-sub-hint-layout.md) — 主文字双居中+副提示锚点贴底
- [PAT-105 Demo View API 提示就近显示双色规范](../2-Areas/Patterns/PAT-105-api-hint-near-element-split.md) — 一接口一提示就近挂；按钮深蓝、字段白色，标题区清空
- [PAT-80 Demo View 纯色块与 TMP 样式基线](../2-Areas/Patterns/PAT-80-demo-view-pure-color-style.md) — Demo 默认纯色块加 TMP

## docs

- [PAT-05 L0/L1/L2 三级文档体系 + S 级标准](../2-Areas/Patterns/PAT-05-l0-l1-l2-docs.md) — Docs 采用 L0/L1/L2 分层并保持互不重复
- [PAT-109 UPM Kit/SDK 包必备 Nova/Docs 文档目录](../2-Areas/Patterns/PAT-109-upm-package-docs-mandatory.md) — Kit/SDK 包必带 Nova/Docs 与 INDEX.md
- [PAT-115 CHANGELOG 写人话](../2-Areas/Patterns/PAT-115-changelog-human-readable.md) — 每项变更一句用户视角概括，禁列字段、签名、文件清单
- [PAT-116 cs↔Docs 镜像同步铁律](../2-Areas/Patterns/PAT-116-cs-doc-mirror-sync.md) — 代码事实变化时必须同轮刷新 Docs 镜像
- [PAT-65 Demo 接口覆盖标准：8 维 + 取舍规则](../2-Areas/Patterns/PAT-65-demo-coverage-standard.md) — 8 维覆盖矩阵 + 重载族折叠 + 模块 12 叶子上限
- [PAT-77 BaseDemoView 三段式 Demo View 模板](../2-Areas/Patterns/PAT-77-base-demo-view-three-zone-template.md) — 标题栏+交互区+反馈区三段式+Nova门面API演示
- [PAT-79 Demo View 竖屏布局铁律（768×1666 + match-by-width）](../2-Areas/Patterns/PAT-79-demo-view-portrait-layout.md) — 竖屏768x1666 三段式 Scaler运行时注入
- [PAT-84 Demo View 控件左侧 Label 模式（酌情 + 140px 固定列）](../2-Areas/Patterns/PAT-84-demo-view-control-left-label.md) — 酌情加左 Label 140px+Row HLG 撑剩余

## editor

- [PAT-133 Editor 外部 CLI 解析：PATH+候选兜底，版本卡实测闭区间](../2-Areas/Patterns/PAT-133-editor-cli-path-resolve-version-range.md) — Editor CLI 解析 PATH 兜底，版本卡闭区间
- [PAT-135 SerializeReference 跨格深拷贝陷阱](../2-Areas/Patterns/PAT-135-serializeref-crosscell-deepcopy-trap.md) — boxedValue 跨格须 JsonUtility 深拷贝
- [PAT-18 EditorWindow 与 EditorUtil 职责分离](../2-Areas/Patterns/PAT-18-editor-window-vs-util-split.md) — EditorWindow与EditorUtil分层职责拆分
- [PAT-35 Editor 绘制统一走 EditorUtil.Draw](../2-Areas/Patterns/PAT-35-editor-draw-only.md) — 业务侧 Editor 绘制统一走 Draw
- [PAT-39 EditorUtil.Draw 纪律强化规则](../2-Areas/Patterns/PAT-39-editor-draw-discipline-enforcement.md) — 缺 Draw 接口先补接口再改 UI
- [PAT-46 多轮 Editor 迭代时每轮都做封装自检](../2-Areas/Patterns/PAT-46-iteration-grep-self-check.md) — 每轮 Editor 迭代后都做封装自检
- [PAT-48 Unity Editor 引用计数 API 泄漏排查与排干](../2-Areas/Patterns/PAT-48-editor-refcount-api-leak-drain.md) — 循环 Unlock/Stop 把泄漏计数一键归零
- [PAT-49 Pipify Step 严禁假设 Runner 持有批锁](../2-Areas/Patterns/PAT-49-pipify-step-no-batch-lock-assumption.md) — Step 内严禁做批锁豁免脚手架
- [PAT-50 Unity 原生 ProgressBar 必须 delayCall 推到下一帧](../2-Areas/Patterns/PAT-50-unity-progressbar-must-delaycall.md) — 同步栈内 Clear 不销毁，弹 Toast 会卡死
- [PAT-51 SBP BuildCache 与 LockReloadAssemblies 不兼容](../2-Areas/Patterns/PAT-51-sbp-buildcache-vs-lock-reload-incompat.md) — Lock 期 hash 不刷致 bundle 命中陈旧

## governance

- [PAT-34 Minds 只沉淀 Nova Framework 本体长期知识](../2-Areas/Patterns/PAT-34-minds-scope-nova-only.md) — Minds 只收录框架长期知识

## hotfix

- [PAT-137 启动前必需依赖不得反向依赖热更后资源](../2-Areas/Patterns/PAT-137-startup-bootstrap-no-hotfix-resource-backref.md) — bootstrap 依赖不可反向绑定热更后资源 否则形成启动闭环
- [PAT-43 启动期"可选远端检查"的异常宽容策略](../2-Areas/Patterns/PAT-43-optional-remote-check-tolerance.md) — 启动期可选远端检查异常宽容降级返回
- [PAT-44 ProcedureCheckVersion 两阶段编排：App 大版本 → Asset 资源差异，ForcedDownload 短路](../2-Areas/Patterns/PAT-44-procedure-checkversion-two-stage.md) — ProcedureCheckVersion两阶段拆分
- [PAT-61 版本一致性优先借助现有资源系统 manifest 而非自造校验](../2-Areas/Patterns/PAT-61-version-consistency-prefer-manifest.md) — manifest 已统管版本禁自加版本戳

## inspector

- [PAT-09 Inspector 配置分组与中文说明模式](../2-Areas/Patterns/PAT-09-inspector-config-i18n.md) — Inspector 配置按业务分组并配中文说明
- [PAT-10 IMGUI 自定义 Popup 必须 Layout.Horizontal 包裹 Label + 控件](../2-Areas/Patterns/PAT-10-imgui-popup-horizontal-wrap.md) — IMGUI Popup需横向包裹避免错位抖动
- [PAT-128 Inspector 持久化 UI 状态勿每帧强写](../2-Areas/Patterns/PAT-128-inspector-oninspectorgui-state-antipattern.md) — 持久化 UI 状态只在 OnEnable 设一次，绘制回调禁每帧强写
- [PAT-20 Editor 配置详情页标题与缩进规则](../2-Areas/Patterns/PAT-20-editor-panel-title-indent.md) — 复杂 Editor 配置面板应有明确标题，并给标题下条目统一缩进
- [PAT-21 Inspector HelpBox 多语义分行规则](../2-Areas/Patterns/PAT-21-inspector-helpbox-multiline.md) — HelpBox 多条信息必须分行
- [PAT-22 IMGUI TextField 切数据源前必须先释放焦点](../2-Areas/Patterns/PAT-22-imgui-textfield-focus-release.md) — IMGUI TextField编辑后需主动释放焦点
- [PAT-24 Inspector 同层级编辑区对齐规则](../2-Areas/Patterns/PAT-24-inspector-row-vertical-alignment.md) — Inspector 同层级编辑区垂直对齐
- [PAT-31 新增 Inspector SOP（继承结构 + 文件命名 + 4 步骤）](../2-Areas/Patterns/PAT-31-inspector-sop.md) — Inspector三文件SOP声明绑定绘制分离
- [PAT-40 新增字段按所属定位插入既有顺序](../2-Areas/Patterns/PAT-40-field-insertion-by-semantic-grouping.md) — 新增字段按语义段插入联动文件同序
- [PAT-74 Inspector 路径输入校验形态先于存在](../2-Areas/Patterns/PAT-74-inspector-path-form-validation.md) — Inspector 路径校形态不校存在性

## methodology

- [PAT-59 早期错误调研结论会沉淀为长期工程负担，需定期回炉复核](../2-Areas/Patterns/PAT-59-ai-research-conclusion-staleness.md) — 错误调研结论会层层固化反向追溯，需周期性回炉看官方源

## module

- [PAT-33 新增 SDK Plugin 的 6 步 SOP](../2-Areas/Patterns/PAT-33-sdk-plugin-sop.md) — SDK Plugin SOP UPM包加ISDKPlugin
- [PAT-69 UGUI 等比铺满父宽的零脚本配置法](../2-Areas/Patterns/PAT-69-ugui-aspect-fit-fill-width.md) — ARF+水平 stretch 实现等比满宽零脚本

## naming

- [PAT-101 Demo 私有依赖与 view 同目录归位 + 命名脱敏](../2-Areas/Patterns/PAT-101-demo-private-asset-colocation.md) — 私有资产同目录归位+命名脱敏
- [PAT-106 Kit Service 类去 Service 后缀](../2-Areas/Patterns/PAT-106-kit-service-no-service-suffix.md) — Kit Service 类禁带 Service 后缀
- [PAT-111 公开 API 命名避开宿主模块字面词重复](../2-Areas/Patterns/PAT-111-api-naming-avoid-host-module-literal.md) — API 名去模块字，类型名留模块字
- [PAT-42 命名具体化 / 去抽象 / 去重复 三原则](../2-Areas/Patterns/PAT-42-naming-concrete-deduplicate.md) — 命名具体化去抽象去类型重复三原则
- [PAT-45 大规模重命名落地后必须 grep 局部变量名](../2-Areas/Patterns/PAT-45-rename-grep-local-variables.md) — 大规模重命名后必须grep局部变量旧名
- [PAT-64 业务侧 UI 类一律 View 后缀（XxxView）](../2-Areas/Patterns/PAT-64-business-ui-view-suffix.md) — 业务侧 UI 类一律 View 后缀

## quality

- [PAT-01 缺陷严重度 P0-P4 分级](../2-Areas/Patterns/PAT-01-defect-severity.md) — Nova 代码审查与问题跟踪统一使用 P0-P4 严重度语言
- [PAT-114 C# XML 注释禁止 HTML 转义](../2-Areas/Patterns/PAT-114-cs-xml-doc-no-html-escape.md) — XML 注释直接写尖括号
- [PAT-58 Pipeline 步骤失败必须显式抛错而非静默跳过](../2-Areas/Patterns/PAT-58-pipeline-fail-fast-no-silent-skip.md) — 反射/外部 API 失败必抛异常禁静默跳过

## review

- [PAT-02 静态审查四维度框架](../2-Areas/Patterns/PAT-02-static-review-four-dim.md) — 静态审查四维度逻辑风格架构安全性能
- [PAT-03 运行时验证三步法](../2-Areas/Patterns/PAT-03-runtime-verify-three-step.md) — 运行时验证三步编译Inspector PlayMode
- [PAT-11 qa 测试结束清场铁律与测试脚本命名规范](../2-Areas/Patterns/PAT-11-qa-battlefield-cleanup.md) — qa测试结束必须清场场景与Inspector复原
- [PAT-129 死代码判定：链路追溯而非单点 grep](../2-Areas/Patterns/PAT-129-dead-code-chain-tracing.md) — 删码前追完整链路，引擎回调与预留接口不可凭单点 grep 判废
- [PAT-134 间歇性网络故障：吞异常≠治本，'突然好了'是反向证据](../2-Areas/Patterns/PAT-134-intermittent-network-fail-diagnosis.md) — 间歇网络故障吞异常非治本，突然好了是反向证据
- [PAT-136 同源 bug 全链路状态分析，拒绝症状驱动](../2-Areas/Patterns/PAT-136-symptom-driven-debug-trap.md) — 同链路反复 bug 先画全链路状态机根除
- [PAT-19 Test 目录只能消费端视角](../2-Areas/Patterns/PAT-19-test-consumer-view.md) — 测试代码以消费端视角写驱动用例
- [PAT-87 按钮文字可见性硬校验（空/同色/透明/位置 四检）](../2-Areas/Patterns/PAT-87-button-text-visibility-check.md) — 按钮 TMP 四检：空+同色+透明+位置外溢 0 容忍
- [PAT-88 检测脚本必须穷举"失败模式"（v1 漏 → 用户翻车 → v2 补的反模式）](../2-Areas/Patterns/PAT-88-detection-script-failure-mode-exhaust.md) — 检测必穷举失败模式 漏一种=用户翻车

## runtime

- [PAT-103 VerticalLayoutGroup 动态行容器必开 ChildControlHeight](../2-Areas/Patterns/PAT-103-vlg-child-control-height-mandatory.md) — 动态行 VLG 必开 ChildControlHeight
- [PAT-117 ScrollRect 不滑动先核对 Viewport vs Content 尺寸](../2-Areas/Patterns/PAT-117-scrollrect-viewport-content-size-check.md) — 滑不动先量 Viewport vs Content 尺寸
- [PAT-29 FrameworkComponentsGroup.GetComponent<T> 必须缓存到成员，禁止热路径反复调用](../2-Areas/Patterns/PAT-29-cache-component-lookup-on-init.md) — Component依赖在Init一次缓存禁运行查找
- [PAT-67 全局禁 UnityEngine.UI.Text，UI 文字一律 TMP](../2-Areas/Patterns/PAT-67-no-ui-text-only-tmp.md) — UI 文字一律 TMP，禁 UGUI Text
- [PAT-68 Reference 与 ObjectPool 辐射使用原则](../2-Areas/Patterns/PAT-68-pool-reference-spread.md) — 数据走 ReferencePool 组件走 ObjectPool
- [PAT-83 Canvas sortingOrder 越界 Clamp + Error 兜底](../2-Areas/Patterns/PAT-83-canvas-sortingorder-overflow-clamp.md) — int写short字段必Error+Clamp 拒静默截断

## upm

- [PAT-139 Nova 包壳策略与内部云仓库命名规则](../2-Areas/Patterns/PAT-139-nova-shell-package-and-internal-repo-naming.md) — Nova 包壳策略与内仓命名规则
- [PAT-41 Nova UPM 包布局与元数据基线](../2-Areas/Patterns/PAT-41-upm-package-layout-and-manifest.md) — Nova UPM 包需稳定布局与元数据

## workflow

- [PAT-04 「改什么读什么」分层上下文策略](../2-Areas/Patterns/PAT-04-read-what-you-change.md) — 改什么读什么，按变更风险控制阅读面
- [PAT-07 Trade-Off 分析框架 + 分阶段交付](../2-Areas/Patterns/PAT-07-tradeoff-phased-delivery.md) — Trade-Off方案分阶段交付小步验证
- [PAT-104 接口废弃直接删 shim 禁留 [Obsolete] 过渡](../2-Areas/Patterns/PAT-104-no-obsolete-shim-rule.md) — 废弃接口同批迁移所有调用点，禁留 [Obsolete] 兼容方法
- [PAT-113 禁手动抬 UPM package.json version](../2-Areas/Patterns/PAT-113-no-manual-version-bump.md) — 版本号只允许由统一发版入口写入
- [PAT-118 原子写 JSON 文件（临时文件 + rename）](../2-Areas/Patterns/PAT-118-atomic-write-json-via-rename.md) — JSON 写入用临时文件+rename
- [PAT-119 UPM 私有 fork 必须显式标注本地改动](../2-Areas/Patterns/PAT-119-upm-private-fork-local-diff-marking.md) — 私有 fork 改动必须留本地标注与包级变更记录
- [PAT-120 脚手架与样例生成工具的人类可见文案字段必须由用户决定](../2-Areas/Patterns/PAT-120-create-sample-user-decides-dirname.md) — 视觉文案命名权归用户，禁机械派生，未给值必先询问
- [PAT-121 发版自动化主/子包路径重写逻辑必须对称](../2-Areas/Patterns/PAT-121-publish-sample-rewrite-symmetric.md) — 主包专属步与子包同款步禁单边演进，扫描集与镜像策略全集对齐
- [PAT-122 sample devPathPrefix 不得带尾斜杠 + Python 兜底](../2-Areas/Patterns/PAT-122-sample-devpathprefix-no-trailing-slash.md) — devPathPrefix 无尾斜杠加 rstrip 兜底
- [PAT-123 UPM Sample displayName 必须等于 sampleName](../2-Areas/Patterns/PAT-123-upm-sample-displayname-equals-samplename.md) — displayName 须等于 sourceDir 末段
- [PAT-124 UPM 发版描述符禁随 tarball 落到外部工程只读区](../2-Areas/Patterns/PAT-124-upm-sample-tarball-exclude-dev-descriptor.md) — 开发期描述符 .npmignore 排除出 tarball
- [PAT-13 UPM 发版默认不级联依赖](../2-Areas/Patterns/PAT-13-publish-no-cascade.md) — UPM发版不级联连带兄弟包
- [PAT-130 UPM Sample 只读 Prefab 路径靠 scene override + import 重写（修订版）](../2-Areas/Patterns/PAT-130-sample-readonly-prefab-path-override-revised.md) — 只读 prefab 路径走 scene override
- [PAT-132 单主题 sample 用 ProcedurePlaying 直开入口 View](../2-Areas/Patterns/PAT-132-procedure-playing-direct-open-entry-view.md) — 单主题 demo OnEnter 直开入口 View
- [PAT-53 发版前校验 CHANGELOG 当前版本节存在，不靠人工自觉](../2-Areas/Patterns/PAT-53-changelog-grep-script-enforce.md) — 发版前校验 CHANGELOG 当前版本节
- [PAT-55 Python 工具脚本优先用 cwd 或环境变量解析项目根](../2-Areas/Patterns/PAT-55-python-script-cwd-or-env-root.md) — 工具脚本不要把项目根硬绑定到 `__file__` 的目录深度
- [PAT-62 README / CHANGELOG 双层同步铁律](../2-Areas/Patterns/PAT-62-readme-changelog-dual-sync.md) — 发版前工程根与包内两层文档全字段对齐，禁单层更新
- [PAT-66 禁止手改 Prefab YAML](../2-Areas/Patterns/PAT-66-no-handcraft-prefab.md) — Prefab 只走 Unity 正常序列化路径
- [PAT-78 Nova Sample Demo 七阶段交付流程](../2-Areas/Patterns/PAT-78-sample-demo-full-flow-sop.md) — Sample Demo 按七阶段交付


---
_共 97 条，分布于 18 个 category。_
