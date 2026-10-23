# DebugOptions

**命名空间**：`NovaFramework.Runtime`

`DebugOptions` 是运行时调试选项容器，替代拆分前的旧选项容器。业务侧如需注册自定义选项，应通过 `RuntimeDebugger.Instance.AddOptionContainer(...)` 注册独立容器，而不是修改框架内置文件。

## 迁移口径

- 旧选项容器已删除，不提供 `[Obsolete]` shim。
- 选项变更事件委托已改为 `DebugOptionsPropertyChanged`。
- Options 属性和 Action 的反射逻辑保持原实现语义。

## 注册、增删与集合

`DebugOptions.Current` 只返回当前字段，启动回调前可为 null，不惰性创建。未定义 NOVA_DISABLE_RUNTIME_DEBUGGER 时，AfterSceneLoad 的 OnStartup 会新建容器并送入 InternalOptionsRegistry；已有服务 handler 则立即转发，否则先缓存，并不强制创建 IDebugService。注册器 SetHandler 会依次转交预注册对象后清缓存，不是可重复初始化或可回滚事务。

业务可用 `RuntimeDebugger.Instance.AddOptionContainer(obj)` 注册普通对象，或实现 `IOptionContainer`/使用 `DynamicOptionContainer`。移除同一来源用 RemoveOptionContainer。AddOption 创建的单独条目位于专属 loose 容器，RemoveOption 只从这个容器删除，不是按名称删除全部反射项。

动态容器 GetOptions/Options 是活集合包装，不是快照；Add 先改列表再同步发 OptionAdded，Remove 只移除首匹配后通知。null/重复条目没有在源容器统一拒绝，监听者异常时源列表已改变。服务在初次 AddContainer 复制当时条目，再对 IsDynamic=true 订阅增删；同容器重复注册会拒绝，动态重复通知会警告，但不同来源可共享同一 OptionDefinition。

普通对象的 ReflectionOptionContainer 首次访问时扫描并缓存；其键的 Equals/GetHashCode 委托目标对象，并非始终按引用身份。目标自定义可变 hash/相等规则会影响注册与移除，null 或抛异常也会传播。移除顺序为删服务集合、解除动态订阅、发 OptionsUpdated，异常可留下部分状态。Options 返回只读包装，元素仍共享、集合仍随服务变化，没有并发枚举保护。

## 反射和元数据

RuntimeDebuggerUtil.ScanForOptions 扫描公开实例成员，排除 MonoBehaviour 所在 Unity 程序集的内建成员和 Browsable(false) 成员。属性要求公开 getter、允许只读，不自动排除索引器；方法要求非静态、void、零参数。扫描只创建包装，不执行属性或方法；真正刷新/点击才执行业务代码。NETFX_CORE 分支使用 DeclaredMembers，继承成员范围与普通反射分支不同。

默认分类为 `Default`、排序为 0、名称为成员名，Category/Sort/DisplayName 特性可覆盖。NumberRange/Increment 只保存数值，不检查区间顺序、正步进或 NaN。DebugOptions 嵌套兼容特性在启用分支继承 Nova 同名特性，禁用分支仅保留 Attribute 形状；编译变体不能当作同一运行时的多个 overload。

OptionDefinition 的 Name/Category/SortPriority/Method/Property 构造时原样保存，不求值、不验证包装；IsMethod/IsProperty 只看包装非 null。无 getter、无有效委托或两个包装都空的定义仍可能在后续控件创建/调用时失败。

## PropertyReference 与方法调用

PropertyReference 可包装 Type+getter/setter 委托，也可包装目标对象与反射属性。CanRead/CanWrite 只判断委托存在，不证明参数类型、索引器、访问权限、裁剪保留或业务条件有效。FromLambda 即使传 null getter，也可能因外层闭包非 null 而表现为 CanRead=true，直到 GetValue 才失败。

- GetValue 立即执行真实 getter；没有 getter 时返回 null，异常继续传播。
- SetValue 立即执行 setter；没有 setter 时抛 InvalidOperationException，不自动 NotifyValueChanged，也不回滚业务副作用。
- 直接传入的 attributes 数组优先于反射属性特性，保存原引用；GetAttribute 只取首匹配。
- 首个 ValueChanged 监听者会订阅目标 INotifyPropertyChanged，最后监听者移除时退订。目标任何属性变化都通知该包装的全部监听者，不按 propertyName 过滤。
- 通知同步逐项执行，不派发主线程、不隔离异常；允许重复/null 监听，回调中增删列表可能使枚举失败。

