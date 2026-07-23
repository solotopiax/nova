/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   SDK 组件编辑器面板定制 - 私有方法
 ***************************************************************/

using System;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class SDKComponentInspector : BaseComponentInspector
    {
        private const string c_TrackGeneratedWorkbookRelativePath = "Library/Nova/Tracks/Tracks.generated.xlsx";

        /// <summary>
        /// 绘制配置区域：SDK Manager 类型选择器、说明与辅助工具按钮。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("SDK 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ISDKManager 接口后，该类型会自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Plugin 条目列表区域：先执行增量同步，再委托 Drawer 绘制。
        /// </summary>
        private void DrawPluginEntries()
        {
            m_Drawer.SyncEntries(m_PluginEntries, serializedObject);
            m_Drawer.Draw(m_PluginEntries, serializedObject);
        }

        /// <summary>
        /// 绘制打点工具区域。
        /// </summary>
        private static void DrawTrackTools()
        {
      
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Button("打开打点表", false, OpenTrackWorkbook, GUILayout.ExpandWidth(true));
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 每次打开前都重新汇总当前工程的 Tracks.xlsx，确保本地查看表反映最新打点表内容。
        /// </summary>
        private static void OpenTrackWorkbook()
        {
            string projectRoot = GetProjectRoot();
            if (!GenerateTrackWorkbook(projectRoot))
            {
                return;
            }

            string workbookPath = Util.SysIO.Path.Combine(projectRoot, c_TrackGeneratedWorkbookRelativePath);
            if (!File.Exists(workbookPath))
            {
                Log.Warning(LogTag.Editor, "未找到打点汇总表：{0}", workbookPath);
                return;
            }

            EditorUtil.FileSystem.OpenFile(workbookPath);
        }

        /// <summary>
        /// 生成模块与 Framework 的 Tracks.xlsx 汇总表。
        /// </summary>
        private static bool GenerateTrackWorkbook(string projectRoot)
        {
            try
            {
                EditorUtil.TrackRegistry.Generate(projectRoot);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Editor, "打点汇总表生成失败。\n{0}", e.Message);
                return false;
            }
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }
    }
}
