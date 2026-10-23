# 共享 Unity MCP 字段、场景与事件编辑

仅在普通资产 Action 不覆盖的实际目标上读取本页。这里复用现有 Provider 的工具，不增加组件专属 Skill，也不使用任意 `execute_code`、反射执行片段或手写 Unity YAML 作为稳定入口。

## 发现与共同保存门

先读取当前 Provider 的 `mcpforunity://custom-tools`、`mcpforunity://project/info`、`mcpforunity://editor/state`、`mcpforunity://editor/prefab-stage`；多实例时从 `mcpforunity://instances` 确认工程并绑定本次工具路由，不能改变其它 Agent 的全局路由。资源 URI 以 Provider 实际列表/模板为准。当前已核对模板包括 `mcpforunity://scene/gameobject/{instance_id}/components`、`mcpforunity://prefab/{encoded_path}`、`mcpforunity://prefab/{encoded_path}/hierarchy`；组件资源按页读取，先元数据后必要值。

从项目事实确认资产自有目录、scenePath/prefabPath、完整对象路径、组件 FullName、实际组件 instanceID 和同类组件的零基索引。instanceID 只在当前 Editor 上下文有效，scene reload、Prefab 隔离加载或关闭 Stage 后重新发现。`manage_prefabs(get_hierarchy)` 在临时加载后卸载内容，其返回 instanceId 不能用于下一次修改或内部对象引用。

先记录目标文件/meta 摘要、已有值、所有已加载场景的路径/活动标记/`isDirty`、当前 Stage 路径/脏态与原工作区差异。写入前展示用途、before/after、所属文件、Prefab 来源或事件列表变化，并与既有授权对应。已存在的未保存草稿先保留并解决归属，不自动保存/丢弃；无草稿证明的自定义 Inspector 或会触发领域回调的业务类型，使用已有领域契约。共享工具不自动脱敏，不对敏感聚合组件做全量属性读取，也不把凭据值放入调用日志。

工具没有 Nova 的 Plan/Receipt、单次 token 或完整锁；同 Editor 单写者串行，逐次提交必要字段并立即只读核对。相同目标值跳过调用。工具报错可能已经修改部分字段或数组大小，先读回，再报告 partial；断线后先恢复只读定位，不盲目重试。

## 已保存场景与内置组件

1. `manage_scene(action="get_loaded_scenes")` / `get_active` 冻结场景。目标未加载时仅在本次范围内用明确 `scene_path` 的 additive load，保留其它场景；目标已加载而非活动时 `set_active_scene(scene_path=...)`，随后再次核对实际活动路径。任何 Stage 开着时先解决其草稿并退出，避免组件工具把其它 Stage 标脏。
2. 按活动场景 `get_hierarchy` 的父子关系和分页确认唯一对象；`find_gameobjects(by_path)` 只辅助定位，仍需排除其它已加载场景的同路径候选。通过组件资源确认目标组件 instanceID/类型及同类次序；写入绑定 GameObject 当前 ID、组件精确类型和 `component_index`，不用默认首个组件猜测。
3. 字段/属性名称、类型、单位和 setter 影响从当前对象及匹配版本源码/Docs确认，必要时只读 `unity_reflect(get_type/get_member)`。`manage_components(set_property)` 接收真实 JSON 值，不是 Nova 的 `valueJson` 包装；Provider 可做宽松转换，Agent 仍传精确布尔、数值、枚举名称和明确引用。

```json
{
  "action": "set_property",
  "target": 123456,
  "search_method": "by_id",
  "component_type": "UnityEngine.Camera",
  "component_index": 0,
  "property": "fieldOfView",
  "value": 45
}
```

示例ID、类型、索引与值必须替换为真实输入。工具支持内置组件和项目组件，并标所属场景脏态；不会保存。对象引用优先用已核实的组件 instanceID 或 `{guid:...}` / `{path:...}`；同类重复引用不能靠名字/组件类型筛选猜测。

4. 写后读回精确组件值，并确认没有请求外资产变化。`manage_scene(save)` **保存活动场景**；传另一个 `path/name` 会 Save As，不能当作指定场景保存器。先再次 `get_active` 确认等于被冻结 scenePath，再对已有路径执行 `save`，核对返回 `data.path` 和 `isDirty=false`。不要保存所有场景，也不要运行 `validate(auto_repair=true)`。
5. 只读核对保存文件中的真实字段、对象引用及工作区写集；需要跨加载证明时在另行授权的验证阶段重新加载后读回。修改场景中的 connected Prefab 实例默认是**此场景实例 override**，不自动 Apply/Revert 到源Prefab。Provider没有独立的Apply/Revert指定override工具；通用字段经反射赋值也未显式记录Prefab override，必须核对实际序列化修改记录，不以工具成功推断持久化。

