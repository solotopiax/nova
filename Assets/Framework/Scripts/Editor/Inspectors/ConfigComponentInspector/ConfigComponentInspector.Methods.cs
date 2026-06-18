/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   配置组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System.Collections.Generic;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：Config 管理器类型选择、打开 Config 全局配置中心入口、配置数据位置折叠面板、
        /// 运行时已加载配置只读展示。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Config 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IConfigManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.Button("打开 Config 全局配置中心", false, ConfigWindow.Open, GUILayout.ExpandWidth(true));

            EditorUtil.Draw.Line();

            if (EditorUtil.Draw.Foldout("配置数据位置", null, true))
            {
                EditorUtil.Draw.IncreaseIndentLevel();
                EditorUtil.Draw.Property("Asset 地址", m_AssetLocationSP, true, GUILayout.Width(175));
                EditorUtil.Draw.DecreaseIndentLevel();
            }

            EditorUtil.Draw.Line();

            DrawRuntimeInfo();
        }

        /// <summary>
        /// Play 模式下展示已加载配置：DevelopMode / Common(AppID/AppAesKey/AppAesIV) / Namespace /
        /// Platform / Channel 与 EnabledSDKConfigs 列表。非 Play 模式直接返回。
        /// </summary>
        private void DrawRuntimeInfo()
        {
            if (!Application.isPlaying) return;
            ConfigComponent component = target as ConfigComponent;
            if (component == null) return;

            if (!EditorUtil.Draw.Foldout("运行时已加载配置", null, true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            if (!component.IsLoadOver)
            {
                EditorUtil.Draw.Label("（配置尚未加载完成）", false);
                EditorUtil.Draw.DecreaseIndentLevel();
                EditorUtil.Draw.Line();
                return;
            }

            DrawReadonlyKeyValue("DevelopMode", component.DevelopMode.ToString());
            DrawReadonlyKeyValue("AppID", component.Common?.AppID ?? string.Empty);
            DrawReadonlyKeyValue("AppAesKey", component.Common?.AppAesKey ?? string.Empty);
            DrawReadonlyKeyValue("AppAesIV", component.Common?.AppAesIV ?? string.Empty);
            DrawReadonlyKeyValue("Namespace", component.Namespace ?? string.Empty);
            DrawReadonlyKeyValue("Platform", component.Platform.ToString());
            DrawReadonlyKeyValue("Channel", component.Channel.ToString());
            DrawReadonlyKeyValue("GameEntranceProcedureName", component.GameEntranceProcedureName);

            DrawDllAssetEntryList("AOT 元数据 DLL", component.AotMetadataDlls);
            DrawDllAssetEntryList("业务 DLL", component.GameDlls);

            EditorUtil.Draw.Label("已启用 SDK 配置", false);

            var configs = component.AllPluginConfigs;
            if (configs == null || configs.Count == 0)
            {
                EditorUtil.Draw.IncreaseIndentLevel();
                EditorUtil.Draw.Label("（无启用配置）", false);
                EditorUtil.Draw.DecreaseIndentLevel();
            }
            else
            {
                EditorUtil.Draw.IncreaseIndentLevel();
                foreach (ISDKPluginConfig cfg in configs)
                {
                    if (cfg == null) continue;
                    DrawPluginConfig(cfg);
                }
                EditorUtil.Draw.DecreaseIndentLevel();
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 以 Foldout 形式绘制单个 PluginConfig，反射读取其 public 实例属性作为只读键值对展示。
        /// </summary>
        /// <param name="cfg">
        /// 待展示的 Plugin 配置实例。
        /// </param>
        private void DrawPluginConfig(ISDKPluginConfig cfg)
        {
            System.Type cfgType = cfg.GetType();
            string foldoutTitle = string.IsNullOrWhiteSpace(cfg.DisplayName) ? cfgType.Name : cfg.DisplayName;
            if (!EditorUtil.Draw.Foldout(foldoutTitle, null, true))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            PropertyInfo[] props = cfgType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo p in props)
            {
                if (p.Name == "DisplayName") continue;
                if (!p.CanRead) continue;
                object value;
                try { value = p.GetValue(cfg); }
                catch { continue; }
                DrawReadonlyKeyValue(p.Name, value == null ? string.Empty : value.ToString());
            }
            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制一行只读键值对；值字段使用 DisabledGroup 包裹的 TextField 呈现。
        /// </summary>
        /// <param name="key">
        /// 键名。
        /// </param>
        /// <param name="value">
        /// 值字符串。
        /// </param>
        private void DrawReadonlyKeyValue(string key, string value)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(key, false, GUILayout.Width(180));
                EditorUtil.Draw.DisabledGroup(true, () =>
                {
                    EditorGUILayout.TextField(value ?? string.Empty);
                });
            });
        }

        /// <summary>
        /// 以折叠面板形式展示 DllAssetEntry 列表；
        /// 列表为空时折叠内显示"（无条目）"，非空时每项一行：左侧"[i] 资产名"，右侧 Asset 地址只读 TextField。
        /// </summary>
        /// <param name="foldoutTitle">
        /// 折叠标题文本。
        /// </param>
        /// <param name="entries">
        /// DllAssetEntry 只读列表。
        /// </param>
        private void DrawDllAssetEntryList(string foldoutTitle, IReadOnlyList<DllAssetEntry> entries)
        {
            if (!EditorUtil.Draw.Foldout(foldoutTitle, null, true)) return;

            EditorUtil.Draw.IncreaseIndentLevel();
            if (entries == null || entries.Count == 0)
            {
                EditorUtil.Draw.Label("（无条目）", false);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DllAssetEntry entry = entries[i];
                    DrawReadonlyKeyValue($"[{i}]", entry.AssetLocation);
                }
            }
            EditorUtil.Draw.DecreaseIndentLevel();
        }

    }
}
