---
name: nova-project-onboard-sdk-kit
description: Use when 项目组要接入已发布的 Nova SDK/Kit 或已有 Framework Native 系统能力，完成配置与业务接入、受控提交已导出的 IAP 商品目录，或定向维护项目自有 Tracks 权威表及本地汇总时使用。
---

# Nova 接入 SDK、Kit 与已有系统能力

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json` 和 [共用执行路径](references/shared-execution.md)，把 `nova-project-manage-upm-package` 和 `nova-project-configure-runtime` 作为必要时独立验收的前置节点。业务目标明确时按共用引用的分支复用已有 UI、Event、Procedure、Persistence、Network Operation，只读取命中的契约，不把它们都变成强制安装依赖。仅在需要定位实际 Framework 时，使用已解析包内 `Agents/Tools/nova_skills.py resolve --project-root <projectRoot>`。目标 SDK/Kit 的路径由实际 `PackageInfo.resolvedPath`、manifest 与 lock 确认；从该包 `Nova/Doc/INDEX.md`、`Nova/Docs/INDEX.md`、`Nova/DOCS/INDEX.md` 中选择首个存在的入口，再只读当前任务需要的页面和匹配版本的 Nova 公开声明。不要以主框架公共文档替代目标包文档，不读取主仓 `.nova/`、`Minds/`、绝对路径或未命中的厂商源码。

按用户目标选择一份引用，不全量加载：

| 用户目标 | 按需引用 |
|---|---|
| 游客/三方登录、账号绑定、云存档 | [身份与存档](references/identity-and-save.md) |
| 广告、支付、统计归因、推送、客服、ABTest、Native 通知权限/评价/设置 | [服务能力](references/service-capabilities.md) |
| 提交已正式导出的完整 IAP 商品目录、核对一次提交结果 | [商品目录上传](references/product-catalog-upload.md) |
| 微信小游戏及其平台长尾能力 | [微信平台](references/wechat-platform.md) |
| 从已有 Excel 批量导入 IAP SKU 或微信购买表 | [内联商品转换与保存](references/inline-products.md) |
| 业务埋点、Tracks 事件/属性/页结构、目标与正式汇总 | [Tracks 维护](references/tracks-maintenance.md) |

已安装并配置好的能力只需解释参数或补业务调用时，先复用对应引用和已有领域 Operation，不重复安装、不重复初始化、不为每个包、渠道、字段或 API 新建 Skill。

目标仅为项目自有 Tracks 既有文字维护时，选择本契约 `tracks-source-text` 任务画像，按 [Tracks 维护](references/tracks-maintenance.md) 直接执行共享表源事务。该画像显式 `requires=[]`，仅需项目根和冻结文字写集，以当前静态审查、真实源表/收据产物及清场验收，不执行 SDK 初始化、UPM、Config 或 Play。画像仍继承原确认、锁、写入边界；完整父工作流及其原依赖的 `SKILL.md/contract.json` 源码指纹必须冻结，不能借任务画像放宽其他接入任务或继承旧分支证据。

## 范围、文档门与冻结输入

SDK/Kit 分支处理已发布、已配置 registry 且包名为 `com.solotopia.nova.framework.sdk.*` 或 `com.solotopia.nova.framework.kit.*` 的单个包；已有 Framework Native 分支按服务引用直接接入其公开门面，跳过 SDK/Kit 包管理和 ConfigMaster/ConfigRuntime 步骤。安装或升级必须先进入 `nova-project-manage-upm-package` 的 `install-latest` 或 `upgrade-latest` 只读 Plan；指定历史版本、降级、来源切换、未发布包、批量升级、Framework 自身升级、任意第三方包或厂商后台操作一律 `not_applicable`。

先从消费项目、当前坐标、已安装包、业务入口和已有配置中读取可确定的事实，再冻结唯一项目根、精确包名、包管理意图、目标 Platform × Channel × DevelopMode、ConfigMaster、目标 ConfigRuntimeSO、目标 SDK/Kit Config 类型、明确字段变更集、目标平台、包内文档入口、平台前置清单及包文档定义的最小本地运行探针。用户要求业务可用时，同时冻结项目自有业务source/程序集、已有按钮或流程、成功/失败/取消、订阅拥有者、账号和持久化边界、目标交互与成功探针；已有授权覆盖必要业务实现时直接继续。让用户只补确需业务决策的渠道/模式、平台物料、进度保留/发货策略和验收目标，不要求新手抄写可读取的类型、路径或参数。目标不唯一且项目事实不能消歧时返回 `blocked`，不得从相邻坐标、Sample 或其他 SDK 猜值。

已安装包可先复核其精确 package id、版本、来源与包内 INDEX；只有它已解析且满足本次冻结意图时，包管理节点才可返回 `not_applicable` 后继续。首次安装或升级 Resolve 后如果找不到包内 INDEX，不进入配置或运行探针：已完成的 UPM 变更保留现场，Workflow 至少返回 `partial`，不自动卸载或回滚。

## 依赖图与受控执行

```text
冻结包、坐标与平台目标
          │
          ├─ UPM Operation：Plan → 用户确认 → Resolve/Verify
          │                         │
          │                         ▼
          └──── 解析目标包 → 包内 INDEX → 平台前置核验
                                             │
                                             ▼
                    Config Operation：单一三维坐标 → Runtime 快照 → 编译
                                             │
                                             ▼
                    已授权业务入口与生命周期 → 必要表/资源/Prefab绑定
                                             │
                                             ▼
                         编译与本地探针 → 精确授权的外部验收
