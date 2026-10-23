# 批量本地化绑定

仅在既有页面或指定目录的本地化绑定分支读取本页。页面绑定由本Operation完成，源文本/字体/语言与正式导出复用`nova-project-update-localization`；普通字段、嵌套Prefab/场景保存与UnityEvent遵循`nova-project-edit-settings`的共享Unity MCP契约。不要每节点、每语言、每字体新增Skill。

## 从自然语言取得明确批量

“把设置页和商店页全部接本地化”先定位这两个真实View/Prefab、其inactive节点、TextMeshProUGUI及现有组件；“整个目录”限定到确认项目自有目录，不扩到Package、Sample、Launcher或全Assets。按GUID/path、完整对象路径、组件身份、variant/嵌套来源和Key/FontMark组成稳定节点清单。展示原值/目标值、已有绑定保留、补组件和无法映射项；不凭当前显示文字自动决定同义Key，不覆盖业务每帧动态更新的计数/玩家输入，也不把启动期独立语言文本交给正式TextLocalizing接管。

权威语言源/正式表存在时从中匹配已确认Key，字体Mark按精确Ordinal及资源来源确认；同文案多Key、翻译/业务语义不唯一时只需用户选择对应项。目标清单、映射、本次完整请求语言集合及布局验收已明确，既有授权覆盖后串行执行；明确请求三种就覆盖三种，明确单语言不扩大，只要求切换但未指定集合时冻结基准与目标至少两种。完成集合有冲突时保留用户原值、跳过依赖缺项并报告partial，不不断整批重试。

## 真实字段、默认与执行层

当前`NovaFramework.Runtime.TextLocalizing`只支持`TMPro.TextMeshProUGUI`，`[RequireComponent]`和`[DisallowMultipleComponent]`不允许重复挂同类型。持久字段是`m_LocalizingKeyName`和`m_LocalizingFontMark`，只读属性是`KeyName/FontMark`；字段声明没有显式默认字符串。添加TMP时`TextLocalizingAutoMount`的Editor钩子为新缺失组件设置FontMark="Main"，不代表既存字段均为Main，也不保证所有MCP添加入口触发该钩子。旧UnityEngine.UI.Text/TMP_InputField不是本次自动组件覆盖对象，必须明确迁移或独立输入适配需求。

先只读枚举目标组件/字段，缺失时复用当前共享MCP增组件，再显式设置两字段。普通非嵌套Prefab的已有自有组件字段可走`settings.inspect-target/patch-target`；需要新增组件或variant/嵌套时优先当前Provider`manage_prefabs(action="modify_contents")`。匹配当前Tool schema、精确`prefab_path/target`和完整组件名；MCP公开参数是`components_to_add`、`component_properties`，Provider会转成Unity端的`componentsToAdd/componentProperties`，不要把内部camelCase放进公开工具调用。仅在确实没有TextLocalizing且已存在TMP时添加：

```json
{
  "action": "modify_contents",
  "prefab_path": "Assets/Game/Prefabs/SettingsView.prefab",
  "target": "SettingsView/Body/Title",
  "components_to_add": ["NovaFramework.Runtime.TextLocalizing"],
  "component_properties": {
    "NovaFramework.Runtime.TextLocalizing": {
      "m_LocalizingKeyName": "Settings_Title",
      "m_LocalizingFontMark": "Main"
    }
  }
}
```

所有样例身份和值必须换成项目实际映射；已有组件时省略components_to_add，相同值不写。Provider仅按类型取首个，完整路径不唯一时可能名字回退，先排除重复目标/组件；不使用临时LoadPrefabContents返回ID跨调用。每个节点/Prefab串行读回及核对同一文件保存，记录变体/嵌套override与源文件摘要，不Unpack或自动Apply到父/源。需要Stage、同Prefab内部引用或场景字段时按edit-settings的精确保存门；当前Stage save全局SaveAssets，无全局脏资产证明不能替用户保存未知工作。工具报错可能已部分修改，先读回，不宣称全部未改。

`TextLocalizing.SetKeyName/SetFontMark`用于授权项目`workspace-edit`实现的运行时动态绑定，会刷新当前实例；不能靠Play调用这些方法当持久Prefab写入。保留现有UIView的刷新/解绑，若业务用代码写TMP文本而组件也写同节点，明确唯一拥有者。当前Awake只缓存本地TMP/预处理器；首次Start等待一次下一帧末尾，取得当前Manager、Check真实池后唯一订阅并刷新，避免监听被EventComponent.Start初始化清空。等待中禁用不订阅、销毁取消；之后OnEnable立即恢复并在重启后换当前Manager，OnDisable安全注销实际监听/释放字体和材质，OnDestroy恢复原TMP预处理器；异步字体带语言/版本校验。独立启动文本已接管实例永不恢复正式监听，持续启用中主动重建公共事件池不由该组件每帧侦测；别复制另一套全局监听与字体缓存。

全工程`FixMissingInPrefabs`仅补缺失组件，不能完成Key映射，且当前最后全局SaveAssets/Refresh；不默认调用它替代明确节点批量。绑定没有注册变更时不调用ui.export；语言源确有变化时复用update-localization的正式export，不手改JSON/生成访问代码。

## 字体、RTL与完整验收

字体刷新需AutoFontAdapt=true、非空FontMark、当前语言font row精确Mark以及可加载AssetLocation/必要MaterialName。FontSizeScaleRatio基于原字号缩放；空Key不会写译文，GetText前必须已有语言文本。组件负责TMP方向与渲染预处理，不镜像RectTransform、按钮顺序、箭头或布局；RTL文案保持正常Unicode顺序，Arabic字体须覆盖所需连写字形。只挂组件不代表字体或布局已验收。

交付逐节点映射/持久绑定、组件补齐、必要语言源→正式导出→Config→文本/字体资源→运行消费链。编译后从现有打开入口全新Play，不手动开关静态绑定；等首次Start等待完成并确认既有Load与InitCurrentLanguage已完成，逐项覆盖本次冻结请求语言集合，在同页面验证Key译文、FontMark字体/材质、目标布局及开关页面后无重复订阅/旧字体覆盖。明确三种以上不能用旧双语言通过代替，明确单语言按其目标验证；只要求切换但未指定集合时基准与目标至少两种。`SetLanguageAsync`当前没有ct且成功返回不含bool，需查询实际Language/HasText/GetText与显示；它持久保存语言偏好，验证前记录实际偏好与已有保存状态，结束用同一公开切换路径恢复，不能仅StopPlay声称无测试配置残留。

缺Unity、字体资源、源映射、可靠保存或完整请求语言集合Play时明确partial/blocked；工具Schema/源码查阅或未覆盖集合的截图不能标validated。清理本轮测试对象、runner/meta、日志、动态字体/场景噪声和测试态偏好，仅恢复能确认由本轮产生的变化，保留正式绑定与用户原工作。按需读`Docs/Runtime/Modules/Localization/{TextLocalizing,LocalizationComponent}.md`及`Docs/Editor/Inspectors/CustomInspectors/TextLocalizing{AutoMount,Validator}.md`；本页仅当前源码/Provider审查，不是所有Prefab现场已验收。
