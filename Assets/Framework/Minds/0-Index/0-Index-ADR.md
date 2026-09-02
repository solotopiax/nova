---
title: Nova ADR 索引（Layer 1）
auto_generated: true
---

# Nova Knowledge Base — ADR 索引

> **本文件由 `$nova-obs health --rebuild-index` 自动生成，请勿手工编辑。**
> 维护方法：修改对应入库文件的 frontmatter（`summary` / `category` / `title`），重跑命令。

加载策略：当用户问及 ADR 相关内容时再加载本文；命中具体编号后再 `obsidian_get_note` 拉全文。

## arch

- [ADR-001 Component + Manager 三层继承链选型](../2-Areas/ADR/ADR-001-component-manager-three-layer.md) — 固定 Component+Manager 三层结构
- [ADR-002 Manager Priority 体系（以代码常量为单一真相源）](../2-Areas/ADR/ADR-002-manager-priority-system.md) — Manager Priority 只以代码常量为准
- [ADR-008 ManagerBase 一律 internal abstract（含 ProcedureManagerBase）](../2-Areas/ADR/ADR-008-managerbase-internal-abstract.md) — 所有 ManagerBase 对外隐藏，只保留接口为公开入口
- [ADR-012 三方插件信息禁止越层暴露](../2-Areas/ADR/ADR-012-third-party-info-isolation.md) — 需要隔离的第三方品牌信息不得穿透到对外层
- [ADR-016 框架层 vs 业务层访问分层铁律](../2-Areas/ADR/ADR-016-framework-vs-business-access.md) — 框架与业务访问入口分层各自封装
- [ADR-017 Component 必须对外完全隔绝持有的 Manager](../2-Areas/ADR/ADR-017-component-manager-isolation.md) — Component对Manager完全隔绝只通过接口
- [ADR-020 程序集依赖方向单向（Editor → Runtime）](../2-Areas/ADR/ADR-020-assembly-dependency-direction.md) — 程序集单向依赖Runtime禁依赖Editor
- [ADR-040 跨 asmdef 写共享状态禁绕跳板字段](../2-Areas/ADR/ADR-040-cross-asmdef-no-jump-board-state.md) — 跨 asmdef 写共享状态走静态门面禁多层跳板
- [ADR-057 基础网络编排层从独立 Kit 包下沉进框架 Network 模块](../2-Areas/ADR/ADR-057-network-kit-base-sink-into-framework.md) — 基础网络编排下沉框架，删 Kit 基础包
- [ADR-062 Proto 引用公共 header 统一用 NovaFramework.Runtime](../2-Areas/ADR/ADR-062-proto-header-namespace-convention.md) — Proto 公共 header 引用统一去 Kit 段
- [ADR-067 登录/绑定/云存档三端职责分离](../2-Areas/ADR/ADR-067-login-bind-save-separation.md) — login 鉴权 / bind 独立包裁决归属 / 覆盖交 save 编排
- [ADR-072 Mobile IAP 平台透传参数编码 uid+tableId+receiptParam（8/8/16 布局）](../2-Areas/ADR/ADR-072-iap-mobile-passthrough-param-layout.md) — IAP透传参数采用8/8/16布局
- [ADR-074 跨 Editor Runtime 导出链的占位符解析统一采用显式上下文](../2-Areas/ADR/ADR-074-placeholder-resolution-explicit-context.md) — 占位符统一算法与多环境显式上下文
- [ADR-075 启动应用配置由 Config 非阻塞等待 Network Ready 后单次刷新](../2-Areas/ADR/ADR-075-config-startup-remote-refresh-after-network-ready.md) — Config 后台等待网络路由就绪并单次刷新磁盘快照
- [ADR-077 Mobile IAP 未完成订单身份采用 tableId+ReceiptParam](../2-Areas/ADR/ADR-077-mobile-iap-order-key-tableid-receiptparam.md) — Mobile订单键含ReceiptParam
- [ADR-078 隐私 AES 默认密钥与应用协议 AES 密钥分域](../2-Areas/ADR/ADR-078-privacy-aes-and-app-aes-separation.md) — 隐私默认 AES 与应用协议 AES 分域
- [ADR-084 Persist 就绪前的框架启动状态使用受限 PlatformPlayerPrefs](../2-Areas/ADR/ADR-084-bootstrap-state-before-persist.md) — Persist 前启动状态使用受限 PlayerPrefs

