---
title: Minds 全量治理审计（2026-06-08）
status: recorded
date: 2026-06-08
summary: 逐项评估当前 Minds Markdown 的保留与优化动作
category: audit
---

# Minds 全量治理审计（2026-06-08）

## 本轮已执行

- 归档：`ADR-019`、`PAT-100`、`MOC-Workflow`、`ExternalLinks`
- 删除：`keyword-rebuild-suggestions-2026-05-24.md`
- 收口：Layer-1 category 枚举、超长 summary、缺失 `type`、错误 wikilink、`PAT-DRAFT` alias
- 后续补充：新增 `4-Archives/Audits/INDEX.md`，并删除零关联归档 `PAT-100`

## 当前统计

- 当前 `.md` 文件数：`258`
- `keep`：`226`
- `optimize`：`0`

## 逐项清单

| path | action | note |
|---|---|---|
| `0-Index/0-Index-ADR.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index-FolderGuide.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index-GLO.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index-MOC.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index-PAT.md` | `keep` | 自动索引已回到当前长度，后续按需再拆。 |
| `0-Index/0-Index-RES.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index-Terms.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Index.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `0-Index/0-Log.md` | `keep` | 入口索引职责清楚，当前仍有效。 |
| `2-Areas/ADR/ADR-001-component-manager-three-layer.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-002-manager-priority-system.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-005-hybridclr-namespace-single-write-path.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-007-procedure-tier-split.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-008-managerbase-internal-abstract.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-010-validation-on-consumer-side.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-011-load-unload-and-ireference-pairing.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-012-third-party-info-isolation.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-013-hotfix-master-switch.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-014-playmode-split-editor-runtime.md` | `keep` | 决策仍有效，已收紧为字段拆分与联动语义。 |
| `2-Areas/ADR/ADR-015-merge-launch-into-splash.md` | `keep` | 决策仍有效，已收紧为启动序列与接管契约。 |
| `2-Areas/ADR/ADR-016-framework-vs-business-access.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-017-component-manager-isolation.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-018-json-via-util-json.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-020-assembly-dependency-direction.md` | `keep` | 规则稳定，已压缩为单向依赖本体。 |
| `2-Areas/ADR/ADR-021-inspector-runtime-drawer-two-layer.md` | `keep` | 双层架构有效，已压缩为职责与分层本体。 |
| `2-Areas/ADR/ADR-022-sdk-plugin-architecture.md` | `keep` | 主决策有效，已收口为插件架构本体。 |
| `2-Areas/ADR/ADR-023-no-editor-prefs-in-framework.md` | `keep` | 长期规则成立，已收口为资产化替代本体。 |
| `2-Areas/ADR/ADR-024-launch-to-app-rename.md` | `keep` | 命名决策有效，已收口为重命名与接口同步本体。 |
| `2-Areas/ADR/ADR-025-yooasset-url-template-placeholders.md` | `keep` | 规则有效，已收口为占位符语义本体。 |
| `2-Areas/ADR/ADR-026-pipify-runner-no-batch-locking.md` | `keep` | 规则有效，已收口为裸跑契约。 |
| `2-Areas/ADR/ADR-027-rule-ban-editor-refcount-batch-apis.md` | `keep` | 约束稳定，已收口为规则层禁用清单。 |
| `2-Areas/ADR/ADR-028-hybridclr-copy-aot-after-buildplayer.md` | `keep` | 构建顺序规则有效，已收口为 BuildPlayer 后拷贝契约。 |
| `2-Areas/ADR/ADR-031-upm-three-piece-mandatory.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-032-drop-novabehaviour-bridge.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-033-maindemo-isolated-topology.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-039-base-demo-view-api-hint-split.md` | `keep` | 已压缩为直接决策记录。 |
| `2-Areas/ADR/ADR-040-cross-asmdef-no-jump-board-state.md` | `keep` | 已压缩为直接规则记录。 |
| `2-Areas/ADR/ADR-041-ui-depth-factor-to-inspector.md` | `keep` | 已压缩为直接决策记录。 |
| `2-Areas/ADR/ADR-042-assetmanager-load-api-all-return-handle.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-043-gamesave-full-explicit-flag.md` | `keep` | 已压缩为直接协议规则。 |
| `2-Areas/ADR/ADR-045-setfull-value-via-datas0.md` | `keep` | 已压缩为直接载荷规则。 |
| `2-Areas/ADR/ADR-046-sound-vibrate-no-by-row-api.md` | `keep` | 已压缩为直接 API 边界规则。 |
| `2-Areas/ADR/ADR-047-editor-active-master-anchor.md` | `keep` | 已压缩为锚点规则记录。 |
| `2-Areas/ADR/ADR-048-nova-prefab-follow-framework.md` | `keep` | 已压缩为版本跟随规则。 |
| `2-Areas/ADR/ADR-049-yooasset-settings-via-configmaster.md` | `keep` | 已压缩为 ConfigMaster 路径规则。 |
| `2-Areas/ADR/ADR-050-hotfix-batch-fail-user-decision.md` | `keep` | 已压缩为失败决策规则。 |
| `2-Areas/ADR/ADR-051-launch-asset-slice-strategy.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-052-asset-cache-two-layer-cleanup.md` | `keep` | 活跃 ADR，仍属当前长期契约。 |
| `2-Areas/ADR/ADR-053-kit-config-templating.md` | `keep` | 已压缩为模板化决策记录。 |
| `2-Areas/ADR/ADR-054-kit-config-three-dim-matrix.md` | `keep` | 已压缩为三维矩阵决策记录。 |
| `2-Areas/ADR/ADR-055-excel-source-into-demo-copies.md` | `keep` | 已压缩为路径生命周期规则。 |
| `2-Areas/ADR/ADR-056-runtimeprovider-config-select-via-workspaceactive.md` | `keep` | 已压缩为锚点收口规则。 |
| `2-Areas/ADR/ADR-057-network-kit-base-sink-into-framework.md` | `keep` | 已压缩为下沉规则记录。 |
| `2-Areas/ADR/ADR-058-per-panel-dimension-mask.md` | `keep` | 已压缩为面板维度规则。 |
| `2-Areas/ADR/ADR-059-serializeref-deepcopy-boxedvalue.md` | `keep` | 已压缩为深拷贝规则。 |
| `2-Areas/ADR/ADR-060-yooasset-settings-global-resources-copy.md` | `keep` | 已压缩为直出包兜底规则。 |
| `2-Areas/ADR/ADR-062-proto-header-namespace-convention.md` | `keep` | 已压缩为 proto 命名规则。 |
| `2-Areas/ADR/ADR-063-tga-dual-cmd-channel-separation.md` | `keep` | 已压缩为双通道隔离规则。 |
| `2-Areas/Glossary/GLO-02-framework-manager-tiers.md` | `keep` | 核心术语条目，仍有长期统一价值。 |
| `2-Areas/Glossary/GLO-03-component-procedure-manager.md` | `keep` | 核心术语条目，仍有长期统一价值。 |
| `2-Areas/Glossary/GLO-04-utility-classes.md` | `keep` | 已收紧为核心工具入口定义，不再混作工具清单。 |
| `2-Areas/Glossary/GLO-05-three-tier-docs.md` | `keep` | 核心术语条目，仍有长期统一价值。 |
| `2-Areas/Glossary/GLO-06-design-patterns-map.md` | `keep` | 已收口为模式理解辅助页，并明确实际规则仍回到 ADR/PAT。 |
| `2-Areas/Glossary/GLO-07-asset-location.md` | `keep` | 核心术语条目，仍有长期统一价值。 |
| `2-Areas/MOC/MOC-App.md` | `keep` | 已删除阶段性实现口吻，保留长期边界与入口导航。 |
| `2-Areas/MOC/MOC-Asset.md` | `keep` | 已切到当前 `ADR-042` 句柄语义，保留长期边界与入口导航。 |
| `2-Areas/MOC/MOC-Config.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Debug.md` | `keep` | 已删除临时债务口吻，回到稳定边界与职责导航。 |
| `2-Areas/MOC/MOC-Event.md` | `keep` | 已明确只承担入口与边界，不再承担调用细节手册。 |
| `2-Areas/MOC/MOC-HybridCLR.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Inspector.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Localization.md` | `keep` | 已切到当前 `ADR-042` 句柄语义并收紧边界说明。 |
| `2-Areas/MOC/MOC-Manager.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Network.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-ObjectPool.md` | `keep` | 已收紧边界说明并切到当前 `ADR-042` 句柄语义。 |
| `2-Areas/MOC/MOC-Persist.md` | `keep` | 已去掉实现口吻，保留装配边界与导航提醒。 |
| `2-Areas/MOC/MOC-Pipify.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Prefab.md` | `keep` | 已切到当前 `ADR-042` 句柄语义并保留实例化/销毁边界。 |
| `2-Areas/MOC/MOC-Procedure.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-SDK.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Sound.md` | `keep` | 已收紧为入口与边界导航，移除实现快照口吻。 |
| `2-Areas/MOC/MOC-Table.md` | `keep` | 已收紧为长期入口图谱，并切换到当前 ADR-042 口径。 |
| `2-Areas/MOC/MOC-UI.md` | `keep` | 模块导航仍有效，可作为架构入口。 |
| `2-Areas/MOC/MOC-Vibrate.md` | `keep` | 已收紧为模块入口与边界导航，去掉实现快照细节。 |
| `2-Areas/Patterns/PAT-01-defect-severity.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-02-static-review-four-dim.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-03-runtime-verify-three-step.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-04-read-what-you-change.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-05-l0-l1-l2-docs.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-07-tradeoff-phased-delivery.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-08-architecture-antipatterns.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-09-inspector-config-i18n.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-10-imgui-popup-horizontal-wrap.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-101-demo-private-asset-colocation.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-102-button-overlay-sub-hint-layout.md` | `keep` | 已改为 demo 分类，并删除现场复盘口吻。 |
| `2-Areas/Patterns/PAT-103-vlg-child-control-height-mandatory.md` | `keep` | 已改为 runtime 分类，并收口为通用动态布局规则。 |
| `2-Areas/Patterns/PAT-104-no-obsolete-shim-rule.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-105-api-hint-near-element-split.md` | `keep` | 已改为 demo 分类，并保留为示教类 View 规则。 |
| `2-Areas/Patterns/PAT-106-kit-service-no-service-suffix.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-107-no-redundant-wrapper-with-proto-field.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-108-upm-kit-public-api-collapse.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-109-upm-package-docs-mandatory.md` | `keep` | 已压缩为 UPM 包文档目录规则本体。 |
| `2-Areas/Patterns/PAT-11-qa-battlefield-cleanup.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-111-api-naming-avoid-host-module-literal.md` | `keep` | 已收紧为命名规则本体，去掉演进注释。 |
| `2-Areas/Patterns/PAT-112-managerconfig-host-to-leaf-passthrough.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-113-no-manual-version-bump.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-114-cs-xml-doc-no-html-escape.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-115-changelog-human-readable.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-116-cs-doc-mirror-sync.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-117-scrollrect-viewport-content-size-check.md` | `keep` | 已改为 runtime 分类，并删除事件现场叙述。 |
| `2-Areas/Patterns/PAT-118-atomic-write-json-via-rename.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-119-upm-private-fork-local-diff-marking.md` | `keep` | 已改名去个人化，主 ID 回到标准 `PAT-119`，旧别名保留兼容。 |
| `2-Areas/Patterns/PAT-120-create-sample-user-decides-dirname.md` | `keep` | 已移除归档协作语义依赖，回到样例脚手架命名规则本体。 |
| `2-Areas/Patterns/PAT-121-publish-sample-rewrite-symmetric.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-122-sample-devpathprefix-no-trailing-slash.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-123-upm-sample-displayname-equals-samplename.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-124-upm-sample-tarball-exclude-dev-descriptor.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-125-runtime-incremental-three-steps.md` | `keep` | 资源模式有效，已压缩为三步与缓存认知本体。 |
| `2-Areas/Patterns/PAT-128-inspector-oninspectorgui-state-antipattern.md` | `keep` | 已补齐重置策略，回到稳定 Inspector 规则。 |
| `2-Areas/Patterns/PAT-129-dead-code-chain-tracing.md` | `keep` | 已补齐判定输出，回到稳定删码判定规则。 |
| `2-Areas/Patterns/PAT-13-publish-no-cascade.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-130-sample-readonly-prefab-path-override-revised.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-132-procedure-playing-direct-open-entry-view.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-133-editor-cli-path-resolve-version-range.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-134-intermittent-network-fail-diagnosis.md` | `keep` | 已补齐验证顺序，回到稳定间歇故障诊断规则。 |
| `2-Areas/Patterns/PAT-135-serializeref-crosscell-deepcopy-trap.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-136-symptom-driven-debug-trap.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-18-editor-window-vs-util-split.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-19-test-consumer-view.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-20-editor-panel-title-indent.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-21-inspector-helpbox-multiline.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-22-imgui-textfield-focus-release.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-24-inspector-row-vertical-alignment.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-27-config-no-serialize.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-28-luban-load-release-symmetric.md` | `keep` | 已切到当前 handle 语义，仍具复用价值。 |
| `2-Areas/Patterns/PAT-29-cache-component-lookup-on-init.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-30-framework-usage-redlines.md` | `keep` | 高价值红线清单，已去掉重复适用场景与来源铺陈。 |
| `2-Areas/Patterns/PAT-31-inspector-sop.md` | `keep` | SOP 仍有效，已压缩为结构、命名与步骤本体。 |
| `2-Areas/Patterns/PAT-32-runtime-module-sop.md` | `keep` | SOP 仍有效，已压缩为目录、命名与步骤本体。 |
| `2-Areas/Patterns/PAT-33-sdk-plugin-sop.md` | `keep` | SOP 仍有效，已压缩为接入步骤与主线程契约。 |
| `2-Areas/Patterns/PAT-34-minds-scope-nova-only.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-35-editor-draw-only.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-36-git-tracked-paths-relative-to-project-root.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-37-no-yooasset-outside-asset-module.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-39-editor-draw-discipline-enforcement.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-40-field-insertion-by-semantic-grouping.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-41-upm-package-layout-and-manifest.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-42-naming-concrete-deduplicate.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-43-optional-remote-check-tolerance.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-44-procedure-checkversion-two-stage.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-45-rename-grep-local-variables.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-46-iteration-grep-self-check.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-48-editor-refcount-api-leak-drain.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-49-pipify-step-no-batch-lock-assumption.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-50-unity-progressbar-must-delaycall.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-51-sbp-buildcache-vs-lock-reload-incompat.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-53-changelog-grep-script-enforce.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-55-python-script-cwd-or-env-root.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-58-pipeline-fail-fast-no-silent-skip.md` | `keep` | 规则有效，已压缩为失败语义与签名对齐。 |
| `2-Areas/Patterns/PAT-59-ai-research-conclusion-staleness.md` | `keep` | 证据治理价值高，已压缩为取证与回炉规则。 |
| `2-Areas/Patterns/PAT-61-version-consistency-prefer-manifest.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-62-readme-changelog-dual-sync.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-64-business-ui-view-suffix.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-65-demo-coverage-standard.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-66-no-handcraft-prefab.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-67-no-ui-text-only-tmp.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-68-pool-reference-spread.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-69-ugui-aspect-fit-fill-width.md` | `keep` | 模式有效，已压缩为零脚本配置本体。 |
| `2-Areas/Patterns/PAT-70-yooasset-packrule-selection.md` | `keep` | 模式有效，已压缩为 PackRule 语义与选型约束。 |
| `2-Areas/Patterns/PAT-74-inspector-path-form-validation.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-77-base-demo-view-three-zone-template.md` | `keep` | 有效模板，已去掉大段复盘与关联说明。 |
| `2-Areas/Patterns/PAT-78-sample-demo-full-flow-sop.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-79-demo-view-portrait-layout.md` | `keep` | 布局规则有效，已收口为竖屏参数铁律。 |
| `2-Areas/Patterns/PAT-80-demo-view-pure-color-style.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-83-canvas-sortingorder-overflow-clamp.md` | `keep` | 活跃模式条目，仍具复用价值。 |
| `2-Areas/Patterns/PAT-84-demo-view-control-left-label.md` | `keep` | 规则有效，已收口为左 Label 模式本体。 |
| `2-Areas/Patterns/PAT-87-button-text-visibility-check.md` | `keep` | 经验有效，已压缩为四检规则本体。 |
| `2-Areas/Patterns/PAT-88-detection-script-failure-mode-exhaust.md` | `keep` | 方法论有效，已压缩为失败模式枚举规则。 |
| `3-Resources/RES-01-karpathy-llm-wiki.md` | `keep` | 已改写为方法摘要页，保留来源与可复用结论。 |
| `3-Resources/Unity/Unity_Addressables_教程.md` | `keep` | 已补使用说明与速览，正文按原稿保全保留。 |
| `3-Resources/Unity/Unity_YooAsset_教程.md` | `keep` | 已补使用说明并清理会话化入口表述。 |
| `4-Archives/ADR/ADR-006-novabehaviour-ibaselife-replace-monobehaviour.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/ADR/ADR-009-uimanager-no-addcomponent-fallback.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/ADR/ADR-019-yooasset-release-mandatory.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/ADR/ADR-038-ui-depth-factor-rebalance.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/ADR/ADR-044-network-kit-dual-overload.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-12-nova-framework-deep-audit-design.md` | `optimize` | 审计设计规格有价值，但旧协作栈表述偏重。 |
| `4-Archives/Audits/INDEX.md` | `keep` | 审计归档入口页，已为历史审计与快照补足导航。 |
| `4-Archives/Audits/2026-04-to-05/00-architecture-review.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/00-optimization-plan.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/00-risk-registry.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-04-12-snapshots/SNP-2026-04-12-01-module-scoring.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-04-12-snapshots/SNP-2026-04-12-02-defect-baseline.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-04-12-snapshots/SNP-2026-04-12-03-dependency-graph.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-04-12-snapshots/SNP-2026-04-12-04-optimization-phases.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-04-12-snapshots/SNP-2026-04-12-05-doc-sync-deviations.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-05-17-batch1-10-static-review.md` | `optimize` | 有审查价值，但执行痕迹偏多。 |
| `4-Archives/Audits/2026-04-to-05/2026-05-17-docs-sync-abpath-merge.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Audits/2026-04-to-05/2026-05-17-night-self-fix-log.md` | `optimize` | 历史价值在，但明显是执行日志体。 |
| `4-Archives/Audits/2026-04-to-05/2026-05-17-qa-abpath-merge.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-003-main-session-as-team-leader.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-004-static-runtime-review-split.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-029-claude-md-locate-in-dot-claude.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-030-tools-publish-to-cc-skill.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-034-architect-mandatory-prerequisite.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-035-code-reviewer-dual-path.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-036-multi-coder-mandatory-worktree.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-037-allow-subagent-nested-spawn.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/ADR/ADR-061-nova-obs-skills-merge.md` | `optimize` | 历史价值在，但技能路由细节可继续压缩。 |
| `4-Archives/Collaboration/INDEX.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-06-main-session-dispatch.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-110-ai-collab-hook-timing-split-turn.md` | `optimize` | 高度绑定旧 hook 时序问题。 |
| `4-Archives/Collaboration/Patterns/PAT-12-no-auto-git-commit.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-126-cc-obs-interaction-tuning.md` | `optimize` | 更像阶段性评估稿，后续仍可瘦身。 |
| `4-Archives/Collaboration/Patterns/PAT-127-hook-single-process-match.md` | `optimize` | 有经验价值，但旧 hook 热路径细节较重。 |
| `4-Archives/Collaboration/Patterns/PAT-131-dispatch-restate-exact-ui-specs.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-14-plan-to-subagent-driven.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-15-agent-scope-discipline.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-16-obs-keyword-trigger.md` | `keep` | 旧机制已收口为双通道规则本体。 |
| `4-Archives/Collaboration/Patterns/PAT-17-obs-memory-split.md` | `keep` | 旧双栈分工已收口为短指针/长知识本体。 |
| `4-Archives/Collaboration/Patterns/PAT-23-obs-archive-signal-keyword-guard.md` | `keep` | 旧机制已收口为取代/归档闭环本体。 |
| `4-Archives/Collaboration/Patterns/PAT-25-rules-obs-patterns-collaboration.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-26-ai-concise-reply.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-38-hook-claude-cli-bare-flag.md` | `optimize` | 明显依赖旧 CLI/hook 事故背景。 |
| `4-Archives/Collaboration/Patterns/PAT-47-ai-skill-no-redundant-confirm.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-52-cc-obs-four-layer-enforcement.md` | `optimize` | 与 PAT-57 主题重叠，后续可考虑主次归并。 |
| `4-Archives/Collaboration/Patterns/PAT-54-obs-rules-pre-action-lookup.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-56-team-vs-personal-claude-md-split.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-57-cc-obs-four-layer-enforcement.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-60-rules-file-section-migration.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-75-main-session-four-parallel-templates.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-76-unity-mcp-batch-execute.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-82-vault-prelookup-mandatory.md` | `optimize` | 保留历史即可，细节仍偏强制机制。 |
| `4-Archives/Collaboration/Patterns/PAT-85-vault-search-synonym-expansion.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-86-direct-promote-bypass-inbox.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-89-bookkeeping-cost-near-zero.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-90-ingest-cascade-update.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-91-session-output-filing-back.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-92-vault-log-append-only.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-93-vault-three-layer-architecture.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-94-sourcing-responsibility-user-owned.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-95-vault-as-codebase.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-96-lint-five-dimension.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-97-revision-fanout-lint.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-98-obs-wikilink-fullslug-mandatory.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Collaboration/Patterns/PAT-99-hook-perf-self-diagnose.md` | `optimize` | 明显面向旧 hook 性能事故，后续仍可再压。 |
| `4-Archives/Glossary/GLO-01-novabehaviour.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Inbox-Legacy/PAT-DRAFT-2026-06-04-prefab-instance-child-migration.md` | `keep` | 已转为归档草稿，仅保留可复用迁移规则。 |
| `4-Archives/Inbox-Legacy/PAT-DRAFT-2026-06-04-unity-asmdef-rename-hardcoded-scan.md` | `keep` | 已转为归档草稿，仅保留改名排查方法。 |
| `4-Archives/Inbox-Legacy/PAT-DRAFT-2026-06-05-generator-template-single-source-fail-fast.md` | `keep` | 已转为归档草稿，仅保留生成器治理规则。 |
| `4-Archives/Patterns/PAT-63-upm-sample-readonly-prefab-path-override.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Patterns/PAT-81-bundle-collector-address-rule.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Records/0-Log-legacy-2026-06-05.md` | `optimize` | 唯一旧时序总表，但工具过程噪音仍偏多。 |
| `4-Archives/Records/ARC-01-sdk-diagnostics-deprecated.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Records/ARC-02-novabehaviour-bridge-deprecated.md` | `keep` | 已归档历史条目，保留检索价值。 |
| `4-Archives/Records/ExternalLinks-legacy.md` | `optimize` | 已归档出活跃资源层，后续仍可继续压缩旧环境细节。 |
| `4-Archives/Records/MOC-Workflow-framework-change-flow.md` | `optimize` | 已归档出活跃导航层，后续仍可再压缩正文。 |
| `AGENTS.md` | `keep` | Minds 路由规则页，当前仍是入口约束。 |
