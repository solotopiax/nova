/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CustomAndroidManifest.cs
 * author:    yingzheng
 * created:   2026/4/23
 * descrip:   Android Manifest XML 操作工具，封装对 AndroidManifest.xml 的读写能力
 ***************************************************************/

using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Android Manifest XML 操作工具。
    /// 封装对 AndroidManifest.xml 的读写能力，供构建处理器使用。
    /// </summary>
    public class CustomAndroidManifest : XmlDocument
    {
        /// <summary>
        /// Android XML 命名空间 URI。
        /// </summary>
        private const string c_AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

        /// <summary>
        /// manifest 文件的磁盘路径。
        /// </summary>
        private readonly string m_Path;

        /// <summary>
        /// XML 命名空间管理器，用于 XPath 查询。
        /// </summary>
        protected readonly XmlNamespaceManager m_NameSpaceManager;

        /// <summary>
        /// /manifest 根元素。
        /// </summary>
        private readonly XmlElement m_ManifestElement;

        /// <summary>
        /// /manifest/application 元素。
        /// </summary>
        private readonly XmlElement m_ApplicationElement;

        /// <summary>
        /// 构造 CustomAndroidManifest 并从指定路径加载 XML 文件。
        /// </summary>
        /// <param name="path">AndroidManifest.xml 文件的完整磁盘路径。</param>
        public CustomAndroidManifest(string path)
        {
            m_Path = path;
            using (var reader = new XmlTextReader(path))
            {
                reader.Read();
                Load(reader);
            }
            m_NameSpaceManager = new XmlNamespaceManager(NameTable);
            m_NameSpaceManager.AddNamespace("android", c_AndroidXmlNamespace);

            m_ManifestElement = SelectSingleNode("/manifest") as XmlElement;
            if (m_ManifestElement == null)
                throw new System.InvalidOperationException($"[CustomAndroidManifest] {path} 缺少根 <manifest> 节点");

            m_ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
            if (m_ApplicationElement == null)
                throw new System.InvalidOperationException($"[CustomAndroidManifest] {path} 缺少 <application> 节点");
        }

        /// <summary>
        /// 将当前 XML 内容保存回原始路径，使用 UTF-8 无 BOM 编码。
        /// </summary>
        public void Save()
        {
            using var writer = new XmlTextWriter(m_Path, new UTF8Encoding(false));
            writer.Formatting = Formatting.Indented;
            Save(writer);
        }

        /// <summary>
        /// 获取具有启动 Intent 的主 Activity 节点。
        /// </summary>
        /// <returns>主 Activity 的 XmlNode，若不存在则返回 null。</returns>
        public XmlNode GetActivityWithLaunchIntent()
        {
            return SelectSingleNode(
                "/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and "
                + "intent-filter/category/@android:name='android.intent.category.LAUNCHER']",
                m_NameSpaceManager);
        }

        /// <summary>
        /// 创建一个带 android 命名空间的 XML 属性。
        /// </summary>
        /// <param name="key">属性名（不含命名空间前缀）。</param>
        /// <param name="value">属性值。</param>
        /// <returns>创建好的 XmlAttribute。</returns>
        public XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            XmlAttribute attr = CreateAttribute("android", key, c_AndroidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        /// <summary>
        /// 获取 /manifest 根元素。
        /// </summary>
        public XmlElement ManifestElement => m_ManifestElement;

        /// <summary>
        /// 获取 /manifest/application 元素。
        /// </summary>
        public XmlElement ApplicationElement => m_ApplicationElement;

        /// <summary>
        /// 获取 Android XML 命名空间 URI。
        /// </summary>
        public string AndroidXmlNamespace => c_AndroidXmlNamespace;

        /// <summary>
        /// tools 命名空间 URI，用于写入 tools:node 等属性。
        /// </summary>
        private const string c_ToolsNamespace = "http://schemas.android.com/tools";

        /// <summary>
        /// 将规则集应用到当前 Manifest XML，按 MetaData→Permission→Provider→Service→Activity→Receiver 顺序执行。
        /// 不调用 Save，由调用方统一保存。
        /// </summary>
        /// <param name="ruleSet">要应用的规则集，为 null 时直接返回。</param>
        public void Apply(ManifestRuleSet ruleSet)
        {
            if (ruleSet == null) return;
            ApplyMetaDatas(ruleSet.MetaDatas);
            ApplyPermissions(ruleSet.Permissions);
            ApplyProviders(ruleSet.Providers);
            ApplyServices(ruleSet.Services);
            ApplyActivities(ruleSet.Activities);
            ApplyReceivers(ruleSet.Receivers);
        }

        /// <summary>
        /// 创建一个带 tools 命名空间的 XML 属性。
        /// </summary>
        /// <param name="key">属性名（不含命名空间前缀）。</param>
        /// <param name="value">属性值。</param>
        /// <returns>创建好的 XmlAttribute。</returns>
        private XmlAttribute CreateToolsAttribute(string key, string value)
        {
            var attr = CreateAttribute("tools", key, c_ToolsNamespace);
            attr.Value = value;
            return attr;
        }

        /// <summary>
        /// 在指定父节点的直接子节点中，按 tag 名称和 android:name 属性值查找匹配节点。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="tag">子节点标签名。</param>
        /// <param name="name">android:name 属性值。</param>
        /// <returns>匹配的 XmlNode，不存在时返回 null。</returns>
        private XmlNode FindChildByAndroidName(XmlNode parent, string tag, string name)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.Name == tag && child.Attributes?["android:name"]?.Value == name)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// 将 exported 布尔值转换为 Manifest 属性字符串 "true" 或 "false"。
        /// </summary>
        /// <param name="exported">布尔值。</param>
        /// <returns>"true" 或 "false"。</returns>
        private string BoolToString(bool exported) => exported ? "true" : "false";

        /// <summary>
        /// 在指定节点上按 Mode 设置 tools:node 属性，或依据 mode 推导默认值。
        /// </summary>
        /// <param name="node">目标节点。</param>
        /// <param name="rule">规则基类实例，提供 Mode 和 ToolsNode。</param>
        private void ApplyToolsNode(XmlNode node, ManifestBaseRule rule)
        {
            string toolsNodeValue = rule.ToolsNode;
            if (string.IsNullOrEmpty(toolsNodeValue)) return;
            var existing = node.Attributes?["tools:node"];
            if (existing != null)
                existing.Value = toolsNodeValue;
            else
                node.Attributes?.Append(CreateToolsAttribute("node", toolsNodeValue));
        }

        /// <summary>
        /// 将 MetaData 规则数组应用到 application 节点下的 meta-data 元素。
        /// </summary>
        /// <param name="rules">MetaDataRule 数组，为 null 时直接返回。</param>
        private void ApplyMetaDatas(MetaDataRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                var existing = FindChildByAndroidName(m_ApplicationElement, "meta-data", rule.Name);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    m_ApplicationElement.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var node = CreateElement("meta-data");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    if (!string.IsNullOrEmpty(rule.Resource))
                        node.Attributes.Append(CreateAndroidAttribute("resource", rule.Resource));
                    else
                        node.Attributes.Append(CreateAndroidAttribute("value", rule.Value));
                    ApplyToolsNode(node, rule);
                    m_ApplicationElement.AppendChild(node);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    if (!string.IsNullOrEmpty(rule.Resource))
                    {
                        var resourceAttr = existing.Attributes?["android:resource"];
                        if (resourceAttr != null)
                            resourceAttr.Value = rule.Resource;
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("resource", rule.Resource));
                    }
                    else
                    {
                        var valueAttr = existing.Attributes?["android:value"];
                        if (valueAttr != null)
                            valueAttr.Value = rule.Value;
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("value", rule.Value));
                    }
                    ApplyToolsNode(existing, rule);
                }
            }
        }

        /// <summary>
        /// 将 Permission 规则数组应用到 manifest 根节点下的 uses-permission 元素，不存在时新增。
        /// </summary>
        /// <param name="rules">PermissionRule 数组，为 null 时直接返回。</param>
        private void ApplyPermissions(PermissionRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                var existing = FindChildByAndroidName(m_ManifestElement, "uses-permission", rule.Name);
                if (existing == null)
                {
                    var node = CreateElement("uses-permission");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    m_ManifestElement.AppendChild(node);
                }
            }
        }

        /// <summary>
        /// 将 Provider 规则数组应用到 application 节点下的 provider 元素。
        /// </summary>
        /// <param name="rules">ProviderRule 数组，为 null 时直接返回。</param>
        private void ApplyProviders(ProviderRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                var existing = FindChildByAndroidName(m_ApplicationElement, "provider", rule.Name);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    m_ApplicationElement.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var node = CreateElement("provider");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    node.Attributes.Append(CreateAndroidAttribute("authorities", rule.Authorities));
                    if (rule.Exported.HasValue)
                        node.Attributes.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    ApplyToolsNode(node, rule);
                    m_ApplicationElement.AppendChild(node);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    var authAttr = existing.Attributes?["android:authorities"];
                    if (authAttr != null)
                        authAttr.Value = rule.Authorities;
                    else
                        existing.Attributes?.Append(CreateAndroidAttribute("authorities", rule.Authorities));
                    if (rule.Exported.HasValue)
                    {
                        var expAttr = existing.Attributes?["android:exported"];
                        if (expAttr != null)
                            expAttr.Value = BoolToString(rule.Exported.Value);
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    }
                    ApplyToolsNode(existing, rule);
                }
            }
        }

        /// <summary>
        /// 将 Service 规则数组应用到 application 节点下的 service 元素。
        /// </summary>
        /// <param name="rules">ServiceRule 数组，为 null 时直接返回。</param>
        private void ApplyServices(ServiceRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                var existing = FindChildByAndroidName(m_ApplicationElement, "service", rule.Name);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    m_ApplicationElement.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var node = CreateElement("service");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    if (!string.IsNullOrEmpty(rule.Permission))
                        node.Attributes.Append(CreateAndroidAttribute("permission", rule.Permission));
                    if (rule.Exported.HasValue)
                        node.Attributes.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    ApplyToolsNode(node, rule);
                    ApplyIntentFilters(node, rule.IntentFilters);
                    m_ApplicationElement.AppendChild(node);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    if (rule.Exported.HasValue)
                    {
                        var expAttr = existing.Attributes?["android:exported"];
                        if (expAttr != null)
                            expAttr.Value = BoolToString(rule.Exported.Value);
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    }
                    if (!string.IsNullOrEmpty(rule.Permission))
                    {
                        var permAttr = existing.Attributes?["android:permission"];
                        if (permAttr != null)
                            permAttr.Value = rule.Permission;
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("permission", rule.Permission));
                    }
                    ApplyToolsNode(existing, rule);
                    ApplyIntentFilters(existing, rule.IntentFilters);
                }
            }
        }

        /// <summary>
        /// 将 Activity 规则数组应用到 application 节点下的 activity 元素。
        /// 当 rule.UseMainActivity 为 true 时，优先定位含 MAIN/LAUNCHER intent-filter 的启动 Activity，
        /// 忽略 rule.Name 做精确匹配，避免因全限定类名差异导致重复新建节点。
        /// </summary>
        /// <param name="rules">ActivityRule 数组，为 null 时直接返回。</param>
        private void ApplyActivities(ActivityRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                XmlNode existing = rule.UseMainActivity
                    ? GetActivityWithLaunchIntent()
                    : FindChildByAndroidName(m_ApplicationElement, "activity", rule.Name);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    m_ApplicationElement.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var node = CreateElement("activity");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    if (rule.Exported.HasValue)
                        node.Attributes.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    ApplyToolsNode(node, rule);
                    ApplyIntentFilters(node, rule.IntentFilters);
                    m_ApplicationElement.AppendChild(node);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    if (rule.Exported.HasValue)
                    {
                        var expAttr = existing.Attributes?["android:exported"];
                        if (expAttr != null)
                            expAttr.Value = BoolToString(rule.Exported.Value);
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    }
                    ApplyToolsNode(existing, rule);
                    ApplyIntentFilters(existing, rule.IntentFilters);
                }
            }
        }

        /// <summary>
        /// 将 Receiver 规则数组应用到 application 节点下的 receiver 元素。
        /// </summary>
        /// <param name="rules">ReceiverRule 数组，为 null 时直接返回。</param>
        private void ApplyReceivers(ReceiverRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                var existing = FindChildByAndroidName(m_ApplicationElement, "receiver", rule.Name);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    m_ApplicationElement.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var node = CreateElement("receiver");
                    node.Attributes.Append(CreateAndroidAttribute("name", rule.Name));
                    if (rule.Exported.HasValue)
                        node.Attributes.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    ApplyToolsNode(node, rule);
                    ApplyIntentFilters(node, rule.IntentFilters);
                    m_ApplicationElement.AppendChild(node);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    if (rule.Exported.HasValue)
                    {
                        var expAttr = existing.Attributes?["android:exported"];
                        if (expAttr != null)
                            expAttr.Value = BoolToString(rule.Exported.Value);
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("exported", BoolToString(rule.Exported.Value)));
                    }
                    ApplyToolsNode(existing, rule);
                    ApplyIntentFilters(existing, rule.IntentFilters);
                }
            }
        }

        /// <summary>
        /// 将 IntentFilter 规则数组应用到指定父节点（activity/service/receiver）下的 intent-filter 元素。
        /// 匹配逻辑：intent-filter 下含有与规则 Actions 全部匹配的 action 子节点，则认为是同一个。
        /// </summary>
        /// <param name="parent">父节点，即 activity/service/receiver 的 XmlNode。</param>
        /// <param name="rules">IntentFilterRule 数组，为 null 时直接返回。</param>
        private void ApplyIntentFilters(XmlNode parent, IntentFilterRule[] rules)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                XmlNode existing = FindMatchingIntentFilter(parent, rule.Actions, rule.Data?.Scheme);
                if (rule.Mode == ManifestRuleMode.Replace && existing != null)
                {
                    parent.RemoveChild(existing);
                    existing = null;
                }
                if (existing == null)
                {
                    var filterNode = CreateElement("intent-filter");
                    if (rule.AutoVerify)
                        filterNode.Attributes.Append(CreateAndroidAttribute("autoVerify", "true"));
                    if (!string.IsNullOrEmpty(rule.ToolsNode))
                        filterNode.Attributes.Append(CreateToolsAttribute("node", rule.ToolsNode));
                    AppendIntentFilterChildren(filterNode, rule);
                    parent.AppendChild(filterNode);
                }
                else if (rule.Mode == ManifestRuleMode.Merge)
                {
                    if (rule.AutoVerify)
                    {
                        var autoVerifyAttr = existing.Attributes?["android:autoVerify"];
                        if (autoVerifyAttr != null)
                            autoVerifyAttr.Value = "true";
                        else
                            existing.Attributes?.Append(CreateAndroidAttribute("autoVerify", "true"));
                    }
                    if (!string.IsNullOrEmpty(rule.ToolsNode))
                        ApplyToolsNode(existing, rule);
                    AppendIntentFilterChildren(existing, rule);
                }
            }
        }

        /// <summary>
        /// 在指定父节点的直接子节点中，查找与规则匹配的 intent-filter。
        /// 匹配条件：actions 数组全部匹配，且 data scheme 相同（均为空也视为相同）。
        /// actions 为 null 或空且 dataScheme 为空时，返回第一个 intent-filter 子节点。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="actions">需全部匹配的 action 名称数组。</param>
        /// <param name="dataScheme">data 元素的 scheme 值，用于区分同 actions 不同 data 的 intent-filter；为 null 时不参与匹配。</param>
        /// <returns>匹配的 intent-filter 节点，不存在时返回 null。</returns>
        private XmlNode FindMatchingIntentFilter(XmlNode parent, string[] actions, string dataScheme = null)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.Name != "intent-filter") continue;
                if ((actions == null || actions.Length == 0) && string.IsNullOrEmpty(dataScheme)) return child;
                bool actionsMatch = true;
                if (actions != null && actions.Length > 0)
                {
                    foreach (var actionName in actions)
                    {
                        bool found = false;
                        foreach (XmlNode sub in child.ChildNodes)
                        {
                            if (sub.Name == "action" && sub.Attributes?["android:name"]?.Value == actionName)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found) { actionsMatch = false; break; }
                    }
                }
                if (!actionsMatch) continue;
                if (dataScheme != null)
                {
                    bool schemeMatch = false;
                    foreach (XmlNode sub in child.ChildNodes)
                    {
                        if (sub.Name == "data" && sub.Attributes?["android:scheme"]?.Value == dataScheme)
                        {
                            schemeMatch = true;
                            break;
                        }
                    }
                    if (!schemeMatch) continue;
                }
                return child;
            }
            return null;
        }

        /// <summary>
        /// 向 intent-filter 节点追加 action、category、data 子元素（基于规则描述）。
        /// </summary>
        /// <param name="filterNode">目标 intent-filter 节点。</param>
        /// <param name="rule">IntentFilterRule 实例，提供 Actions、Categories、Data。</param>
        private void AppendIntentFilterChildren(XmlNode filterNode, IntentFilterRule rule)
        {
            if (rule.Actions != null)
            {
                foreach (var actionName in rule.Actions)
                {
                    var actionNode = CreateElement("action");
                    actionNode.Attributes.Append(CreateAndroidAttribute("name", actionName));
                    filterNode.AppendChild(actionNode);
                }
            }
            if (rule.Categories != null)
            {
                foreach (var categoryName in rule.Categories)
                {
                    var categoryNode = CreateElement("category");
                    categoryNode.Attributes.Append(CreateAndroidAttribute("name", categoryName));
                    filterNode.AppendChild(categoryNode);
                }
            }
            if (rule.Data != null)
            {
                var dataNode = CreateElement("data");
                if (!string.IsNullOrEmpty(rule.Data.Scheme))
                    dataNode.Attributes.Append(CreateAndroidAttribute("scheme", rule.Data.Scheme));
                if (!string.IsNullOrEmpty(rule.Data.Host))
                    dataNode.Attributes.Append(CreateAndroidAttribute("host", rule.Data.Host));
                if (!string.IsNullOrEmpty(rule.Data.PathPrefix))
                    dataNode.Attributes.Append(CreateAndroidAttribute("pathPrefix", rule.Data.PathPrefix));
                filterNode.AppendChild(dataNode);
            }
        }
    }
}
