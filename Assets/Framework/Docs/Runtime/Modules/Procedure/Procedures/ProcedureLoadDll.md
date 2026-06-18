# ProcedureLoadDll

`ProcedureLoadDll` 是 HybridCLR 启动链里的关键流程页。它解决的问题只有一个：

- **把框架从“资源系统已可用”推进到“业务程序集已加载、业务 Procedure 已注册、可以跳进业务入口”。**

它不是热更下载流程，也不是配置导出流程，而是连接 `Asset / Config / HybridCLR / Procedure` 的启动桥。

## 什么时候先看这页

优先看这页的场景：

- 你要看业务 DLL 是在什么时候加载的。
- 你要排查为什么业务入口 Procedure 没有被找到。
- 你要判断 `ConfigManager.Namespace / GameEntranceProcedureName / GameDlls` 在启动里怎么被消费。
- 你要排查 `RegisterAdditionalProcedures` 为什么要延后到下一帧。

## 输入 / 输出

### 输入

- `IAssetManager`
- `IConfigManager`
- `ConfigRuntimeSO.Namespace`
- `ConfigRuntimeSO.GameEntranceProcedureName`
- `ConfigRuntimeSO.AotMetadataDlls`
- `ConfigRuntimeSO.GameDlls`

### 输出

- AOT metadata 已补完
- 业务 DLL 已加载
- `Util.Assembly` 缓存已刷新
- 业务程序集中的 `ProcedureBase` 子类已注册
- `m_EntranceType` 已定位，可跳转业务入口

## 主链路

### 1. 先 `Yield`，再做后续工作

`RunLoadAsync()` 开头先执行：

- `await UniTask.Yield()`

目的不是异步好看，而是避开 FSM 正在 `ChangeState` 的时刻。  
如果在 `OnEnter()` 同步调用栈里直接 `RegisterAdditionalProcedures()`，会撞上 `Fsm.AddStates` 的守卫。

### 2. Bootstrap + LoadManifest

先通过 `IAssetManager`：

- `BootstrapAsync(ct)`
- `LoadManifestAsync(null, ct)`

这一步保证底层资源系统和清单已就绪。  
即使上一个流程已经跑过 `LoadManifestAsync`，这里仍然按幂等方式再调用一次，确保“跳过热更检查”的路径也成立。

如果 HostPlayMode 下远端版本文件或 Manifest 不可达，`AssetManager.LoadManifestAsync` 会尝试回退到随包内置清单。回退成功时，`ProcedureLoadDll` 会继续加载 `ConfigRuntimeSO` 和业务 DLL，相当于使用 Player 内置资源跳过本轮远端热更；回退失败才会继续抛出资源清单错误。

### 3. 加载 ConfigRuntimeSO

- `await configManager.LoadAsync()`

这里依赖的是 `ConfigManager` 的幂等语义。  
如果配置已经在更早阶段加载完成，这里直接复用。

### 4. 先补 AOT metadata，再加载业务 DLL

顺序要求非常严格：

1. 并行加载 `AotMetadataDlls`
2. 顺序加载 `GameDlls`

原因：

- AOT metadata 彼此无顺序依赖，适合并行
- 业务 DLL 之间可能有依赖关系，顺序加载更稳妥
- AOT metadata 必须先于业务 DLL 补齐

### 5. 刷新程序集缓存

- `Util.Assembly.RefreshAssemblies()`

否则后续对业务程序集和业务 Procedure 的反射视图仍然可能是旧的。

### 6. 扫描并注册业务 Procedure

主流程会：

1. 用 `configManager.Namespace` 找到业务程序集
2. 扫描其中全部非抽象 `ProcedureBase` 子类
3. `Activator.CreateInstance(...)`
4. `procedureOwner.Owner.RegisterAdditionalProcedures(procs)`

这是“框架内置流程”与“业务流程”真正接起来的地方。

### 7. 定位业务入口并等待 OnUpdate 跳转

最后通过：

- `Namespace + "." + GameEntranceProcedureName`

拼出业务入口全名，定位到 `m_EntranceType`。  
真正的状态跳转发生在 `OnUpdate()`，不是在 `RunLoadAsync()` 里直接跳。

## 前置条件

- `IAssetManager` 必须已注册
- `IConfigManager` 必须已注册
- `ConfigRuntimeSO.Namespace` 必须有值
- `ConfigRuntimeSO.GameEntranceProcedureName` 必须有值
- `ConfigRuntimeSO.GameDlls` 必须能覆盖业务程序集

其中任何一项缺失，最后都会变成“程序集找不到”或“入口 Procedure 找不到”。

## 常见失败点

### 0. 远端版本文件不可达

表现：

- `RequestPackageVersionAsync failed`
- `HttpCode=0`
- `Cannot resolve destination host`

优先排查：

- 当前是否是 HostPlayMode
- Player 包内是否带了内置版本文件和内置 Manifest
- 回退内置清单的 Warning 是否出现
- CDN / DNS / 主备 URL 模板是否正确

### 1. `IConfigManager` 未就绪

表现：

- 直接抛 `InvalidOperationException`

优先排查：

- `ConfigComponent` 是否存在
- 启动顺序是否已被修改

### 2. `Namespace` 为空

表现：

- 找不到业务程序集

优先排查：

- `ConfigWindow` 导出的 `ConfigRuntimeSO` 是否包含正确 `Namespace`

### 3. `GameDlls` 配置不完整

表现：

- `Util.Assembly.GetAssembly(businessAssemblyName)` 返回 `null`

优先排查：

- `ConfigMasterSO.GameDlls`
- DLL 是否真的被拷贝到可加载位置

### 4. `GameEntranceProcedureName` 配错

表现：

- 入口 Procedure 全名拼出来了，但在业务程序集里找不到

优先排查：

- 名称是否只是相对类型名
- 是否与 `Namespace` 拼接后的真实类型一致

### 5. 忘了 `Yield`

表现：

- `RegisterAdditionalProcedures` 触发 `Fsm.AddStates` 守卫

这不是可选优化，而是这条流程能不能成立的关键细节。

## 关键源码入口

关键源码：

- [ProcedureLoadDll.cs](../../../../../Scripts/Runtime/Modules/Procedure/Procedures/ProcedureLoadDll.cs)
- [ConfigManager.cs](../../../../../Scripts/Runtime/Modules/Config/Managers/Implements/ConfigManager.cs)
- [Util.HybridCLR.md](../../../Utils/Util.HybridCLR.md)

关键入口方法：

- `OnEnter`
- `OnUpdate`
- `RunLoadAsync`
- `RegisterAdditionalProcedures`
- `Util.Assembly.RefreshAssemblies`

## 相关文档

- [ProcedureManager.md](../ProcedureManager.md)
- [IProcedureManager.md](../IProcedureManager.md)
- [ConfigComponent.md](../../Config/ConfigComponent.md)
- [ConfigManager.md](../../Config/ConfigManager.md)
- [IAssetManager.md](../../Asset/AssetManager/Interfaces/IAssetManager.md)
- [Util.Assembly.md](../../../Utils/Util.Assembly.md)
- [Util.HybridCLR.md](../../../Utils/Util.HybridCLR.md)
