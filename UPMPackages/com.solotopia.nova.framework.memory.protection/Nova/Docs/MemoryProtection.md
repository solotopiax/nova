# 内存数据防篡改

功能由独立核心扩展包 `com.solotopia.nova.framework.memory.protection` 提供，实现在包内 `Nova/Runtime`。它单向依赖主 Framework，不使用 SDK Plugin、Component 或场景节点。长期敏感整数使用 `ProtectedInt` / `ProtectedLong`，临时计算和 UI 使用普通数值。

公开命名空间保持 `NovaFramework.Runtime`；类型所在程序集为 `NovaFramework.MemoryProtection.Runtime`。业务 asmdef 必须显式引用该程序集，仅引用 `NovaFramework.Runtime` 已不足以使用这些类型。主 Framework 不内置本功能，也不强制安装本包。

这两个类型用于运行时字段，不作为 Unity Inspector 序列化类型。已有 Prefab、Scene 和配置中的整数保留原字段，从加载结果构造防护对象；不要直接更换序列化字段类型，以免丢失已有 Inspector 数据。

## 配置与插件

安装本包后，包内 Editor 通过主框架通用 `PrivacyPanelExtensions` 注册 `ConfigWindow → 通用配置 → 隐私配置` 的“内存数据防护”区域。`EnableMemoryTamperDetection`、`EnableMemoryKeyRotation` 默认均为 `true`，沿用 Platform × Channel × DevelopMode 保存与导出。关闭检测即使用普通数值，轮换开关不会单独启用防护。运行时密钥自动生成，不存盘，与现有磁盘 AES Key/IV 独立。

两项配置存入主框架 `PrivacyConfigs.ExtensionConfigsJson`，扩展键为 `com.solotopia.nova.framework.memory.protection`。旧资产中的两个同名布尔字段由兼容迁移读取，已有 `false` 保持关闭，不因迁包恢复为默认开启；保存与导出继续走原隐私配置链。未安装本包时主框架保留通用扩展数据，不显示本包面板。

包内 `MemoryProtectionConfigs` 负责读取和写回本包配置：`TryRead(PrivacyConfigs, out config)`、`FromPrivacy(PrivacyConfigs)` 解析快照，`WriteToPrivacyJson(string)` 只更新本包键并保留其他扩展。缺少本包键时两项默认 `true`；格式错误时保留普通数值兼容并发出脱敏警告。`MemoryProtection.Initialize` 接收本包的 `MemoryProtectionConfigs`，正常启动由包 Manager 自动执行，业务不需要手动调用。

扩展 JSON 的内容形如：

```json
{
  "com.solotopia.nova.framework.memory.protection": {
    "EnableMemoryTamperDetection": true,
    "EnableMemoryKeyRotation": true
  }
}
```

包内 `MemoryProtectionLegacyConfigMigration.MigrateProjectAssets()` 在 Editor 延迟初始化和 `.asset` 导入后检查 `Assets` 中旧 Config 资产，只在原 YAML 结构、Privacy 对象数量、三维坐标及旧字段数量全部匹配时，通过 Unity 序列化接口保存迁移结果。已有本包扩展键不覆盖。脏资产、非标准单对象 YAML、计数或坐标不匹配、非法 JSON 会保守跳过并警告；应按提示处理后重新迁移和导出，不能把跳过当成旧开关已经迁移。此迁移保留其他扩展键与本包未知字段。

旧开关仅接受 `0/1/false/true`，同一对象中的重复旧字段也会拒绝；缺失字段沿用原默认 true。迁移先核完整资产写集，再保存当前资产一次，不把已能解析的部分开关先行迁移。

后端为 Code Stage 的 **Anti-Cheat Toolkit**，核对版本 **6.9.1**。它提供 C# 源码，但属于商业插件，不是开源软件。外网用户从 [Unity 商店购买 Anti-Cheat Toolkit](https://marketplace.unity.com/packages/tools/utilities/anti-cheat-toolkit-202695)，通过 Package Manager 的 My Assets 下载、导入并编译。内网获授权项目可安装 `com.codestage.anti.cheat.tookit`；这是自定义 UPM 名称，保留 `tookit` 拼写，不能按此名字在官方注册表购买。不能同时保留 Assets 与 UPM 副本。

