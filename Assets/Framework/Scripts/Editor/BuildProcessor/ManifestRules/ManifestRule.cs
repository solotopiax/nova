/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ManifestRule.cs
 * author:    yingzheng
 * created:   2026/4/23
 * descrip:   AndroidManifest 规则类型定义，SDK 处理器通过规则声明对 Manifest 的修改意图
 ***************************************************************/

namespace NovaFramework.Editor
{
    /// <summary>
    /// 规则应用模式，控制 Manifest 节点写入行为。
    /// </summary>
    public enum ManifestRuleMode
    {
        /// <summary>
        /// 不存在时新增，已存在时跳过。
        /// </summary>
        Add,

        /// <summary>
        /// 删除已有同名节点，重新创建。
        /// </summary>
        Replace,

        /// <summary>
        /// 已存在时合并属性，不存在时新增。
        /// </summary>
        Merge,
    }

    /// <summary>
    /// Manifest 规则基类，所有具体规则类型均继承此类。
    /// </summary>
    public abstract class ManifestBaseRule
    {
        /// <summary>
        /// 规则应用模式，默认为 Add。
        /// </summary>
        public ManifestRuleMode Mode = ManifestRuleMode.Add;

        /// <summary>
        /// 可选，覆盖 tools:node 属性值；为 null 时由 Mode 推导。
        /// </summary>
        public string ToolsNode;
    }

    /// <summary>
    /// 所有规则的容器，一个 SDK 提交一个 ManifestRuleSet。
    /// </summary>
    public sealed class ManifestRuleSet
    {
        /// <summary>
        /// meta-data 规则数组，在 application 节点下操作。
        /// </summary>
        public MetaDataRule[] MetaDatas;

        /// <summary>
        /// uses-permission 规则数组，在 manifest 根节点下操作。
        /// </summary>
        public PermissionRule[] Permissions;

        /// <summary>
        /// provider 规则数组，在 application 节点下操作。
        /// </summary>
        public ProviderRule[] Providers;

        /// <summary>
        /// service 规则数组，在 application 节点下操作。
        /// </summary>
        public ServiceRule[] Services;

        /// <summary>
        /// activity 规则数组，在 application 节点下操作。
        /// </summary>
        public ActivityRule[] Activities;

        /// <summary>
        /// receiver 规则数组，在 application 节点下操作。
        /// </summary>
        public ReceiverRule[] Receivers;
    }

    /// <summary>
    /// meta-data 节点规则，描述对 application 下 meta-data 元素的操作意图。
    /// Value 与 Resource 互斥：有 Resource 时写 android:resource，否则写 android:value。
    /// </summary>
    public sealed class MetaDataRule : ManifestBaseRule
    {
        /// <summary>
        /// meta-data 的 android:name 属性值。
        /// </summary>
        public string Name;

        /// <summary>
        /// meta-data 的 android:value 属性值；与 Resource 互斥，优先级低于 Resource。
        /// </summary>
        public string Value;

        /// <summary>
        /// meta-data 的 android:resource 属性值（如 @drawable/xxx）；非空时写 android:resource 而非 android:value。
        /// </summary>
        public string Resource;

        /// <summary>
        /// 构造写入 android:value 的 MetaDataRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="value">android:value 属性值。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        public MetaDataRule(string name, string value, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            Name = name;
            Value = value;
            Mode = mode;
        }

