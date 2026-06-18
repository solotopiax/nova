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
                "(3)开发期推荐使用，无网络开销"
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

            // 唯一分组：热更配置（共 6 个字段，EnableHotfix 为总开关，置于首位）
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
                        "(2)关闭后启动流程直跳 LoadDll",
                        "(3)关闭后跳过版本检查、资源补丁、强更下载三个阶段",
                        "(4)关闭后 RuntimePlayMode 自动锁定为 OfflinePlayMode"
                    }, false, GUILayout.ExpandWidth(true));
                });

                // 以下字段在 EnableHotfix==false 时联动灰度禁用
                using (new EditorGUI.DisabledScope(!m_EnableHotfix.boolValue))
                {
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
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)运行时按当前节点上的 DevelopMode 选择 Debug 或 Release 这一组地址",
                            "(2)支持 {Platform}/{Package}/{Version} 占位符，框架会在运行时替换",
                            "(3){Platform}=PlatformType 枚举名，如 Android/iOS/WebGL；{Package}=YooAsset 当前资源包名，如：\"Default\"；{Version}=Application.version，如 1.0.0"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 1. 补丁就绪自动开始下载 —— 决定整个补丁流程是否启动，最关键
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

                    // 2. 失败/取消时强制退出 —— 决定异常路径行为
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
                            "(1)下载中单文件失败时，由资源系统内部按「下载失败重试次数」自动重试",
                            "(2)整批重试耗尽仍失败时，弹出重试确认弹窗（文本在 ProcedureComponent 的 Launcher 弹窗配置中维护）",
                            "(3)点击「重试」：失败计数清零并重新下载，累计重试次数仅用于日志追踪，不设上限",
                            "(4)点击「取消」：勾选本项则退出应用，不勾选则跳过本次热更新直接进入游戏"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 3. 下载最大并发数 —— 性能与限速核心参数
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
                            "(1)推荐取值 3-8",
                            "(2)数值过高易触发移动端运营商限速",
                            "(3)数值过低会拖慢补丁进度"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 4. 下载失败重试次数 —— 容错策略
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
                            "(1)单文件失败时的自动重试次数",
                            "(2)取值 0 表示一次失败即终止整批下载"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 5. 启动期切片下载 tag 列表 —— 切片策略，空列表=整包下载
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.PropertyField(m_LaunchHotfixTags, "启动期热更 Tag 列表：", true);
                    });
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
                            "(1)勾选后，每次启动热更新完成时自动清理本地不再使用的旧版本资源文件",
                            "(2)不勾选则由业务侧自行决定清理时机",
                            "(3)清理后无法快速回退到旧版本资源"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 8. 版本检查超时 —— 弱网相关，发生频率较低
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("版本检查超时（秒）：", m_CheckTimeout, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)版本请求连续无新字节流入时中止",
                            "(2)数值过短易在弱网环境下误判失败"
                        }, false, GUILayout.ExpandWidth(true));
                    });

                    // 9. 文件下载空闲超时 —— 弱网相关，发生频率较低
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.Property("文件下载空闲超时（秒）：", m_IdleTimeout, true, GUILayout.Width(180f));
                    });
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(16f);
                        EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                        {
                            "(1)下载过程中连续无新字节流入即中止单文件请求",
                            "(2)不影响整批下载的总时长上限"
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
