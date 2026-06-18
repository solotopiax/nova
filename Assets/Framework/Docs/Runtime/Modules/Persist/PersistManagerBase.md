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
- 递减 `m_AutoSaveTimer`，归零时调用 `Save()` 并重置计时器为 `m_AutoSaveInterval`。

### 扩展类型默认实现

扩展类型通过 `GetString`/`SetString` 中转，无需子类重复实现：
- `GetLong`：`long.TryParse(GetString(...))`；解析失败返回 defaultValue
- `GetDouble`：`double.TryParse(..., InvariantCulture)`；解析失败返回 defaultValue
- `GetBytes`：`Convert.FromBase64String(GetString(...))`；异常时 `Log.Warning` 并返回 defaultValue
- `GetObject<T>`：`Util.Json.Deserialize<T>(GetString(...))`；异常时 `Log.Warning` 并返回 defaultValue
- `SetLong`：`SetString(..., value.ToString())`
- `SetDouble`：`SetString(..., value.ToString(InvariantCulture))`
- `SetBytes`：`SetString(..., Convert.ToBase64String(value))`
- `SetObject<T>`：`SetString(..., Util.Json.Serialize(value))`

---

## § 10 常见误区

| 误区 | 正确理解 |
|------|---------|
| Priority=0 是最高优先级，会最先 Update | Priority=0 确保 Update 最先执行（值越小越先），但 Shutdown 最后执行（值越小越后），有意保证持久化 Manager 最晚关闭 |
| 直接继承 PersistManagerBase 作为 Manager | 当前三个 Manager 直接继承本类；旧文档提到的 XxxManagerBase 中间层已被移除，改由本类统一承载公共逻辑 |
| GetBytes/GetObject 异常会抛出 | 已改为 Log.Warning 并返回 defaultValue，不会中断调用方逻辑 |

---

## § 13 关联文档

- [PersistManagerConfigBase.md](PersistManagerConfigBase.md)
- [PlayerPrefsManager.md](PlayerPrefsManager.md)
- [FileFragmentManager.md](FileFragmentManager.md)
- [SQLiteManager.md](SQLiteManager.md)
- [PersistComponent.md](PersistComponent.md)
- [FrameworkManager.md](../FrameworkManager.md)