保存成功仅证明场景字段落盘，未证明摄像机效果、UI交互、Physics或游戏启动。恢复本轮改变的活动场景及加载布局；只关闭本轮新增且已保存/无脏态的场景，不触碰原工作。

## Prefab 变体、嵌套与内置组件

先 `manage_prefabs(get_info)` 读取 `guid/prefabType/isVariant/parentPrefab`，`get_hierarchy` 读取完整路径、组件列表和 `prefab.isNestedRoot/assetPath/parentPath/nestingDepth`。明确本次修改当前变体、外层Prefab中的嵌套override，还是嵌套源资产；只有用户任务覆盖该源资产影响时才改源。不给新手默认选择扩大所有实例的源修改。

目标GameObject路径和组件类型各自唯一、引用不依赖跨上下文内部ID时，优先隔离编辑：

```json
{
  "action": "modify_contents",
  "prefab_path": "Assets/Game/Prefabs/SettingsPanel.prefab",
  "target": "SettingsPanel/Body/ConfirmButton",
  "component_properties": {
    "UnityEngine.UI.Button": { "interactable": false }
  }
}
```

`target` 使用已查证完整路径；Provider会回退按名查首个对象，因此路径缺失/重名不可继续。`component_properties` 每种类型只用 `GetComponent` 首个同类，发现多个同类不能采用本入口。该调用 `LoadPrefabContents` 后保存同一 `prefab_path` 并卸载；不自动 Unpack，不升级到父/嵌套源文件。变体及嵌套继承须通过保存文件实际override、来源关系和父/源资产摘要核对；只有get_info/get_hierarchy不能读回任意字段，缺完整字段证据时保持partial并指出需补的具体查询。

需要同类组件索引或同Prefab内部对象引用时，可采用 `open_prefab_stage` → 重新发现实际Stage对象/组件ID → `manage_components(set_property,component_index=...)` → `save_prefab_stage`。Stage须是本轮打开的正确资产、没有既有草稿；修改后只保存该Stage，并在读回验证后关闭本轮Stage。**当前 `save_prefab_stage` 会调用全局 `AssetDatabase.SaveAssets`**，没有现成全局脏资产枚举能力；只有已获准的受控宿主能证明无其它脏资产，或已有授权明确覆盖识别出的额外保存范围时才执行，不能把磁盘 `git status` 当作内存无脏态证明。

无法获得该证明时，明确报告“Stage保存会全局保存，缺只保存目标Stage或全局脏态查询的受控契约”；不因而声称所有Prefab均不可编辑，也不改用generic asset modify绕过。同样，Provider暂无精确的Apply/Revert property override入口，不能用Unpack、批量Apply或编辑源资产代替。

## UnityEvent 持久绑定

先分清UGUI持久UnityEvent与业务在代码中 `AddListener` 的运行时订阅；`manage_ui` 是UI Toolkit文件/视觉树工具，不用于Button的UGUI事件绑定。业务源代码负责的注册与注销仍走对应业务Skill。

由Agent查清真实事件字段（当前UGUI `Button` 的序列化根为 `m_OnClick`）、现有完整持久监听列表、目标组件instanceID/FullName/真实程序集、方法精确名称及有效签名。只读 `unity_reflect(get_type/get_member)` 可确认程序集、public实例方法、返回类型、重载与参数；不能靠名称推断。确定动态事件参数或静态常量模式、调用状态和参数来源；模式/状态使用匹配当前Unity的真实名称，不照抄数字。绑定不执行回调方法。

当前Provider `ComponentOps.SetProperty` 对UnityEvent强制走SerializedProperty，支持真实 `m_PersistentCalls/m_Calls` 树。下例仅说明**完整已确认监听列表的替换**格式；全部ID、类型、程序集、方法和模式必须由当前对象核实，保留用户未要求删除的监听项：

```json
{
  "action": "set_property",
  "target": 123456,
  "search_method": "by_id",
  "component_type": "UnityEngine.UI.Button",
  "component_index": 0,
  "property": "m_OnClick",
  "value": {
    "m_PersistentCalls": {
      "m_Calls": [
        {
          "m_Target": { "instanceID": 234567 },
          "m_TargetAssemblyTypeName": "Game.SettingsController, Game.Runtime",
          "m_MethodName": "OpenSettings",
          "m_Mode": "Void",
          "m_Arguments": {
            "m_ObjectArgument": null,
            "m_ObjectArgumentAssemblyTypeName": "UnityEngine.Object, UnityEngine.CoreModule",
            "m_IntArgument": 0,
            "m_FloatArgument": 0,
            "m_StringArgument": "",
            "m_BoolArgument": false
          },
          "m_CallState": "RuntimeOnly"
        }
      ]
    }
  }
}
```

