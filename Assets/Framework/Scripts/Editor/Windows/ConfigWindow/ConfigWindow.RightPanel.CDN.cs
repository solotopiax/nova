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
using System.Collections.Generic;
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
        private const float c_CdnWhitelistLabelWidth = 210f;
        private const float c_CdnSelectButtonWidth = 60f;
        private const float c_CdnCreateButtonWidth = 60f;
        private const float c_CdnOpenButtonWidth = 90f;
        private const string c_AppDownloadRulesTemplateFileName = "AppDownloadRulesTemplate.json";
        private const string c_AppDownloadRulesFileName = "AppDownloadRules.json";
        private const string c_CdnEndpointDocsUrl = "https://help.aliyun.com/zh/oss/user-guide/regions-and-endpoints?spm=a2c4g.11186623.0.i30#concept-zt4-cvy-5db";
        private const string c_CloudflareZoneIdDocsUrl = "https://developers.cloudflare.com/fundamentals/account/find-account-and-zone-ids/";
        private const string c_CloudflareTokenDocsUrl = "https://developers.cloudflare.com/fundamentals/api/get-started/create-token/";

        /// <summary>
        /// 绘制 CDN 内容部署面板，字段写入 ConfigMasterSO WorkingCopy，网络操作由 EditorUtil.CDN 执行。
        /// <para>CDN 面板走 WorkingCopy 延迟落盘，对齐矩阵类维度语义：标题与维度 toggle 复用
        /// DrawPanelTitleWithMask（加维分裂 / 减维合并由 DimensionProjector 处理），字段显示值经
        /// DimensionalResolver.ResolveCDNEditorConfigs 按当前坐标解析，编辑提交经 CommitCdnField 双分支写入
        /// （IsGlobal 写顶层 CDNEditorConfigs，否则写当前坐标 CDNEditorConfigsOverrides 条目）。</para>
        /// </summary>
        private void DrawCdnDeploymentPanel()
        {
            ConfigMasterSO workingSrc = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (workingSrc == null) return;

            if (workingSrc.CDNEditorConfigs == null)
            {
                workingSrc.CDNEditorConfigs = new CDNEditorConfigs();
            }

            EditorUtil.Config.DimensionProjector.Coord curCoord = new(
                workingSrc.CurrentPlatform,
                workingSrc.CurrentChannel,
                workingSrc.CurrentDevelopMode);
            CDNEditorConfigs resolved = EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
                workingSrc,
                curCoord.Platform,
                curCoord.Channel,
                curCoord.Mode);

            // 标题 + 内联维度三 toggle + 维度 HelpBox 全套，与 Common / Namespace 等面板一致
            DrawPanelTitleWithMask("CDN 内容分发网络部署", workingSrc, EditorUtil.Config.DimensionProjector.PanelKind.CDNEditorConfigs, null);

            // 每帧刷新 SerializedObject（对齐 Namespace / Common 面板）：
            // 维度 toggle 经 OnCdnEnabled/Disabled 直改 C# 层 CDNEditorConfigsMask 与 CDNEditorConfigsOverrides（绕过 SerializedProperty），
            // 若不每帧 Update() 把 native 最新值同步进 SO 缓存，SO 中 mask 停留旧值 false；
            // 后续任一 ApplyModifiedProperties 触发整树回写 native 时，会用旧 false 覆盖刚勾的 true（clobber），toggle 表现为勾选后复原。
            // CDN 字段控件均为普通 TextField（BeginChangeCheck 实时提交），控件 ID 基于固定 path 不漂移，不受每帧 Update 影响（同 Namespace 面板结论）。
            m_MasterSO.Update();

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

            DrawCdnSectionTitle("资源部署");
            DrawCdnVersionCheckTemplateRow();
            DrawCdnVersionCheckLocalFileRow(resolved, workingSrc, curCoord);
            DrawCdnVersionCheckRemoteFileRow(resolved, workingSrc, curCoord);
            DrawCdnVersionCheckPathHelp();
            DrawCdnHotfixAutoLinkToggleRow(resolved, workingSrc, curCoord);
            DrawCdnLocalDirectoryRow(resolved, workingSrc, curCoord);
            DrawCdnRemoteDirectoryRow(resolved, workingSrc, curCoord);
            DrawCdnHotfixResourcePathHelp();
            m_CleanCdnRemoteBeforeDeploy = DrawCdnCleanRemoteBeforeDeploy(m_CleanCdnRemoteBeforeDeploy, c_CdnLabelWidth);
            DrawCdnWideButton("批量部署到 CDN", m_IsCdnDeploying, OnDeployCdn);

            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Line();
            EditorUtil.Draw.Space(12f);

            DrawCdnSectionTitle("白名单部署");
            DrawCdnAssetCheckWhitelistDeviceIDs(resolved, workingSrc, curCoord);
            DrawCdnAssetCheckRemoteDirectoryRow(
                "配置文件-云端文件位置",
                resolved.PresetOSSPath,
                resolved.AssetCheckWhitelistRemoteFilePath,
                workingSrc,
                curCoord,
                (cfg, value) => cfg.AssetCheckWhitelistRemoteFilePath = value);
            DrawCdnAssetCheckAutoLinkToggleRow(resolved, workingSrc, curCoord);
            ResolveCdnAssetCheckVersionFileDisplayPaths(
                resolved,
                workingSrc,
                curCoord,
                out string assetCheckBytesDisplayPath,
                out string assetCheckHashDisplayPath,
                out string assetCheckVersionDisplayPath,
                out string assetCheckVersionResolveError,
                out string bytesError,
                out string hashError,
                out string versionError);
            DrawCdnAssetCheckLocalFileRow(
                "版本文件(.bytes)-本地文件位置",
                resolved.AssetCheckManifestBytesLocalFilePath,
                assetCheckBytesDisplayPath,
                ".bytes",
                resolved.AutoLinkLatestAssetCheckVersionFiles,
                true,
                !string.IsNullOrEmpty(assetCheckVersionResolveError) || !string.IsNullOrEmpty(bytesError),
                workingSrc,
                curCoord,
                (cfg, value) => cfg.AssetCheckManifestBytesLocalFilePath = value);
            if (!string.IsNullOrEmpty(bytesError))
                DrawCdnPathErrorHelp(bytesError, c_CdnWhitelistLabelWidth);
            DrawCdnAssetCheckLocalFileRow(
                "版本文件(.hash)-本地文件位置",
                resolved.AssetCheckManifestHashLocalFilePath,
                assetCheckHashDisplayPath,
                ".hash",
                resolved.AutoLinkLatestAssetCheckVersionFiles,
                !resolved.AutoLinkLatestAssetCheckVersionFiles,
                !string.IsNullOrEmpty(assetCheckVersionResolveError) || !string.IsNullOrEmpty(hashError),
                workingSrc,
                curCoord,
                (cfg, value) => cfg.AssetCheckManifestHashLocalFilePath = value);
            if (!string.IsNullOrEmpty(hashError))
                DrawCdnPathErrorHelp(hashError, c_CdnWhitelistLabelWidth);
            DrawCdnAssetCheckLocalFileRow(
                "版本文件(.version)-本地文件位置",
                resolved.AssetCheckPackageVersionLocalFilePath,
                assetCheckVersionDisplayPath,
                ".version",
                resolved.AutoLinkLatestAssetCheckVersionFiles,
                !resolved.AutoLinkLatestAssetCheckVersionFiles,
                !string.IsNullOrEmpty(assetCheckVersionResolveError) || !string.IsNullOrEmpty(versionError),
                workingSrc,
                curCoord,
                (cfg, value) => cfg.AssetCheckPackageVersionLocalFilePath = value);
            if (!string.IsNullOrEmpty(versionError))
                DrawCdnPathErrorHelp(versionError, c_CdnWhitelistLabelWidth);
            if (!string.IsNullOrEmpty(assetCheckVersionResolveError))
                DrawCdnPathErrorHelp(assetCheckVersionResolveError, c_CdnWhitelistLabelWidth);
            DrawCdnAssetCheckRemoteDirectoryRow(
                "版本文件-云端目录位置",
                resolved.PresetOSSPath,
                resolved.AssetCheckVersionRemoteDirectory,
                workingSrc,
                curCoord,
                (cfg, value) => cfg.AssetCheckVersionRemoteDirectory = value);
            DrawCdnAssetCheckWhitelistHelp();
            m_CleanCdnWhitelistRemoteBeforeDeploy = DrawCdnCleanRemoteBeforeDeploy(m_CleanCdnWhitelistRemoteBeforeDeploy, c_CdnWhitelistLabelWidth);
            DrawCdnWideButton("批量部署到CDN", m_IsCdnWhitelistDeploying, OnDeployCdnWhitelist);

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
            DrawCdnCloudflareHelp();
            DrawCdnWideButton("批量清除 CDN 缓存", m_IsCdnPurging, OnPurgeCdnCache);

            EditorUtil.Draw.Space(16f);
        }

        /// <summary>
        /// 绘制 App 大版本更新规则模板的只读位置与打开文件夹入口。
        /// </summary>
        private static void DrawCdnVersionCheckTemplateRow()
        {
            string templatePath = EditorUtil.FileSystem.ResolveTemplatePath(c_AppDownloadRulesTemplateFileName);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("版本检查-模板文件位置", false, GUILayout.Width(c_CdnLabelWidth));
                EditorUtil.Draw.DisabledGroup(true, () =>
                    EditorUtil.Draw.TextField(templatePath, false, GUILayout.ExpandWidth(true)));
                EditorUtil.Draw.Button(
                    "打开文件夹",
                    false,
                    () => OpenCdnVersionCheckTemplateDirectory(templatePath),
                    GUILayout.Width(c_CdnOpenButtonWidth));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 将单字段编辑按当前维度坐标双分支写入 WorkingCopy：
        /// CDNEditorConfigsMask 为 IsGlobal 时写顶层 CDNEditorConfigs 对应字段；否则写当前坐标 CDNEditorConfigsOverrides 条目的
        /// Config 对应字段（无条目时经 EnsureCDNEditorConfigsOverrideAtCoord 以当前 Resolve 快照新建）。
        /// 写完点亮保存按钮并 SetDirty；CDN 面板走 WorkingCopy 延迟落盘，不即时 SaveAsset。
        /// </summary>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        /// <param name="value">编辑后的字段值。</param>
        /// <param name="assign">字段赋值动作，作用于目标 CDNEditorConfigs 实例。</param>
        private void CommitCdnField(
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            string value,
            Action<CDNEditorConfigs, string> assign)
        {
            if (workingSrc == null || assign == null) return;

            if (workingSrc.CDNEditorConfigsMask.IsGlobal)
            {
                if (workingSrc.CDNEditorConfigs == null)
                    workingSrc.CDNEditorConfigs = new CDNEditorConfigs();
                assign(workingSrc.CDNEditorConfigs, value);
            }
            else
            {
                CDNEditorConfigsOverride entry = EditorUtil.Config.DimensionProjector.EnsureCDNEditorConfigsOverrideAtCoord(workingSrc, curCoord);
                if (entry != null)
                {
                    if (entry.Config == null)
                        entry.Config = new CDNEditorConfigs();
                    assign(entry.Config, value);
                }
            }
            m_IsDirty = true;
            EditorUtility.SetDirty(workingSrc);
        }

        /// <summary>
        /// 将布尔字段按当前维度坐标写入 WorkingCopy。
        /// </summary>
        private void CommitCdnBoolField(
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            bool value,
            Action<CDNEditorConfigs, bool> assign)
        {
            if (workingSrc == null || assign == null) return;

            if (workingSrc.CDNEditorConfigsMask.IsGlobal)
            {
                workingSrc.CDNEditorConfigs ??= new CDNEditorConfigs();
                assign(workingSrc.CDNEditorConfigs, value);
            }
            else
            {
                CDNEditorConfigsOverride entry = EditorUtil.Config.DimensionProjector.EnsureCDNEditorConfigsOverrideAtCoord(
                    workingSrc,
                    curCoord);
                if (entry != null)
                {
                    entry.Config ??= new CDNEditorConfigs();
                    assign(entry.Config, value);
                }
            }

            m_IsDirty = true;
            EditorUtility.SetDirty(workingSrc);
        }

        /// <summary>
        /// 将白名单设备 ID 字符串数组按当前维度坐标写入 WorkingCopy，并保持列表引用互相独立。
        /// </summary>
        private void CommitCdnWhitelistDeviceIDs(
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            IReadOnlyList<string> values)
        {
            List<string> snapshot = values != null ? new List<string>(values) : new List<string>();
            if (workingSrc.CDNEditorConfigsMask.IsGlobal)
            {
                workingSrc.CDNEditorConfigs ??= new CDNEditorConfigs();
                workingSrc.CDNEditorConfigs.AssetCheckWhitelistDeviceIDs = snapshot;
            }
            else
            {
                CDNEditorConfigsOverride entry = EditorUtil.Config.DimensionProjector.EnsureCDNEditorConfigsOverrideAtCoord(workingSrc, curCoord);
                if (entry != null)
                {
                    entry.Config ??= new CDNEditorConfigs();
                    entry.Config.AssetCheckWhitelistDeviceIDs = snapshot;
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
        /// 绘制版本检查规则文件说明；位置固定在对应两个配置项之后。
        /// </summary>
        private static void DrawCdnVersionCheckPathHelp()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 用于配置提示用户大版本更新的规则文件，应用每次启动都会从云端拉取",
                    "(2) 本地文件会上传到「PresetOSSPath + 云端文件位置」",
                    "(3) 该配置文件与热更新版本检测无关，请勿混淆",
                    "(4) 本地文件位置和云端文件位置支持 {Platform}/{Channel}/{Package}/{Version} 占位符",
                    "(5) {Platform}=当前平台；{Channel}=当前渠道；{Package}=YooAsset 默认资源包名；{Version}=Application.version",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制热更资源目录说明；位置固定在对应两个配置项之后。
        /// </summary>
        private static void DrawCdnHotfixResourcePathHelp()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 用于批量部署热更新资源：本地目录下的文件会上传到「PresetOSSPath + 云端目录位置」",
                    "(2) 本地目录位置和云端目录位置支持 {Platform}/{Channel}/{Package}/{Version} 占位符",
                    "(3) {Platform}=当前平台；{Channel}=当前渠道；{Package}=YooAsset 默认资源包名；{Version}=Application.version",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制白名单部署用途、文件组成与远端目录说明。
        /// </summary>
        private static void DrawCdnAssetCheckWhitelistHelp()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 用于部署启动资源校验白名单：命中设备会改用本目录中的三个 YooAsset 版本文件完成版本检查",
                    "(2) 设备 ID 将去除空项、首尾空白并去重，生成 VersionsCheckWhiteList.json 字符串数组",
                    "(3) 配置文件上传到「PresetOSSPath + 配置文件云端文件位置」；文件位置为空或非法时不上传配置文件",
                    "(4) .bytes/.hash/.version 文件上传到「PresetOSSPath + 版本文件云端目录位置」",
                    "(5) 本地文件位置和云端目录位置支持 {Platform}/{Channel}/{Package}/{Version} 占位符",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制 Cloudflare 缓存清理的用途、分批规则与权限要求。
        /// </summary>
        private static void DrawCdnCloudflareHelp()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 用于清除 Cloudflare CDN 已缓存的指定 URL，使更新后的云端资源尽快生效",
                    "(2) 多个缓存路径支持使用英文逗号、英文分号或换行分隔，重复路径会自动去除",
                    "(3) 请求按输入顺序发送，每批最多 100 条；任一批失败时立即停止后续请求",
                    "(4) API Token 需要目标 Zone 的 Zone -> Cache Purge 权限",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制与当前分区字段列对齐的清理开关，并在值列下方说明执行顺序、范围和失败行为。
        /// </summary>
        private static bool DrawCdnCleanRemoteBeforeDeploy(bool value, float labelWidth)
        {
            bool newValue = value;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("清理云端文件和目录", false, GUILayout.Width(labelWidth));
                newValue = EditorUtil.Draw.Toggle(value, GUILayout.Width(18f));
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 默认关闭；勾选后会在上传前清理本次部署目标",
                    "(2) 只清理本次部署涉及的文件和目录，不会清空整个 PresetOSSPath",
                    "(3) 清理失败时立即停止，不继续上传",
                }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
            return newValue;
        }

        /// <summary>
        /// 以可增删的字符串数组绘制 VersionsCheckWhiteList.json 设备 ID 内容。
        /// </summary>
        private void DrawCdnAssetCheckWhitelistDeviceIDs(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            List<string> values = resolved.AssetCheckWhitelistDeviceIDs != null
                ? new List<string>(resolved.AssetCheckWhitelistDeviceIDs)
                : new List<string>();
            bool changed = false;
            int removeIndex = -1;

            if (values.Count == 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.Label("配置文件-设备ID（字符串数组）", false, GUILayout.Width(c_CdnWhitelistLabelWidth));
                    EditorUtil.Draw.Label("暂无设备 ID", m_DescStyle, false);
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(4f);
            }

            for (int index = 0; index < values.Count; index++)
            {
                int capturedIndex = index;
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.Label(
                        capturedIndex == 0 ? "配置文件-设备ID（字符串数组）" : string.Empty,
                        false,
                        GUILayout.Width(c_CdnWhitelistLabelWidth));
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorUtil.Draw.TextField(values[capturedIndex] ?? string.Empty, false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck())
                    {
                        values[capturedIndex] = edited;
                        changed = true;
                    }
                    EditorUtil.Draw.Button(
                        "删除",
                        false,
                        false,
                        () => removeIndex = capturedIndex,
                        GUILayout.Width(c_CdnSelectButtonWidth));
                    EditorUtil.Draw.Space(16f);
                });
                EditorUtil.Draw.Space(4f);
            }

            if (removeIndex >= 0)
            {
                values.RemoveAt(removeIndex);
                changed = true;
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f + c_CdnWhitelistLabelWidth);
                EditorUtil.Draw.Button(
                    "添加设备 ID",
                    false,
                    () =>
                    {
                        values.Add(string.Empty);
                        CommitCdnWhitelistDeviceIDs(workingSrc, curCoord, values);
                        GUI.FocusControl(null);
                    },
                    GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);

            if (changed)
                CommitCdnWhitelistDeviceIDs(workingSrc, curCoord, values);
        }

        /// <summary>
        /// 在热更资源路径上方绘制独占一行的自动关联开关。
        /// </summary>
        private void DrawCdnHotfixAutoLinkToggleRow(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            DrawCdnAutoLinkToggleRow(
                resolved.AutoLinkLatestVersion,
                workingSrc,
                curCoord,
                c_CdnLabelWidth,
                (cfg, value) => cfg.AutoLinkLatestVersion = value,
                new[]
                {
                    "(1) 开启后会从当前目录或其上级包目录中，自动选择最后生成的完整 YooAsset 版本目录",
                    "(2) 最新版本按 .version 文件最后写入时间判断，不比较目录名、日期或语义版本号",
                    "(3) 文件命名使用当前 ConfigMaster 当前维度的 YooAsset 配置，不读取 YooAssetSettings.asset 的实际值",
                });
        }

        /// <summary>
        /// 在白名单三个版本文件上方绘制独占一行的自动关联开关。
        /// </summary>
        private void DrawCdnAssetCheckAutoLinkToggleRow(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            DrawCdnAutoLinkToggleRow(
                resolved.AutoLinkLatestAssetCheckVersionFiles,
                workingSrc,
                curCoord,
                c_CdnWhitelistLabelWidth,
                (cfg, value) => cfg.AutoLinkLatestAssetCheckVersionFiles = value,
                new[]
                {
                    "(1) 开启后以版本文件(.bytes)的配置目录为锚点，自动关联匹配的 .bytes/.hash/.version",
                    "(2) 最新版本按 .version 文件最后写入时间判断，三个路径会自动刷新并设为只读",
                    "(3) 文件命名使用当前 ConfigMaster 当前维度的 YooAsset 配置，不读取 YooAssetSettings.asset 的实际值",
                });
        }

        /// <summary>
        /// 绘制与字段标题左边缘对齐的自动关联开关。
        /// </summary>
        private void DrawCdnAutoLinkToggleRow(
            bool committedValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            float labelWidth,
            Action<CDNEditorConfigs, bool> assign,
            string[] helpMessages)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("自动关联最新版本", false, GUILayout.Width(labelWidth));
                bool edited = EditorUtil.Draw.Toggle(committedValue, GUILayout.Width(18f));
                if (edited != committedValue)
                {
                    GUI.FocusControl(null);
                    CommitCdnBoolField(workingSrc, curCoord, edited, assign);
                }
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
            DrawCdnAutoLinkHelp(helpMessages);
        }

        /// <summary>
        /// 在自动关联开关下方绘制与字段整行左边缘对齐的说明。
        /// </summary>
        private static void DrawCdnAutoLinkHelp(string[] messages)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(
                    MessageType.Info,
                    messages,
                    false,
                    GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 解析白名单三个版本文件的显示路径；自动模式以已配置 bytes 文件的父目录为锚点。
        /// </summary>
        private static void ResolveCdnAssetCheckVersionFileDisplayPaths(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            out string bytesDisplayPath,
            out string hashDisplayPath,
            out string versionDisplayPath,
            out string error,
            out string bytesError,
            out string hashError,
            out string versionError)
        {
            bytesDisplayPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                resolved.AssetCheckManifestBytesLocalFilePath,
                curCoord.Platform,
                curCoord.Channel) ?? string.Empty;
            hashDisplayPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                resolved.AssetCheckManifestHashLocalFilePath,
                curCoord.Platform,
                curCoord.Channel) ?? string.Empty;
            versionDisplayPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                resolved.AssetCheckPackageVersionLocalFilePath,
                curCoord.Platform,
                curCoord.Channel) ?? string.Empty;
            error = string.Empty;
            bytesError = string.Empty;
            hashError = string.Empty;
            versionError = string.Empty;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!resolved.AutoLinkLatestAssetCheckVersionFiles)
            {
                EditorUtil.CDN.TryValidateManualLocalFile(
                    bytesDisplayPath,
                    projectRoot,
                    ".bytes",
                    out bytesError);
                EditorUtil.CDN.TryValidateManualLocalFile(
                    hashDisplayPath,
                    projectRoot,
                    ".hash",
                    out hashError);
                EditorUtil.CDN.TryValidateManualLocalFile(
                    versionDisplayPath,
                    projectRoot,
                    ".version",
                    out versionError);
                return;
            }

            string packageName = EditorUtil.CDN.ResolveDefaultPackageName();
            string packageFilePrefix = ResolveCdnPackageFilePrefix(workingSrc, curCoord, packageName);
            if (EditorUtil.CDN.TryResolveLatestAssetCheckVersionFiles(
                    bytesDisplayPath,
                    projectRoot,
                    packageName,
                    packageFilePrefix,
                    out string latestBytesPath,
                    out string latestHashPath,
                    out string latestVersionPath,
                    out error))
            {
                bytesDisplayPath = latestBytesPath;
                hashDisplayPath = latestHashPath;
                versionDisplayPath = latestVersionPath;
            }
        }

        /// <summary>
        /// 从当前 ConfigMaster 当前维度解析 YooAsset 文件前缀，不读取 YooAssetSettings.asset 实际值。
        /// </summary>
        private static string ResolveCdnPackageFilePrefix(
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            string packageName)
        {
            return EditorUtil.CDN.ResolvePackageFilePrefix(
                workingSrc,
                curCoord.Platform,
                curCoord.Channel,
                curCoord.Mode,
                packageName,
                Application.version,
                DateTime.Now);
        }

        /// <summary>
        /// 在指定标签宽度后绘制路径解析错误。
        /// </summary>
        private static void DrawCdnPathErrorHelp(string error, float labelWidth)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f + labelWidth);
                EditorUtil.Draw.HelpBox(
                    MessageType.Error,
                    new[] { error },
                    false,
                    GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制一个白名单版本文件本地位置，并提供选择和打开文件夹入口。
        /// </summary>
        private void DrawCdnAssetCheckLocalFileRow(
            string label,
            string committedValue,
            string displayValue,
            string extension,
            bool readOnly,
            bool selectionEnabled,
            bool hasError,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            Action<CDNEditorConfigs, string> assign)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(label, false, GUILayout.Width(c_CdnWhitelistLabelWidth));
                Color previousBackgroundColor = GUI.backgroundColor;
                Color previousContentColor = GUI.contentColor;
                if (hasError)
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                    GUI.contentColor = new Color(1f, 0.55f, 0.5f, 1f);
                }
                EditorUtil.Draw.DisabledGroup(readOnly, () =>
                {
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorUtil.Draw.TextField(displayValue, false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck() && edited != committedValue)
                        CommitCdnField(workingSrc, curCoord, edited, assign);
                });
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;
                EditorUtil.Draw.DisabledGroup(!selectionEnabled, () =>
                    EditorUtil.Draw.Button(
                        "选择",
                        false,
                        () => SelectCdnAssetCheckLocalFile(displayValue, extension, workingSrc, curCoord, assign),
                        GUILayout.Width(c_CdnSelectButtonWidth)));
                EditorUtil.Draw.Button(
                    "打开文件夹",
                    false,
                    () => OpenCdnVersionCheckLocalFileDirectory(displayValue, curCoord.Platform, curCoord.Channel),
                    GUILayout.Width(c_CdnOpenButtonWidth));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制白名单配置或版本文件使用的 OSS 云端目录。
        /// </summary>
        private void DrawCdnAssetCheckRemoteDirectoryRow(
            string label,
            string presetOssPath,
            string committedValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            Action<CDNEditorConfigs, string> assign)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(label, false, GUILayout.Width(c_CdnWhitelistLabelWidth));
                EditorUtil.Draw.DisabledGroup(true, () =>
                    EditorUtil.Draw.TextField(GetCdnPresetDisplay(presetOssPath), false, GUILayout.MinWidth(250f)));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(
                    committedValue,
                    false,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != committedValue)
                {
                    CommitCdnField(workingSrc, curCoord, edited, assign);
                }
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
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
            Action<CDNEditorConfigs, string> assign,
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
            Action<CDNEditorConfigs, string> assign,
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
        /// 绘制版本检查本地文件位置，并提供文件选择与打开所在文件夹入口。
        /// </summary>
        private void DrawCdnVersionCheckLocalFileRow(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            string displayPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                resolved.VersionCheckLocalFilePath,
                curCoord.Platform,
                curCoord.Channel) ?? string.Empty;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            EditorUtil.CDN.TryValidateManualLocalFile(
                displayPath,
                projectRoot,
                ".json",
                out string localFileError);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("版本检查-本地文件位置", false, GUILayout.Width(c_CdnLabelWidth));
                Color previousBackgroundColor = GUI.backgroundColor;
                Color previousContentColor = GUI.contentColor;
                if (!string.IsNullOrEmpty(localFileError))
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                    GUI.contentColor = new Color(1f, 0.55f, 0.5f, 1f);
                }
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(
                    resolved.VersionCheckLocalFilePath,
                    false,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != resolved.VersionCheckLocalFilePath)
                    CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.VersionCheckLocalFilePath = v);
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;
                EditorUtil.Draw.Button(
                    "选择",
                    false,
                    () => SelectCdnVersionCheckLocalFile(resolved.VersionCheckLocalFilePath, workingSrc, curCoord),
                    GUILayout.Width(c_CdnSelectButtonWidth));
                EditorUtil.Draw.Button(
                    "新建",
                    false,
                    () => CreateCdnVersionCheckLocalFile(resolved.VersionCheckLocalFilePath, workingSrc, curCoord),
                    GUILayout.Width(c_CdnCreateButtonWidth));
                EditorUtil.Draw.Button(
                    "打开文件夹",
                    false,
                    () => OpenCdnVersionCheckLocalFileDirectory(
                        resolved.VersionCheckLocalFilePath,
                        curCoord.Platform,
                        curCoord.Channel),
                    GUILayout.Width(c_CdnOpenButtonWidth));
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
            if (!string.IsNullOrEmpty(localFileError))
                DrawCdnPathErrorHelp(localFileError, c_CdnLabelWidth);
        }

        /// <summary>
        /// 绘制版本检查云端文件位置；固定 OSS 前缀只读，文件后缀可编辑。
        /// </summary>
        private void DrawCdnVersionCheckRemoteFileRow(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("版本检查-云端文件位置", false, GUILayout.Width(c_CdnLabelWidth));
                EditorUtil.Draw.DisabledGroup(true, () =>
                    EditorUtil.Draw.TextField(
                        GetCdnPresetDisplay(resolved.PresetOSSPath),
                        false,
                        GUILayout.MinWidth(250f)));
                EditorGUI.BeginChangeCheck();
                string edited = EditorUtil.Draw.TextField(
                    resolved.VersionCheckRemoteFilePath,
                    false,
                    GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck() && edited != resolved.VersionCheckRemoteFilePath)
                    CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.VersionCheckRemoteFilePath = v);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
        }

        /// <summary>
        /// 绘制本地目录输入、选择和打开文件夹按钮；输入与选择均经 CommitCdnField 按当前坐标写回。
        /// </summary>
        /// <param name="resolved">当前坐标 Resolve 出的整套生效配置。</param>
        /// <param name="workingSrc">编辑期 ConfigMasterSO 实例（工作副本）。</param>
        /// <param name="curCoord">当前维度坐标。</param>
        private void DrawCdnLocalDirectoryRow(
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            string displayDirectory = resolved.LocalDirectory ?? string.Empty;
            string resolveError = string.Empty;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (resolved.AutoLinkLatestVersion)
            {
                string configuredDirectory = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                    resolved.LocalDirectory,
                    curCoord.Platform,
                    curCoord.Channel) ?? string.Empty;
                string packageName = EditorUtil.CDN.ResolveDefaultPackageName();
                string packageFilePrefix = ResolveCdnPackageFilePrefix(workingSrc, curCoord, packageName);
                if (!EditorUtil.CDN.TryResolveLatestPackageDirectory(
                        configuredDirectory,
                        projectRoot,
                        packageName,
                        packageFilePrefix,
                        out displayDirectory,
                        out resolveError))
                {
                    displayDirectory = configuredDirectory;
                }
            }
            else
            {
                string configuredDirectory = EditorUtil.CDN.ResolveEditorPathPlaceholders(
                    resolved.LocalDirectory,
                    curCoord.Platform,
                    curCoord.Channel) ?? string.Empty;
                EditorUtil.CDN.TryValidateManualLocalDirectory(
                    configuredDirectory,
                    projectRoot,
                    out resolveError);
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("热更资源-本地目录位置", false, GUILayout.Width(c_CdnLabelWidth));
                Color previousBackgroundColor = GUI.backgroundColor;
                Color previousContentColor = GUI.contentColor;
                if (!string.IsNullOrEmpty(resolveError))
                {
                    GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
                    GUI.contentColor = new Color(1f, 0.55f, 0.5f, 1f);
                }
                EditorUtil.Draw.DisabledGroup(resolved.AutoLinkLatestVersion, () =>
                {
                    EditorGUI.BeginChangeCheck();
                    string edited = EditorUtil.Draw.TextField(displayDirectory, false, GUILayout.ExpandWidth(true));
                    if (EditorGUI.EndChangeCheck() && edited != resolved.LocalDirectory)
                        CommitCdnField(workingSrc, curCoord, edited, (cfg, v) => cfg.LocalDirectory = v);
                });
                GUI.backgroundColor = previousBackgroundColor;
                GUI.contentColor = previousContentColor;

                EditorUtil.Draw.Button(
                    "选择",
                    false,
                    () => SelectCdnLocalDirectory(displayDirectory, workingSrc, curCoord),
                    GUILayout.Width(c_CdnSelectButtonWidth));
                EditorUtil.Draw.Button(
                    "打开文件夹",
                    false,
                    () => OpenCdnLocalDirectory(
                        displayDirectory,
                        curCoord.Platform,
                        curCoord.Channel),
                    GUILayout.Width(c_CdnOpenButtonWidth));
                EditorUtil.Draw.Space(16f);
            });
            if (!string.IsNullOrEmpty(resolveError))
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f + c_CdnLabelWidth);
                    EditorUtil.Draw.HelpBox(
                        MessageType.Error,
                        new[] { resolveError },
                        false,
                        GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Space(16f);
                });
            }
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
            CDNEditorConfigs resolved,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("热更资源-云端目录位置", false, GUILayout.Width(c_CdnLabelWidth));
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
            CDNEditorConfigs resolved,
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
        /// 在用户选择的项目内目录中复制模板并固定命名为 AppDownloadRules.json，随后写回本地文件位置。
        /// </summary>
        private void CreateCdnVersionCheckLocalFile(
            string currentValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string initialDirectory = ResolveCdnLocalFileInitialDirectory(
                projectRoot,
                currentValue,
                curCoord.Platform,
                curCoord.Channel);
            string targetDirectory = EditorUtility.OpenFolderPanel(
                "选择 AppDownloadRules.json 创建目录",
                initialDirectory,
                string.Empty);
            if (string.IsNullOrEmpty(targetDirectory)) return;

            string normalizedRoot = IOPath.GetFullPath(projectRoot).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
            string normalizedTarget = IOPath.GetFullPath(targetDirectory).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar);
            if (!string.Equals(normalizedTarget, normalizedRoot, StringComparison.Ordinal) &&
                !normalizedTarget.StartsWith(normalizedRoot + IOPath.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("创建失败", "目标目录必须位于 Unity 项目根目录内。", "知道了");
                return;
            }

            string destinationPath = IOPath.Combine(normalizedTarget, c_AppDownloadRulesFileName);
            if (File.Exists(destinationPath) &&
                !EditorUtility.DisplayDialog(
                    "文件已存在",
                    $"{c_AppDownloadRulesFileName} 已存在，是否使用模板覆盖？",
                    "覆盖",
                    "取消"))
            {
                return;
            }

            try
            {
                string templatePath = EditorUtil.FileSystem.ResolveTemplatePath(c_AppDownloadRulesTemplateFileName);
                string templateFullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, templatePath));
                string createdPath = CreateAppDownloadRulesFile(templateFullPath, normalizedTarget);
                string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(createdPath);
                GUI.FocusControl(null);
                CommitCdnField(workingSrc, curCoord, relativePath, (cfg, v) => cfg.VersionCheckLocalFilePath = v);
                AssetDatabase.Refresh();
                Repaint();
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, "创建 AppDownloadRules.json 失败：{0}", exception.Message);
                EditorUtility.DisplayDialog("创建失败", exception.Message, "知道了");
            }
        }

        /// <summary>
        /// 将版本检查模板复制到目标目录，并固定输出文件名为 AppDownloadRules.json。
        /// </summary>
        /// <returns>创建文件的绝对路径。</returns>
        private static string CreateAppDownloadRulesFile(string templateFullPath, string targetDirectory)
        {
            if (string.IsNullOrEmpty(templateFullPath) || !File.Exists(templateFullPath))
                throw new FileNotFoundException("版本检查模板文件不存在。", templateFullPath);
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException("版本检查规则文件的目标目录不存在。");

            string destinationPath = IOPath.Combine(targetDirectory, c_AppDownloadRulesFileName);
            File.Copy(templateFullPath, destinationPath, true);
            return destinationPath;
        }

        /// <summary>
        /// 打开版本检查模板所在目录。
        /// </summary>
        private static void OpenCdnVersionCheckTemplateDirectory(string templatePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            string templateFullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, templatePath));
            EditorUtil.FileSystem.OpenFolder(IOPath.GetDirectoryName(templateFullPath));
        }

        /// <summary>
        /// 解析版本检查文件选择器的初始目录；当前配置不可用时回退项目根。
        /// </summary>
        private static string ResolveCdnLocalFileInitialDirectory(
            string projectRoot,
            string currentValue,
            PlatformType platform,
            ChannelType channel)
        {
            string resolvedPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(currentValue, platform, channel);
            if (string.IsNullOrEmpty(resolvedPath)) return projectRoot;
            string fullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, resolvedPath));
            string directory = IOPath.GetDirectoryName(fullPath);
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : projectRoot;
        }

        /// <summary>
        /// 选择项目内的版本检查本地文件，并按当前坐标写回工程根相对路径。
        /// </summary>
        private void SelectCdnVersionCheckLocalFile(
            string currentValue,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string initialDirectory = ResolveCdnLocalFileInitialDirectory(
                projectRoot,
                currentValue,
                curCoord.Platform,
                curCoord.Channel);

            string selected = EditorUtility.OpenFilePanel("选择版本检查本地文件", initialDirectory, string.Empty);
            if (string.IsNullOrEmpty(selected)) return;

            string normalizedRoot = IOPath.GetFullPath(projectRoot).TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
                + IOPath.DirectorySeparatorChar;
            string normalizedSelected = IOPath.GetFullPath(selected);
            if (!normalizedSelected.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                Log.Warning(LogTag.Editor, "版本检查本地文件必须位于 Unity 项目根目录内：{0}", selected);
                return;
            }
            string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(normalizedSelected);

            GUI.FocusControl(null);
            CommitCdnField(workingSrc, curCoord, relativePath, (cfg, v) => cfg.VersionCheckLocalFilePath = v);
        }

        /// <summary>
        /// 选择项目内指定扩展名的白名单版本文件，并按当前坐标写回工程根相对路径。
        /// </summary>
        private void SelectCdnAssetCheckLocalFile(
            string currentValue,
            string extension,
            ConfigMasterSO workingSrc,
            EditorUtil.Config.DimensionProjector.Coord curCoord,
            Action<CDNEditorConfigs, string> assign)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string initialDirectory = ResolveCdnLocalFileInitialDirectory(
                projectRoot,
                currentValue,
                curCoord.Platform,
                curCoord.Channel);
            string selected = EditorUtility.OpenFilePanel(
                $"选择 {extension} 版本文件",
                initialDirectory,
                extension.TrimStart('.'));
            if (string.IsNullOrEmpty(selected)) return;

            string normalizedRoot = IOPath.GetFullPath(projectRoot)
                .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
                + IOPath.DirectorySeparatorChar;
            string normalizedSelected = IOPath.GetFullPath(selected);
            if (!normalizedSelected.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                Log.Warning(LogTag.Editor, "白名单版本文件必须位于 Unity 项目根目录内：{0}", selected);
                return;
            }

            GUI.FocusControl(null);
            CommitCdnField(
                workingSrc,
                curCoord,
                EditorUtil.FileSystem.GetProjectRelativePath(normalizedSelected),
                assign);
        }

        /// <summary>
        /// 解析版本检查本地文件位置，并在系统文件管理器中打开其所在目录。
        /// </summary>
        private static void OpenCdnVersionCheckLocalFileDirectory(
            string relativePath,
            PlatformType platform,
            ChannelType channel)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;
            string resolvedPath = EditorUtil.CDN.ResolveEditorPathPlaceholders(relativePath, platform, channel);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                EditorUtil.FileSystem.OpenFolder(projectRoot);
                return;
            }
            string fullPath = IOPath.GetFullPath(IOPath.Combine(projectRoot, resolvedPath ?? string.Empty));
            string directory = IOPath.GetDirectoryName(fullPath);
            EditorUtil.FileSystem.OpenFolder(directory);
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
        /// 启动白名单配置与三个 YooAsset 版本文件部署；重复点击在入口处直接忽略。
        /// </summary>
        private void OnDeployCdnWhitelist()
        {
            if (m_IsCdnWhitelistDeploying) return;
            DeployCdnWhitelistAsync().Forget();
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
                CDNEditorConfigs config = CreateCdnConfigSnapshot();
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                    ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
                string packageName = EditorUtil.CDN.ResolveDefaultPackageName();
                string packageFilePrefix = ResolveCdnPackageFilePrefix(
                    source,
                    new EditorUtil.Config.DimensionProjector.Coord(
                        source.CurrentPlatform,
                        source.CurrentChannel,
                        source.CurrentDevelopMode),
                    packageName);
                int count = await EditorUtil.CDN.DeployAsync(
                    config,
                    projectRoot,
                    source.CurrentPlatform,
                    source.CurrentChannel,
                    packageFilePrefix,
                    m_CleanCdnRemoteBeforeDeploy,
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
        /// 按配置分别部署白名单 JSON 与三个版本文件，完成后恢复进度条与忙碌状态。
        /// </summary>
        private async UniTask DeployCdnWhitelistAsync()
        {
            m_IsCdnWhitelistDeploying = true;
            try
            {
                ConfigMasterSO source = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
                if (source == null)
                    throw new InvalidOperationException("未找到 CDN 白名单部署配置。");
                CDNEditorConfigs config = CreateCdnConfigSnapshot();
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                    ?? throw new InvalidOperationException("无法解析 Unity 项目根目录。");
                string packageName = EditorUtil.CDN.ResolveDefaultPackageName();
                string packageFilePrefix = ResolveCdnPackageFilePrefix(
                    source,
                    new EditorUtil.Config.DimensionProjector.Coord(
                        source.CurrentPlatform,
                        source.CurrentChannel,
                        source.CurrentDevelopMode),
                    packageName);
                int count = await EditorUtil.CDN.DeployAssetCheckWhitelistAsync(
                    config,
                    projectRoot,
                    source.CurrentPlatform,
                    source.CurrentChannel,
                    packageFilePrefix,
                    m_CleanCdnWhitelistRemoteBeforeDeploy,
                    (completed, total, path) => EditorUtility.DisplayProgressBar(
                        "批量部署白名单到 CDN",
                        $"{completed}/{total}  {path}",
                        total > 0 ? completed / (float)total : 0f));
                EditorUtility.DisplayDialog("部署完成", $"已成功上传 {count} 个白名单相关文件。", "知道了");
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Editor, $"[CDN] 白名单部署失败：{exception.Message}");
                EditorUtility.DisplayDialog("部署失败", exception.Message, "知道了");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                m_IsCdnWhitelistDeploying = false;
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
                CDNEditorConfigs config = CreateCdnConfigSnapshot();
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
        /// 部署与清缓存操作作用于当前坐标生效的那份配置（IsGlobal 顶层 / 命中 CDNEditorConfigsOverrides 条目 / 逐字段回落）。
        /// </summary>
        /// <returns>与当前坐标生效值一致的独立配置快照。</returns>
        private CDNEditorConfigs CreateCdnConfigSnapshot()
        {
            ConfigMasterSO source = m_WorkingCopy != null ? m_WorkingCopy : m_Master;
            if (source == null)
                throw new InvalidOperationException("未找到 CDN 部署配置。");
            return EditorUtil.Config.DimensionalResolver.ResolveCDNEditorConfigs(
                source,
                source.CurrentPlatform,
                source.CurrentChannel,
                source.CurrentDevelopMode);
        }
    }
}