MethodReference 构造只保存委托/反射包装，Invoke 才执行；目标方法的参数、反射包装异常和业务异常均可传播。Action 隐式转换只是延后执行的闭包，null Action 不会在转换阶段自动拒绝。

DebugOptions 自身先广播 DebugOptionsPropertyChanged，再广播 INotifyPropertyChanged；第一轮监听者失败会阻止后者，不验证属性名。需要 UI 刷新的自定义属性应显式发送正确变更事件，并在 UI 线程使用这些容器。

## 控件选择与读写

OptionControlFactory 从 Resources 加载数据控件，按 `(PropertyType, !CanWrite)` 缓存首个可绑定 Prefab，含缺失结果的负缓存；Action 使用首个动作控件。缓存不按特性区分，也不在资源变化后自动清理；缺资源或 Bind 失败可能在克隆后只销毁组件、留下对象，不能把 CanCreateControl=true 视为绑定成功。

| 控件 | 可绑定类型与行为 |
|---|---|
| BoolControl | bool；Toggle 写真实 setter，只读状态禁用交互 |
| EnumControl | 枚举名/值数组，左右切换通过 Wrap；空枚举没有有效循环长度，flags/未定义值未统一规范化 |
| NumberControl | int/short/byte/sbyte/uint/ushort/float/double；不含 long/ulong/decimal。类型范围与 NumberRange 交集供拖拽/按钮，Increment 设步进 |
| StringControl | 可写 string；输入变化直接调用 setter |
| ReadOnlyControl | 只读 string；使用 Convert.ToString 显示 |
| ActionControl | 点击调用零参方法；捕获并记录调用异常，不回滚业务动作 |

DataBoundControl.Bind 先保存并订阅包装，再 OnBind/Refresh；重复绑定不会解除旧包装，失败不回滚。OnDisable 解除当前订阅，OnEnable 在已 Start 后重订并 Refresh。Refresh 的 getter 位于 UI 更新 try 外，getter 异常继续传播；只隔离 OnValueUpdated 的显示转换异常。新旧值按 object 引用比较，装箱数值可能反复刷新/写入，UI 赋值事件也可能回写 setter。

NumberControl 显示采用 InvariantCulture 的 decimal 转换；NaN/Infinity 或超 decimal 范围会失败。输入提交使用 Convert.ChangeType，转换或 setter 异常会还原输入文字，但不单独用 Min/Max 再钳制；不能依赖文本输入强制执行 NumberRange。Spinner/按钮使用当前 culture，拖动整数最终转 int，uint 大值等边界与显示转换并不完全对称。

OptionsTab 按分类和 SortPriority 建 UI，排序会修改其持有的分类列表；同 Option 对象重复可使字典登记失败。刷新和全选会读取真实 getter，选择变化立即逐项 Pin/Unpin，不是批量事务；异常可能留下部分固定结果或选择抑制标记。OptionsUpdated 清旧 UI 并排下帧重建，控件创建部分失败时选项/控件数组未保证等长。

PinOption(name) 匹配所有同名项，PinAllOptions(category) 按精确分类匹配；不存在静默返回。清固定项不移除选项来源。完整 UI 所有权见 [RuntimeDebugger.Services.md](RuntimeDebugger.Services.md#固定-ui-与-dockconsole)。

## 构建边界

反射依赖成员保留。`Core/Options/link.xml` 当前仍写 `Assembly-CSharp`/无命名空间 `DebugOptions`，而源码类型为 `NovaFramework.Runtime.DebugOptions`，编译归属为 `NovaFramework.Runtime`；不能用这份旧声明证明当前选项类型已被裁剪保留。源码启用分支有 Preserve，但业务容器、属性与方法还需按真实构建保留策略处理，不能把 Editor 扫描成功当作 Player/AOT 结果。

## 同步规则

`DebugOptions` 属于 RuntimeDebugger 的公开扩展面。以下改动必须同轮同步本页和 [RuntimeDebugger.md](RuntimeDebugger.md)：

- 属性 / Action 反射规则变化。
- 注册入口或事件委托命名变化。
- 选项容器目录、命名空间或程序集归属变化。
- 删除旧兼容入口或新增公开 shim。