隐私页提供实际 API 检测、购买、导入、内网入口和重新检查。插件缺失/API 不兼容时，安装 Framework、配置导出、Play、普通读写不阻塞；`MemoryProtection.IsActive` 和字段的 `IsProtected` 为 `false`，不会伪造轮换成功。这时没有内存防篡改能力。

## 存档到内存的完整链路

1. 启动先等待 `Nova.Config.LoadAsync()`，Config 初始化磁盘 AES 并提交隐私快照，业务再等待 `Nova.Persist.LoadAsync()`。本包 Manager 从主框架通用 `IPrivacyConfigProvider.PrivacyConfigs` 读取一次配置；常规 Tick 与首次业务访问都可以完成配置就绪后的懒初始化。
2. Persist 按既有配置解密存档，得到普通值，例如 `1000`。缺档/坏档区分、云端余额认证和防旧档重放仍属于存储与业务；重新建立内存基线不能证明存档可信。
3. `new ProtectedLong("wallet.gold", 1000)` 将可信输入交给 ACTk 的 `ObscuredLong`。ACTk 生成随机密钥、编码值和校验信息；本包不另存明文余额。Config 初始化前创建的字段暂按普通数值存储，配置加载后自动迁移。
4. `TryRead` 调用 ACTk 的公开解码与 `HashUtils.ValidateHash`，校验通过才输出 `1000`。计算可暂用原始类型；不能宣称整个进程没有明文副本。
5. `TryWrite(900)` 先校验旧状态，再由 ACTk 建立 `900` 的新状态；已经异常的字段拒绝覆盖写，防止重新建立合法基线。
6. 保存时只读出已校验普通值，经原 Persist 序列化、按既有配置 AES 加密保存。内存编码值、密钥和校验信息不进入存档。耐久性沿用所选存储实现与显式/自动 Save 语义。

建议保留存档 DTO，只替换运行中的长期字段：

```csharp
private ProtectedLong m_Gold;
// savedGold 已由既有加载逻辑完成缺档/坏档及可信性检查。
public void ApplyLoadedGold(long savedGold)
{
    m_Gold?.Dispose();
    m_Gold = new ProtectedLong("wallet.gold", savedGold);
}

// 失败时停止扣款，不把输出的 0 当成合法余额。
public bool TrySpend(long cost)
{
    if (cost < 0 || !m_Gold.TryRead(out long gold) || gold < cost) return false;
    return m_Gold.TryWrite(gold - cost);
}

// 成功后再更新原 DTO 并调用原有 Persist 保存；失败不更新、不保存。
public bool TryCaptureGold(out long savedGold) => m_Gold.TryRead(out savedGold);
```

也可直接用受保护类型做 Json.NET 字段，类型级 Converter 保持普通整数 JSON（如 `{"Gold":1000}`），有/无插件互相读档无需迁移。Converter 拒绝非整数、溢出和隔离值序列化，不使用可能包含玩家标识的 JSON 路径；没有已标注上下文时用固定 `json.protected_int32` / `json.protected_int64`。需精确字段归因时优先用 DTO 与明确逻辑标识。`Persist.GetObject<T>` 的既有反序列化失败语义为警告并返回默认值，业务不能据此把坏档当成新玩家档。

## 异常与轮换

这两个类型使用引用身份，避免可变结构体副本轮换。所有 API 仅允许 Unity 主线程。隔离或释放后 `TryRead` / `TryWrite` 返回 `false`；简便 `Value` 属性会抛脱敏 `MemoryProtectionException`，应在业务边界处理，避免未捕获异常被 TGA 当成崩溃。

逻辑字段名必须非空白且最多 128 字符，构造省略初值时为 `0`；它会进入脱敏异常与通知，不能放玩家标识或秘密。配置只在初始化时采样，注册后端也必须在初始化前；修改状态快照、读取 IsActive 或后端版本不代表所有业务字段都已受保护。Json.NET 读取 existingValue 时只继承明确 FieldId，重新创建状态，不复用原编码存储。

