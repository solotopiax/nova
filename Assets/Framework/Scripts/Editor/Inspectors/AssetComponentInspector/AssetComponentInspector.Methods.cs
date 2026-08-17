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
            // 顶层：实现选择（不加 Foldout，平铺展示）
            EditorUtil.Draw.TypesSelector("Asset 管理器", m_AssetManagerTypeNames, m_CurAssetManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IAssetManager 接口后，该类型将自动出现在此列表中。" });

            EditorUtil.Draw.Line();

            // 顶层平铺：加载模式（不属于热更范畴，与加载管理器同级）
            // 编辑器模式 —— 永远 enable，4 选 1 自定义 Popup（与下方 RuntimePlayMode 视觉一致：枚举原名无空格）
            DrawEditorPlayModePopup();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)编辑器下的资源加载模式",
                "(2)EditorSimulateMode 直接读取 Editor 资源",
                "(3)EditorSimulateMode 不产生资源补丁，不会进入 ProcedureHotfix",
                "(4)开发期推荐使用，无网络开销"
            }, false, GUILayout.ExpandWidth(true));

            // 终端模式 —— 永远 enable，3 选 1 自定义 Popup（禁 EditorSimulateMode）
            // EditorUtil.Draw 无 IntPopup 封装，此处局部实现以满足限制选项集需求
            DrawRuntimePlayModePopup();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)终端发布版的资源加载模式",
                "(2)OfflinePlayMode 不连服，与 EnableHotfix = false 双向联动",
                "(3)HostPlayMode 与 WebPlayMode 联机，与 EnableHotfix = true 双向联动"
            }, false, GUILayout.ExpandWidth(true));

            EditorUtil.Draw.Line();

            // 顶层平铺：资源包名列表（③ 资源包配置段首项；不使用 Foldout，直接平铺增删）
            DrawPackagesList();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)本组件管理的所有资源包名",
                "(2)默认含 Default 包，多包项目按需追加",
                "(3)DefaultPackageName 留空时回退至此列表首项"
            }, false, GUILayout.ExpandWidth(true));

            // 顶层平铺：默认包名（下拉，选项严格来自 Packages 列表）
            DrawDefaultPackageNamePopup();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)从 资源包名列表 中选择默认包",
                "(2)新增/重命名包名后请在此重新选择"
            }, false, GUILayout.ExpandWidth(true));

            // 顶层平铺：资源解密器类型
            EditorUtil.Draw.Property("资源解密器类型：", m_DecryptorType, true, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)AB 包加密方式，与打包时的加密器保持一致",
                "(2)None 表示不加密"
            }, false, GUILayout.ExpandWidth(true));

            // 顶层平铺：场景卸载时自动清理
            EditorUtil.Draw.Property("场景卸载时自动清理：", m_AutoCleanupOnSceneUnload, true, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)勾选后场景卸载时自动调用默认包 CleanupAsync 释放未引用资源",
                "(2)未勾选时由业务侧自行决定清理时机"
            }, false, GUILayout.ExpandWidth(true));

            EditorUtil.Draw.Line();

            // 唯一分组：热更配置（EnableHotfix 为总开关，置于首位）
            if (EditorUtil.Draw.Foldout("热更配置", "AssetHotfixConfigGroup", true))
            {
                // 0. 总开关 —— 关闭后直跳 LoadDll；与 RuntimePlayMode 双向联动
                EditorGUI.BeginChangeCheck();
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.Property("启用热更新：", m_EnableHotfix, true, GUILayout.Width(180f));
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
                        "(1)热更新功能总开关",
                        "(2)关闭后只跳过资源热更新检查与下载流程",
                        "(3)App 大版本检查与应用更新流程不受此开关影响",
                        "(4)关闭后 RuntimePlayMode 自动锁定为 OfflinePlayMode"
                    }, false, GUILayout.ExpandWidth(true));
                });

                // 以下字段在 EnableHotfix==false 时联动灰度禁用
                using (new EditorGUI.DisabledScope(!m_EnableHotfix.boolValue))
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)运行时按当前节点上的 DevelopMode 选择 Debug 或 Release 这一组地址",
                            "(2)支持 {Platform}/{Channel}/{Package}/{Version} 占位符，框架会在运行时替换",
                            "(3){Platform}=PlatformType 枚举名；{Channel}=Config 导出时选中的渠道；{Package}=YooAsset 当前资源包名；{Version}=Application.version",
                            "(4)主地址失败后，同一资源包后续新下载改走备用地址；已开始的下载不受影响",
                            "(5)备用地址失败后不会自动绕回主地址"
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
                                "(1)该功能用于让指定测试设备在启动时使用独立版本文件地址进行灰度验证",
                                "(2)仅在启用热更新且 HostPlayMode / WebPlayMode 下生效",
                                "(3)关闭或当前 DevelopMode 没有可用 URL 时自动跳过，不阻断启动",
                                "(4)测试设备首次正常启动后缓存稳定 DeviceID，后续启动当次参与白名单判断",
                                "(5)运行时按 DevelopMode 选择 Debug 或 Release 主备地址",
                                "(6)配置文件 VersionsCheckWhiteList.json 为 DeviceID JSON 字符串数组",
                                "(7)命中后只切换 YooAsset 版本元数据，Bundle 仍走上方常规主机服务器",
                                "(8)支持 {Platform}/{Channel}/{Package}/{Version}；这些地址沿用资源下载机制，不进入业务接口的 DoH 路由"
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
                        }
                    }
                }

                // 1. 启动期切片下载 tag 列表 —— 数组 Foldout 禁用时只降低内容色，保留 Inspector 原始背景
                bool enableHotfix = m_EnableHotfix.boolValue;
                using (new EditorGUI.DisabledScope(!enableHotfix))
                {
                    Color previousContentColor = GUI.contentColor;
                    if (!enableHotfix)
                    {
                        GUI.contentColor = new Color(0.5f, 0.5f, 0.5f, previousContentColor.a);
                    }

                    try
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(16f);
                            EditorUtil.Draw.PropertyField(m_LaunchHotfixTags, "启动期热更 Tag 列表：", true);
                        });
                    }
                    finally
                    {
                        GUI.contentColor = previousContentColor;
                    }

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)空列表：启动期对全部资源做整包差异更新（适合包体小或单机项目）",
                            "(2)填入 tag 列表：启动期仅更新命中这些 tag 的资源，其余资源在运行时按需增量下载（适合中重度或含 DLC 的项目）",
                            "(3)需配套首包构建按 tag 内置使用"
                        }, false, GUILayout.ExpandWidth(true));
                    });
                }

                using (new EditorGUI.DisabledScope(!m_EnableHotfix.boolValue))
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
                            "清空 YooAsset Editor 沙盒缓存及框架自主保存的 .version 文件；DeviceID 与其他本地文件不会删除"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 2. 补丁就绪自动开始下载 —— 决定整个补丁流程是否启动
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("补丁就绪自动开始下载：", m_AutoHotfix, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)勾选后启动期补丁清单就绪即自动开始下载",
                            "(2)未勾选时需由业务侧手动触发下载"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 3. 失败/取消时强制退出 —— 决定异常路径行为
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("失败/取消时强制退出：", m_QuitOnFailedOrCancel, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)单文件失败后，先按「下载失败重试次数」自动重试",
                            "(2)任一文件耗尽重试后，整批停止并显示失败弹窗",
                            "(3)点击「重试」重新下载整批文件，次数不限",
                            "(4)点击「取消」：勾选则退出应用；未勾选则跳过热更进入游戏"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 4. 下载最大并发数 —— 性能与限速核心参数
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("下载最大并发数：", m_MaxDownloadConcurrency, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)同时下载的单文件数量",
                            "(2)建议 3-8；过高可能被限速，过低会降低下载速度"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 5. 下载失败重试次数 —— 容错策略
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("下载失败重试次数：", m_RetryDownloadCount, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)每个文件下载失败后的自动重试次数",
                            "(2)0 表示不自动重试；首次失败即终止整批下载"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 6. 热更完成后自动清理旧缓存 —— 磁盘管理策略
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
                            "(3)已删除的文件再次需要时必须重新下载"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 7. 版本检查请求超时 —— 控制远端版本文件请求的总时长
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
                            "(1)单次远端版本文件请求的总时长上限",
                            "(2)超时后按备用地址和本地版本回退策略继续"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 8. 单文件字节流入超时 —— 只检测连续无新字节的停滞时间
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
                            "(1)单个文件连续无新字节流入的时长上限",
                            "(2)收到任意新字节后重新计时",
                            "(3)超时后按「下载失败重试次数」自动重试"
                        }, false, GUILayout.ExpandWidth(true));
                    });
                }
            }

            EditorUtil.Draw.Line();
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
        /// 绘制 EditorPlayMode 自定义 Popup（4 选 1，全部枚举可用）。
        /// 与 RuntimePlayMode 共用同款 IntPopup，避免 PropertyField 默认 nicify 把
        /// HostPlayMode 拆成 "Host Play Mode" 导致同面板上下风格分裂。
        /// </summary>
        private void DrawEditorPlayModePopup()
        {
            int curValue = m_EditorPlayMode.intValue;

            int[] optionValues = { (int)AssetPlayMode.EditorSimulateMode, (int)AssetPlayMode.OfflinePlayMode, (int)AssetPlayMode.HostPlayMode, (int)AssetPlayMode.WebPlayMode };
            string[] optionLabels = { "EditorSimulateMode", "OfflinePlayMode", "HostPlayMode", "WebPlayMode" };

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
        /// 绘制 RuntimePlayMode 自定义 Popup（3 选 1，禁 EditorSimulateMode）。
        /// 含联动逻辑：选中 OfflinePlayMode 时强制 EnableHotfix=false；选中其他时强制 EnableHotfix=true。
        /// </summary>
        private void DrawRuntimePlayModePopup()
        {
            // 异常值归一：若当前为 EditorSimulateMode（运行时不允许），回落到 OfflinePlayMode
            int curValue = m_RuntimePlayMode.intValue;
            if (curValue == (int)AssetPlayMode.EditorSimulateMode)
                curValue = (int)AssetPlayMode.OfflinePlayMode;

            int[] optionValues = { (int)AssetPlayMode.OfflinePlayMode, (int)AssetPlayMode.HostPlayMode, (int)AssetPlayMode.WebPlayMode };
            string[] optionLabels = { "OfflinePlayMode", "HostPlayMode", "WebPlayMode" };

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
    }
}
