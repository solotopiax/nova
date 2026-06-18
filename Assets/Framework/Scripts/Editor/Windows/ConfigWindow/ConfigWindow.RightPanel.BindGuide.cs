/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.BindGuide.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   ConfigWindow 右侧面板绑定引导卡片（m_Master == null 时展示）
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制激活 Master 绑定引导卡片；在 m_Master 为 null 时代替常规面板显示，
        /// 提供「浏览选择 ConfigMaster」与「新建 ConfigMaster」两个操作入口。
        /// </summary>
        private void DrawBindGuide()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "未检测到激活的 ConfigMaster。" }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(4f);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1) 浏览选择已存在的 ConfigMaster.asset，完成绑定后窗口立即刷新。",
                        "(2) 或新建一份 ConfigMaster.asset，推荐放入 Assets/Samples/<DemoName>/Editor/。",
                    }, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(8f);
                    EditorUtil.Draw.Button("浏览选择 ConfigMaster", false, BrowseAndBindConfigMaster);
                    EditorUtil.Draw.Space(4f);
                    EditorUtil.Draw.Button("新建 ConfigMaster", false, CreateAndBindConfigMaster);
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(16f);
            });
        }

        /// <summary>
        /// 打开文件浏览对话框，选择已有的 ConfigMaster.asset 并绑定为激活 Master；
        /// 绑定成功后注入 YooAsset 配置并刷新窗口状态。
        /// </summary>
        private void BrowseAndBindConfigMaster()
        {
            string absolute = EditorUtility.OpenFilePanel("选择 ConfigMaster.asset", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(absolute)) return;

            string relative = ToProjectRelativePath(absolute);
            if (string.IsNullOrEmpty(relative)) return;

            ConfigMasterSO loaded = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(relative);
            if (loaded == null)
            {
                Log.Warning(LogTag.Editor, "[ConfigWindow.BindGuide] 路径下未找到 ConfigMasterSO：{0}", relative);
                return;
            }

            EditorUtil.Config.WorkspaceActive.Set(loaded);
            BindMaster(loaded);
        }

        /// <summary>
        /// 打开"在 Assets 内保存文件"对话框，新建 ConfigMaster.asset 并绑定为激活 Master；
        /// 创建成功后注入 YooAsset 配置并刷新窗口状态。
        /// </summary>
        private void CreateAndBindConfigMaster()
        {
            string relative = EditorUtility.SaveFilePanelInProject(
                "新建 ConfigMaster.asset",
                "ConfigMaster",
                "asset",
                "请选择保存位置（推荐 Assets/Samples/<DemoName>/Editor/）");
            if (string.IsNullOrEmpty(relative)) return;

            ConfigMasterSO created = CreateInstance<ConfigMasterSO>();
            AssetDatabase.CreateAsset(created, relative);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ConfigMasterSO loaded = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(relative);
            if (loaded == null)
            {
                Log.Warning(LogTag.Editor, "[ConfigWindow.BindGuide] 新建后未能加载 ConfigMasterSO：{0}", relative);
                return;
            }

            EditorUtil.Config.WorkspaceActive.Set(loaded);
            BindMaster(loaded);
        }

        /// <summary>
        /// 将指定 ConfigMasterSO 绑定到窗口内存状态：
        /// 初始化 SerializedObject、同步 EnumGrid、刷新 Plugin 缓存、注入 YooAsset、触发重绘。
        /// </summary>
        /// <param name="master">已确认非 null 的 ConfigMasterSO 实例。</param>
        private void BindMaster(ConfigMasterSO master)
        {
            m_Master = master;
            m_MasterSO = new SerializedObject(m_Master);
            EditorUtil.Config.StructureGuard.SyncEnumGrid(m_Master);
            RefreshPluginCache();
            m_LastKnownChannel = m_Master.CurrentChannel;
            EditorUtil.Config.YooAssetInjector.Inject(m_Master);
            Repaint();
        }
    }
}
