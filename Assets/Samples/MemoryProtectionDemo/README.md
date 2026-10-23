# MemoryProtectionDemo

这是 `Nova Framework - MemoryProtection` 的独立钱包示例。入口为本目录的 `MemoryProtectionDemo.unity`，启动预加载完成后直接打开 `DemoMemoryProtectionView`。命名空间为 `NovaFramework.Memory.Protection.Samples.Runtime`，不依赖其他 Sample。

## 安装与运行

1. 安装 `com.solotopia.nova.framework.memory.protection` 及其声明的 Framework 依赖，再从 Package Manager 导入 `MemoryProtectionDemo`。业务公开类型仍在 `NovaFramework.Runtime` 命名空间，由 `NovaFramework.MemoryProtection.Runtime` 程序集提供。
2. 双击实际导入目录中的 `MemoryProtectionDemo.unity`。在 ConfigWindow 激活本 Sample 的 ConfigMaster，核对目标 Platform、Channel、DevelopMode 和隐私配置，按当前坐标导出配置。
3. 等待编译完成后进入 Play。需要构建 Player 时，将该场景设为启动场景，并按实际目标完成资源、HybridCLR 和 Player 构建准备。

Anti-Cheat Toolkit（ACTk）是可选商业依赖。未安装或 API 不兼容时，页面使用普通数值兼容模式，仍可加载、修改和保存，状态区明确显示未启用保护，两项 Editor 诊断按钮不可用。需要实际防护时，在 `ConfigWindow → 通用配置 → 隐私配置` 使用“购买”入口前往 [Unity 商店 Anti-Cheat Toolkit](https://marketplace.unity.com/packages/tools/utilities/anti-cheat-toolkit-202695)，通过 My Assets 下载、导入并编译，再重新检查。内网获授权项目可按隐私页的内网入口安装，勿同时保留 Assets 与 UPM 两份插件。

`EnableMemoryTamperDetection` 与 `EnableMemoryKeyRotation` 控制检测与自动轮换。检测关闭时使用普通数值，轮换开关不会单独开启保护。运行时密钥自动生成，不需要填写或保存。完整接口与插件兼容要求见已安装包的 `Nova/Docs/MemoryProtection.md`。

## 存档到内存再保存

`DemoMemoryProtectionWallet` 仅使用 `Nova.Persist.FileFragment` 的固定分类 `MemoryProtectionDemo`、条目 `wallet`。存档只有 `SchemaVersion`、`Gold`、`Diamonds` 三个普通整数。

1. `ProcedurePreload` 等待存储与 UI 就绪，页面从普通 DTO 读取金币、钻石。缺档时创建 1000 金币、100 钻石的内存初值，尚不保存。坏结构或越界值拒绝加载，原存档保留。
2. 以稳定字段 ID `memory_protection_demo.wallet.gold`、`memory_protection_demo.wallet.diamonds` 建立 `ProtectedLong`、`ProtectedInt`。启用防护且插件可用时，长期状态由防护后端持有。
3. 点击增减按钮，先 `TryRead` 校验旧状态，再计算并 `TryWrite`。余额不足、溢出或字段隔离时拒绝修改，不把失败返回的零值当作余额。
4. 点击保存，两项都校验通过后才转换为普通 DTO，调用 `FileFragment.SetString`、`Save`。内存密钥、校验信息和插件对象不写入存档。

关闭页面会释放本页字段并退订通知；再次进入场景会重新加载。显式新建只恢复本例内存初值，点击保存才写盘。保存返回成功不证明设备掉电耐久性，真实持久化需结束旧存储实例后独立重开读取。

## 页面按钮

| 按钮 | 作用 |
|---|---|
| 读取演示存档 | 释放旧字段，从固定存档重新建立本页运行时值 |
| 新建演示钱包 | 恢复 1000 金币、100 钻石的内存初值，暂不写盘 |
| 金币 +100 / -100 | 校验钱包后增减金币，负余额和溢出拒绝 |
| 钻石 +10 / -10 | 校验钱包后增减钻石，负余额和溢出拒绝 |
| 保存演示钱包 | 两项均有效才保存普通整数 DTO |
| 模拟校验失败 | 仅 Editor，让本页金币下一次后端读取返回校验失败 |
| 轮换本页字段 | 仅 Editor，轮换本页两个字段并核对前后余额 |

“模拟校验失败”调用 Sample 专属 Editor 桥，不进入 Player。预期可观察到字段隔离通知，随后增减和保存被拒绝，演示存档不被异常值覆盖。这是后端失败与业务拦截断言，不是 ACTk 密文破坏测试。“轮换本页字段”不改变全局自动轮换时间，不伪造批次埋点。生产自动轮换由 MemoryProtection 自行调度。

本 Sample 只演示内存防护钱包。脚手架保留本地化、源表、资源收集与可选 Running/Content 构建骨架，未额外提供网络、声音、振动、表格或热更业务页面。正式 UI 注册由 `Excels/UIs/UIs.xlsx` 的唯一业务行导出到 `Jsons/UIs.json` 和 `Scripts/Runtime/DataTypes/UIs/`，资源地址为 `DemoMemoryProtectionView`。

Editor、Player、IL2CPP、真机及埋点后台接收各需独立证据。演示初值与本地存档不提供服务端余额认证或防旧档重放能力。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