## asset

- [ADR-014 AssetPlayMode 拆分为 EditorPlayMode + RuntimePlayMode（删 LaunchConfig.DefaultPlayMode）](../2-Areas/ADR/ADR-014-playmode-split-editor-runtime.md) — AssetPlayMode拆分编辑期与运行期独立
- [ADR-025 YooAsset 远端 URL 模板占位符设计：编译宏决定 Platform，可选占位符不依赖运行时配置](../2-Areas/ADR/ADR-025-yooasset-url-template-placeholders.md) — YooAsset URL占位符编译宏决定Platform
- [ADR-042 AssetManager 移除裸 Load API，所有 LoadXxx 返回 Handle 接口，调用方持有并负责释放](../2-Areas/ADR/ADR-042-assetmanager-load-api-all-return-handle.md) — LoadXxx 全返 Handle，调用方持有并 Release
- [ADR-052 下载缓存两层清理分工，内存引用计数与磁盘沙盒分离](../2-Areas/ADR/ADR-052-asset-cache-two-layer-cleanup.md) — 内存引用计数与磁盘旧资源文件分两层独立清理
- [ADR-055 Excel 源数据迁入各 Demo 独立副本，路径生命周期收敛单链](../2-Areas/ADR/ADR-055-excel-source-into-demo-copies.md) — Excel 源移进各 Demo 副本，删 Docs 搬运
- [ADR-060 YooAssetSettings 只保留 Editor 权威源并在 Player 构建期临时 staging](../2-Areas/ADR/ADR-060-yooasset-settings-global-resources-copy.md) — 构建期临时生成唯一 YooAssetSettings 副本

## core

- [ADR-011 Load/Unload 与 IReference Get/Put 配对收口](../2-Areas/ADR/ADR-011-load-unload-and-ireference-pairing.md) — Load与Unload及IReference必须配对
- [ADR-018 JSON 序列化统一走 Util.Json，类型定义限用 Newtonsoft.Json](../2-Areas/ADR/ADR-018-json-via-util-json.md) — JSON 读写统一走 Util.Json

## editor

- [ADR-023 框架范围禁用 EditorPrefs（状态恢复走项目内资产）](../2-Areas/ADR/ADR-023-no-editor-prefs-in-framework.md) — 框架范围禁用EditorPrefs改走项目资产
- [ADR-026 Pipify Runner 不冻结 Domain Reload、不进入 Asset Editing](../2-Areas/ADR/ADR-026-pipify-runner-no-batch-locking.md) — Runner 全程裸跑，禁两组批锁 API
- [ADR-056 RuntimeProvider 配置选取统一收口 WorkspaceActive 锚点](../2-Areas/ADR/ADR-056-runtimeprovider-config-select-via-workspaceactive.md) — 配置选取改走 WorkspaceActive 锚点
- [ADR-059 SerializeReference 跨格深拷贝改用 boxedValue](../2-Areas/ADR/ADR-059-serializeref-deepcopy-boxedvalue.md) — 跨格深拷贝改用 JsonUtility round-trip
- [ADR-064 PlugPals 依赖检测与可选库三原则](../2-Areas/ADR/ADR-064-plugpals-dependency-detection.md) — 依赖进 dependencies，宏由 asmdef 配置
- [ADR-073 Excel 导出业务边界：仅 Table 通用，其他模块专用](../2-Areas/ADR/ADR-073-excel-export-business-boundary.md) — 仅 Table 通用，其他模块按业务语义定制
- [ADR-081 Nova Project Action 采用按操作性质分类的受控执行层](../2-Areas/ADR/ADR-081-project-action-controlled-execution-layer.md) — Project Skill 统一调用受控 C# Action

## hotfix

