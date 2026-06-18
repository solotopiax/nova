# FileFragmentManager

`FileFragmentManager` 是按业务分类拆文件的持久化后端。

它把每个 `classify` 对应成一个 `.dat` 文件，文件内容是 JSON，可选 AES 加密。运行时核心不是“整库常驻”，而是“目录扫描 + 分类懒加载 + 脏片段写回”。

## 什么时候先看这页

- 你要做分业务、分模块的本地存档。
- 你在排查某个分类为什么第一次访问时才真正反序列化。
- 你需要一个比 PlayerPrefs 更适合分片存档、又比 SQLite 更轻的后端。

## 依赖与边界

### 它依赖什么

- `PersistManagerBase<FileFragmentManagerConfig>`
- `FileFragmentItemGroup`
- `Path.Persist.FileFragment.FolderFullPath`
- 文件系统读写与 JSON 序列化

### 它对外负责什么

- 以 `classify.dat` 为单位组织数据
- 首次访问某分类时懒加载该文件
- 对变更分类做脏追踪并在保存时序列化

### 它不负责什么

- 不做复杂查询
- 不做跨分类聚合事务
- 不保证每次改值都立刻写磁盘

## 核心流程

### 1. Initialize 只确定根目录并触发一次 Load

初始化阶段会：

1. 接入公共配置
2. 设定根目录为 `Path.Persist.FileFragment.FolderFullPath`
3. 执行 `Load()`

### 2. Load 负责“扫描目录并预热已存在分片”

`Load()` 有重入保护；若正在加载，直接返回 `false`。

真正的 `LoadInternal()` 会：

- 确保目录存在
- 找出所有 `.dat` 文件
- 过滤掉已加载分类
- 在线程池并行反序列化

### 3. 读写接口使用分类级懒加载

`HasItem / GetXxx / SetXxx / RemoveItem` 这类操作都会先 `EnsureLoaded(classify)`：

- 已加载则直接读写内存
- 未加载则尝试从对应 `.dat` 文件反序列化
- 文件不存在时创建空组

这意味着“模块可启动很快，分类按需进入内存”。

### 4. Save 先处理待删文件，再写脏分片

保存顺序是：

1. 物理删除 `m_PendingDeletes`
2. 遍历 `m_DirtyFragments`
3. 逐个分类序列化，成功的才从脏集合移除

失败的分类会保留在脏集合里，等待后续重试。

### 5. 空分类会被转成“待删文件”

删除最后一个条目后，如果该分类为空：

- 内存组被移除
- 脏标记被清掉
- 该分类加入 `m_PendingDeletes`

真正删文件发生在下一次 `Save()`。

## 适合它的场景

- 本地存档天然可以按章节、关卡、角色或系统分片
- 需要直接按文件隔离，而不是全部挤进同一键空间
- 需要比 SQLite 更轻、更直观的落盘方式

## 风险点 / 易错点

- `Load()` 虽然会扫描目录，但后续很多操作仍然依赖分类级懒加载，不要误解成“所有文件都完整常驻解析”。
- `RemoveAll(classify)` 只是标记待删；物理删除要等 `Save()`。
- 如果某个分类文件反序列化失败，系统会记录 warning 并以空数据运行，这可能掩盖历史数据损坏。
- 文件分片并不意味着跨分类一致性事务；多个分类的保存是逐片段处理的。

## 继续阅读

关键源码：

- [FileFragmentManager.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentManager.cs)
- [FileFragmentManager.Methods.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentManager.Methods.cs)
- [FileFragmentItemGroup.cs](../../../../Scripts/Runtime/Modules/Persist/Managers/FileFragment/FileFragmentItemGroup.cs)

相关文档：

- [PersistComponent.md](PersistComponent.md)
- [IFileFragmentManager.md](IFileFragmentManager.md)
- [FileFragmentManagerConfig.md](FileFragmentManagerConfig.md)
- [FileFragmentItemGroup.md](FileFragmentItemGroup.md)