异常时本包隔离字段，阻断后续读、覆盖写、轮换和序列化，首次触发 `MemoryProtection.TamperDetected`。通知仅含逻辑字段名、类型、阶段、原因和隔离动作；不自动封号，也不使用原厂诱饵值恢复余额。业务停止结算、请求可信源，确认后创建新字段并释放旧对象；订阅与注销配对。

默认每 **60 秒**开始轮换，每帧最多 **32 个**字段。每个字段先校验，再以该可信值调用 ACTk 构造路径生成新随机密钥，最后复核数值未变。无插件、禁用或无目标不发轮换事件。目标已释放/隔离/回收计跳过；关闭时剩余目标计跳过，批次结果 `cancelled`。全局仅保留弱引用。

## 独立示例

主仓示例位于 `Assets/Samples/MemoryProtectionDemo/MemoryProtectionDemo.unity`，发布后从本包 Package Manager 的 `MemoryProtectionDemo` Sample 导入。示例在 `ProcedurePreload` 完成后由 `ProcedurePlaying` 直接打开 `DemoMemoryProtectionView`，展示普通存档 DTO、金币/钻石保护字段、增减、校验后保存及关闭释放。MainDemo 已移除原 Core 1.10 入口。

示例存档分类为 `MemoryProtectionDemo`、条目为 `wallet`，字段逻辑 ID 为 `memory_protection_demo.wallet.gold` 和 `memory_protection_demo.wallet.diamonds`；缺档初始值为 1000 金币、100 钻石。无插件时仍可普通读写。Editor 专属桥仅对示例字段模拟一次后端读取失败，验证隔离与结算/保存拦截；这不是 ACTk 算法密文破坏测试。另可轮换示例字段并复核数值，不改变自动调度或伪造批次埋点。桥实现不进入 Player，也不是业务公开诊断 API。

## 埋点和生命周期

事件为 `nova_memory_tamper_detected`、`nova_memory_key_rotation_result`，完整表见本包 `Nova/Tracks/Tracks.xlsx → 打点表-MemoryProtection`，主框架 TrackRegistry 自动发现并汇总此表。字段 ID 必须为 `wallet.gold` 这样的稳定名称，不传玩家 ID、存档内容或内存地址。

首次异常即时生成快照，后续被拒绝的访问每 30 秒汇总计数；后端错误不等于确认作弊。轮换每批一次，目标数等于完成、失败、跳过之和；`success` 表示没有失败，可能含跳过。没有上次轮换时批次 ID 为空，间隔为 `-1`。载荷不含数值、密钥、摘要、原厂检测对象、原始异常和堆栈。

队列最多 64 条，满时丢最旧。Ready 的 `ITrackPlugin` 出现后，每次最多转发 4 条，沿既有 TGA 路由，不等待网络、不跨会话保存；客户端转发不证明后台接收。本包在 `BeforeSceneLoad` 自动注册 Manager，复用 `FrameworkManagersGroup` 的 Update 与 Shutdown。其 Priority 为 20，SDK Manager 为 16，管理器逆序关闭时先终结轮换批次、转发剩余队列，再关闭 SDK；Shutdown/再次 Play 清理旧状态。主 Framework 无本功能的直接轮询或初始化调用。

ACTk 适配随 `NovaFramework.MemoryProtection.Runtime` 发布，缓存精确 ACTk 公开反射 API，兼容 Assets 与 UPM，不使用全局宏或商业强依赖。不创建原厂 detector，不走其 DEBUG 检测日志；本包在返回前自行判断原厂校验结果。包内 `Core/` 仅是空的第三方源码槽，不包含 ACTk 付费源码；安装、检查和运行本包不会替换或降级已安装的 ACTk。

裁剪文件在本包 `Nova/Runtime/Actk/link.xml`，Editor 处理器在 `Nova/Editor/MemoryProtectionLinkerProcessor.cs`，提供可选 `ACTk.Runtime` 的裁剪保留规则，不创建 `Assets/NovaGenerated`。AOT/HybridCLR 沿既有 Pipify 主链准备；Editor 验证不能替代 IL2CPP Player 和真机验收。

内存保护提高扫描修改成本，无法抵抗完全控制客户端的攻击者；RMB 余额和交易最终仍应由服务端裁定。
