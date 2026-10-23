# PersistManagerBase

**类签名**：`internal abstract class PersistManagerBase<TConfig> : FrameworkManager where TConfig : PersistManagerConfigBase`
**命名空间**：`NovaFramework.Runtime`

持久化管理器泛型基类，提取 FileFragment / PlayerPrefs / SQLite 三套 Manager 的公共逻辑：Priority=0 使 Shutdown 最后执行、自动保存计时器、入参校验和扩展类型（long/double/bytes/object）的 virtual 默认实现。泛型约束 `where TConfig : PersistManagerConfigBase` 保证 `InitializeBase` 能安全读取公共配置字段。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|------|----|----|
| `Managers/PersistManagerBase.cs` | `PersistManagerBase<TConfig>` | 泛型抽象基类定义 |

---

## § 3 继承关系

```text
FrameworkManager
  └── PersistManagerBase<TConfig>  (internal abstract, where TConfig : PersistManagerConfigBase)
        ├── PlayerPrefsManager (internal sealed partial, implements IPlayerPrefsManager)
        ├── FileFragmentManager (internal sealed partial, implements IFileFragmentManager)
        └── SQLiteManager (internal sealed partial, implements ISQLiteManager)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_UseAESEncrypt` | `bool` | `false` | 是否启用 AES 加密，由 InitializeBase 从 config 写入 |
| `m_AutoSaveInterval` | `float` | `0` | 自动保存间隔（秒），≤ 0 表示禁用 |
| `m_AutoSaveTimer` | `float` | `0` | 距下次自动保存的剩余时间（秒） |

---

## § 5 完整公开 API

```csharp
// 优先级（override）
public override int Priority => 0;   // 确保 Shutdown 最后执行，其他模块可在 Shutdown 前安全写入

// 基类初始化（子类 Initialize 首行调用）
protected void InitializeBase(TConfig config);

// 自动保存（子类 Update 中调用）
protected void TickAutoSave(float deltaTime);

// 输入校验（子类内部使用）
protected static void ValidateClassify(string classify);
protected static void ValidateClassifyAndItem(string classify, string item);

// 子类必须实现
public abstract UniTask Initialize(TConfig config);
public abstract override void Update();
public abstract override void Shutdown();
public abstract UniTask<bool> Load();
public abstract bool Save();
public abstract bool Save(string classify);
public abstract bool HasItem(string classify, string item);
public abstract bool RemoveItem(string classify, string item);
public abstract void RemoveAll(string classify);
public abstract string[] GetAllItemNames(string classify);
public abstract void GetAllItemNames(string classify, List<string> results);
public abstract string[] GetAllClassifyNames();
public abstract int Count(string classify);
public abstract bool   GetBool  (string classify, string item, bool   defaultValue = default);
public abstract void   SetBool  (string classify, string item, bool   value);
public abstract int    GetInt   (string classify, string item, int    defaultValue = default);
public abstract void   SetInt   (string classify, string item, int    value);
public abstract float  GetFloat (string classify, string item, float  defaultValue = default);
public abstract void   SetFloat (string classify, string item, float  value);
public abstract string GetString(string classify, string item, string defaultValue = "");
public abstract void   SetString(string classify, string item, string value);

// 扩展类型（virtual 默认实现，通过 GetString/SetString 中转；子类可 override 优化）
public virtual long   GetLong  (string classify, string item, long   defaultValue = default);
public virtual void   SetLong  (string classify, string item, long   value);
public virtual double GetDouble(string classify, string item, double defaultValue = default);
public virtual void   SetDouble(string classify, string item, double value);
public virtual byte[] GetBytes (string classify, string item, byte[] defaultValue = null);
public virtual void   SetBytes (string classify, string item, byte[] value);
public virtual T      GetObject<T>(string classify, string item, T   defaultValue = default);
public virtual void   SetObject<T>(string classify, string item, T   value);
```

---

## § 9 关键算法

### InitializeBase

子类 `Initialize` 中第一行调用，统一从 `TConfig` 读取公共配置：
```
InitializeBase(config)
  → m_UseAESEncrypt    = config.UseAESEncrypt
  → m_AutoSaveInterval = config.AutoSaveInterval
  → m_AutoSaveTimer    = config.AutoSaveInterval  // 初始化计时器
```

### TickAutoSave

子类在 `Update()` 中调用，统一接入自动保存机制：
- `m_AutoSaveInterval <= 0`：直接返回，自动保存已禁用。
- 使用子类传入的非缩放帧间隔递减计时器；归零时先重置计时器，再调用 `Save()`，每次 Tick 最多保存一次。返回的 bool 不被检查，异常继续传播；保存失败不会立即补一次重试。

