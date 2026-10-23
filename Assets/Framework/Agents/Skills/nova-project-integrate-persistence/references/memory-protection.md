# 关键整数的内存防护接入

本分支处理项目自有业务里的 `ProtectedInt` / `ProtectedLong` 接入。能力由独立核心包 `com.solotopia.nova.framework.memory.protection`（显示名 `Nova Framework - MemoryProtection`）提供，源码位于包内 `Nova/Runtime/`，程序集为 `NovaFramework.MemoryProtection.Runtime`，公开命名空间仍为 `NovaFramework.Runtime`；不是 SDK/Kit，也不需要场景组件。本分支没有可调度的稳定运行 Action，只使用已授权工作区编辑、公开 C# API 和既有 Unity 编译/Play 通道；三项 `nova.project.persistence.*` 维护 Action 不用于内存防护。

## 先取得可推断的项目事实

- 从当前工程实际解析的独立包读取 `Nova/Docs/INDEX.md` 并核对公开类型和方法；开发仓查 `UPMPackages/<包名>/`，消费工程按注册包的真实路径查阅。未安装时复用 `nova-project-manage-upm-package` 的安装分支，并沿用已有授权；不以安装 Framework 推断此可选核心包已存在，也不反向修改包或复制实现。
- 核对业务 asmdef 是否已引用 `NovaFramework.MemoryProtection.Runtime`；已授权源码接入允许向冻结的业务 asmdef 添加这一必要引用，不改程序集名称、命名空间或其他依赖。包不存在时先处理安装，不能只补一个断链引用。ACTk 是另一个可选商业插件，不是此包的强制安装依赖。
- 查找现有余额、计数、体力等长期整数的业务 owner、声明类型、初始化/加载、读取、修改、结算、保存及释放点；路径、命名空间和当前入口由 Agent 取得。保持既有业务结构，不因缺某个调用方法就要求用户提供代码模板。
- 冻结稳定逻辑字段名、可信初值来源、算术及范围规则、检测失败后哪些提交必须停止、已授权业务写集和隔离探针。字段名不得包含玩家 ID、内存地址、业务数值或凭据。业务规则、可信恢复来源无法推断时，只询问这些缺项。
- 只做内存接入时无需新建 `classify/item`、修改存档 schema、选择数据库或提供 AES/Cipher 密钥。真正涉及持久化读取时复用原调用与格式，并遵守 `await Nova.Config.LoadAsync();` 后再 `await Nova.Persist.LoadAsync();` 的顺序；已有启动 owner 已完成时不再重复加载。

## 可用状态和配置

两个开关属于包自己的 `MemoryProtectionConfigs`：`EnableMemoryTamperDetection`、`EnableMemoryKeyRotation` 缺省均为 true。配置通过 `PrivacyConfigs.ExtensionConfigsJson` 中以 `MemoryProtectionConfigs.ExtensionKey`（值为包名）命名的对象保存，通用隐私配置不再声明这两个专用字段；开关不是插件安装状态。缺少该扩展项沿用默认值，格式损坏时本包停用两个开关并记录脱敏提示，不覆盖其他扩展数据。

本包的 `MemoryProtectionManager` 自动注册到既有管理器集合，Priority 为 20；配置加载后，通过 `IPrivacyConfigProvider.PrivacyConfigs` 在管理器轮询或首次字段访问时初始化。逆序关闭时先于 Priority 为 16 的 SDK 清空防护队列，再由原集合继续关闭 SDK。消费业务只等待既有 Config 启动完成，不重复调用 `MemoryProtection.Initialize`、注册后端或接管 Tick/Shutdown；主 Framework 不反向依赖此可选包。

