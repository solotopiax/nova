/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   App 组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class AppComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置信息。
        /// </summary>
        private void DrawConfigs()
        {
            // 顶层：实现选择（不加 Foldout，平铺展示）
            EditorUtil.Draw.TypesSelector("App 管理器", m_AppManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IAppManager 接口后，该类型将自动出现在此列表中。" });

            EditorUtil.Draw.Line();

            EditorUtil.Draw.Property("启用 App 更新：", m_EnableAppUpdate, true, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)App 大版本检查与更新下载功能总开关",
                "(2)关闭后不请求版本配置，也不会进入推荐更新或强制更新流程",
                "(3)资源热更新仍由 Asset 面板的 EnableHotfix 独立控制"
            }, false, GUILayout.ExpandWidth(true));

            EditorUtil.Draw.Line();

            // 总开关关闭时，以下三组 App 更新配置统一灰度禁用。
            using (new EditorGUI.DisabledScope(!m_EnableAppUpdate.boolValue))
            {
                // Foldout 1：版本检查
                if (EditorUtil.Draw.Foldout("版本检查", "AppVersionCheckGroup", true))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        DrawTemplatePathHintReadOnlyOpenFolderOnly(c_AppDownloadRulesTemplateFileName, "版本检查-模板文件位置：", 180f);
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查URL-Debug：", m_AppDownloadCheckUrlDebug, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查URL-Debug [备用]：", m_AppDownloadCheckUrlFallbackDebug, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查URL-Release：", m_AppDownloadCheckUrlRelease, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查URL-Release [备用]：", m_AppDownloadCheckUrlFallbackRelease, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)先按上方模板文件生成 JSON，并上传到 CDN；可通过「Config全局配置中心 - CDN 内容分发网络部署」或「Pipify 自动化管线编排中心 - 添加步骤」自动上传",
                        "(2)启动时先尝试当前 DevelopMode 对应的主地址，失败或空内容时自动切到备用地址",
                        "(3)URL 支持 {Platform} / {Channel} / {Package} / {Version}，语义与 Asset 主机服务器 URL 一致",
                        "(4){Platform} 由 Player 编译宏决定，不读取 Editor Active BuildTarget 或 ConfigMaster；{Channel} 为导出渠道，{Package} 为默认资源包名，{Version} 为 Application.version",
                        "(5)全部候选轮次耗尽时本次大版本检查直接返回 NoDownload"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查超时（秒）：", m_TimeoutSeconds, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)每个主备候选的单次物理请求都完整使用该超时，不是整条链的总超时",
                        "(2)数值过短易在弱网环境下误判失败",
                        "(3)推荐默认值 5"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("主备完整轮数：", m_VersionCheckFallbackRoundCount, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)一轮会依次尝试当前有效且去重后的所有主备地址",
                        "(2)主、备均配置时，轮数 1 表示主→备；轮数 2 表示主→备→主→备",
                        "(3)轮数增大会增加弱网下的最长等待时间"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("请求重试次数：", m_RetryRequestCount, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)首次完整执行不计入重试次数；每增加 1 次都会重新执行全部主备轮次",
                        "(2)候选数 C、轮数 R、重试次数 K 的最大物理请求数为 C × R × (K + 1)",
                        "(3)404/408/429、5xx、无响应或无效内容继续；其他 4xx 停止并按 NoDownload 降级",
                        "(4)调用方取消时立即停止，不再重试或切换候选"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("最近成功域名优先：", m_PreferLastSuccessfulHost, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)开启后，下一次版本检查优先尝试当前进程内最近取得有效规则的域名",
                        "(2)只调整新请求链的候选顺序，其他域名仍会在后续候选中被尝试",
                        "(3)该偏好不写入本地存储；候选全部失败不会清除，配置候选变更或 Manager 重置时才失效"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("启用 UWR 埋点：", m_EnableUWRTracks, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)控制 App 版本检查主备链路的 UnityWebRequest 埋点",
                        "(2)只控制埋点上报，不会禁止或改变版本检查请求",
                        "(3)同一次物理请求只记录一次，避免与底层传输埋点重复"
                        }, false, GUILayout.ExpandWidth(true));
                    });
                }

                EditorUtil.Draw.Line();

                // Foldout 2：更新规则
                if (EditorUtil.Draw.Foldout("更新规则", "AppUpdateRuleGroup", true))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)两个规则可任意组合启用",
                        "(2)命中时优先级：强制更新规则 > 推荐更新规则",
                        "(3)命中任一规则都会弹出大版本更新提示",
                        "(4)弹窗使用 Resources/BuiltIn/Prefabs/LauncherDialogPanel 资源，强制更新与推荐更新共用该 Prefab"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("启用推荐更新规则：", m_UseRecommendedDownloadRule, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)本地版本号小于 CDN 配置的推荐更新版本号时触发",
                        "(2)命中后弹出推荐更新提示",
                        "(3)用户取消后继续热更检查和后续启动"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("启用强制更新规则：", m_UseForcedDownloadRule, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)本地版本号小于 CDN 配置的强制更新版本号时触发",
                        "(2)命中后弹出强制更新提示",
                        "(3)用户操作被锁定，无法跳过"
                        }, false, GUILayout.ExpandWidth(true));
                    });
                }

                EditorUtil.Draw.Line();

                // Foldout 3：更新下载
                if (EditorUtil.Draw.Foldout("更新下载", "AppDownloadGroup", true))
                {
                    // DownloadRoute 始终可编辑
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("更新下载方式：", m_DownloadRoute, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                        "(1)命中推荐更新或强制更新规则后，都使用这里的下载方式",
                        "(2)Store 模式跳转应用商店",
                        "(3)Apk 模式由 App 内部下载 APK 文件"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    bool isStoreRoute = m_DownloadRoute.intValue == (int)AppDownloadRoute.Store;

                    // 商店段：Apk 路由时灰度禁用
                    using (new EditorGUI.DisabledScope(!isStoreRoute))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("Android 商店地址：", m_AndroidStoreUrl, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                        "(1)Android 商店跳转 URL，可填 Google Play 或国内商店落地页",
                        "(2)仅在 DownloadRoute 为 Store 时生效",
                        "(3)当前平台为 Android 时才需要配置"
                            }, false, GUILayout.ExpandWidth(true));
                        });

                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("iOS 商店地址：", m_AppStoreUrl, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                        "(1)iOS App Store 跳转 URL",
                        "(2)仅在 DownloadRoute 为 Store 时生效",
                        "(3)当前平台为 iOS 时才需要配置"
                            }, false, GUILayout.ExpandWidth(true));
                        });
                    }

                    // APK 段：Store 路由时灰度禁用
                    using (new EditorGUI.DisabledScope(isStoreRoute))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("主下载地址：", m_PrimaryDownloadUrl, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                        "(1)APK 主下载地址",
                        "(2)仅在 DownloadRoute 为 Apk 时生效",
                        "(3)启动期版本检查命中更新规则时会校验该字段"
                            }, false, GUILayout.ExpandWidth(true));
                        });

                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("备用下载地址：", m_FallbackDownloadUrl, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                        "(1)APK 备用下载地址（可选）",
                        "(2)当前启动期版本检查不会校验该字段",
                        "(3)仅在 DownloadRoute 为 Apk 时生效"
                            }, false, GUILayout.ExpandWidth(true));
                        });
                    }
                }
            }

            EditorUtil.Draw.Line();
            EditorUtil.Draw.Line();
        }
    }
}
