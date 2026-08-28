/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.Methods.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   Nova 全局环境配置窗口总调度（OnEnable/OnDisable/OnGUI/DrawBody）
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 窗口启用时的初始化：从 WorkspaceActive 读取激活 Master、克隆 WorkingCopy、绑 SerializedObject、
        /// 补 Entry 格子、扫 Plugin 类型、注入 YooAsset 配置、订阅场景切换事件、跑 Luban 检测、弹缺失引用清理对话。
        /// </summary>
        private void OnEnable()
        {
            EnsureEditingPlatform();
            bool reconciled = EditorUtil.Config.WorkspaceActive.ReconcileScene(EditorSceneManager.GetActiveScene().path);
            m_Master = reconciled ? EditorUtil.Config.WorkspaceActive.Get() : null;
            if (m_Master != null)
            {
                RebuildWorkingCopy();
                EditorUtil.Config.StructureGuard.SyncEnumGrid(m_Master);
                RefreshPluginCache();
                m_LastKnownChannel = m_Master.CurrentChannel;
                EditorUtil.Config.YooAssetInjector.Inject(m_Master);
            }
            EditorSceneManager.sceneOpened -= OnSceneOpenedRefresh;
            EditorSceneManager.sceneOpened += OnSceneOpenedRefresh;
            RunLubanCheck();
            RunPython3Check();
            RunHybridCLRCheck();
            PromptMissingRefsIfAny();
        }

        /// <summary>
        /// 窗口关闭时：销毁 WorkingCopy 防域重载泄漏，注销场景切换监听。
        /// </summary>
        private void OnDisable()
        {
            DestroyWorkingCopy();
            EditorSceneManager.sceneOpened -= OnSceneOpenedRefresh;
        }

        /// <summary>
        /// 绘制窗口界面；Play Mode 下整体禁用并在顶部提示。
        /// 每帧同步 hasUnsavedChanges / saveChangesMessage，使关窗弹框随脏状态正确响应。
        /// </summary>
        private void OnGUI()
        {
            // hasUnsavedChanges / saveChangesMessage 是 EditorWindow 可写普通属性，每帧同步以驱动关窗弹框
            hasUnsavedChanges = m_IsDirty;
            saveChangesMessage = "配置存在未保存的修改，是否保存？";
            EnsureStyles();
            EditorUtil.Draw.DisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode, DrawBody);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "请退出 Play Mode 后编辑。" }, false);
            }
        }

        /// <summary>
        /// 顶栏 + 左树 + 右面板 的三段式布局，最顶部绘制主标题行。
        /// </summary>
        private void DrawBody()
        {
            DrawMainTitle();
            DrawTopBar();
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                DrawLeftTree();
                DrawRightPanel();
            });
            ApplyPendingCoordSwitch();
            PollChannelChangeForRepaint();
        }

        /// <summary>
        /// 绘制窗口主标题行（居中加粗，上下各 8px 空行，底部细分隔线）。
        /// </summary>
        private void DrawMainTitle()
        {
            // 保险调用：防止域重载等边缘路径绕过 OnGUI 顶层 EnsureStyles 的情况
            EnsureStyles();
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Label("Config 全局配置中心", m_MainTitleStyle, false);
                EditorUtil.Draw.FlexibleSpace();
            });
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 应用延迟的坐标切换：在 DrawRightPanel 绘制完成后调用，此时当前编辑的字段已在旧坐标格子下完成失焦提交，
        /// 再写入新的平台、渠道与模式；编辑平台仅保存在窗口状态中，不写入 WorkingCopy 或 ConfigMasterSO。
        /// </summary>
        private void ApplyPendingCoordSwitch()
        {
            if (!m_HasPendingCoordSwitch) return;
            m_HasPendingCoordSwitch = false;
            if (m_WorkingCopy == null) return;
            m_EditingPlatform = m_PendingPlatform;
            m_WorkingCopy.CurrentChannel = m_PendingChannel;
            m_WorkingCopy.CurrentDevelopMode = m_PendingDevelopMode;
            m_LastKnownChannel = m_PendingChannel;
            Repaint();
        }

        /// <summary>
        /// 确保窗口拥有有效的编辑平台；首次打开优先使用 Unity Active BuildTarget，未映射时回退 Android。
        /// </summary>
        private void EnsureEditingPlatform()
        {
            if (m_EditingPlatform != PlatformType.None) return;
            PlatformType activePlatform = EditorUtil.Config.ActivePlatform.Current;
            m_EditingPlatform = activePlatform != PlatformType.None ? activePlatform : PlatformType.Android;
        }

        /// <summary>
        /// 判断当前编辑平台是否与 Unity Active BuildTarget 对应平台一致。
        /// </summary>
        /// <returns>两者均有效且一致时返回 true。</returns>
        private bool IsEditingPlatformAligned()
        {
            return IsPlatformAligned(m_EditingPlatform, EditorUtil.Config.ActivePlatform.Current);
        }

        /// <summary>
        /// 比较显式编辑平台与活动构建平台，用于统一导出和 YooAsset 生效门禁。
        /// </summary>
        /// <param name="editingPlatform">ConfigWindow 当前编辑平台。</param>
        /// <param name="activePlatform">Unity Active BuildTarget 对应平台。</param>
        /// <returns>两者均有效且一致时返回 true。</returns>
        private static bool IsPlatformAligned(PlatformType editingPlatform, PlatformType activePlatform)
        {
            return editingPlatform != PlatformType.None && editingPlatform == activePlatform;
        }

        /// <summary>
        /// 轮询 CurrentChannel 变化触发 Repaint，保证 Inspector 修改也能被窗口感知。
        /// </summary>
        private void PollChannelChangeForRepaint()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (workingSrc != null && workingSrc.CurrentChannel != m_LastKnownChannel)
            {
                m_LastKnownChannel = workingSrc.CurrentChannel;
                Repaint();
            }
        }

        /// <summary>
        /// 刷新 SerializeReference Plugin 类型缓存及 Kit Config 类型缓存。
        /// </summary>
        private void RefreshPluginCache()
        {
            m_PluginTypeCache = EditorUtil.Config.SDKPluginScanner.ScanAll();
            m_KitTypeCache = EditorUtil.Config.KitConfigScanner.ScanAll();
        }

        /// <summary>
        /// 跑一次 Luban 环境检测并缓存结果。
        /// </summary>
        private void RunLubanCheck()
        {
            m_LubanCheckResult = EditorUtil.Environment.LubanChecker.Check();
        }

        /// <summary>
        /// 跑一次 Python3 环境检测并缓存结果（结果来自 SessionState 缓存，不阻塞）。
        /// </summary>
        private void RunPython3Check()
        {
            m_Python3CheckResult = EditorUtil.Environment.Python3Checker.Check();
        }

        /// <summary>
        /// 跑一次 HybridCLR 环境检测并缓存结果（结果来自 SessionState 缓存，不阻塞）。
        /// </summary>
        private void RunHybridCLRCheck()
        {
            m_HybridCLRCheckResult = EditorUtil.Environment.HybridCLRChecker.Check();
        }

        /// <summary>
        /// 场景切换回调（仅响应 Single 加载模式）；切换后重新从 WorkspaceActive 读取激活 Master，
        /// 若 Master 发生变更则销毁旧 WorkingCopy、重建新 WorkingCopy。YooAsset 注入由 SceneRoute 统一完成。
        /// </summary>
        /// <param name="scene">新打开的场景。</param>
        /// <param name="mode">打开模式。</param>
        private void OnSceneOpenedRefresh(Scene scene, OpenSceneMode mode)
        {
            if (mode != OpenSceneMode.Single) return;
            if (!EditorUtil.Config.WorkspaceActive.ReconcileScene(scene.path)) return;
            ConfigMasterSO fresh = EditorUtil.Config.WorkspaceActive.Get();
            if (ReferenceEquals(fresh, m_Master)) return;
            ResolveDirtyForSceneChange();
            m_Master = fresh;
            DestroyWorkingCopy();
            if (m_Master != null)
            {
                RebuildWorkingCopy();
                RefreshPluginCache();
                EditorUtil.Config.StructureGuard.SyncEnumGrid(m_Master);
            }
            m_IsDirty = false;
            Repaint();
        }

        /// <summary>
        /// 基于当前 m_Master 克隆 WorkingCopy、绑定 SerializedObject；
        /// 调用前必须确保 m_Master != null。调用前应先 DestroyWorkingCopy 销毁旧副本。
        /// </summary>
        private void RebuildWorkingCopy()
        {
            m_WorkingCopy = UnityEngine.Object.Instantiate(m_Master);
            m_WorkingCopy.hideFlags = UnityEngine.HideFlags.DontSave;
            m_MasterSO = new SerializedObject(m_WorkingCopy);
            m_IsDirty = false;
        }

        /// <summary>
        /// 销毁 WorkingCopy 及其绑定的 SerializedObject；安全处理 null 情况。
        /// <para>同步清空依赖 m_MasterSO 的 ReorderableList 缓存与折叠状态，</para>
        /// <para>避免下一帧 GUI 访问已 Dispose 的 SerializedProperty 抛 NullReferenceException。</para>
        /// </summary>
        private void DestroyWorkingCopy()
        {
            m_HybridCLRAotMetadataDllsList = null;
            m_HybridCLRStartupGameDllsList = null;
            m_HybridCLRRunningGameDllsList = null;
            m_AotDllFoldouts.Clear();
            m_StartupGameDllFoldouts.Clear();
            m_RunningGameDllFoldouts.Clear();

            if (m_MasterSO != null)
            {
                m_MasterSO.Dispose();
                m_MasterSO = null;
            }
            if (m_WorkingCopy != null)
            {
                UnityEngine.Object.DestroyImmediate(m_WorkingCopy);
                m_WorkingCopy = null;
            }
        }

        /// <summary>
        /// 将 WorkingCopy 写回真实资产并落盘；保存后重建 WorkingCopy 保持后续编辑隔离。
        /// </summary>
        private void CommitWorkingCopyToAsset()
        {
            if (m_Master == null || m_WorkingCopy == null) return;
            EditorUtility.CopySerialized(m_WorkingCopy, m_Master);
            // CopySerialized 会连带复制 WorkingCopy 的 (Clone) 后缀名，此处用资产文件名还原
            string assetPath = AssetDatabase.GetAssetPath(m_Master);
            if (!string.IsNullOrEmpty(assetPath))
                m_Master.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            EditorUtility.SetDirty(m_Master);
            AssetDatabase.SaveAssets();
            m_IsDirty = false;
            // 保存后重建 WorkingCopy，保持后续编辑基于最新已落盘状态
            RebuildWorkingCopy();
            EditorUtil.Config.Events.NotifyActiveConfigMasterSaved(m_Master);
        }

        /// <summary>
        /// 延迟初始化自定义 GUIStyle。每个 style 独立判空，防止域重载后 native 对象失效但 C# 引用非 null 的情况。
        /// </summary>
        private void EnsureStyles()
        {
            // 注意：域重载后 GUIStyle 的 native 层会失效，不能用单一守卫字段统一拦截，
            // 必须每个 style 独立 null 检查，保证任意入口进来都能安全重建。
            if (m_MainTitleStyle == null)
            {
                m_MainTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            if (m_TitleStyle == null)
            {
                m_TitleStyle = new GUIStyle(EditorStyles.whiteLargeLabel)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                };
            }

            if (m_SectionTitleStyle == null)
            {
                m_SectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.7f, 0.85f, 1f) }
                };
            }

            if (m_StatusReadyStyle == null)
            {
                m_StatusReadyStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.4f, 0.85f, 0.4f) },
                    fontSize = 13
                };
            }

            if (m_StatusErrorStyle == null)
            {
                m_StatusErrorStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = new Color(0.95f, 0.4f, 0.35f) },
                    fontSize = 13
                };
            }

            if (m_CodeStyle == null)
            {
                m_CodeStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = false,
                    font = Font.CreateDynamicFontFromOSFont("Menlo", 11),
                    normal = { textColor = new Color(0.8f, 0.95f, 0.7f) }
                };
            }

            if (m_DescStyle == null)
            {
                m_DescStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
                };
            }
        }
    }
}