| 公开状态 | 能说明什么 |
|---|---|
| `MemoryProtection.IsActive` | 当前包已初始化、检测配置启用且有可用防护后端 |
| `MemoryProtection.DetectionEnabled / KeyRotationEnabled` | 当前配置开关；值为 true 不能单独证明防护或轮换实际执行 |
| `MemoryProtection.ActkVersion` | 当前后端报告的插件版本；空字符串表示没有后端 |
| 字段 `IsProtected` | 当前字段有后端存储且未释放、未隔离；仍须检查每次 `TryRead/TryWrite` 返回值 |
| 字段 `IsQuarantined` | 当前字段已被隔离，应停止依赖它的结算和保存 |

未安装 ACTk、适配不可用或检测关闭时，同一公开类型允许普通数值存取，不能宣称有内存防护。安装本包后，`Nova/Open Config → 通用配置 → 隐私配置 → 内存数据防护` 由包内 Editor 提供检测、购买、Package Manager/My Assets 导入或已授权内仓安装指引与重新检查入口：绿色表示已检测，黄色表示未检测。识别到插件不代表购买许可已核实，也不代替 Player 验收；卸载本包后这一专用配置区不再展示，通用扩展数据仍由主框架保存。本分支不自动购买、下载、安装商业插件、切换配置或导出配置；这些动作需要对应任务的既有精确授权。

## 业务字段与失败阻断

`ProtectedInt` / `ProtectedLong` 是引用类型。复制引用会共享同一状态，不能把它当独立数值快照；owner 负责其生命周期。不要批量替换临时运算变量，也不把原厂 ACTk 类型写入业务存档契约。

以下只是单字段消费方式，业务仍沿用自己的金额规则和可信加载值。创建、读写、JSON 转换及释放均在 Unity 主线程执行；不要移到 `Task.Run`、Job 或后台存档回调中。

```csharp
using System;
using NovaFramework.Runtime;

internal sealed class WalletState : IDisposable
{
    private readonly ProtectedLong m_Coins;
    private readonly ProtectedInt m_Energy;

    /// <summary>在既有配置及所需存档加载完成后，用可信初值建立业务状态。</summary>
    internal WalletState(long coins, int energy)
    {
        m_Coins = new ProtectedLong("wallet.coins", coins);
        m_Energy = new ProtectedInt("wallet.energy", energy);
    }

    /// <summary>示例仅扣一个字段；校验或写入失败时不允许后续发奖、提交或保存。</summary>
    internal bool TrySpend(long amount)
    {
        if (amount < 0 || !m_Coins.TryRead(out long coins) || coins < amount)
            return false;
        return m_Coins.TryWrite(coins - amount);
    }

    /// <summary>只在所有读取成功后提供普通整数快照；false 时不得保存默认输出。</summary>
    internal bool TrySnapshot(out long coins, out int energy)
    {
        coins = 0;
        energy = 0;
        if (!m_Coins.TryRead(out long currentCoins) ||
            !m_Energy.TryRead(out int currentEnergy))
            return false;
        coins = currentCoins;
        energy = currentEnergy;
        return true;
    }

    /// <summary>由业务 owner 在原生命周期结束处释放，之后不再复用旧字段。</summary>
    public void Dispose()
    {
        m_Coins.Dispose();
        m_Energy.Dispose();
    }
}
```

- `TryRead` 返回 false 时输出为 0；这个 0 不是合法余额或可用恢复值。`TryWrite` 失败也必须沿业务失败路径退出，不能继续发奖、网络提交或保存。
- `Value` 是便捷属性，失败时抛 `MemoryProtectionException`；关键结算边界优先使用 Try 方法，不能靠捕获后默认成功、返回 0 或立即覆盖写绕过隔离。
- 多字段结算不能只证明一个字段读写成功；沿现有事务/提交策略确保其他值与外部提交不会在检测失败后继续。本 API 不提供跨字段原子事务，也不自动封号、修复存档或拉取服务端数据。
- 如业务需要响应通知，可成对订阅/注销 `MemoryProtection.TamperDetected`，只消费 `MemoryTamperInfo` 的逻辑字段和受控原因；失败也可能来自后端错误，不能直接当作作弊裁决。不上传或打印值、密钥、密文、hash 或原始异常。
- 已隔离字段不以 `TryWrite` 恢复。使用项目已确认的可信重载/重建策略，替换时先结束旧 owner；恢复来源不明确时保留失败状态，不自行生成“正确余额”。

