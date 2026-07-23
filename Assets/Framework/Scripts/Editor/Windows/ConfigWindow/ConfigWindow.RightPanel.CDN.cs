/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.CDN.cs
 * author:    Codex
 * created:   2026/7/21
 * descrip:   ConfigWindow 右侧 CDN 内容部署与缓存清理面板（ADR-055 维度接入）
 ***************************************************************/

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        private const float c_CdnLabelWidth = 150f;
        private const float c_CdnSelectButtonWidth = 60f;
        private const float c_CdnOpenButtonWidth = 90f;
        private const string c_CdnEndpointDocsUrl = "https://help.aliyun.com/zh/oss/user-guide/regions-and-endpoints?spm=a2c4g.11186623.0.i30#concept-zt4-cvy-5db";
        private const string c_CloudflareZoneIdDocsUrl = "https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/";
        private const string c_CloudflareTokenDocsUrl = "https://developers.cloudflare.com/fundamentals/api/get-started/create-token/";

        /// <summary>
        /// 绘制 CDN 内容部署面板，字段写入 ConfigMasterSO WorkingCopy，网络操作由 EditorUtil.CDN 执行。
        /// <para>CDN 面板走 WorkingCopy 延迟落盘，对齐矩阵类维度语义：标题与维度 toggle 复用
        /// DrawPanelTitleWithMask（加维分裂 / 减维合并由 DimensionProjector 处理），字段显示值经
        /// DimensionalResolver.ResolveCdn 按当前坐标解析，编辑提交经 CommitCdnField 双分支写入
        /// （IsGlobal 写顶层 CdnDeployment，否则写当前坐标 CdnOverrides 条目）。</para>
        /// </summary>
        private void DrawCdnDeploymentPanel()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (workingSrc == null) return;

            if (workingSrc.CdnDeployment == null)
            {
                workingSrc.CdnDeployment = new CdnDeploymentConfig();
            }

            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                workingSrc.CurrentPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);
            CdnDeploymentConfig resolved = EditorUtil.Config.DimensionalResolver.ResolveCdn(
                workingSrc,
                curCoord.Platform,
                curCoord.Channel,
                curCoord.Mode);

            // 标题 + 内联维度三 toggle + 维度 HelpBox 全套，与 Common / Namespace 等面板一致
            DrawPanelTitleWithMask("CDN 内容分发网络部署", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.Cdn, null);

            // 每帧刷新 SerializedObject（对齐 Namespace / Common 面板）：
            // 维度 toggle 经 OnCdnEnabled/Disabled 直改 C# 层 CdnMask 与 CdnOverrides（绕过 SerializedProperty），
            // 若不每帧 Update() 把 native 最新值同步进 SO 缓存，SO 中 mask 停留旧值 false；
            // 后续任一 ApplyModifiedProperties 触发整树回写 native 时，会用旧 false 覆盖刚勾的 true（clobber），toggle 表现为勾选后复原。
            // CDN 字段控件均为普通 TextField（BeginChangeCheck 实时提交），控件 ID 基于固定 path 不漂移，不受每帧 Update 影响（同 Namespace 面板结论）。
            m_MasterSO.Update();

            // 用途说明：三段式功能概览，置于维度 HelpBox 之后、首个分区标题之前
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 阿里云 OSS 配置：部署目标的 Endpoint / 密钥 / 固定 OSS 路径前缀",
                    "(2) 部署：选择本地目录，上传到「PresetOSSPath + 云端目录后缀」指向的 OSS 位置",
                    "(3) Cloudflare 缓存清理：按缓存路径逐条调用 Purge 接口刷新 CDN 缓存",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);

            DrawCdnSectionTitle(
                "阿里云 OSS 配置",
                () => EditorUtil.Draw.LinkButton(
                    c_CdnEndpointDocsUrl,
                    "查询各地域Endpoint信息"));
            DrawCdnTextRow("Endpoint", resolved.Endpoint, workingSrc, curCoord, (cfg, v) => cfg.Endpoint = v);
            DrawCdnTextRow("AccessKeyID", resolved.AccessKeyID, workingSrc, curCoord, (cfg, v) => cfg.AccessKeyID = v);
            DrawCdnPasswordRow("AccessKeySecret", resolved.AccessKeySecret, workingSrc, curCoord, (cfg, v) => cfg.AccessKeySecret = v);
            DrawCdnTextRow("PresetOSSPath", resolved.PresetOSSPath, workingSrc, curCoord, (cfg, v) => cfg.PresetOSSPath = v);

            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Line();
            EditorUtil.Draw.Space(12f);

            DrawCdnSectionTitle("部署");
            DrawCdnPathPlaceholderHelp();
            DrawCdnLocalDirectoryRow(resolved, workingSrc, curCoord);
            DrawCdnRemoteDirectoryRow(resolved, workingSrc, curCoord);
            DrawCdnWideButton("批量部署到 CDN", m_IsCdnDeploying, OnDeployCdn);

            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Line();
            EditorUtil.Draw.Space(12f);

            DrawCdnSectionTitle("Cloudflare 缓存清理");
            DrawCdnTextRow(
                "Zone ID",
                resolved.ZoneID,
                workingSrc,
                curCoord,
                (cfg, v) => cfg.ZoneID = v,
                () => EditorUtil.Draw.LinkButton(c_CloudflareZoneIdDocsUrl, "查询 Zone ID"));
            DrawCdnPasswordRow(
                "API Token",
                resolved.Token,
                workingSrc,
                curCoord,
                (cfg, v) => cfg.Token = v,
                () => EditorUtil.Draw.LinkButton(c_CloudflareTokenDocsUrl, "创建 API Token"));
            DrawCdnCachePathsRow(resolved, workingSrc, curCoord);
            DrawCdnWideButton("批量清除 CDN 缓存", m_IsCdnPurging, OnPurgeCdnCache);

            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 将单字段编辑按当前维度坐标双分支写入 WorkingCopy：
        /// CdnMask 为 IsGlobal 时写顶层 CdnDeployment 对应字段；否则写当前坐标 CdnOverrides 条目的
        /// Config 对应字段（无条目时经 EnsureCdnOverrideAtCoord 以当前 Resolve 快照新建）。
        /// 写完点亮保存按钮并 SetDirty；CDN 面板走 WorkingCopy 延迟落盘，不即时 SaveAsset。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        /// <param name="value">编辑后的字段值。</param>
        /// <param name="assign">字段赋值动作，作用于目标 CdnDeploymentConfig 实例。</param>
        private void CommitCdnField(
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            string value,
            Action<CdnDeploymentConfig, string> assign)
        {
            if (workingSrc == null || assign == null) return;

            if (workingSrc.CdnMask.IsGlobal)
            {
                if (workingSrc.CdnDeployment == null)
                    workingSrc.CdnDeployment = new CdnDeploymentConfig();
                assign(workingSrc.CdnDeployment, value);
            }
            else
            {
                CdnDeploymentOverride entry = EditorUtil.Config.DimensionProjector.EnsureCdnOverrideAtCoord(workingSrc, curCoord);
                if (entry != null)
                {
                    if (entry.Config == null)
                        entry.Config = new CdnDeploymentConfig();
                    assign(entry.Config, value);
                }
            }
            m_IsDirty = true;
            EditorUtility.SetDirty(workingSrc);
        }

        /// <summary>
        /// 绘制 CDN 面板分区标题。
        /// </summary>
        /// <param name="title">分区标题。</param>
        /// <param name="trailingContent">可选的右侧尾部内容。</param>
        private void DrawCdnSectionTitle(string title, Action trailingContent = null)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(title, m_SectionTitleStyle, false);
                if (trailingContent != null)
                {
                    EditorUtil.Draw.FlexibleSpace();
                    trailingContent();
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制与 Asset 主机服务器 URL 同口径的部署路径占位符说明。
        /// </summary>
        private static void DrawCdnPathPlaceholderHelp()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "本地目录和云端目录支持 {Platform}/{Channel}/{Package}/{Version} 占位符",
                    "{Platform}=当前平台；{Channel}=当前渠道；{Package}=YooAsset 默认资源包名；{Version}=Application.version",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制普通单行 CDN 文本字段；显示值为当前坐标 Resolve 结果，编辑实时提交经 CommitCdnField 双分支写回。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="committedValue">当前坐标生效的已提交值。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        /// <param name="assign">字段赋值动作。</param>
        private void DrawCdnTextRow(
            string label,
            string committedValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            Action<CdnDeploymentConfig, string> assign,
            Action trailingContent = null)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(label, false, GUILayout.Width(c_CdnLabelWidth));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(committedValue, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != committedValue)
                    CommitCdnField(workingSrc, curCoord, edited, assign);
                if (trailingContent != null)
                {
                    EditorUtil.Draw.Space(8f);
                    trailingContent();
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制使用遮罩显示的 CDN 敏感字段；显示值为当前坐标 Resolve 结果，编辑实时提交经 CommitCdnField 双分支写回。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="committedValue">当前坐标生效的已提交真实值。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        /// <param name="assign">字段赋值动作。</param>
        private void DrawCdnPasswordRow(
            string label,
            string committedValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            Action<CdnDeploymentConfig, string> assign,
            Action trailingContent = null)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(label, false, GUILayout.Width(c_CdnLabelWidth));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.PasswordField(committedValue, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != committedValue)
                    CommitCdnField(workingSrc, curCoord, edited, assign);
                if (trailingContent != null)
                {
                    EditorUtil.Draw.Space(8f);
                    trailingContent();
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制本地目录输入、选择和打开文件夹按钮；输入与选择均经 CommitCdnField 按当前坐标写回。
        /// </summary>
        /// <param name="resolved">当前坐标 Resolve 出的整套生效配置。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        private void DrawCdnLocalDirectoryRow(
            CdnDeploymentConfig resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("本地目录", false, GUILayout.Width(c_CdnLabelWidth));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(resolved.LocalDirectory, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != resolved.LocalDirectory)
                    CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.LocalDirectory = v);
                EditorUtil.Draw.Button(
                    "选择",
                    false,
                    () => SelectCdnLocalDirectory(resolved.LocalDirectory, workingSrc, curCoord),
                    GUILayout.Width(c_CdnSelectButtonWidth));
                EditorUtil.Draw.Button(
                    "打开文件夹",
                    false,
                    () => OpenCdnLocalDirectory(
                        resolved.LocalDirectory,
                        curCoord.Platform,
                        curCoord.Channel),
                    GUILayout.Width(c_CdnOpenButtonWidth));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制只读固定 OSS 前缀与可编辑远端后缀；前缀按当前坐标 Resolve 后的 PresetOSSPath 显示，
        /// 后缀编辑经 CommitCdnField 按当前坐标写回。
        /// </summary>
        /// <param name="resolved">当前坐标 Resolve 出的整套生效配置。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        private void DrawCdnRemoteDirectoryRow(
            CdnDeploymentConfig resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("云端目录", false, GUILayout.Width(c_CdnLabelWidth));
                EditorUtil.Draw.DisabledGroup(true, () =>
                    EditorUtil.Draw.TextField(
                        GetCdnPresetDisplay(resolved.PresetOSSPath),
                        false,
                        GUILayout.MinWidth(250f)));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(resolved.RemotePathSuffix, false, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != resolved.RemotePathSuffix)
                    CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.RemotePathSuffix = v);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制缓存路径多行输入框；编辑实时提交经 CommitCdnField 按当前坐标写回。
        /// </summary>
        /// <param name="resolved">当前坐标 Resolve 出的整套生效配置。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        private void DrawCdnCachePathsRow(
            CdnDeploymentConfig resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("缓存路径", false, GUILayout.Width(c_CdnLabelWidth));
                EditorUtil.Draw.Layout.Vertical(() =>
                {
                    EditorUtil.Draw.Label("（多个路径请用英文逗号隔开）", m_DescStyle, false);
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorUtil.Draw.TextArea(
                        resolved.CachePaths ?? string.Empty,
                        false,
                        GUILayout.MinHeight(80f),
                        GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck() && edited != resolved.CachePaths)
                        CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.CachePaths = v);
                }, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制占满内容宽度的 CDN 操作按钮。
        /// </summary>
        /// <param name="label">按钮文字。</param>
        /// <param name="busy">对应操作是否正在执行。</param>
        /// <param name="onClick">点击回调。</param>
        private void DrawCdnWideButton(string label, bool busy, Action onClick)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.DisabledGroup(busy, () =>
                    EditorUtil.Draw.Button(label, false, onClick, GUILayout.ExpandWidth(true)));
                EditorUtil.Draw.Space(16f);
            });
        }

        /// <summary>
        /// 打开同步文件夹选择器，并把项目根相对路径经 CommitCdnField 按当前坐标写回 WorkingCopy。
        /// </summary>
        /// <param name="currentValue">当前坐标生效的本地目录值，作为选择器初始路径。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        private void SelectCdnLocalDirectory(
            string currentValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            string selected = EditorUtil.Draw.Panel.SelectFolder(
                "选择 CDN 部署本地目录",
                EditorUtil.CDN.ResolveEditorPathPlaceholders(
                    currentValue,
                    curCoord.Platform,
                    curCoord.Channel) ?? string.Empty);
            if (string.IsNullOrEmpty(selected)) return;

            GUI.FocusControl(null);
            CommitCdnField(workingSrc, curCoord, selected, (cfg, v) => cfg.LocalDirectory = v);
        }

        /// <summary>
        /// 解析并在系统文件管理器中打开已配置的项目根相对目录。
        /// </summary>
        /// <param name="relativePath">含可选占位符的项目根相对目录。</param>
        /// <param name="platform">当前 ConfigWindow 平台。</param>
        private static void OpenCdnLocalDirectory(
            string relativePath,
            PlatformType platform,
            ChannelType channel)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            string resolvedPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(relativePath, platform, channel);
            string fullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, resolvedPath ?? string.Empty));
            EditorUtil.FileSystem.OpenFolder(fullPath);
        }

        /// <summary>
        /// 为只读固定前缀补齐视觉分隔斜杠，不修改实际序列化值。
        /// </summary>
        /// <param name="preset">PresetOSSPath 原值。</param>
        /// <returns>用于只读显示的固定前缀。</returns>
        private static string GetCdnPresetDisplay(string preset)
        {
            return string.IsNullOrWhiteSpace(preset) ? string.Empty : preset.TrimEnd('/', '\\') + "/";
        }

        /// <summary>
        /// 启动阿里云 OSS 目录部署；重复点击在入口处直接忽略。
        /// </summary>
        private void OnDeployCdn()
        {
            if (m_IsCdnDeploying) return;
            DeployCdnAsync().Forget();
        }

        /// <summary>
        /// 执行阿里云 OSS 目录部署，并保证进度条和忙碌状态在成功或失败后恢复。
        /// </summary>
        private async UniTask DeployCdnAsync()
        {
            m_IsCdnDeploying = true;
            try
            {
                ConfigMasterSO source = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
                if (source == null)
                    throw new InvalidOperationException("未找到 CDN 部署配置。");
                CdnDeploymentConfig config = CreateCdnConfigSnapshot();
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                    ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
                int count = await EditorUtil.CDN.DeployAsync(
                    config,
                    projectRoot,
                    source.CurrentPlatform,
                    source.CurrentChannel,
                    (completed, total, path) => EditorUtility.DisplayProgressBar(
                        "批量部署到 CDN",
                        $"{completed}/{total}  {path}",
                        total > 0 ? completed / (float)total : 0f));
                EditorUtility.DisplayDialog("部署完成", $"已成功上传 {count} 个文件。", "知道了");
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, $"[CDN] 部署失败：{exception.Message}");
                EditorUtility.DisplayDialog("部署失败", exception.Message, "知道了");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_IsCdnDeploying = false;
                Repaint();
            }
        }

        /// <summary>
        /// 启动 Cloudflare 缓存清理；重复点击在入口处直接忽略。
        /// </summary>
        private void OnPurgeCdnCache()
        {
            if (m_IsCdnPurging) return;
            PurgeCdnCacheAsync().Forget();
        }

        /// <summary>
        /// 执行 Cloudflare 缓存清理，并保证进度条和忙碌状态在成功或失败后恢复。
        /// </summary>
        private async UniTask PurgeCdnCacheAsync()
        {
            m_IsCdnPurging = true;
            try
            {
                CdnDeploymentConfig config = CreateCdnConfigSnapshot();
                int count = await EditorUtil.CDN.PurgeAsync(
                    config,
                    (completed, total) => EditorUtility.DisplayProgressBar(
                        "批量清除 CDN 缓存",
                        $"{completed}/{total}",
                        total > 0 ? completed / (float)total : 0f));
                EditorUtility.DisplayDialog("清理完成", $"已成功清理 {count} 条缓存路径。", "知道了");
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, $"[CDN] 清缓存失败：{exception.Message}");
                EditorUtility.DisplayDialog("清理失败", exception.Message, "知道了");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_IsCdnPurging = false;
                Repaint();
            }
        }

        /// <summary>
        /// 按当前维度坐标从 WorkingCopy Resolve 出 CDN 配置快照，避免执行期间继续编辑影响本次请求；
        /// 部署与清缓存操作作用于当前坐标生效的那份配置（IsGlobal 顶层 / 命中 CdnOverrides 条目 / 逐字段回落）。
        /// </summary>
        /// <returns>与当前坐标生效值一致的独立配置快照。</returns>
        private CdnDeploymentConfig CreateCdnConfigSnapshot()
        {
            ConfigMasterSO source = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (source == null)
                throw new InvalidOperationException("未找到 CDN 部署配置。");
            return EditorUtil.Config.DimensionalResolver.ResolveCdn(
                source,
                source.CurrentPlatform,
                source.CurrentChannel,
                source.CurrentDevelopMode);
        }
    }
}