        /// <summary>
        /// 构造写入 android:resource 的 MetaDataRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="resource">android:resource 属性值（如 @drawable/xxx）。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        /// <returns>Resource 字段已赋值的 MetaDataRule 实例。</returns>
        public static MetaDataRule WithResource(string name, string resource, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            return new MetaDataRule(name, null, mode) { Resource = resource };
        }
    }

    /// <summary>
    /// uses-permission 节点规则，描述对 manifest 根节点下权限声明的操作意图。
    /// </summary>
    public sealed class PermissionRule
    {
        /// <summary>
        /// 权限的 android:name 属性值，例如 "android.permission.INTERNET"。
        /// </summary>
        public string Name;

        /// <summary>
        /// 构造 PermissionRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        public PermissionRule(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// provider 节点规则，描述对 application 下 ContentProvider 元素的操作意图。
    /// </summary>
    public sealed class ProviderRule : ManifestBaseRule
    {
        /// <summary>
        /// provider 的 android:name 属性值。
        /// </summary>
        public string Name;

        /// <summary>
        /// provider 的 android:authorities 属性值。
        /// </summary>
        public string Authorities;

        /// <summary>
        /// provider 的 android:exported 属性值；为 null 时不写入该属性。
        /// </summary>
        public bool? Exported;

        /// <summary>
        /// 构造 ProviderRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="authorities">android:authorities 属性值。</param>
        /// <param name="exported">android:exported 属性值，null 时不写入。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        public ProviderRule(string name, string authorities, bool? exported = null, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            Name = name;
            Authorities = authorities;
            Exported = exported;
            Mode = mode;
        }
    }

    /// <summary>
    /// service 节点规则，描述对 application 下 Service 元素的操作意图。
    /// </summary>
    public sealed class ServiceRule : ManifestBaseRule
    {
        /// <summary>
        /// service 的 android:name 属性值。
        /// </summary>
        public string Name;

        /// <summary>
        /// service 的 android:exported 属性值；为 null 时不写入该属性。
        /// </summary>
        public bool? Exported;

        /// <summary>
        /// service 的 android:permission 属性值；为 null 时不写入该属性。
        /// </summary>
        public string Permission;

        /// <summary>
        /// 该 service 节点下的 intent-filter 规则数组。
        /// </summary>
        public IntentFilterRule[] IntentFilters;

        /// <summary>
        /// 构造 ServiceRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="exported">android:exported 属性值，null 时不写入。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        public ServiceRule(string name, bool? exported = null, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            Name = name;
            Exported = exported;
            Mode = mode;
        }
    }

    /// <summary>
    /// activity 节点规则，描述对 application 下 Activity 元素的操作意图。
    /// </summary>
    public sealed class ActivityRule : ManifestBaseRule
    {
        /// <summary>
        /// activity 的 android:name 属性值。UseMainActivity 为 true 时此字段仅用于新建兜底，不参与查找。
        /// </summary>
        public string Name;

        /// <summary>
        /// activity 的 android:exported 属性值；为 null 时不写入该属性。
        /// </summary>
        public bool? Exported;

        /// <summary>
        /// 该 activity 节点下的 intent-filter 规则数组。
        /// </summary>
        public IntentFilterRule[] IntentFilters;

        /// <summary>
        /// 为 true 时，优先定位含 MAIN/LAUNCHER intent-filter 的启动 Activity，忽略 Name 做精确匹配。
        /// 适用于需要向主 Activity 注入 intent-filter 但不确定其完整类名的场景。
        /// </summary>
        public bool UseMainActivity;

        /// <summary>
        /// 构造 ActivityRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="exported">android:exported 属性值，null 时不写入。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        public ActivityRule(string name, bool? exported = null, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            Name = name;
            Exported = exported;
            Mode = mode;
        }
    }

    /// <summary>
    /// receiver 节点规则，描述对 application 下 BroadcastReceiver 元素的操作意图。
    /// </summary>
    public sealed class ReceiverRule : ManifestBaseRule
    {
        /// <summary>
        /// receiver 的 android:name 属性值。
        /// </summary>
        public string Name;

        /// <summary>
        /// receiver 的 android:exported 属性值；为 null 时不写入该属性。
        /// </summary>
        public bool? Exported;

        /// <summary>
        /// 该 receiver 节点下的 intent-filter 规则数组。
        /// </summary>
        public IntentFilterRule[] IntentFilters;

        /// <summary>
        /// 构造 ReceiverRule。
        /// </summary>
        /// <param name="name">android:name 属性值。</param>
        /// <param name="exported">android:exported 属性值，null 时不写入。</param>
        /// <param name="mode">规则应用模式，默认 Add。</param>
        public ReceiverRule(string name, bool? exported = null, ManifestRuleMode mode = ManifestRuleMode.Add)
        {
            Name = name;
            Exported = exported;
            Mode = mode;
        }
    }

    /// <summary>
    /// intent-filter 节点规则，描述对 activity/service/receiver 下 IntentFilter 元素的操作意图。
    /// </summary>
    public sealed class IntentFilterRule : ManifestBaseRule
    {
        /// <summary>
        /// 是否设置 android:autoVerify="true"，用于 App Links 验证。
        /// </summary>
        public bool AutoVerify;

        /// <summary>
        /// intent-filter 下的 action 名称数组，每项对应一个 action android:name 属性。
        /// </summary>
        public string[] Actions;

        /// <summary>
        /// intent-filter 下的 category 名称数组，每项对应一个 category android:name 属性。
        /// </summary>
        public string[] Categories;

        /// <summary>
        /// intent-filter 下的 data 元素描述，用于 Deep Link 等场景。
        /// </summary>
        public IntentData Data;
    }

    /// <summary>
    /// intent-filter 下 data 元素的属性描述，用于 Deep Link / App Links 配置。
    /// </summary>
    public sealed class IntentData
    {
        /// <summary>
        /// data 的 android:scheme 属性值，例如 "https"。
        /// </summary>
        public string Scheme;

        /// <summary>
        /// data 的 android:host 属性值，例如 "example.com"。
        /// </summary>
        public string Host;

        /// <summary>
        /// data 的 android:pathPrefix 属性值，例如 "/"。
        /// </summary>
        public string PathPrefix;
    }
}