- [ADR-005 HybridCLR 程序集与命名空间唯一写入路径](../2-Areas/ADR/ADR-005-hybridclr-namespace-single-write-path.md) — HybridCLR命名空间单写入路径同步
- [ADR-007 Procedure 分档（框架内置 vs 业务 DLL 延迟注册）](../2-Areas/ADR/ADR-007-procedure-tier-split.md) — Procedure按AOT/业务DLL分档注册
- [ADR-013 热更总开关 EnableHotfix 与启动流程二分](../2-Areas/ADR/ADR-013-hotfix-master-switch.md) — EnableHotfix作为热更总开关收敛入口
- [ADR-032 放弃 NovaBehaviour 桥接，回归 HybridCLR 原生 MonoBehaviour](../2-Areas/ADR/ADR-032-drop-novabehaviour-bridge.md) — 热更脚本回归原生 MonoBehaviour
- [ADR-050 Hotfix 整批失败的用户决策与两层重试机制](../2-Areas/ADR/ADR-050-hotfix-batch-fail-user-decision.md) — 整批失败弹窗重试，单文件与用户重试两层独立计数
- [ADR-051 启动期资源切片策略 A/B 二选一，框架 API 不绑产品决策](../2-Areas/ADR/ADR-051-launch-asset-slice-strategy.md) — 整包差异 XOR 切片增量二选一，框架透传不选策略
- [ADR-065 启动期清单三级离线回退仅接受本地可启动版本](../2-Areas/ADR/ADR-065-asset-manifest-three-tier-offline-fallback.md) — 远端失败优先复用启动范围完整的本地版本
- [ADR-076 启动设备白名单仅切换版本元数据路由，失败不阻断常规热更新](../2-Areas/ADR/ADR-076-startup-whitelist-metadata-routing.md) — 白名单命中仅切换元数据地址，Bundle 仍走常规 CDN
- [ADR-080 资源下载按文件独立执行完整主备轮次与重试](../2-Areas/ADR/ADR-080-asset-package-sticky-fallback-routing.md) — 每个 Asset 文件独立冻结候选计划并按 C×R×(K+1) 执行

## inspector

- [ADR-021 Inspector + RuntimeDrawer 双层架构](../2-Areas/ADR/ADR-021-inspector-runtime-drawer-two-layer.md) — Inspector与RuntimeDrawer双层解耦绘制
- [ADR-039 BaseDemoView 废弃 SetApiHint 改双接口就近拆解](../2-Areas/ADR/ADR-039-base-demo-view-api-hint-split.md) — 废 SetApiHint；改 hint 就近双色拆解

## module