公共配置在 `InitializeBase()` 中读取并保存在 Manager 字段里；之后修改 DTO 不会自动更新 Manager。此处不检查 null 配置或浮点有限性，也不初始化 AES Key/IV。分类与条目基础校验只拒绝 null/空字符串，纯空白可通过；各存储实现的附加限制见对应文档。

### 扩展类型默认实现

扩展类型通过 `GetString`/`SetString` 中转，无需子类重复实现：
- `GetLong`：`long.TryParse(GetString(...))`；解析失败返回 defaultValue
- `GetDouble`：`double.TryParse(..., InvariantCulture)`；解析失败返回 defaultValue
- `GetBytes`：`Convert.FromBase64String(GetString(...))`；异常时 `Log.Warning` 并返回 defaultValue
- `GetObject<T>`：`Util.Json.Deserialize<T>(GetString(...))`；异常时 `Log.Warning` 并返回 defaultValue
- `SetLong`：`SetString(..., value.ToString())`
- `SetDouble`：`SetString(..., value.ToString(InvariantCulture))`
- `SetBytes`：非 null 转 Base64，null 转空串，再调用 `SetString`；不会删除条目
- `SetObject<T>`：`SetString(..., Util.Json.Serialize(value))`

`defaultValue` 只控制返回结果，不会自动写入或创建条目。`GetBytes` 和 `GetObject<T>` 的 try/catch 仅包住解码/反序列化；此前的 `GetString`、IO、AES 解密与名称校验异常继续传播。合法 JSON 若反序列化为 null，会原样返回 null，不替换为自定义默认对象。`SetObject<T>` 序列化失败直接抛出。

空串由具体存储实现决定：FileFragment/SQLite 的已存空串会原样返回，Base64 解码得到空数组；PlayerPrefs 会先把空原始值当作缺项。布尔值统一存为 `"1"`/`"0"`，有值时只有 `"1"` 为 true，其他非空内容为 false；这与数值解析失败返回默认值不同。

### IPersistManager 的共同调用面与实现差异

`IPersistManager` 声明加载、保存、增删、枚举及八类值的读写接口；具体默认行为来自三种存储实现，不能由相同签名推导相同持久化边界。

| 操作 | PlayerPrefs | FileFragment | SQLite（非 WebGL） |
|---|---|---|---|
| `Load()` | 重建平台分类/条目索引 | 扫描并恢复现有分片；加载中重入返回 false | 仅判断连接非 null，不预加载表 |
| `HasItem / GetXxx / Count / GetAllItemNames` | 索引查询或平台值读取 | 可能懒加载、恢复并隔离磁盘文件 | 可能创建表、SELECT 并解密 |
| `GetAllClassifyNames()` | 内存分类索引 | 当前已注册内存分类 | 数据库表名 |
| `Save()` | 刷新全部脏索引后平台全局 Save | 处理待删文件，再逐脏分片保存 | 逐脏表各自事务提交 |
| `Save(classify)` | 只刷新该类索引，底层仍全局 Save | 已缓存分类才保存，不处理待删队列 | 已缓存表才提交 |
| `RemoveItem / RemoveAll` | 平台删键；整类删除立即 Save | 内存删除并排队，需无参 Save 完成物理删除 | 条目删除进缓冲；整类删除仅对已缓存表执行 DROP |

数组枚举返回副本，列表重载追加而不清空；没有跨实现统一的条目顺序承诺。多分类保存不构成跨分类事务。SQLite 在 WebGL 下读值返回传入默认值，枚举为空，写入/删除无操作，Load/Save 返回 false。

---

## § 10 常见误区

| 误区 | 正确理解 |
|------|---------|
| Priority=0 是最高优先级，会最先 Update | Priority=0 确保 Update 最先执行（值越小越先），但 Shutdown 最后执行（值越小越后），有意保证持久化 Manager 最晚关闭 |
| 直接继承 PersistManagerBase 作为 Manager | 当前三个 Manager 直接继承本类；旧文档提到的 XxxManagerBase 中间层已被移除，改由本类统一承载公共逻辑 |
| GetBytes/GetObject 会吞掉所有错误 | 仅解码/反序列化异常记 Warning 并返回默认值；底层读取、解密和参数校验错误仍可能抛出 |

---

## § 13 关联文档

- [PersistManagerConfigBase.md](PersistManagerConfigBase.md)
- [PlayerPrefsManager.md](PlayerPrefsManager.md)
- [FileFragmentManager.md](FileFragmentManager.md)
- [SQLiteManager.md](SQLiteManager.md)
- [PersistComponent.md](PersistComponent.md)
- [FrameworkManager.md](../FrameworkManager.md)
