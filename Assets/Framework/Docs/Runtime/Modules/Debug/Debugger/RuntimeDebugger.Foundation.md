# RuntimeDebugger Foundation

`Core/Foundation` 是调试器随包携带的基础实现，统一编译在 NovaFramework.Runtime。其旧式集合、协程、服务和输入分支保留既有行为，不应因命名接近框架公共工具就当作可互换 API；新增框架能力仍遵循模块自己的契约。

## 服务注册与延迟创建

DebugServiceRegistry.RegisterAssembly 根据类型所属 Assembly 登记候选；首次 UpdateStubs 扫描 ServiceAttribute、ServiceConstructor 和 ServiceSelector，之后缓存不自动刷新。后注册的程序集不会自动进入既有 stub 缓存。构造器标记要求静态、零参、返回类型恰好匹配服务接口；选择器要求静态、零参、返回 Type。重复构造器/选择器以后扫描值覆盖，存在具体 Type 时不再采用 selector。

GetService 可创建 Registry、扫描程序集并实际构造对象；MonoBehaviour 新建 GameObject/AddComponent，ScriptableObject 用 CreateInstance，其余 Activator.CreateInstance。构造可能先通过 Awake 自注册，再由 Registry 补登记；不会等待 IAsyncService.IsLoaded，不提供创建失败回滚。非 Play 返回 null；退出守卫和部分重载恢复逻辑仅在 Editor 编译，不能推导 Player 全有相同退出门。

HasService 不创建目标服务，但仍可能惰性建立 Registry；按可赋值类型查首匹配，首项为空不继续找后项。Unity 对象存为 object 的空值判断也不等同 Unity destroyed 语义。RegisterService 同实例可忽略、其他重复会失败，不相容/空服务可在类型校验失败路径继续抛异常；UnRegister 只按类型移除，不 Dispose/Destroy。

扫描 GetTypes 的异常按程序集记录后继续；类型扫描本身失败可留下部分缓存，后续调用不自动重扫。服务集合没有并发锁。Editor OnDisable 依次 Dispose/DestroyImmediate 后清集合，首个异常可阻断剩余清理；销毁当前服务导致注册列表变化时，基于实时 Count 的枚举还可能跳过下一项。SubsystemRegistration 只重置部分门面/退出标记，不统一清 LoadingCount、stub、程序集和全部实例。

DebugServiceBase 在 Awake 注册、OnDestroy 按类型注销，要求派生调用 base；同名类型注销不是按实例身份释放。DebugDependencyServiceBase 的加载协程先加 LoadingCount，已有服务直接跳过，只有新取得的 IAsyncService 才等待；缺依赖 yield break、异常或协程停止没有 finally 保证减计数。DebugSceneServiceBase 加载 additive 场景、按 SceneName 查对象并挂到持久层级；查找不保证来自目标场景，销毁 root 不等于卸载场景。IsLoading/HasService/IsLoaded 三者不能替代彼此。

## 集合