- [ADR-015 合并 ProcedureLaunch 进 ProcedureSplash 并约定 UI 层级](../2-Areas/ADR/ADR-015-merge-launch-into-splash.md) — Launch合并到Splash简化启动编排
- [ADR-022 SDK 模块插件架构决策（去 Composite + 具体类型驱动 + SetConfig + UPM 边界 + 去 MonoBehaviour + UniTask 主线程切换）](../2-Areas/ADR/ADR-022-sdk-plugin-architecture.md) — SDK插件以UPM包+ISDKPlugin注册架构
- [ADR-024 Launch 模块整体重命名为 App 并以 AppVersionResult 三档替代 LaunchCheckResult](../2-Areas/ADR/ADR-024-launch-to-app-rename.md) — Launch模块改名App并以三档枚举替代
- [ADR-033 MainDemo 演示拓扑：各 sample 独立 Nova.prefab + MainDemo 内树形导航](../2-Areas/ADR/ADR-033-maindemo-isolated-topology.md) — sample 独立闭包；MainDemo 树形导航
- [ADR-043 GameSave 全量/非全量由 proto full 字段显式区分](../2-Areas/ADR/ADR-043-gamesave-full-explicit-flag.md) — 协议 full=true 是全量唯一信号，废弃 keys 空判
- [ADR-045 SetFullAsync 全量上传 value 复用 datas[0].Value（Key 空串）](../2-Areas/ADR/ADR-045-setfull-value-via-datas0.md) — 全量上传载荷复用 datas[0]，Key 留空串
- [ADR-046 Sound/Vibrate 模块对外 API 不暴露按行重载](../2-Areas/ADR/ADR-046-sound-vibrate-no-by-row-api.md) — Sound/Vibrate 对外 API 仅按名重载
- [ADR-047 Editor 激活 ConfigMaster 锚点（Globals.json + GUID + 四段回退）](../2-Areas/ADR/ADR-047-editor-active-master-anchor.md) — Globals.json 锚定 Editor 激活 master
- [ADR-048 Nova.prefab 跟主框架版本走（amends ADR-033）](../2-Areas/ADR/ADR-048-nova-prefab-follow-framework.md) — sample scene 引用主框架包内 prefab 实例
- [ADR-049 YooAsset Settings 路径 ConfigMaster 化（含 UPM 包本地 fork）](../2-Areas/ADR/ADR-049-yooasset-settings-via-configmaster.md) — YooAsset 设置路径收口 ConfigMaster
- [ADR-053 Kit 配置模板化（IKitConfig + ConfigWindow Kit 一级组 + Kit 入口极简收口）](../2-Areas/ADR/ADR-053-kit-config-templating.md) — Kit 固有配置模板化，入口收口为极简形态
- [ADR-054 Kit 配置进三维矩阵 + Network 表删维度列 PreFilter 降级纯拷贝（反转 ADR-053 决策 3）](../2-Areas/ADR/ADR-054-kit-config-three-dim-matrix.md) — 环境差异上移 Config 三维矩阵，网络表降纯拷贝
- [ADR-058 ConfigWindow per-panel 可勾选维度（PanelDimensionMask + DimensionProjector + Override 旁路）](../2-Areas/ADR/ADR-058-per-panel-dimension-mask.md) — 面板级维度掩码 + Override 旁路按需多维配置
- [ADR-063 TGA 插件 ServerCmdName 与 ReportCmdName 双通道不可合并](../2-Areas/ADR/ADR-063-tga-dual-cmd-channel-separation.md) — TGA 埋点上报与账号绑定双通道禁合并
- [ADR-069 云存档支持按 target_uid 跨用户查询](../2-Areas/ADR/ADR-069-gamesave-cross-user-query.md) — target_uid 空查自身、非空查指定，鉴权归服务端
- [ADR-070 SDK 启用真相源归 ConfigMaster.EnabledSDKs，插件统一按启用配置实例化](../2-Areas/ADR/ADR-070-sdk-enable-via-configmaster-enabledsdks.md) — EnabledSDKs 唯一启用源，插件按配置统一实例化
- [ADR-071 DataMaster 读参 topicId 实为 Params 字典 key（topic_name），非 experiment.topicId](../2-Areas/ADR/ADR-071-datamaster-topicid-is-params-key.md) — 读参 topicId 传 Params 字典 key，非实验字段
- [ADR-082 RTL 本地化转换固定在 TMP 渲染边界](../2-Areas/ADR/ADR-082-rtl-localization-render-boundary.md) — RTL 文本仅在 TMP 渲染前转换
- [ADR-083 Nova 网络统一采用 UnityWebRequest 与共享主备执行机制](../2-Areas/ADR/ADR-083-uwr-primary-fallback-network.md) — UWR 系统 DNS 承载三模块共享主备执行机制

## quality

- [ADR-010 谁使用谁校验（Component 不做参数校验，下沉 Manager 层）](../2-Areas/ADR/ADR-010-validation-on-consumer-side.md) — 参数校验下沉到真正使用参数的 Manager
- [ADR-027 规则层禁用 LockReloadAssemblies / StartAssetEditing 引用计数 API](../2-Areas/ADR/ADR-027-rule-ban-editor-refcount-batch-apis.md) — 风格规范 §4·1 列入两组批锁 API 零容忍

## runtime

- [ADR-041 UIDepthConfig 静态常量下线，深度换算系数散到 UIComponent Inspector](../2-Areas/ADR/ADR-041-ui-depth-factor-to-inspector.md) — 深度因子静态常量改 Inspector 字段
- [ADR-068 网络业务失败响应也解析并携带业务体](../2-Areas/ADR/ADR-068-netresponse-fail-carries-data.md) — 业务失败码也解析业务体，Fail 加带 data 重载

## workflow

- [ADR-028 HybridCLR 拷贝 AOT 裁剪 dll 必须在 BuildPlayer 之后](../2-Areas/ADR/ADR-028-hybridclr-copy-aot-after-buildplayer.md) — CopyAotDll 必须在 BuildPlayer 之后
- [ADR-031 每个 UPM 包必须自带 CHANGELOG、LICENSE、README](../2-Areas/ADR/ADR-031-upm-three-piece-mandatory.md) — Nova 自维护 UPM 包缺少三件套时，发版应直接中止
- [ADR-066 EDM 公共依赖与 OpenUPM 工程固定策略](../2-Areas/ADR/ADR-066-edm-openupm-registry-strategy.md) — OpenUPM 工程固定，registry 链须覆盖传递依赖


---
_共 69 条，分布于 10 个 category。_