```

UPM Plan、Config 写入和平台前置分别遵守其精确授权边界；已有授权覆盖的步骤持续执行，Workflow 不因编排扩大授权。包更新、编译和 domain reload 稳定前不得读取新包内容或执行下一节点。平台前置优先复用目标包 INDEX 声明的既有 Unity Editor、MCP、Importer 或项目配置入口；Android 原生依赖解析交给 `nova-project-resolve-android-dependencies`，构建、导出与设备诊断按共用引用路由到已有 Operation。没有可验证的已声明入口时保留阻塞，不手写 Gradle、Info.plist、Manifest、Prefab、Scene、ScriptableObject 或任意 JSON 字段补丁。

Config 变更只能通过 `nova-project-configure-runtime` 的受控 Unity 编辑或已开放的类型化 Action 完成。不得调用通用反射、`execute_code` 或“按字段名写 JSON”来修改 SDK/Kit 配置。包文档没有说明目标 Config 类型、字段语义、平台条件或安全输入方式时，停止在文档门，不把 Scanner 发现、插件实例存在或无报错编译当成已接入。

稳定步骤直接执行已有 Action 的 `describe → plan → execute → verify`，复用 Schema、计划与 Receipt，避免每次重新拼临时脚本。Tracks 结构按对应引用复用同领域 CLI，汇总复用 `nova.project.tracks.generate-registry`；叶子编辑不扩展为新 Skill。SDK 顶层 Config、广告内嵌 ChannelConfig、IAP 内嵌 StoreConfig 分别走其真实编辑入口，不能把内嵌类型传给顶层插件 Scanner。`enable` 修改 Master 全局白名单；首次启用可能需要完整矩阵实例，不能为消除错误自动扩大单格写入范围。

## 仅维护 Tracks 表源与汇总

用户只要求现有文字维护时选择 `tracks-source-text`；新增事件、属性、事件页、定向目标或单色样式并生成本地全源汇总时选择 `tracks-source-structure`。两者均按 [Tracks 维护](references/tracks-maintenance.md) 使用既有事务，不执行 SDK 安装、Config 或业务初始化；结构分支验收真实源与汇总的 `static + artifact`，保留原确认、完整 Skill 闭包指纹、写入边界与锁。修改生成器或业务代码时另补实际编译及对应运行证据；业务发送与厂商收件仍独立验收。

## 已确认业务接入

自然语言要求“按钮可登录 / 看广告领奖 / 购买并发货 / 恢复进度 / 打开客服 / 接入通知或实验”时，安装和配置是前置，不是最终交付。按 [共用执行路径](references/shared-execution.md) 的业务分支，直接修改已确认项目自有入口，调用已核实的Nova公开API；UI/序列化绑定复用对应UI或edit-settings，业务事件、流程、存档和协议复用已有Operation。不修改Framework、SDK/Kit、vendor或生成代码，不复制Sample为完整实现，也不为每个渠道、方法新增Skill。

业务编排核对调用前置、初始化/会话、订阅与注销、错误/取消、持久化和副作用；表驱动任务继续权威源→正式导出→生成访问→Config→运行消费。SDK Observable/Replay事件不同于Nova.Event池化事件，不能代换：订阅须成对释放，重放可能再次触发历史结果，奖励/发货沿项目既有唯一业务标识和幂等消费处理。没有真实协议、奖励/订单身份或账号/进度策略时指出具体缺项，继续完成不依赖缺项的已授权本地工作。

写业务代码与执行真实服务分开验收。默认业务接入副作用覆盖本地实现、配置和观察；真实登录/绑定、云存档上传/覆盖、支付/补单/发货、Topic订阅、统计/归因上报、实验曝光与push任务仍需冻结精确环境、账号/UID来源、目标商品/订单/任务、请求数量/重放语义、数据和接收方，再由已明确授权的运行阶段执行。完整开发授权不等于无限次生产请求，缺外部条件不把本地Mock当真实结果。

仅提交 IAP 商品目录时，按[商品目录上传](references/product-catalog-upload.md)复用 `nova.project.iap.upload-product-catalog` 的 `describe → plan → execute → verify`，不重装包、不默认初始化支付或运行 Play。真实发送须已有精确环境、目录/客户端版本、目标与次数授权；一次性确认不由 Tool 参数替用户授权。2xx 目前最高仍为 `partial`，只证明 HTTP 接受；版本冲突、失败、取消或重载均不自动重发，不将目录分支当支付、验单和发货整任务通过。

## 密钥、平台与最小运行证据

- 密钥、Token、密码、AES Key/IV、Cookie、私钥、设备标识和厂商后台凭据不进入 Prompt、Plan、日志、截图、Diff 或最终报告。只报告已脱敏的字段标签、是否已由用户按包文档的安全入口配置，以及下一步需要谁确认；不得回显任何值。
- 平台前置只按目标包文档逐项核验。厂商后台建档、凭据发放、商店配置、外部上传和真机安装需单独精确授权与可用能力；缺少时如实报告 `partial` 或 `blocked`，继续本次可完成的本地实现。
- 最小运行探针必须是包文档明示的本地初始化、注册、可用性或错误信号，并在用户允许的现有项目入口中收集。Unity 编译、ConfigRuntimeSO 导出、插件 Config 已启用或单条“无异常”日志都不能单独证明厂商服务可用。
- 若包文档把真实设备、厂商回调或后台结果列为必要条件，已授权业务代码、绑定和本地验证继续完成；外部证据取得前整体为partial。只读日志问题交给 `nova-project-diagnose-device-runtime`，不声称真机、登录、支付、广告、推送、归因或厂商后台已成功。

## 结果边界

`tracks-source-text` 仅在完整冻结文字事务成功、只读 Verify 核对真实产物和非目标部件、当前任务/画像证据一致且清场完成后报告 `success`；该结论只证明本次源表文字维护。增加 SDK 业务调用、事件结构、正式汇总或厂商接收目标时回到对应完整分支，不能沿用文字画像的证据门。

达到精确 UPM 解析、包内文档、必要三维配置、编译和包文档定义的最小本地探针时，配置接入可报告 `success`；已有满足配置不重复导出。用户要求业务功能时，还须交付其明确入口、依赖链及成功/失败/取消/注销/幂等探针，不能把安装/Ready当业务完成。必需真实服务证据缺失、运行探针或平台核验未完成时为 `partial`；缺少唯一输入、确认、文档或安全入口且尚未执行为 `blocked`。本 Workflow 不默认导入 Sample、构建 Bundle / Player、安装应用、发布 CDN、使用凭据、提交或推送 Git；已授权业务实现继续按共用分支完成。
