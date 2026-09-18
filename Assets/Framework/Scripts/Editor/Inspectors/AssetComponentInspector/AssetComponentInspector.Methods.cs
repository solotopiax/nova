/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Asset组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class AssetComponentInspector : BaseComponentInspector
    {
        private const float c_StartupWhitelistUrlLabelWidth = 185f;

        /// <summary>
        /// 绘制配置信息。
        /// </summary>
        private void DrawConfigs()
        {
            bool isWebGLBuildTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;

            // 顶层：实现选择（不加 Foldout，平铺展示）
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorUtil.Draw.TypesSelector("Asset 管理器", m_AssetManagerTypeNames, m_CurAssetManagerTypeName, false, null, GUILayout.Width(180f));
                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IAssetManager 接口后，该类型将自动出现在此列表中。" }, false);
            }

            EditorUtil.Draw.Line();

            // 顶层平铺：加载模式（不属于热更范畴，与加载管理器同级）
            // 编辑器模式 —— 永远 enable，3 选 1 自定义 Popup（与下方 RuntimePlayMode 视觉一致：枚举原名无空格）
            DrawEditorPlayModePopup();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)控制在编辑器中运行时的资源来源",
                "(2)EditorSimulateMode 直接使用工程资源，启动更快，适合日常开发",
                "(3)OfflinePlayMode 和 HostPlayMode 用于验证真实资源包"
            }, false, GUILayout.ExpandWidth(true));

            // 终端模式 —— 永远 enable，2 选 1 自定义 Popup（禁 EditorSimulateMode）
            // EditorUtil.Draw 无 IntPopup 封装，此处局部实现以满足限制选项集需求
            DrawRuntimePlayModePopup();
            EditorUtil.Draw.HelpBox(
                MessageType.Info,
                GetRuntimePlayModeHelpBoxMessages(EditorUserBuildSettings.activeBuildTarget),
                false,
                GUILayout.ExpandWidth(true));

            using (new EditorGUI.DisabledScope(!isWebGLBuildTarget))
            {
                DrawWebGLAssetStrategyPopup();
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)TagsOnLaunch：启动时准备指定 Tag 的资源，适合作为默认方案",
                    "(2)OnDemand：使用时再加载资源，启动更快，首次使用可能需要等待",
                    "(3)AllOnLaunch：启动时准备全部资源，运行更平滑，但启动更慢且内存占用更高",
                    "(4)仅 WebGL 平台生效"
                }, false, GUILayout.ExpandWidth(true));
            }

            EditorUtil.Draw.Line();

            // 顶层平铺：资源包名列表（③ 资源包配置段首项；不使用 Foldout，直接平铺增删）
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                DrawPackagesList();
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)本组件管理的所有资源包名",
                    "(2)默认含 Default 包，多包项目按需追加",
                    "(3)默认资源包名留空时使用列表第一项"
                }, false, GUILayout.ExpandWidth(true));
            }

            // 顶层平铺：默认包名（下拉，选项严格来自 Packages 列表）
            DrawDefaultPackageNamePopup();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)从 资源包名列表 中选择默认包",
                "(2)新增/重命名包名后请在此重新选择"
            }, false, GUILayout.ExpandWidth(true));

            // 顶层平铺：场景卸载时自动清理
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorUtil.Draw.Property("场景卸载时自动清理：", m_AutoCleanupOnSceneUnload, false, GUILayout.Width(180f));
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)勾选后场景卸载时自动释放未使用资源",
                    "(2)未勾选时由业务侧自行决定清理时机"
                }, false, GUILayout.ExpandWidth(true));
            }

            EditorUtil.Draw.Line();

            // 唯一分组：热更配置（EnableHotfix 为总开关，置于首位）
            if (EditorUtil.Draw.Foldout("热更配置", "AssetHotfixConfigGroup", true))
            {
                // 0. 总开关 —— 关闭后直跳 LoadDll；与 RuntimePlayMode 双向联动
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("启用热更新：", m_EnableHotfix, false, GUILayout.Width(180f));
                    });
                    if (EditorGUI.EndChangeCheck())
                    {
                        // EnableHotfix 变动 → 联动 RuntimePlayMode
                        if (m_EnableHotfix.boolValue)
                        {
                            // 开启热更 → RuntimePlayMode 若为 OfflinePlayMode 则升至 HostPlayMode
                            if (m_RuntimePlayMode.intValue == (int)AssetPlayMode.OfflinePlayMode)
                                m_RuntimePlayMode.intValue = (int)AssetPlayMode.HostPlayMode;
                        }
                        else
                        {
                            // 关闭热更 → 强制 RuntimePlayMode = OfflinePlayMode
                            m_RuntimePlayMode.intValue = (int)AssetPlayMode.OfflinePlayMode;
                        }
                        serializedObject.ApplyModifiedProperties();
                        serializedObject.Update();
                    }
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)控制启动时是否检查并下载资源更新",
                            "(2)关闭后仍会正常检查应用版本更新",
                            "(3)关闭后终端加载模式自动切换为 OfflinePlayMode"
                        }, false, GUILayout.ExpandWidth(true));
                    });
                }

                // 以下字段在 EnableHotfix==false 时联动灰度禁用
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !m_EnableHotfix.boolValue))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)运行时按当前 Debug 或 Release 配置选择对应地址",
                            "(2)支持 {Platform}/{Channel}/{Package}/{Version} 占位符",
                            "(3)主地址不可用时会尝试备用地址"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 主机服务器地址 URL
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("主机服务器URL-Debug：", m_HostServerUrlDebug, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("主机服务器URL-Debug [备用]：", m_HostServerUrlFallbackDebug, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("主机服务器URL-Release：", m_HostServerUrlRelease, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("主机服务器URL-Release [备用]:", m_HostServerUrlFallbackRelease, true, GUILayout.Width(180f));
                    });
                    // 启动白名单 —— 热更配置下的二级折叠组，标题复选框控制功能开关
                    bool whitelistExpanded = false;
                    bool enableStartupWhitelist = m_EnableStartupWhitelist.boolValue;
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(15f);
                        whitelistExpanded = EditorUtil.Draw.ColoredToggleFoldoutHeader(
                            "启用白名单",
                            "AssetStartupWhitelistGroup",
                            GUI.contentColor,
                            m_EnableStartupWhitelist.boolValue,
                            out enableStartupWhitelist,
                            null,
                            defaultOpen: false,
                            toggleAfterTitle: true);
                    });
                    if (enableStartupWhitelist != m_EnableStartupWhitelist.boolValue)
                    {
                        m_EnableStartupWhitelist.boolValue = enableStartupWhitelist;
                        serializedObject.ApplyModifiedProperties();
                    }

                    if (whitelistExpanded)
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(32f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                                "(1)用于让指定测试设备提前验证版本元数据",
                                "(2)测试设备使用单独配置的版本地址，资源下载仍使用常规地址",
                                "(3)首次启动无 DeviceID 或请求失败时自动跳过，不阻断启动"
                            }, false, GUILayout.ExpandWidth(true));
                        });

                        using (new EditorGUI.DisabledScope(!m_EnableStartupWhitelist.boolValue))
                        {
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("配置文件URL-Debug：", m_StartupWhitelistUrlDebug, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("配置文件URL-Debug [备用]：", m_StartupWhitelistUrlFallbackDebug, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("配置文件URL-Release：", m_StartupWhitelistUrlRelease, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("配置文件URL-Release [备用]：", m_StartupWhitelistUrlFallbackRelease, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("版本文件根URL-Debug：", m_StartupWhitelistMetadataRootUrlDebug, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("版本文件根URL-Debug [备用]：", m_StartupWhitelistMetadataRootUrlFallbackDebug, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("版本文件根URL-Release：", m_StartupWhitelistMetadataRootUrlRelease, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("版本文件根URL-Release [备用]：", m_StartupWhitelistMetadataRootUrlFallbackRelease, true, GUILayout.Width(c_StartupWhitelistUrlLabelWidth));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("主备完整轮数：", m_StartupWhitelistFallbackRoundCount, true, GUILayout.Width(180f));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "每轮依次尝试白名单文件的全部有效主备地址。" }, false, GUILayout.ExpandWidth(true));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("请求重试次数：", m_StartupWhitelistRetryRequestCount, true, GUILayout.Width(180f));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "全部轮次失败后的重试次数；每次重试重新执行全部轮次。" }, false, GUILayout.ExpandWidth(true));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("最近成功域名优先：", m_StartupWhitelistPreferLastSuccessfulHost, true, GUILayout.Width(180f));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "新请求优先使用本进程最近成功的白名单域名；失败后仍会尝试其他地址。" }, false, GUILayout.ExpandWidth(true));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("启用 UWR 埋点：", m_StartupWhitelistEnableUWRTracks, true, GUILayout.Width(180f));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "仅控制白名单请求链埋点，不影响请求。" }, false, GUILayout.ExpandWidth(true));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.Property("请求超时（秒）：", m_StartupWhitelistCheckTimeout, true, GUILayout.Width(180f));
                            });
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                EditorUtil.Draw.Space(32f);
                                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "白名单文件单次请求超时；主备请求分别计时。" }, false, GUILayout.ExpandWidth(true));
                            });
                        }
                    }
                }

                // 1. 启动资源 Tag 列表 —— WebGL 仅 TagsOnLaunch 使用，非 WebGL 由热更新开关控制
                bool enableHotfix = m_EnableHotfix.boolValue;
                bool webGLTagsOnLaunch = isWebGLBuildTarget
                    && m_WebGLAssetStrategy.intValue == (int)WebGLAssetStrategy.TagsOnLaunch;
                bool launchTagsEditable = isWebGLBuildTarget ? webGLTagsOnLaunch : enableHotfix;
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || !launchTagsEditable))
                {
                    Color previousContentColor = GUI.contentColor;
                    if (!launchTagsEditable)
                    {
                        GUI.contentColor = new Color(0.5f, 0.5f, 0.5f, previousContentColor.a);
                    }

                    try
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.PropertyField(
                                m_LaunchHotfixTags,
                                isWebGLBuildTarget ? "启动预热 Tag 列表：" : "启动期热更 Tag 列表：",
                                true);
                        });
                    }
                    finally
                    {
                        GUI.contentColor = previousContentColor;
                    }

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        string[] messages;
                        if (!isWebGLBuildTarget)
                        {
                            messages = new[]
                            {
                                "空列表：启动期对全部资源做整包差异更新。",
                                "填入 Tag：启动期仅更新命中范围，其余资源由业务按需下载。"
                            };
                        }
                        else if (webGLTagsOnLaunch)
                        {
                            messages = new[]
                            {
                                "填写启动阶段必须提前准备的资源 Tag；未填写有效 Tag 时按 OnDemand 运行。"
                            };
                        }
                        else if (m_WebGLAssetStrategy.intValue == (int)WebGLAssetStrategy.AllOnLaunch)
                        {
                            messages = new[]
                            {
                                "当前策略会在启动时准备全部资源，无需配置 Tag。"
                            };
                        }
                        else
                        {
                            messages = new[]
                            {
                                "当前策略按需加载资源，无需配置 Tag。"
                            };
                        }
                        EditorUtil.Draw.HelpBox(MessageType.Info, messages, false, GUILayout.ExpandWidth(true));
                    });
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || (!m_EnableHotfix.boolValue && !isWebGLBuildTarget)))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.DangerButton(
                            "清空本地热更资源缓存",
                            true,
                            EditorUtil.Asset.Cache.ClearAllHotfixResources,
                            GUILayout.ExpandWidth(true));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "清空当前工程在 Editor 中下载的资源缓存，不会删除其他本地数据。"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 2. 失败/取消时强制退出 —— WebGL 固定关闭，其他平台保持原配置
                    using (new EditorGUI.DisabledScope(isWebGLBuildTarget))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            if (isWebGLBuildTarget)
                            {
                                EditorUtil.Draw.Label("失败/取消时强制退出：", false, GUILayout.Width(180f));
                                EditorUtil.Draw.Toggle(false, GUILayout.ExpandWidth(true));
                            }
                            else
                            {
                                EditorUtil.Draw.Property("失败/取消时强制退出：", m_QuitOnFailedOrCancel, false, GUILayout.Width(180f));
                            }
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, isWebGLBuildTarget
                                ? new[] { "WebGL 平台下不使用此项，失败或取消后可继续启动。" }
                                : new[]
                                {
                                    "(1)启动资源准备失败时会显示重试或取消提示",
                                    "(2)勾选后取消会退出应用；未勾选则跳过本次资源准备并继续进入游戏"
                                }, false, GUILayout.ExpandWidth(true));
                        });
                    }

                    // 3. 资源请求最大并发数 —— 非 WebGL 下载与 WebGL Warmup 共用
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("资源请求最大并发数：", m_MaxDownloadConcurrency, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)控制同时进行的资源请求数量",
                            "(2)建议 3-8；过高可能被限速，过低会降低下载速度"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 4. CDN 主备、重试、域名偏好与埋点只在 HostPlayMode 下生效。
                    bool usesCdnResources = m_RuntimePlayMode.intValue == (int)AssetPlayMode.HostPlayMode;
                    using (new EditorGUI.DisabledScope(!usesCdnResources))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("主备完整轮数：", m_FallbackRoundCount, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "每轮依次尝试全部有效的 CDN 主备地址。" }, false, GUILayout.ExpandWidth(true));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("资源请求重试次数：", m_RetryDownloadCount, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "单个 CDN 资源完成全部轮次仍失败后的重试次数。" }, false, GUILayout.ExpandWidth(true));
                        });

                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("最近成功域名优先：", m_PreferLastSuccessfulHost, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "新的 CDN 请求优先使用本进程最近成功的域名；失败后仍会尝试其他地址。" }, false, GUILayout.ExpandWidth(true));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("启用 UWR 埋点：", m_EnableUWRTracks, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "仅控制 CDN 资源请求埋点，不影响资源加载。" }, false, GUILayout.ExpandWidth(true));
                        });
                    }

                    // 5. 热更完成后自动清理旧缓存 —— 仅非 WebGL Downloader 热更成功后执行
                    using (new EditorGUI.DisabledScope(!m_EnableHotfix.boolValue || isWebGLBuildTarget))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("热更完成后自动清理缓存：", m_AutoClearUnusedCacheOnHotfix, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                                "(1)勾选后，热更成功时删除当前清单不再使用的本地缓存文件",
                                "(2)未勾选时不自动清理，由业务决定清理时机",
                                "(3)已删除的文件再次需要时必须重新下载",
                                "(4)WebGL 平台下不使用此项"
                            }, false, GUILayout.ExpandWidth(true));
                        });
                    }

                    // 6. 版本检查请求超时 —— 控制远端版本文件请求的总时长
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查请求超时（秒）：", m_CheckTimeout, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)单次版本检查请求的最长等待时间",
                            "(2)HostPlayMode 下超时后会继续尝试其他可用地址"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 7. Manifest 请求超时 —— 控制 .hash/.bytes 单次物理请求的总时长
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("Manifest 请求超时（秒）：", m_ManifestRequestTimeout, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)单次 Manifest 请求的最长等待时间",
                            "(2)HostPlayMode 下超时后会继续尝试其他可用地址"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 8. WebGL Bundle 请求超时 —— WebGL 无可靠字节流入进度时使用
                    using (new EditorGUI.DisabledScope(!isWebGLBuildTarget))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("WebGL Bundle 请求超时（秒）：", m_WebGLBundleRequestTimeout, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                                "(1)仅 WebGL 生效，控制单个资源文件请求的最长等待时间",
                                "(2)请根据最大资源文件大小和用户网络速度预留足够时间",
                                "(3)非 WebGL 平台请使用下方的单文件字节流入超时"
                            }, false, GUILayout.ExpandWidth(true));
                        });
                    }

                    // 9. 单文件字节流入超时 —— 非 WebGL 检测连续无新字节的停滞时间
                    using (new EditorGUI.DisabledScope(isWebGLBuildTarget))
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.Property("单文件字节流入超时（秒）：", m_IdleTimeout, true, GUILayout.Width(180f));
                        });
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                            {
                                "(1)非 WebGL 平台下，单个文件连续无新字节流入的时长上限",
                                "(2)收到任意新字节后重新计时",
                                "(3)WebGL 下该项不可编辑，请使用上方的 Bundle 请求超时"
                            }, false, GUILayout.ExpandWidth(true));
                        });
                    }
                }
            }

            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 获取当前构建平台对应的终端加载模式说明；WebGL 额外说明首包布局要求。
        /// </summary>
        /// <param name="buildTarget">Unity 当前活动构建平台。</param>
        /// <returns>用于终端加载模式下方单个 HelpBox 的用户提示。</returns>
        internal static string[] GetRuntimePlayModeHelpBoxMessages(BuildTarget buildTarget)
        {
            if (buildTarget == BuildTarget.WebGL)
            {
                return new[]
                {
                    "(1)OfflinePlayMode 只使用随网页发布的资源，构建资源时必须选择 ClearAndCopyAll；选择 None 时游戏无法启动",
                    "(2)HostPlayMode 支持 None、ClearAndCopyByTags、ClearAndCopyAll；选择 None 时请先把当前版本的完整资源上传到 CDN",
                    "(3)按 Tag 发布时，HostPlayMode 可从 CDN 获取其余资源；OfflinePlayMode 无法加载未随网页发布的资源"
                };
            }

            return new[]
            {
                "(1)OfflinePlayMode 使用随安装包发布的资源，不检查远端更新",
                "(2)HostPlayMode 检查并使用远端资源更新"
            };
        }

        /// <summary>
        /// 绘制"资源包名列表"：使用 Unity 默认 List 渲染（自带 Size 字段 + 索引条目 + 增删行尾按钮）。
        /// 通过 EditorUtil.Draw.PropertyField(includeChildren:true) 直接展开列表，不再叠加自定义 +/× 控件。
        /// </summary>
        private void DrawPackagesList()
        {
            EditorGUI.BeginChangeCheck();
            EditorUtil.Draw.PropertyField(m_Packages, "资源包名列表", true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
        }

        /// <summary>
        /// 绘制"默认资源包名"下拉：选项直接为当前 m_Packages 列表条目，无占位项。
        /// 当 m_Packages 为空时退化为只读 Label，避免空选项 Popup 引发歧义。
        /// 当前 DefaultPackageName 不在选项内时自动归一为首项并回写。
        /// </summary>
        private void DrawDefaultPackageNamePopup()
        {
            int packageCount = m_Packages != null ? m_Packages.arraySize : 0;
            if (packageCount == 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label("默认资源包名：", false, GUILayout.Width(180f));
                    EditorUtil.Draw.Label("(请先在 资源包名列表 中添加至少一项)", EditorStyles.miniLabel);
                });
                return;
            }

            string[] options = new string[packageCount];
            for (int i = 0; i < packageCount; i++)
            {
                SerializedProperty element = m_Packages.GetArrayElementAtIndex(i);
                options[i] = string.IsNullOrEmpty(element.stringValue) ? "(空)" : element.stringValue;
            }

            string current = m_DefaultPackageName.stringValue;
            int curIndex = -1;
            for (int i = 0; i < packageCount; i++)
            {
                if (m_Packages.GetArrayElementAtIndex(i).stringValue == current)
                {
                    curIndex = i;
                    break;
                }
            }
            if (curIndex < 0)
            {
                curIndex = 0;
                m_DefaultPackageName.stringValue = m_Packages.GetArrayElementAtIndex(0).stringValue;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("默认资源包名：", false, GUILayout.Width(180f));
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorUtil.Draw.Popup(curIndex, options);
                if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < packageCount)
                {
                    m_DefaultPackageName.stringValue = m_Packages.GetArrayElementAtIndex(newIndex).stringValue;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                }
            });
        }

        /// <summary>
        /// 绘制 EditorPlayMode 自定义 Popup（3 选 1，全部枚举可用）。
        /// 与 RuntimePlayMode 共用同款 IntPopup，避免 PropertyField 默认 nicify 把
        /// HostPlayMode 拆成 "Host Play Mode" 导致同面板上下风格分裂。
        /// </summary>
        private void DrawEditorPlayModePopup()
        {
            int curValue = m_EditorPlayMode.intValue;

            int[] optionValues = { (int)AssetPlayMode.EditorSimulateMode, (int)AssetPlayMode.OfflinePlayMode, (int)AssetPlayMode.HostPlayMode };
            string[] optionLabels = { "EditorSimulateMode", "OfflinePlayMode", "HostPlayMode" };

            int newValue = curValue;
            bool changed = false;

            // Label + IntPopup 同行渲染（Horizontal 包裹），与下方 RuntimePlayMode 视觉对齐
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("编辑器加载模式：", false, GUILayout.Width(180f));
                EditorGUI.BeginChangeCheck();
                newValue = EditorUtil.Draw.IntPopup(curValue, optionLabels, optionValues);
                changed = EditorGUI.EndChangeCheck();
            });

            if (changed)
            {
                m_EditorPlayMode.intValue = newValue;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
        }

        /// <summary>
        /// 绘制 RuntimePlayMode 自定义 Popup（2 选 1，禁 EditorSimulateMode）。
        /// 含联动逻辑：选中 OfflinePlayMode 时强制 EnableHotfix=false；选中其他时强制 EnableHotfix=true。
        /// </summary>
        private void DrawRuntimePlayModePopup()
        {
            // 异常值归一：若当前为 EditorSimulateMode（运行时不允许），回落到 OfflinePlayMode
            int curValue = m_RuntimePlayMode.intValue;
            if (curValue == (int)AssetPlayMode.EditorSimulateMode)
                curValue = (int)AssetPlayMode.OfflinePlayMode;

            int[] optionValues = { (int)AssetPlayMode.OfflinePlayMode, (int)AssetPlayMode.HostPlayMode };
            string[] optionLabels = { "OfflinePlayMode", "HostPlayMode" };

            int newValue = curValue;
            bool changed = false;

            // Label + IntPopup 同行渲染（Horizontal 包裹），与 EditorUtil.Draw.Property 视觉对齐
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("终端加载模式：", false, GUILayout.Width(180f));
                EditorGUI.BeginChangeCheck();
                newValue = EditorUtil.Draw.IntPopup(curValue, optionLabels, optionValues);
                changed = EditorGUI.EndChangeCheck();
            });

            if (changed)
            {
                m_RuntimePlayMode.intValue = newValue;

                // RuntimePlayMode → 联动 EnableHotfix
                if (newValue == (int)AssetPlayMode.OfflinePlayMode)
                {
                    // 离线模式 → 强制关闭热更
                    m_EnableHotfix.boolValue = false;
                }
                else
                {
                    // 联机模式 → 强制开启热更
                    m_EnableHotfix.boolValue = true;
                }
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }
        }

        /// <summary>
        /// 绘制 WebGL 资源策略 Popup，并保留 OnDemand / TagsOnLaunch / AllOnLaunch 原始拼写。
        /// </summary>
        private void DrawWebGLAssetStrategyPopup()
        {
            int currentValue = m_WebGLAssetStrategy.intValue;
            int[] optionValues =
            {
                (int)WebGLAssetStrategy.TagsOnLaunch,
                (int)WebGLAssetStrategy.OnDemand,
                (int)WebGLAssetStrategy.AllOnLaunch,
            };
            string[] optionLabels = { "TagsOnLaunch", "OnDemand", "AllOnLaunch" };

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("WebGL 资源策略：", false, GUILayout.Width(180f));
                EditorGUI.BeginChangeCheck();
                int newValue = EditorUtil.Draw.IntPopup(currentValue, optionLabels, optionValues);
                if (EditorGUI.EndChangeCheck())
                {
                    m_WebGLAssetStrategy.intValue = newValue;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                }
            });
        }
    }
}