| 类型/操作 | 当前行为与调用限制 |
|---|---|
| DebugList.Add/AddRange | 允许 null/重复，容量不足按至少 32 扩展；逐项追加，不预先复制输入，失败保留前项。AddRange(self) 会随 Count 增长，不能用于追加自身 |
| DebugList 索引器/Buffer | 暴露物理容量，不检查 index<Count；容量内可读旧槽，数组引用不是快照 |
| Insert/RemoveAt | 超上界 Insert 当追加、RemoveAt 可静默无操作；负索引可能在部分搬移/Count 改变后才失败，与标准 IList 边界不同 |
| Clear/Clean/Trim | Clear() 仅 Count=0；Clear(true)/Clean 清不用槽；Trim 改内部容量。CopyTo 会先 Trim 自身，再复制目标 |
| 枚举/只读包装 | AsReadOnly 是活包装；枚举每步读取当前 Count，无版本锁，途中删除可跳项，添加可延长迭代 |
| Sort/序列化 | 冒泡稳定升序，最坏平方成本；比较异常留部分排序，不稳定比较器可能不终止。序列化前清空不用槽，反序列化后不修非法 Count/Buffer |
| CircularBuffer | 固定容量覆盖两端、活数组段及满状态枚举限制，见 [Console](RuntimeDebugger.Console.md#circularbuffer-的实际边界) |

这些集合不提供线程安全，也不因为 Clear/Dispose 的命名而保证擦除所有旧引用。

DebugList.IsReadOnly 固定返回 false，描述集合可变性，不执行排序；AsReadOnly 创建的包装才拒绝集合写入。不能把包装和原列表的同名属性混为一谈。

## 组件、断言与 Unity 对象工具

DebugMonoBehaviour 懒缓存 Transform/GameObject/Collider/Rigidbody；没有组件时返回 null，下次仍查，不添加组件。DebugSingleton 仅返回已登记实例，缺失抛 InvalidOperationException；重复时保留旧实例，按对象组件数量销毁新对象或组件。DebugAutoSingleton 在 Play 缺失时可创建，Editor 会先搜索，Player 直接创建，不能保证与尚未 Awake 的场景对象不重复。关闭回调的静态清理有 Editor 条件，HasInstance 只说明当前引用。

DebugMonoBehaviourEx.CheckFields 按 Type 缓存字段描述，处理 RequiredField/Import/AutoSearch/AutoCreate。Import 缺服务警告后继续；字段反射、AddComponent 或赋值异常可留部分填充。当前 AutoCreate 成功后仍会走到后续错误路径，没有统一成功跳过。EditorOnly 兼容属性当前不参与筛选。NETFX_CORE 与普通反射的继承字段范围不同；派生不调用 base.Awake 就不会填充。

DebugAssert 普通条件失败先记 Error 再抛 Exception，必需对象为空会记 Error、尝试禁用对应实例并抛 NullReferenceException；日志回调本身异常可阻断后续步骤。对象空值使用 EqualityComparer<object>，不保证识别 Unity 已销毁对象。Editor 断言包装仅在对应调用宏保留。IsFixedUpdate 是普通字段，没有自动同步 FixedUpdate 阶段。

| 工具 | 实际副作用与限制 |
|---|---|
| Hierarchy.Get/索引器 | 按路径缓存现有/新 Transform，必要时递归创建父级；失败可留已创建对象，清缓存不销毁它们 |
| DebugInstantiate | 先克隆对象，再把非 null RuntimeDebugger.TextFont 应用到含 inactive 的全部 UGUI Text；不处理 TMP，字体应用失败不回收克隆 |
| GameObject 扩展 | GetComponentOrAdd 可触发 Awake/OnEnable；Enable 可触生命周期；Remove 用延期 Destroy；SetLayerRecursive 遍历整个子树，异常不回滚 |
| Transform 扩展 | CreateChild 建对象、重置 local 并继承层；Match 只复制世界位置/旋转；SetLocals 复制 local 三项；SetParentMaintainLocals 使用 worldPositionStays=false；孩子枚举是实时视图 |
| IList 扩展 | PopLast 空时失败；Random 空时失败并消耗 Unity 全局随机状态；RandomOrDefault 仅为空回 default，不接受 null 列表 |
| DebugFileUtil | DeleteDirectory 递归删除，IOException 时让线程后只重试一次，没有路径隔离/回滚；GetBytesReadable 为当前 culture 的近似 1024 显示，负值/long 极值不提供规范化保证 |
| AssetUtil（Editor） | CreateAsset 真正创建唯一资产并 SaveAssets，可能提交其他脏资产；选择函数改变 Editor Selection，不是只读工具 |
| Coroutines.WaitForSecondsRealTime | 用 realtimeSinceStartup 循环 yield，不受 timeScale 直接影响；负/NaN 可立即完成，Infinity 可永久等，取消依外部 StopCoroutine |
| DebugStringExtensions.Fmt | 当前 culture 的 string.Format，不转义富文本，格式/ToString 异常继续传播 |
| NetFxExtensions | 类型可赋值关系，不按名称；IsInstanceOfType 对 null 对象调用 GetType 会失败 |

## 数学与反射

DebugReflection.GetAttribute 读取/构造特性，GetPropertyValue/SetPropertyValue 才实际调用访问器；目标、访问器、索引参数、类型与裁剪可用性未由 helper 验证。真实业务异常可能包装为 TargetInvocationException。MethodReference/PropertyReference 的变更通知和执行边界见 [DebugOptions.md](DebugOptions.md)。

DebugMath 的 Angle/NearestAngle/WrapAngle 使用角度；WrapAngle 只修正一圈，720 得到 360，-180 得到 180，不是任意值的完整归一。FacingNormalized 用单位方向 dot 映射到 0～1，零向量不代表有效朝向；Average 直接先相加，可能在数学平均有限时已溢出。LerpUnclamped 不夹 t；Sqr/Sqrt/Approx 直接使用浮点与 Mathf，不额外拒绝 NaN、Infinity 或负开方。

Wrap 通过反复加减 max 处理，int 仅拒负 max，float 没有同等校验；max=0 或不合适的负/非有限值可能不终止，大圈数也是线性成本。不能给空枚举长度作为 Wrap 模数。

TweenFunctions 包括 Linear 及 Quad/Cubic/Quart/Quint/Sine/Expo/Circ/Elastic/Back/Bounce 的 In、Out、InOut、OutIn 变体，按源码公式计算 t/b/c/d；c 是增量、d 是持续时间。函数不钳 t、不验证 d 或有限性，d=0 可产生 NaN/Infinity，Circ 越界可出根号域，Elastic/Back 可过冲。DebugMath.Ease 当前把 `(t, from, to, 1)` 直接交这些公式，to 实际进入增量位置，不是统一的目标终点语义；未知函数枚举抛 ArgumentOutOfRangeException。

SpringLerp 按 RoundToInt(deltaTime×1000) 次毫秒步进累计，再做标量/Vector3 Lerp 或 Quaternion Slerp；小间隔会量化停滞，负间隔无迭代，大间隔同步循环成本增加。SmoothClamp 在 value<min 时原样返回，其他路径计算缓动并记 Debug.Log，不是纯钳制函数；scrollMax=min 等边界无额外校验。

## 内置 MiniJSON

`Core/Foundation/External/MiniJSON.cs` 的 Json 供内部简易报告/manifest 使用，不等同框架 Util.Json。Deserialize 只读取第一个值，不检查尾部；重复对象键覆盖，数组/对象逗号校验宽松，未闭合字符串可能返回部分文本。空白/EOF、坏 Unicode 转义可抛异常，错误 null 与合法 JSON null 不可区分；没有长度/深度/循环保护。

数字解析按是否含点选择 long/double，使用当前 culture；无点科学记数或溢出可解析失败为 0。序列化 float/double 用 InvariantCulture，decimal 先转 double 可能失精度，NaN/Infinity 可写出不合标准 JSON 的数字文本；未知对象退回 ToString 字符串。列表/字典循环可递归不终止，键 ToString 碰撞、枚举变化和用户 ToString 异常不统一隔离，也不自动脱敏。

源码：[DebugServiceRegistry.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/Scripts/Service/DebugServiceRegistry.cs)、[DebugList.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/Scripts/Collections/DebugList.cs)、[DebugMath.Tweening.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/Scripts/Helpers/DebugMath.Tweening.cs)、[MiniJSON.cs](../../../../../Scripts/Runtime/Modules/Debug/Core/Foundation/External/MiniJSON.cs)。
