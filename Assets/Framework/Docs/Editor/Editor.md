# Framework Editor

**程序集**：`NovaFramework.Editor.asmdef`  
**命名空间**：`NovaFramework.Editor`

## 子模块

| 目录 | 说明 |
|---|---|
| [`EditorUtil/`](EditorUtil/EditorUtil.md) | 编辑器工具与 Draw 封装 |
| [`Definitions/`](Definitions/IEditorRuntimeDrawer.md) | Editor 侧公共接口与数据结构 |
| [`DataPipeline/`](DataPipeline/DataPipeline.md) | 数据导出与预处理流水线 |
| [`Inspectors/`](Inspectors/Inspectors.md) | 各 Runtime 组件的自定义 Inspector |
| [`Menus/`](Menus/Menus.md) | 菜单项 |
| [`Windows/`](Windows/ConfigWindow.md) | `ConfigWindow`、`PipifyWindow`、`CheckUpdateWindow`、`PlugPalsWindow`、`KitsViewWindow` |

## 当前 Inspector 模型

### BaseComponentInspector 的职责

`BaseComponentInspector` 当前只负责这些基础能力：

- 统一设置与恢复 `EditorGUIUtility.labelWidth`
- 处理编译开始/结束状态回调
- 在 `OnInspectorGUI()` 开头执行 `serializedObject.Update()`
- 提供 `FinalRefreshInspectorGUI()` 统一 `ApplyModifiedProperties()` 与 `Repaint()`
- 提供模板路径相关辅助方法

它**不**持有统一的 RuntimeDrawer 列表，也**不**在基类里统一调度 `IEditorRuntimeDrawer`。

### IEditorRuntimeDrawer 的实际位置

`IEditorRuntimeDrawer` 是一个**可选扩展接口**：

- 接口定义位于 `Editor/Definitions/IEditorRuntimeDrawer.cs`
- 具体 Inspector 可以自行维护 `List<IEditorRuntimeDrawer>`
- 当前 `EventComponentInspector` 就采用了这种模式
- 是否使用、何时调用，由具体 Inspector 决定，不是 `BaseComponentInspector` 的基类契约

## 典型 Inspector 流程

当前常见写法是：

1. `OnEnable()` 里绑定 `SerializedProperty`
2. `base.OnInspectorGUI()` 做基础准备
3. 子类调用自己的 `DrawConfigs()` / `DrawRuntimeInfos()` / `DrawRuntimeLists()`
4. 最后调用 `FinalRefreshInspectorGUI()`

## Editor 侧几个当前事实

- `PlugPalsWindow` 属于 `Windows/`，不是 `Tools/`
- `KitsViewWindow` 属于 `Windows/`
- `Windows/EnvironmentWindow/` 目录为空壳：`EnvironmentWindow` 类并不存在，「环境检测」UI 内嵌在 `ConfigWindow` 左树「环境检测」组（Luban / Python3 / HybridCLR 三条目），检测引擎在 `EditorUtil.Environment`；详见 [EnvironmentWindow.md](Windows/EnvironmentWindow.md)
- 业务侧 Editor UI 以 `EditorUtil.Draw` 为主，不鼓励在 Inspector/Window 里直接扩散原生 IMGUI 写法

## 阅读顺序建议

### 托管引用列表 Drawer

`SerializeReferenceListDrawer<TInterface,TWrapper>` 按全局字段、标题、条目、添加按钮的固定顺序绘制，派生的高度与绘制 hook 必须保持对应；全局绘制返回高度增量。包装实例从第一个 `targetObject` 的字段/数组路径反射取得，不支持任意属性、基类 private 字段遍历或完整多对象编辑，索引格式/越界错误也不是一律返回 null。GUI 的 Begin/EndProperty 没有 finally 保证，异常交由 Unity/调用方处理。

添加菜单按 TypeCache、抽象/接口过滤及派生条件列候选；这不等于已经验证 Serializable、封闭泛型或无参构造器。菜单回调先插入再构造实例，失败可能留下未完成草稿；它不冻结原目标/路径，也不在回调时重新校验重复。空列表属性仍可能打开菜单，最终写入由实际序列化对象和 Inspector 生命周期承担。删除只做本次元素删除并 Apply，不承诺其他 Unity 引用类型的二次删除效果。

### 本地设备安装脚本

`Editor/Tools/Deploys/install_apk.py` 先列出设备再执行 `adb install`，没有解析在线/唯一设备、指定 serial、`-r`、超时或安装后包名/版本/启动验收。`install_aab.py` 用 bundletool 生成 APKS 后安装；主入口在转换前删除已有输出，转换失败不恢复旧 APKS。两者只把 `CalledProcessError` 转成失败结果，程序缺失等 `OSError` 仍传播。

AAB 脚本接收八个位置参数，APK 脚本接收两个；多余项不消费，路径存在性和文件类型校验有限。AAB 的签名密码进入 Java 命令参数，进程异常字符串可能包含命令；主入口不主动打印密码不等于全调用链无泄漏。应由受控构建/安装流程约束产物、设备与凭据，脚本退出成功本身不是设备运行验收。

API 签名、属性绑定、声明默认值和当前调用表达式可从源码及生成索引重建；这些事实不能替代字段的实际序列化值、执行时配置、资源归属或失败恢复约定。调用索引中的同名 token、重载候选、interface/反射/委托入口只作定位线索，未经过真实绑定核对就不等同于完整调用图或“没有调用者”的证明。

文档中的参数来源和生效阶段按实际执行链读取：窗口输入或持久化设置可能先形成克隆预览，执行写入前仍须核对原来源，最终产物与当前运行配置另行验收。历史静态审查、编译、Play 或设备记录只说明当时的来源版本和环境，不因文档归并成为当前成功保证。

- 要看面板或窗口行为：先读对应 `Inspectors/` 或 `Windows/` 文档
- 要看共通 UI 封装：读 `EditorUtil.Draw`
- 要看 Runtime 与 Editor 的连接点：读组件对应的 Inspector 文档
