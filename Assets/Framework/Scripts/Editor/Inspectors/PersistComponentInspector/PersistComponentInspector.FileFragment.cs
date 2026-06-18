/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.FileFragment.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   Persist 组件编辑器面板 —— FileFragment 数据区
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PersistComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// FileFragment 初始化：扫描目录，读取各 .dat 文件到内存。
        /// </summary>
        private void OnEnableFileFragment()
        {
            m_FF_Values.Clear();
            m_FF_EditBuffers.Clear();
            m_FF_EditStates.Clear();

            string rootPath = Path.Persist.FileFragment.FolderFullPath;
            if (!Util.SysIO.Directory.Exists(rootPath))
            {
                return;
            }

            bool useAES = m_UseAESForFileFragment.boolValue;
            foreach (var filePath in Util.SysIO.Directory.GetFiles(rootPath, "*" + c_FFFileExtension, System.IO.SearchOption.TopDirectoryOnly))
            {
                string classify = Util.SysIO.Path.GetFileNameWithoutExtension(filePath);
                var raw = TryReadFragmentFile(filePath, useAES);
                if (raw == null)
                {
                    raw = new Dictionary<string, string>();
                }

                m_FF_Values[classify] = raw;
                m_FF_EditBuffers[classify] = new Dictionary<string, string>();
                m_FF_EditStates[classify] = new Dictionary<string, bool>();

                foreach (var item in raw.Keys)
                {
                    m_FF_EditBuffers[classify][item] = string.Empty;
                    m_FF_EditStates[classify][item] = false;
                }
            }
        }

        /// <summary>
        /// 绘制 Editor 模式 FileFragment 数据区（扫描 .dat 文件并解析，支持编辑与删除）。
        /// </summary>
        private void DrawEditorFileFragment()
        {
            bool useAES = m_UseAESForFileFragment.boolValue;
            string rootPath = Path.Persist.FileFragment.FolderFullPath;
            string header = $"FileFragment（Editor）({m_FF_Values.Count})";

            bool sectionOpen = false;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                sectionOpen = DrawClassifyFoldout("FF_section", header);
                EditorUtil.Draw.FlexibleSpace();
                if (Util.SysIO.Directory.Exists(rootPath))
                {
                    EditorUtil.Draw.Button("打开文件夹", false, () =>
                        EditorUtil.FileSystem.OpenFolder(rootPath), GUILayout.Width(75));
                }
                if (m_FF_Values.Count > 0)
                {
                    EditorUtil.Draw.DangerButton("清除全部", false, () =>
                    {
                        if (EditorUtil.Draw.Panel.Confirm("确认清除", "将删除 FileFragment 中的全部 Persist 数据，此操作不可撤销。"))
                        {
                            ClearAllFileFragment(rootPath);
                        }
                    }, GUILayout.Width(70));
                }
            });

            if (!sectionOpen)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                m_FFSearchText = DrawBackendSearchBar(m_FFSearchText);
                if (m_FF_Values.Count == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    var classifies = new List<string>(m_FF_Values.Keys);
                    foreach (var classify in classifies)
                    {
                        var values = m_FF_Values[classify];

                        bool hasMatch = false;
                        foreach (var kv in values)
                        {
                            if (MatchSearch(m_FFSearchText, classify, kv.Key, kv.Value))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        string key = "FF_ed_" + classify;
                        bool open = false;
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            open = DrawClassifyFoldout(key, $"{classify} ({values.Count})");
                            EditorUtil.Draw.FlexibleSpace();
                            EditorUtil.Draw.DangerButton("清除", false, () =>
                                DeleteFragmentClassify(classify, rootPath), GUILayout.Width(44));
                        });

                        if (!open)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            if (values.Count == 0)
                            {
                                EditorUtil.Draw.Label("（无法解析，可能已启用 AES 加密）", EditorStyles.centeredGreyMiniLabel, false);
                            }
                            else
                            {
                                var items = new List<string>(values.Keys);
                                foreach (var item in items)
                                {
                                    string raw = values[item];
                                    if (!MatchSearch(m_FFSearchText, classify, item, raw))
                                    {
                                        continue;
                                    }

                                    string buf = m_FF_EditBuffers[classify].TryGetValue(item, out var b) ? b : string.Empty;
                                    bool editing = m_FF_EditStates[classify].TryGetValue(item, out var e) && e;

                                    DrawItemRow(item, ref buf, ref editing, raw, false,
                                        plainValue =>
                                        {
                                            m_FF_EditStates[classify][item] = false;
                                            m_FF_EditBuffers[classify][item] = string.Empty;
                                            m_FF_Values[classify][item] = plainValue;
                                            WriteFragmentFile(Util.SysIO.Path.Combine(rootPath, classify + c_FFFileExtension), m_FF_Values[classify], useAES);
                                        },
                                        () =>
                                        {
                                            m_FF_Values[classify].Remove(item);
                                            m_FF_EditBuffers[classify].Remove(item);
                                            m_FF_EditStates[classify].Remove(item);
                                            if (m_FF_Values[classify].Count == 0)
                                            {
                                                DeleteFragmentClassify(classify, rootPath);
                                            }
                                            else
                                            {
                                                WriteFragmentFile(Util.SysIO.Path.Combine(rootPath, classify + c_FFFileExtension), m_FF_Values[classify], useAES);
                                            }
                                        });

                                    m_FF_EditBuffers[classify][item] = buf;
                                    m_FF_EditStates[classify][item] = editing;
                                }
                            }
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 切换 FileFragment AES 开关时，以新加密状态重写所有 .dat 文件（文件级加解密），然后重载内存。
        /// m_FF_Values 中始终存放明文（TryReadFragmentFile 已在加载时解密），可直接作为迁移源。
        /// </summary>
        /// <param name="enableAES">
        /// true：将文件从明文 JSON 重写为 AES 加密文件；false：将文件从 AES 加密重写为明文 JSON。
        /// </param>
        private void MigrateFileFragmentAES(bool enableAES)
        {
            string rootPath = Path.Persist.FileFragment.FolderFullPath;
            foreach (var classify in m_FF_Values)
            {
                WriteFragmentFile(
                    Util.SysIO.Path.Combine(rootPath, classify.Key + c_FFFileExtension),
                    classify.Value,
                    enableAES);
            }
            OnEnableFileFragment();
        }

        /// <summary>
        /// 删除 FileFragment 所有分类文件并清空内存数据。
        /// </summary>
        /// <param name="rootPath">FileFragment 根目录绝对路径。</param>
        private void ClearAllFileFragment(string rootPath)
        {
            var classifies = new List<string>(m_FF_Values.Keys);
            foreach (var classify in classifies)
            {
                DeleteFragmentClassify(classify, rootPath);
            }
        }

        /// <summary>
        /// 删除指定分类对应的 .dat 文件并清除内存中的分类数据。
        /// </summary>
        /// <param name="classify">分类名（对应文件名，不含扩展名）。</param>
        /// <param name="rootPath">FileFragment 根目录绝对路径。</param>
        private void DeleteFragmentClassify(string classify, string rootPath)
        {
            string filePath = Util.SysIO.Path.Combine(rootPath, classify + c_FFFileExtension);
            if (Util.SysIO.File.Exists(filePath))
            {
                Util.SysIO.File.Delete(filePath);
            }

            m_FF_Values.Remove(classify);
            m_FF_EditBuffers.Remove(classify);
            m_FF_EditStates.Remove(classify);
        }

        /// <summary>
        /// 绘制 Runtime 模式 FileFragment 数据区（通过 Manager 读取，只读展示）。
        /// </summary>
        /// <param name="mgr">FileFragment Manager 实例，为 null 时表示 WebGL 平台不可用。</param>
        private void DrawRuntimeFileFragment(IFileFragmentManager mgr)
        {
            if (mgr == null)
            {
                EditorUtil.Draw.Label("FileFragment", "Manager 未初始化", false);
                return;
            }

            string rootPath = Path.Persist.FileFragment.FolderFullPath;
            string[] files = Util.SysIO.Directory.Exists(rootPath)
                ? Util.SysIO.Directory.GetFiles(rootPath, "*" + c_FFFileExtension, System.IO.SearchOption.TopDirectoryOnly)
                : System.Array.Empty<string>();

            bool open = DrawClassifyFoldout("FF_rt", $"FileFragment（Runtime）({files.Length})");
            if (!open)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                m_FFSearchText = DrawBackendSearchBar(m_FFSearchText);
                if (files.Length == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    foreach (var filePath in files)
                    {
                        string classify = Util.SysIO.Path.GetFileNameWithoutExtension(filePath);
                        var items = mgr.GetAllItemNames(classify);

                        bool hasMatch = false;
                        foreach (var item in items)
                        {
                            if (MatchSearch(m_FFSearchText, classify, item, mgr.GetString(classify, item)))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        bool clOpen = DrawClassifyFoldout("FF_rt_" + classify, $"{classify} ({items.Length})");

                        if (!clOpen)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            foreach (var item in items)
                            {
                                string value = mgr.GetString(classify, item);
                                if (!MatchSearch(m_FFSearchText, classify, item, value))
                                {
                                    continue;
                                }

                                EditorUtil.Draw.Layout.Horizontal(() =>
                                {
                                    EditorUtil.Draw.Label(item, false, GUILayout.MinWidth(80));
                                    EditorUtil.Draw.FlexibleSpace();
                                    EditorUtil.Draw.Label(value, EditorStyles.wordWrappedLabel, false, GUILayout.MaxWidth(280));
                                });
                            }
                        });
                    }
                }
            });
        }
    }
}