## JSON 与真实存档边界

继续使用当前项目的 `Util.Json.Serialize/Deserialize`（Newtonsoft.Json）链。两个类型已带 `ProtectedIntegerJsonConverter`：写出普通 JSON 整数，不保存密钥、ACTk 状态或对象内部字段；读取只接受整数令牌并校验 int/long 范围。无插件时格式相同。隔离值会拒绝序列化，不能捕获后覆盖旧存档。

保留原有存档数字字段名与 DTO；从普通数值 DTO 的可信加载结果建立上述运行态对象，保存前通过完整成功的快照形成普通数值 DTO，再沿原存储入口处理。直接反序列化受保护类型时，转换器沿用既有受保护对象的 `FieldId`；没有既有对象时，`ProtectedInt` / `ProtectedLong` 分别使用固定的 `json.protected_int32` / `json.protected_int64`。若需要准确的业务逻辑字段名，应选择普通 DTO 后显式构造。不得把此转换器对 Newtonsoft.Json 的支持外推为 Unity Inspector、JsonUtility、SQLite ORM 或所有存储后端都可直接保存包装对象。

本分支只修改已授权业务源码、必要的 `NovaFramework.MemoryProtection.Runtime` 业务程序集引用及隔离验证，不编辑或迁移真实存档文件、ConfigMaster/ConfigRuntime、Prefab/Scene、生成物、Framework/本包实现或第三方插件。静态接入不授权运行真实存档迁移；数据维护和插件/配置改动分别走对应已授权任务。

## 可复用的独立 Demo 参考

本包随附 `MemoryProtectionDemo`，开发态位于 `Assets/Samples/MemoryProtectionDemo`，消费态先从本包的 Samples 导入。示例命名空间是 `NovaFramework.Memory.Protection.Samples.Runtime`，入口为 `DemoMemoryProtectionView`；MainDemo 不再承载旧 `Core → 1.10` 页面。`DemoMemoryProtectionWallet` 通过已加载的 `IFileFragmentManager` 读取普通整数 DTO、构造金币/钻石字段、检查失败后停止结算和保存，并在关闭时释放。只借鉴其公开接入链，真实业务继续使用自己的存档格式、范围规则与可信恢复来源；固定演示初值不构成真实余额恢复策略。

该 Demo 的 Editor 桥会反射 Nova 自有状态，仅为本页字段模拟校验失败或执行单字段轮换；它不是消费端公开能力，不复制到业务，也不能作为 ACTk 算法篡改、自动批次轮换或 Player 验收证据。

## 验证和交付

以实际业务入口为边界检查字段身份、加载顺序、所有失败返回的调用方处理、主线程、owner 释放及普通整数 JSON。已有可用插件时，在隔离探针中记录 `IsActive` 与字段 `IsProtected`，再核验正常读写、失败阻断和序列化拒绝；不得为制造异常修改真实玩家数据或原厂源码，也不通过反射私有字段伪装公开能力。没有可用后端时，只验普通存取及同格式 JSON，并明确“防护未生效，防护路径尚未验证”。不为此反复卸载/安装插件或切换真实配置。

`success` 仅覆盖本次已证实的请求目标：源码接入、编译及所需隔离运行检查齐全且清场后才报告接入完成；用户只要求普通兼容时可据其证据完成该范围。用户要求实际防护而缺后端/真实异常验证，或只做了静态/编译检查时为 `partial`；缺业务规则、可信来源或授权写集时为 `blocked`；要求本能力不支持的数值类型、Framework 改造或真实存档维护时转交对应范围，当前分支返回 `not_applicable`。清理本轮探针及 `.meta`、日志、测试对象/目录，必要 Refresh 后检查工作树；实际 Player/设备、HybridCLR 与 TGA 后台接收仍各自验收。
