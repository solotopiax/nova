/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.ResourceEncryption.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   ConfigMaster 工作副本中的资源加解密配置面板
 ***************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;
using DimensionAxis = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.DimensionAxis;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow
    {
        private const string c_ResourceEncryptionPanelTitle = "资源加解密配置";
        private const float c_ResourceKeyLabelWidth = 150f;
        private const int c_ResourceKeyCardPadding = 8;
        private const float c_ResourceKeyCardRowSpacing = 2f;
        // 密钥数据只访问工作副本；列表高亮和输入状态属于界面临时状态。
        /// <summary>资源密钥的唯一编辑来源；所有持久化数据均归属于工作副本。</summary>
        private ResourceKeyStoreData ResourceKeyDraft => m_WorkingCopy.ResourceEncryption;
        private string m_ResourceKeyError;
        private int m_ResourceKeyCreationMode;
        private string m_ResourceKeyIdToAdd = string.Empty;
        private string m_ResourceKeyToImport = string.Empty;
        private int m_ResourceSelectedPackage;
        private bool m_ResourceShowKeys;
        private bool m_ResourceAddingKey;
        private GUIStyle m_ResourceKeyCardStyle;
        // 只保留界面草稿标识，保存成功后取消黄色提示。
        private readonly HashSet<ResourceKeyEntry> m_ResourceNewKeys = new HashSet<ResourceKeyEntry>();

        /// <summary>绘制工作副本中的资源密钥；修改由 Config 顶部全局保存统一提交。</summary>
        private void DrawResourceEncryptionPanel()
        {
            bool previousChanged = GUI.changed;
            try { DrawResourceEncryptionContent(); }
            finally { GUI.changed = previousChanged; }
        }

        /// <summary>绘制配置内容；真实数据变更显式标记待保存，资源包选择和显示状态不标脏。</summary>
        private void DrawResourceEncryptionContent()
        {
            // 主标题保留顶层位置；下方内容沿用 CDN 面板的 16 像素缩进与分区范式。
            DrawResourceKeyDimensionHeader();
            EditorUtil.Draw.Space(8f);
            DrawResourceKeyHelp(MessageType.Info, new[]
            {
                "(1) 上方维度决定各环境使用哪把当前加密密钥；所有已保留密钥共用，供新旧资源解密。加密方案仍在 YooAsset / Pipify 中选择。",
                "(2) 修改后先点击顶部“保存”，再点击“导出”生成客户端密钥源码。ConfigMaster 和生成文件一起提交到项目私有仓库。",
                "(3) 构建平台使用实际目标，渠道和开发模式使用激活 Config 已保存的值；Pipify 导出步骤的参数不会自动修改它们。"
            });
            if (!string.IsNullOrEmpty(m_ResourceKeyError))
                DrawResourceKeyHelp(MessageType.Error, new[] { m_ResourceKeyError });
            DrawResourceKeySectionTitle("客户端密钥源码");
            DrawResourceKeyField("源码导出目录", () =>
            {
                string directory = EditorUtil.Draw.TextField(ResourceKeyDraft.ClientCodeOutputDirectory, false, GUILayout.ExpandWidth(true));
                if (directory != ResourceKeyDraft.ClientCodeOutputDirectory)
                {
                    ResourceKeyDraft.ClientCodeOutputDirectory = directory;
                    MarkResourceKeysChanged();
                }
                EditorUtil.Draw.Button("选择", false, SelectResourceKeyOutputDirectory,
                    GUILayout.Width(64f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            });
            DrawResourceKeyHelp(MessageType.Info, new[]
            {
                "(1) 请选择 Assets 下的目录；命名空间使用 Config 的名字空间配置。",
                "(2) 所属程序集须随客户端编译，不能放在 Editor 或热更代码目录。"
            });
            DrawResourceKeySectionSeparator();
            if (ResourceKeyDraft.DimensionMask.ByPlatform)
                DrawResourceKeyHelp(MessageType.Info, new[] { "按平台分别配置并启用 Nova 清单加密时，请使用 Pipify 构建资源。官方清单加密接口不提供目标平台，缺少平台时会停止构建。" });

            DrawResourceKeySectionTitle("选择资源包");
            string[] current = GetResourcePackageNames();
            string[] names = current.Concat(ResourceKeyDraft.Packages.Select(package => package.PackageName)).Distinct().ToArray();
            if (names.Length == 0)
            {
                DrawResourceKeyHelp(MessageType.Info, new[] { "尚未配置 YooAsset 资源包，请先在 YooAsset Collector 中创建。" });
                return;
            }
            m_ResourceSelectedPackage = Mathf.Clamp(m_ResourceSelectedPackage, 0, names.Length - 1);
            int selected = m_ResourceSelectedPackage;
            DrawResourceKeyField("资源包（PackageName）", () =>
                selected = EditorUtil.Draw.Popup(selected, names, false, GUILayout.ExpandWidth(true)));
            if (selected != m_ResourceSelectedPackage)
            {
                m_ResourceSelectedPackage = selected;
                m_ResourceKeyToImport = string.Empty;
                m_ResourceKeyIdToAdd = string.Empty;
                m_ResourceShowKeys = false;
                m_ResourceAddingKey = false;
            }
            string packageName = names[m_ResourceSelectedPackage];
            ResourcePackageKeys package = ResourceKeyDraft.Packages.FirstOrDefault(item => item.PackageName == packageName)
                ?? new ResourcePackageKeys { PackageName = packageName };
            string activeKeyId = ResourceKeyStore.ResolveActiveKeyId(ResourceKeyDraft, package, GetResourceKeyEditingCoord());
            bool exists = current.Contains(packageName);
            if (!exists)
                DrawResourceKeyHelp(MessageType.Warning, new[] { "此资源包已不在 YooAsset 配置中。这里保留旧密钥，供旧资源解密。" });

            DrawResourceKeySectionSeparator();
            DrawResourceKeySectionTitle("现有密钥", () =>
            {
                EditorUtil.Draw.DisabledGroup(!exists || m_ResourceAddingKey, () =>
                    EditorUtil.Draw.Button("新增密钥", false, () => m_ResourceAddingKey = true, GUILayout.Width(88f), GUILayout.Height(EditorGUIUtility.singleLineHeight)));
                EditorUtil.Draw.Button(m_ResourceShowKeys ? "隐藏密钥" : "显示密钥", false,
                    () => m_ResourceShowKeys = !m_ResourceShowKeys, GUILayout.Width(88f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            });
            if (package.Keys.Count == 0)
                DrawResourceKeyHelp(MessageType.Info, new[] { "此资源包还没有密钥。点击右上角“新增密钥”，编号可填写 v1。" });
            else
            {
                DrawResourceKeyField("当前加密密钥", () => EditorUtil.Draw.Label(
                    string.IsNullOrEmpty(activeKeyId) ? "尚未选择，请从下方设为当前" : activeKeyId, false));
                DrawResourceKeyHelp(MessageType.Info, new[]
                {
                    "(1) 当前密钥用于新资源加密，其他密钥保留用于旧资源解密。",
                    "(2) 更换密钥请新增编号（例如 v2），不要覆盖或删除仍被旧资源使用的密钥。"
                });
                for (int index = 0; index < package.Keys.Count; index++)
                {
                    ResourceKeyEntry key = package.Keys[index];
                    DrawResourceKeyCard(() => DrawResourceKeyEntry(package, key), m_ResourceNewKeys.Contains(key));
                }
            }

            if (exists && m_ResourceAddingKey)
                DrawResourceKeyCard(() => DrawResourceNewKey(package), true);
            if (!exists)
            {
                EditorUtil.Draw.Space(8f);
                DrawResourceKeyRow(() => EditorUtil.Draw.Button("移除旧资源包的密钥记录", false, () => DeleteResourcePackage(package), GUILayout.Height(EditorGUIUtility.singleLineHeight)));
            }
        }

        /// <summary>资源密钥使用独立维度掩码，沿用统一三维标题与开关布局。</summary>
        private void DrawResourceKeyDimensionHeader()
        {
            PanelDimensionMask mask = ResourceKeyDraft.DimensionMask;
            EditorUtil.Draw.DisabledGroup(m_WorkingCopy == null, () =>
                DrawTitleWithMaskCore(c_ResourceEncryptionPanelTitle, mask, 30f,
                    (axis, enabled) => QueueResourceKeyDimensionChange(axis, enabled), deferConfirmation: true));
        }

        /// <summary>当前编辑坐标来自顶栏工作副本；构建取数另用已保存配置，互不混用。</summary>
        private Coord GetResourceKeyEditingCoord() => new Coord(m_EditingPlatform,
            m_WorkingCopy != null ? m_WorkingCopy.CurrentChannel : NovaFramework.Runtime.ChannelType.None,
            m_WorkingCopy != null ? m_WorkingCopy.CurrentDevelopMode : NovaFramework.Runtime.DevelopMode.Debug);

        /// <summary>延迟确认并投影全部包的当前编号，减维保留解密密钥，变更等待顶部全局保存。</summary>
        private void QueueResourceKeyDimensionChange(DimensionAxis axis, bool enabled)
        {
            ResourceKeyStoreData expected = ResourceKeyDraft;
            Coord coord = GetResourceKeyEditingCoord();
            EditorApplication.delayCall += () =>
            {
                if (this == null || m_WorkingCopy == null || !ReferenceEquals(expected, ResourceKeyDraft)) return;
                string name = axis == DimensionAxis.Platform ? "平台类型" : axis == DimensionAxis.Channel ? "渠道类型" : "开发模式";
                string message = enabled
                    ? $"按{name}分别选择当前加密密钥？已有各组合会沿用原来的编号，之后可在顶部切换并分别修改。"
                    : $"取消后，按顶部当前{name}合并各组的当前加密编号。其他{name}的编号选择会被覆盖；所有已保留密钥继续保留，旧资源仍可解密。";
                if (!EditorUtil.Draw.Panel.Confirm(enabled ? "开启分别配置" : "合并当前密钥选择", message + "\n\n修改后仍需点击顶部“保存”。",
                    enabled ? "开启分别配置" : "合并选择", "返回")) { if (this != null) Repaint(); return; }
                try
                {
                    if (enabled) EditorUtil.Config.DimensionProjector.OnDimensionEnabled(expected, coord, axis);
                    else EditorUtil.Config.DimensionProjector.OnDimensionDisabled(expected, coord, axis);
                    MarkResourceKeysChanged();
                    m_ResourceKeyError = null;
                }
                catch (Exception exception) { m_ResourceKeyError = exception.Message; }
                if (this != null) Repaint();
            };
        }

        /// <summary>沿用 CDN 面板的两侧留白和行间距；卡片内部使用零额外缩进。</summary>
        private static void DrawResourceKeyRow(Action draw, float inset = 16f, float spacing = 4f)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                // 零缩进也保留边界槽，阻止卡片首个字段的默认 margin 向父布局传播。
                EditorUtil.Draw.Space(inset);
                draw();
                EditorUtil.Draw.Space(inset);
            });
            EditorUtil.Draw.Space(spacing);
        }

        /// <summary>字段显式共享固定标签列，不依赖全局 labelWidth 或中文长度。</summary>
        private static void DrawResourceKeyField(string label, Action drawValue, float inset = 16f,
            float labelWidth = c_ResourceKeyLabelWidth, float spacing = 4f)
        {
            DrawResourceKeyRow(() =>
            {
                EditorUtil.Draw.Label(label, false, GUILayout.Width(labelWidth));
                drawValue();
            }, inset, spacing);
        }

        /// <summary>卡片保留内边距，缩短标签列以保持输入起点与卡片外字段一致。</summary>
        private static void DrawResourceKeyCardField(string label, Action drawValue)
        {
            DrawResourceKeyField(label, drawValue, inset: 0f,
                labelWidth: c_ResourceKeyLabelWidth - c_ResourceKeyCardPadding,
                spacing: c_ResourceKeyCardRowSpacing);
        }

        /// <summary>分区标题与右侧小按钮共用同一内容边界。</summary>
        private void DrawResourceKeySectionTitle(string title, Action trailingContent = null)
        {
            DrawResourceKeyRow(() =>
            {
                EditorUtil.Draw.Label(title, m_SectionTitleStyle, false, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (trailingContent == null) return;
                EditorUtil.Draw.FlexibleSpace();
                trailingContent();
            });
        }

        /// <summary>说明紧跟所属字段，边缘与该分区字段一致。</summary>
        private static void DrawResourceKeyHelp(MessageType type, string[] messages, float inset = 16f)
        {
            DrawResourceKeyRow(() => EditorUtil.Draw.HelpBox(type, messages, false), inset, 8f);
        }

        /// <summary>大区域之间沿用 CDN 页面的留白及分隔线。</summary>
        private static void DrawResourceKeySectionSeparator()
        {
            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Line();
            EditorUtil.Draw.Space(12f);
        }

        /// <summary>以局部黄色背景区分新增编辑条目及未保存新密钥，结束后恢复 GUI 状态。</summary>
        private void DrawResourceKeyCard(Action draw, bool isNew)
        {
            DrawResourceKeyRow(() =>
            {
                Color original = GUI.backgroundColor;
                try
                {
                    if (isNew) GUI.backgroundColor = new Color(1f, 0.86f, 0.3f);
                    EditorUtil.Draw.Layout.Vertical(m_ResourceKeyCardStyle, draw, GUILayout.ExpandWidth(true));
                }
                finally { GUI.backgroundColor = original; }
            }, spacing: 8f);
        }

        /// <summary>在列表末尾绘制新增密钥编辑条目，输入有效后才加入配置草稿。</summary>
        private void DrawResourceNewKey(ResourcePackageKeys package)
        {
            DrawResourceKeyRow(() => EditorUtil.Draw.Label("新增密钥（尚未保存）", EditorStyles.boldLabel, false),
                inset: 0f, spacing: c_ResourceKeyCardRowSpacing);
            DrawResourceKeyCardField("添加方式", () => m_ResourceKeyCreationMode = EditorUtil.Draw.Popup(m_ResourceKeyCreationMode,
                new[] { "自动生成（推荐）", "手动输入" }, false, GUILayout.ExpandWidth(true)));
            DrawResourceKeyCardField("密钥编号（如 v1）", () =>
                m_ResourceKeyIdToAdd = EditorUtil.Draw.TextField(m_ResourceKeyIdToAdd, false, GUILayout.ExpandWidth(true)));
            if (m_ResourceKeyCreationMode == 1)
            {
                DrawResourceKeyCardField("密钥内容（Base64）", () =>
                    m_ResourceKeyToImport = EditorUtil.Draw.TextField(m_ResourceKeyToImport, false, GUILayout.ExpandWidth(true)));
                DrawResourceKeyHelp(MessageType.Info, new[] { "填写 32 字节密钥的 Base64 文本。" }, inset: 0f);
            }
            DrawResourceKeyRow(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Button("取消", false, () =>
                {
                    m_ResourceAddingKey = false;
                    m_ResourceKeyIdToAdd = string.Empty;
                    m_ResourceKeyToImport = string.Empty;
                }, GUILayout.Width(64f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorUtil.Draw.Button(m_ResourceKeyCreationMode == 0 ? "生成并添加" : "添加密钥", false,
                    () => AddResourceKey(package, m_ResourceKeyCreationMode == 0), GUILayout.Width(100f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }, inset: 0f, spacing: 0f);
        }

        /// <summary>重建工作副本后清除条目高亮与新增输入；不读取独立密钥文件。</summary>
        private void InitializeResourceKeyPanel()
        {
            m_ResourceKeyCardStyle = null;
            m_ResourceKeyError = null;
            m_ResourceAddingKey = false;
            m_ResourceKeyIdToAdd = string.Empty;
            m_ResourceKeyToImport = string.Empty;
            m_ResourceNewKeys.Clear();
        }

        /// <summary>直接写入工作副本后同步 SerializedObject，进入顶部保存状态。</summary>
        private void MarkResourceKeysChanged()
        {
            m_MasterSO?.Update();
            m_IsDirty = true;
        }

        /// <summary>选择 Assets 内的目录并保存项目相对路径；取消选择不产生修改。</summary>
        private void SelectResourceKeyOutputDirectory()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null || m_WorkingCopy == null) return;
                string selected = EditorUtility.OpenFolderPanel("选择客户端密钥源码导出目录", Application.dataPath, string.Empty);
                if (string.IsNullOrEmpty(selected)) return;
                try
                {
                    ResourceKeyDraft.ClientCodeOutputDirectory = ResourceKeyCodeGenerator.NormalizeOutputDirectory(selected);
                    MarkResourceKeysChanged();
                    m_ResourceKeyError = null;
                }
                catch (Exception exception) { m_ResourceKeyError = exception.Message; }
                Repaint();
            };
        }

        /// <summary>绘制只读密钥值及当前编号、删除操作；隐藏时不向控件传递真实密钥。</summary>
        private void DrawResourceKeyEntry(ResourcePackageKeys package, ResourceKeyEntry key)
        {
            string activeKeyId = ResourceKeyStore.ResolveActiveKeyId(ResourceKeyDraft, package, GetResourceKeyEditingCoord());
            DrawResourceKeyCardField("密钥编号", () => EditorUtil.Draw.Label(
                key.KeyId + (activeKeyId == key.KeyId ? "（当前加密）" : "（保留解密）"), false));
            DrawResourceKeyCardField("密钥内容", () => EditorUtil.Draw.DisabledGroup(true, () =>
                EditorUtil.Draw.TextField(m_ResourceShowKeys ? key.KeyBase64 : "••••••••", false, GUILayout.ExpandWidth(true))));
            DrawResourceKeyRow(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.DisabledGroup(activeKeyId == key.KeyId && !string.IsNullOrEmpty(package.ActiveKeyId), () =>
                    EditorUtil.Draw.Button("设为当前加密密钥", false, () =>
                    {
                        EditorUtil.Config.DimensionProjector.SetResourceActiveKeyId(ResourceKeyDraft, package, GetResourceKeyEditingCoord(), key.KeyId);
                        MarkResourceKeysChanged();
                    }, GUILayout.Width(140f), GUILayout.Height(EditorGUIUtility.singleLineHeight)));
                EditorUtil.Draw.Button("删除", false, () => DeleteResourceKey(package, key), GUILayout.Width(64f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }, inset: 0f, spacing: 0f);
        }

        /// <summary>读取官方收集器的已有 PackageName，不创建或修改 YooAsset Package。</summary>
        private static string[] GetResourcePackageNames()
        {
            if (!BundleCollectorSettingData.HasSettingAsset()) return Array.Empty<string>();
            return BundleCollectorSettingData.Setting.Packages.Select(package => package.PackageName)
                .Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray();
        }

        /// <summary>校验编号后随机生成或手动输入 32 字节密钥；首个密钥自动成为当前加密密钥。</summary>
        private void AddResourceKey(ResourcePackageKeys package, bool generate)
        {
            if (!ResourceKeyStore.IsValidIdentifier(m_ResourceKeyIdToAdd) || package.Keys.Any(key => key.KeyId == m_ResourceKeyIdToAdd))
            {
                m_ResourceKeyError = "keyId 须为 1～64 个非控制字符、无首尾空白，且在包裹内不能重复。";
                return;
            }
            byte[] bytes = null;
            try
            {
                if (generate)
                {
                    bytes = new byte[32];
                    using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
                }
                else bytes = Convert.FromBase64String(m_ResourceKeyToImport);
                if (bytes.Length != 32) throw new FormatException();
                ResourceKeyEntry key = new ResourceKeyEntry { KeyId = m_ResourceKeyIdToAdd, KeyBase64 = Convert.ToBase64String(bytes) };
                if (!ResourceKeyDraft.Packages.Contains(package)) ResourceKeyDraft.Packages.Add(package);
                bool isFirstKey = package.Keys.Count == 0;
                package.Keys.Add(key);
                m_ResourceNewKeys.Add(key);
                m_ResourceAddingKey = false;
                if (string.IsNullOrEmpty(package.ActiveKeyId)) package.ActiveKeyId = key.KeyId;
                if (isFirstKey) ResourceKeyStore.InitializeFirstActiveKey(package, key.KeyId);
                if (string.IsNullOrEmpty(ResourceKeyStore.ResolveActiveKeyId(ResourceKeyDraft, package, GetResourceKeyEditingCoord())))
                    EditorUtil.Config.DimensionProjector.SetResourceActiveKeyId(ResourceKeyDraft, package, GetResourceKeyEditingCoord(), key.KeyId);
                m_ResourceKeyIdToAdd = string.Empty;
                m_ResourceKeyToImport = string.Empty;
                MarkResourceKeysChanged();
                m_ResourceKeyError = null;
            }
            catch (FormatException) { m_ResourceKeyError = "手动输入的密钥须为 32 字节 Base64。"; }
            finally { if (bytes != null) Array.Clear(bytes, 0, bytes.Length); }
        }

        /// <summary>删除密钥前提醒旧资源依赖；删除当前密钥后要求重新选择当前编号。</summary>
        private void DeleteResourceKey(ResourcePackageKeys package, ResourceKeyEntry key)
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorUtility.DisplayDialog("删除资源密钥", "旧资源仍需要对应旧密钥，删除后将无法解密。", "删除", "保留")) return;
                package.Keys.Remove(key);
                if (package.ActiveKeyId == key.KeyId) package.ActiveKeyId = string.Empty;
                foreach (ResourceActiveKeyOverride entry in package.ActiveKeyOverrides)
                    if (entry.ActiveKeyId == key.KeyId) entry.ActiveKeyId = string.Empty;
                MarkResourceKeysChanged();
                if (this != null) Repaint();
            };
        }

        /// <summary>删除整包配置前提醒会移除全部保留密钥；仅修改内存草稿。</summary>
        private void DeleteResourcePackage(ResourcePackageKeys package)
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorUtility.DisplayDialog("删除 Package 密钥配置", "此操作只移除本 Package 的密钥配置，不会删除 YooAsset Package；旧资源可能无法解密。", "删除", "保留")) return;
                ResourceKeyDraft.Packages.Remove(package);
                MarkResourceKeysChanged();
                if (this != null) Repaint();
            };
        }

    }
}