监听列表是数组，替换会改变长度并可能先应用数组大小；已有列表未能完整读取时不能按“新增一条”假设只保留样例项。组件资源采用反射序列化，可能省略UnityEvent私有持久列表；缺失字段不等于空列表。文本序列化工程可**只读**读取既有场景/Prefab文件中的目标对象与列表以核实，用工具修改后再核对落盘 `target/method/mode/arguments/callState`；二进制文件或目标localId不能可靠定位时报告具体缺口，不手写YAML。

场景事件按前节精确保存。Prefab内部回调目标必须来自同次有效Stage，不能使用get_hierarchy临时内容ID或指向另一次加载的Prefab资产组件；因此其写入可能需要上方Stage保存门。只涉及已保存且正确类型的外部资产引用、无内部上下文引用时可使用 `modify_contents`；仍先证明完整原列表与目标身份。

保存后只读检查每个持久target、方法、签名、参数、模式/状态及原监听保留。源码/工具Schema支持UnityEvent不代表当前绑定已执行验收；用户要求按钮可用时另需真实Play交互证据，字段保存不能替代。

## UI Toolkit 与其它资产

`manage_ui(read/update)` 修改已确认自有 `.uxml/.uss` 文件，update会覆盖完整内容并导入；先保留原内容、只做目标差异，再read文件/必要visual tree验证。`modify_visual_element` 修改当前视觉树，不能直接当作UXML持久文件修改。`render_ui` 可能创建RenderTexture/PanelSettings，不能用作只读保存验证。

已存在 `PanelSettings` 的 `update_panel_settings(path,settings)` 支持实际列出的scaleMode、screenMatchMode、match、referenceDpi、fallbackDpi、sortingOrder、targetDisplay、clearColor、clearDepthStencil、referenceResolution、colorClearValue、dynamicAtlasSettings、themeStyleSheet；先核实类型和当前值，match会Clamp01，未知字段可能被略过，逐字段读回。该工具同样全局 `SaveAssets`，遵守上方全局脏态门；不要让`attach_ui_document`自动挑首个或创建默认PanelSettings。

`manage_asset(search/get_info/get_components)` 用于发现真实资产及组件。generic `modify` 反射写入、仅部分类型明确实现且全局SaveAssets，不作为本Skill稳定字段/保存路线。`manage_scriptable_object(modify,dry_run=true)` 可核实已明确普通SO字段，但实际modify可能部分应用并全局保存；已有普通SO优先Nova的单资产Action，不能用它绕过Config/Pipify、vendor或生命周期限制。Importer、Material等超出本次普通Inspector目标时转既有领域能力，不把generic工具发现当作全类型可安全编辑。

## 审查与证据边界

本页依据当前项目解析的 unity-mcp v10.3.0 Editor 服务源码及当前 Provider Tool/Resource Schema；消费项目升级后只核对当前分支所需方法。持久回调修改应在保存、重新加载后读回监听数量、目标、方法、状态及参数，并按本次需求验证实际分发。直接调用按钮事件只证明回调分发；鼠标、触摸和 EventSystem 输入须通过对应的真实交互判断。验收结果当次报告，不链接历史测试档案。

同轮真实 `modify_contents` 保存了变体与外层嵌套 Prefab 的 Button 属性：基类值不变，变体类型及对应源保留，嵌套源关系及外层 override 保留。路径必须来自**当前 `get_hierarchy` 返回的实际根名和完整层级**；构造前对象名或源 Prefab 根名不能代替加载后派生/外层根名，查不到目标时先重新查询，不退化为按名猜首个对象。首次使用构造前外层名未找到目标，按实际根名重新定位后才完成保存。

该轮没有执行 `save_prefab_stage`；上述单场景和单 Prefab 验收不证明全局脏资产保存安全，也不解除同 Prefab 内部事件引用与重复组件所需的 Stage 保存门。初始化资源、UI Toolkit、其它资产及未列入正式记录的分支仍需各自证据。最终记录实际 Adapter、精确文件、只读保存证据、剩余工具缺口与清场，不能因存在入口把覆盖记录标为验收完成。
