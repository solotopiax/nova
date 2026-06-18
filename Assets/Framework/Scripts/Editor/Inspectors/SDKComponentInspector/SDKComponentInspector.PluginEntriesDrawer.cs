/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponentInspector.PluginEntriesDrawer.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 组件编辑器面板定制 —— Plugin 条目列表绘制器（族单选 + Missing 清理）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class SDKComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// Plugin 条目列表绘制器。
        /// 职责：
        ///   1. 反射扫描所有非抽象 ISDKPlugin 实现类（含跨程序集）
        ///   2. 与序列化的 m_PluginEntries 做双向同步（新增 append / 缺失标 IsMissing）
        ///   3. 按接口族分组，每族一行 Popup 单选（对齐 TypesSelector 视觉风格）
        ///   4. Missing 区域：红色警告 + "清理所有 Missing"按钮（逆序 DeleteArrayElementAtIndex）
        /// </summary>
        internal sealed class PluginEntriesDrawer : IDisposable
        {
            #region Constants

            /// <summary>
            /// Missing 条目默认 Priority（用于新 append 的初始值）。
            /// </summary>
            private const int c_DefaultPriority = 100;

            #endregion

            #region Fields

            /// <summary>
            /// 反射扫描得到的所有非抽象 ISDKPlugin 实现类型（不含接口、抽象类）。
            /// 编译后随 SyncEntries 刷新。
            /// </summary>
            private List<Type> m_ScannedPluginTypes = new List<Type>();

            /// <summary>
            /// 红色标签样式（Missing 条目显示）。
            /// </summary>
            private GUIStyle m_RedLabelStyle;

            #endregion

            #region Public API

            /// <summary>
            /// 增量同步：反射扫描 ISDKPlugin 实现类，追加新 Entry，标记 Missing。
            /// 每帧 OnInspectorGUI 调用，但内部通过比较避免重复申请 serializedObject 写入。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 的 SerializedProperty。</param>
            /// <param name="so">持有 entriesProp 的 SerializedObject。</param>
            public void SyncEntries(SerializedProperty entriesProp, SerializedObject so)
            {
                if (entriesProp == null || so == null) return;

                RefreshScannedTypes();
                bool dirty = AppendMissingTypes(entriesProp);
                MarkMissingEntries(entriesProp);

                if (dirty)
                {
                    so.ApplyModifiedProperties();
                }
            }

            /// <summary>
            /// 绘制整个 Plugin 列表区域：族单选 Popup + Missing 区域。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 的 SerializedProperty。</param>
            /// <param name="so">持有 entriesProp 的 SerializedObject。</param>
            public void Draw(SerializedProperty entriesProp, SerializedObject so)
            {
                if (entriesProp == null) return;

                DrawGroupedEntries(entriesProp, so);
                DrawMissingSection(entriesProp, so);
                EditorUtil.Draw.Line();
            }

            /// <summary>
            /// 释放资源：释放运行时 GUIStyle。
            /// </summary>
            public void Dispose()
            {
                m_RedLabelStyle = null;
            }

            #endregion

            #region Sync Methods

            /// <summary>
            /// 通过 UnityEditor.TypeCache 重新扫描所有非抽象 ISDKPlugin 实现类，写入 m_ScannedPluginTypes。
            /// </summary>
            private void RefreshScannedTypes()
            {
                m_ScannedPluginTypes.Clear();
                var derived = UnityEditor.TypeCache.GetTypesDerivedFrom<ISDKPlugin>();
                foreach (Type t in derived)
                {
                    if (!t.IsAbstract && !t.IsInterface)
                    {
                        m_ScannedPluginTypes.Add(t);
                    }
                }
            }

            /// <summary>
            /// 对反射扫描到的每个类型，若 m_PluginEntries 中尚不存在对应 TypeName，则 append 新 Entry。
            /// 新 Entry 的 Enabled 默认为 false；但对于某个族在 append 前完全没有任何条目的情况，
            /// 该族本轮首个被 append 的条目 Enabled 置 true，作为"初次出现自动选第一个"的默认行为。
            /// 此逻辑仅在 append 时触发一次，后续帧不会重复执行，因此不会覆盖用户手动将该族切为"无"的操作。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            /// <returns>是否有 dirty 写入。</returns>
            private bool AppendMissingTypes(SerializedProperty entriesProp)
            {
                // 统计 append 前各族是否已存在至少一条有效条目（TypeName 存在于当前 entriesProp）。
                // 使用与 DrawGroupedEntries 相同的调用顺序决定族归属，确保面板显示与数据一致：
                // Normal > Monetize > Ad > Attribution > Account > Cloud
                bool hasExistingNormal = false;
                bool hasExistingMonetize = false;
                bool hasExistingAd = false;
                bool hasExistingAttribution = false;
                bool hasExistingAccount = false;
                bool hasExistingCloud = false;
                bool hasExistingIAP = false;
                int existingCount = entriesProp.arraySize;
                for (int i = 0; i < existingCount; i++)
                {
                    string existingTypeName = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("TypeName").stringValue;
                    if (string.IsNullOrEmpty(existingTypeName)) continue;
                    Type existingType = Type.GetType(existingTypeName);
                    if (existingType == null) continue;
                    if (!hasExistingNormal && IsNormalTrackPlugin(existingType)) hasExistingNormal = true;
                    if (!hasExistingMonetize && IsMonetizeTrackPlugin(existingType)) hasExistingMonetize = true;
                    if (!hasExistingAd && IsAdPlugin(existingType)) hasExistingAd = true;
                    if (!hasExistingAttribution && IsAttributionPlugin(existingType)) hasExistingAttribution = true;
                    if (!hasExistingAccount && IsAccountPlugin(existingType)) hasExistingAccount = true;
                    if (!hasExistingCloud && IsCloudPlugin(existingType)) hasExistingCloud = true;
                    if (!hasExistingIAP && IsIAPPlugin(existingType)) hasExistingIAP = true;
                }

                // 本轮 append 时，各族是否已 append 过第一个条目（用于"首 append 自动启用"判定）。
                bool firstAppendedNormal = false;
                bool firstAppendedMonetize = false;
                bool firstAppendedAd = false;
                bool firstAppendedAttribution = false;
                bool firstAppendedAccount = false;
                bool firstAppendedCloud = false;
                bool firstAppendedIAP = false;

                bool dirty = false;
                foreach (Type pluginType in m_ScannedPluginTypes)
                {
                    string aqn = pluginType.AssemblyQualifiedName;
                    if (string.IsNullOrEmpty(aqn)) continue;
                    if (EntryIndexOf(entriesProp, aqn) >= 0) continue;

                    // 确定此类型所属族（按 DrawGroupedEntries 调用顺序取第一个匹配族）并决定 Enabled 初值。
                    bool enabledDefault = false;
                    if (IsNormalTrackPlugin(pluginType))
                    {
                        if (!hasExistingNormal && !firstAppendedNormal)
                        {
                            enabledDefault = true;
                            firstAppendedNormal = true;
                        }
                    }
                    else if (IsMonetizeTrackPlugin(pluginType))
                    {
                        if (!hasExistingMonetize && !firstAppendedMonetize)
                        {
                            enabledDefault = true;
                            firstAppendedMonetize = true;
                        }
                    }
                    else if (IsAdPlugin(pluginType))
                    {
                        if (!hasExistingAd && !firstAppendedAd)
                        {
                            enabledDefault = true;
                            firstAppendedAd = true;
                        }
                    }
                    else if (IsAttributionPlugin(pluginType))
                    {
                        if (!hasExistingAttribution && !firstAppendedAttribution)
                        {
                            enabledDefault = true;
                            firstAppendedAttribution = true;
                        }
                    }
                    else if (IsAccountPlugin(pluginType))
                    {
                        if (!hasExistingAccount && !firstAppendedAccount)
                        {
                            enabledDefault = true;
                            firstAppendedAccount = true;
                        }
                    }
                    else if (IsCloudPlugin(pluginType))
                    {
                        if (!hasExistingCloud && !firstAppendedCloud)
                        {
                            enabledDefault = true;
                            firstAppendedCloud = true;
                        }
                    }
                    else if (IsIAPPlugin(pluginType))
                    {
                        if (!hasExistingIAP && !firstAppendedIAP)
                        {
                            enabledDefault = true;
                            firstAppendedIAP = true;
                        }
                    }

                    int newIdx = entriesProp.arraySize;
                    entriesProp.arraySize = newIdx + 1;
                    SerializedProperty newEntry = entriesProp.GetArrayElementAtIndex(newIdx);
                    newEntry.FindPropertyRelative("TypeName").stringValue = aqn;
                    newEntry.FindPropertyRelative("Enabled").boolValue = enabledDefault;
                    newEntry.FindPropertyRelative("Priority").intValue = c_DefaultPriority;
                    dirty = true;
                }
                return dirty;
            }

            /// <summary>
            /// 遍历所有序列化条目，通过 Type.GetType 检测类型是否存在，更新内存中的 IsMissing 标记。
            /// IsMissing 为 [NonSerialized]，无需写回序列化对象。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            private void MarkMissingEntries(SerializedProperty entriesProp)
            {
                // IsMissing 是 [NonSerialized] 字段，无法通过 SerializedProperty 访问；
                // 直接操作 target 对象上的字段（通过反射写入，运行时读取）。
                // 注意：仅 Editor 环境访问，无线程安全问题。
                SDKComponent component = entriesProp.serializedObject.targetObject as SDKComponent;
                if (component == null) return;

                IReadOnlyList<SDKPluginEntry> entries = component.PluginEntries;
                if (entries == null) return;

                for (int i = 0; i < entries.Count; i++)
                {
                    SDKPluginEntry entry = entries[i];
                    if (entry == null) continue;
                    entry.IsMissing = !string.IsNullOrEmpty(entry.TypeName) && Type.GetType(entry.TypeName) == null;
                }
            }

            /// <summary>
            /// 在 m_PluginEntries 数组中查找 TypeName 匹配的元素索引。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            /// <param name="typeName">AssemblyQualifiedName。</param>
            /// <returns>找到时返回索引，未找到返回 -1。</returns>
            private static int EntryIndexOf(SerializedProperty entriesProp, string typeName)
            {
                int count = entriesProp.arraySize;
                for (int i = 0; i < count; i++)
                {
                    SerializedProperty elem = entriesProp.GetArrayElementAtIndex(i);
                    if (elem.FindPropertyRelative("TypeName").stringValue == typeName) return i;
                }
                return -1;
            }

            #endregion

            #region Draw Methods

            /// <summary>
            /// 按接口族分组，每族一行 Popup 单选，对齐 TypesSelector 视觉风格。
            /// 选中项 Enabled=true，同族其余条目 Enabled=false。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 的 SerializedProperty。</param>
            /// <param name="so">持有 entriesProp 的 SerializedObject。</param>
            private void DrawGroupedEntries(SerializedProperty entriesProp, SerializedObject so)
            {
                SDKComponent component = entriesProp.serializedObject.targetObject as SDKComponent;
                IReadOnlyList<SDKPluginEntry> entries = component?.PluginEntries;
                if (entries == null || entries.Count == 0) return;

                DrawGroupSelector("普通埋点", entriesProp, entries, so, IsNormalTrackPlugin);
                DrawGroupSelector("变现埋点", entriesProp, entries, so, IsMonetizeTrackPlugin);
                DrawGroupSelector("广告", entriesProp, entries, so, IsAdPlugin);
                DrawGroupSelector("归因埋点", entriesProp, entries, so, IsAttributionPlugin);
                DrawGroupSelector("账号登录", entriesProp, entries, so, IsAccountPlugin);
                DrawGroupSelector("云服务", entriesProp, entries, so, IsCloudPlugin);
                DrawGroupSelector("支付", entriesProp, entries, so, IsIAPPlugin);   
            }

            /// <summary>
            /// 绘制一个族的单选 Popup 行。
            /// 首项为"无"（表示该族不启用任何插件），其余项为各 Plugin 的 FullName。
            /// 用户切换选项时将选中条目 Enabled 置 true，同族其余条目 Enabled 置 false；
            /// 选择"无"（index 0）时将同族所有条目 Enabled 置 false。
            /// </summary>
            /// <param name="groupName">族显示名称，用作 Popup 标签。</param>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            /// <param name="entries">运行时 Entry 只读列表（含 IsMissing 标记）。</param>
            /// <param name="so">持有 entriesProp 的 SerializedObject。</param>
            /// <param name="groupFilter">判断 Plugin 是否属于此族的谓词。</param>
            private static void DrawGroupSelector(string groupName, SerializedProperty entriesProp, IReadOnlyList<SDKPluginEntry> entries, SerializedObject so, Func<Type, bool> groupFilter)
            {
                var groupIndices = new List<int>();
                var groupTypes = new List<Type>();
                for (int i = 0; i < entries.Count; i++)
                {
                    SDKPluginEntry entry = entries[i];
                    if (entry == null || entry.IsMissing) continue;
                    Type t = Type.GetType(entry.TypeName);
                    if (t != null && groupFilter(t))
                    {
                        groupIndices.Add(i);
                        groupTypes.Add(t);
                    }
                }

                if (groupIndices.Count == 0)
                {
                    // 该族无任何已安装的 Plugin，展示禁用的"无"占位行，提示用户安装对应 UPM 包。
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(groupName, GUILayout.Width(180f));
                    EditorGUILayout.Popup(0, new[] { "无" });
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();
                    return;
                }

                // options[0] = "无"，options[i+1] = groupTypes[i].FullName。
                // 选中 index 0 表示该族不启用任何 Plugin。
                var options = new string[groupTypes.Count + 1];
                options[0] = "无";
                for (int i = 0; i < groupTypes.Count; i++)
                {
                    options[i + 1] = groupTypes[i].FullName;
                }

                // 当前选中项：0 为"无"（该族全 false）；若存在 Enabled=true 的条目则取其 popup index（i+1）。
                int curPopupIndex = 0;
                for (int i = 0; i < groupIndices.Count; i++)
                {
                    if (entries[groupIndices[i]].Enabled)
                    {
                        curPopupIndex = i + 1;
                        break;
                    }
                }

                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(groupName, GUILayout.Width(180f));

                EditorGUI.BeginChangeCheck();
                int newPopupIndex = EditorGUILayout.Popup(curPopupIndex, options);
                if (EditorGUI.EndChangeCheck() && newPopupIndex != curPopupIndex)
                {
                    // newPopupIndex == 0 → 全部置 false（用户主动选"无"）。
                    // newPopupIndex > 0  → 仅 index-1 对应条目置 true，其余置 false。
                    for (int i = 0; i < groupIndices.Count; i++)
                    {
                        SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(groupIndices[i]);
                        entryProp.FindPropertyRelative("Enabled").boolValue = (newPopupIndex > 0 && i == newPopupIndex - 1);
                    }
                    so.ApplyModifiedProperties();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }

            /// <summary>
            /// 绘制 Missing 区域：若存在 Missing 条目则显示警告 HelpBox 和"清理所有 Missing"按钮。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            /// <param name="so">持有 entriesProp 的 SerializedObject。</param>
            private void DrawMissingSection(SerializedProperty entriesProp, SerializedObject so)
            {
                SDKComponent component = entriesProp.serializedObject.targetObject as SDKComponent;
                IReadOnlyList<SDKPluginEntry> entries = component?.PluginEntries;
                if (entries == null) return;

                var missingEntries = new List<(int index, string typeName)>();
                for (int i = 0; i < entries.Count; i++)
                {
                    SDKPluginEntry entry = entries[i];
                    if (entry != null && entry.IsMissing)
                    {
                        missingEntries.Add((i, entry.TypeName));
                    }
                }

                if (missingEntries.Count == 0) return;

                EditorUtil.Draw.Space(4f);
                DrawMissingWarnings(missingEntries);
                EditorUtil.Draw.DangerButton("清理所有 Missing", true, () => RemoveAllMissingEntries(entriesProp));
            }

            /// <summary>
            /// 绘制每条 Missing 条目的红色警告标签。
            /// </summary>
            /// <param name="missingEntries">Missing 条目列表（索引 + TypeName）。</param>
            private void DrawMissingWarnings(List<(int index, string typeName)> missingEntries)
            {
                GUIStyle redStyle = GetRedLabelStyle();
                foreach ((int _, string typeName) in missingEntries)
                {
                    string shortName = ExtractShortName(typeName);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Label($"[Missing] {shortName}", redStyle, false, GUILayout.ExpandWidth(true));
                    });
                }
            }

            /// <summary>
            /// 逆序删除所有 TypeName 为空或类型不存在（Missing）的序列化条目。
            /// 完全在 SerializedProperty 层操作，不依赖运行时视图，避免帧边界索引偏移。
            /// 删除完成后立即 ApplyModifiedProperties，防止 DangerButton 触发 ExitGUI 导致提交丢失。
            /// </summary>
            /// <param name="entriesProp">m_PluginEntries 属性。</param>
            private static void RemoveAllMissingEntries(SerializedProperty entriesProp)
            {
                var toRemove = new List<int>();
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    string typeName = entriesProp.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("TypeName").stringValue;
                    bool missing = string.IsNullOrEmpty(typeName) || Type.GetType(typeName) == null;
                    if (missing) toRemove.Add(i);
                }

                for (int i = toRemove.Count - 1; i >= 0; i--)
                {
                    entriesProp.DeleteArrayElementAtIndex(toRemove[i]);
                }

                entriesProp.serializedObject.ApplyModifiedProperties();
            }

            #endregion

            #region Group Filter Methods

            /// <summary>
            /// 普通埋点：实现了 ITrackPlugin 但不继承 IMonetizeTrackPlugin 和 IAttributionPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于普通埋点族。</returns>
            private static bool IsNormalTrackPlugin(Type type)
            {
                return typeof(ITrackPlugin).IsAssignableFrom(type)
                    && !typeof(IMonetizeTrackPlugin).IsAssignableFrom(type)
                    && !typeof(IAttributionPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 变现埋点：实现了 IMonetizeTrackPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于变现埋点族。</returns>
            private static bool IsMonetizeTrackPlugin(Type type)
            {
                return typeof(IMonetizeTrackPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 归因：实现了 IAttributionPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于归因族。</returns>
            private static bool IsAttributionPlugin(Type type)
            {
                return typeof(IAttributionPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 广告：实现了 IAdPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于广告族。</returns>
            private static bool IsAdPlugin(Type type)
            {
                return typeof(IAdPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 账号：实现了 IAuthPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于账号族。</returns>
            private static bool IsAccountPlugin(Type type)
            {
                return typeof(IAuthPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 云服务：实现了 IPushPlugin 或 IRemoteConfigPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于云服务族。</returns>
            private static bool IsCloudPlugin(Type type)
            {
                return typeof(IPushPlugin).IsAssignableFrom(type)
                    || typeof(IRemoteConfigPlugin).IsAssignableFrom(type);
            }

            /// <summary>
            /// 支付：实现了 IIAPPlugin。
            /// </summary>
            /// <param name="type">Plugin 类型。</param>
            /// <returns>是否属于支付族。</returns>
            private static bool IsIAPPlugin(Type type)
            {
                return typeof(IIAPPlugin).IsAssignableFrom(type);
            }

            #endregion

            #region Helper Methods

            /// <summary>
            /// 从 AssemblyQualifiedName 提取短类名（最后一段命名空间后的类名部分）。
            /// </summary>
            /// <param name="assemblyQualifiedName">AssemblyQualifiedName 字符串。</param>
            /// <returns>短类名；解析失败时返回原始字符串。</returns>
            private static string ExtractShortName(string assemblyQualifiedName)
            {
                if (string.IsNullOrEmpty(assemblyQualifiedName)) return assemblyQualifiedName;
                int commaIdx = assemblyQualifiedName.IndexOf(',');
                string fullName = commaIdx > 0 ? assemblyQualifiedName.Substring(0, commaIdx) : assemblyQualifiedName;
                int dotIdx = fullName.LastIndexOf('.');
                return dotIdx >= 0 ? fullName.Substring(dotIdx + 1) : fullName;
            }

            /// <summary>
            /// 获取红色标签样式（延迟初始化，避免静态构造期 EditorStyles 未就绪）。
            /// </summary>
            /// <returns>红色标签 GUIStyle。</returns>
            private GUIStyle GetRedLabelStyle()
            {
                if (m_RedLabelStyle == null)
                {
                    m_RedLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = new Color(1f, 0.35f, 0.35f) }
                    };
                }
                return m_RedLabelStyle;
            }

            #endregion
        }
    }
}
