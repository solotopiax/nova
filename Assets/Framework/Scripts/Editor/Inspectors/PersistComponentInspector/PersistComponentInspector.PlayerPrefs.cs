/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.PlayerPrefs.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   Persist 组件编辑器面板 —— PlayerPrefs 数据区
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
        /// PlayerPrefs 初始化：加载分类索引与条目值到内存。
        /// </summary>
        private void OnEnablePlayerPrefs()
        {
            m_PP_Values.Clear();
            m_PP_EditBuffers.Clear();
            m_PP_EditStates.Clear();

            var classifyRaw = UnityEngine.PlayerPrefs.GetString(c_PPClassifyIndexKey, "");
            if (string.IsNullOrEmpty(classifyRaw))
            {
                return;
            }

            foreach (var classify in classifyRaw.Split(','))
            {
                if (string.IsNullOrEmpty(classify))
                {
                    continue;
                }

                var itemsRaw = UnityEngine.PlayerPrefs.GetString(classify + c_PPItemIndexSuffix, "");
                var values = new SortedDictionary<string, string>();
                var buffers = new SortedDictionary<string, string>();
                var states = new SortedDictionary<string, bool>();

                foreach (var item in itemsRaw.Split(','))
                {
                    if (string.IsNullOrEmpty(item))
                    {
                        continue;
                    }

                    string raw = UnityEngine.PlayerPrefs.GetString(classify + c_PPKeySeparator + item, "");
                    values[item] = raw;
                    buffers[item] = string.Empty;
                    states[item] = false;
                }

                m_PP_Values[classify] = values;
                m_PP_EditBuffers[classify] = buffers;
                m_PP_EditStates[classify] = states;
            }
        }

        /// <summary>
        /// 绘制 Editor 模式 PlayerPrefs 数据区（直接读写 UnityEngine.PlayerPrefs，支持编辑与删除）。
        /// </summary>
        private void DrawEditorPlayerPrefs()
        {
            bool useAES = m_UseAESForPlayerPrefs.boolValue;
            string header = $"PlayerPrefs（Editor）({m_PP_Values.Count})";

            bool sectionOpen = false;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                sectionOpen = DrawClassifyFoldout("PP_section", header);
                if (m_PP_Values.Count > 0)
                {
                    EditorUtil.Draw.FlexibleSpace();
                    EditorUtil.Draw.DangerButton("清除全部", false, () =>
                    {
                        if (EditorUtil.Draw.Panel.Confirm("确认清除", "将删除 PlayerPrefs 中的全部 Persist 数据，此操作不可撤销。"))
                        {
                            ClearAllPlayerPrefs();
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
                m_PPSearchText = DrawBackendSearchBar(m_PPSearchText);
                if (m_PP_Values.Count == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    var classifies = new List<string>(m_PP_Values.Keys);
                    foreach (var classify in classifies)
                    {
                        var values = m_PP_Values[classify];

                        bool hasMatch = false;
                        foreach (var kv in values)
                        {
                            if (MatchSearch(m_PPSearchText, classify, kv.Key, kv.Value))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        string key = "PP_ed_" + classify;
                        bool open = false;
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            open = DrawClassifyFoldout(key, $"{classify} ({values.Count})");
                            EditorUtil.Draw.FlexibleSpace();
                            EditorUtil.Draw.DangerButton("清除", false, () =>
                                DeletePlayerPrefsClassify(classify), GUILayout.Width(44));
                        });

                        if (!open)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            var items = new List<string>(values.Keys);
                            foreach (var item in items)
                            {
                                string raw = values[item];
                                if (!MatchSearch(m_PPSearchText, classify, item, raw))
                                {
                                    continue;
                                }

                                string buf = m_PP_EditBuffers[classify][item];
                                bool editing = m_PP_EditStates[classify][item];

                                DrawItemRow(item, ref buf, ref editing, raw, useAES,
                                    plainValue =>
                                    {
                                        m_PP_EditStates[classify][item] = false;
                                        m_PP_EditBuffers[classify][item] = string.Empty;
                                        string stored = EncodeForStorage(plainValue, useAES);
                                        UnityEngine.PlayerPrefs.SetString(classify + c_PPKeySeparator + item, stored);
                                        UnityEngine.PlayerPrefs.Save();
                                        m_PP_Values[classify][item] = stored;
                                    },
                                    () => DeletePlayerPrefsItem(classify, item));

                                m_PP_EditBuffers[classify][item] = buf;
                                m_PP_EditStates[classify][item] = editing;
                            }
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 切换 PlayerPrefs AES 开关时，将已存储的全部条目做批量明文↔密文转换并回写，然后重载内存。
        /// </summary>
        /// <param name="enableAES">
        /// true：当前存档为明文，转为密文；false：当前存档为密文，转为明文。
        /// </param>
        private void MigratePlayerPrefsAES(bool enableAES)
        {
            foreach (var classify in m_PP_Values)
            {
                foreach (var item in classify.Value)
                {
                    string current = item.Value;
                    string migrated;
                    try
                    {
                        migrated = enableAES
                            ? Util.Encrypt.AES.EncryptString(current)
                            : Util.Encrypt.AES.DecryptString(current);
                    }
                    catch
                    {
                        migrated = current;
                    }
                    UnityEngine.PlayerPrefs.SetString(classify.Key + c_PPKeySeparator + item.Key, migrated);
                }
            }
            UnityEngine.PlayerPrefs.Save();
            OnEnablePlayerPrefs();
        }

        /// <summary>
        /// 删除所有 Persist 托管的 PlayerPrefs 键（分类索引 + 全部条目），并重载内存数据。
        /// </summary>
        private void ClearAllPlayerPrefs()
        {
            foreach (var classify in m_PP_Values.Keys)
            {
                foreach (var item in m_PP_Values[classify].Keys)
                {
                    UnityEngine.PlayerPrefs.DeleteKey(classify + c_PPKeySeparator + item);
                }
                UnityEngine.PlayerPrefs.DeleteKey(classify + c_PPItemIndexSuffix);
            }
            UnityEngine.PlayerPrefs.DeleteKey(c_PPClassifyIndexKey);
            UnityEngine.PlayerPrefs.Save();
            OnEnablePlayerPrefs();
        }

        /// <summary>
        /// 删除指定分类下所有条目键及分类索引，并同步更新内存与分类列表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void DeletePlayerPrefsClassify(string classify)
        {
            foreach (var item in m_PP_Values[classify].Keys)
            {
                UnityEngine.PlayerPrefs.DeleteKey(classify + c_PPKeySeparator + item);
            }
            UnityEngine.PlayerPrefs.DeleteKey(classify + c_PPItemIndexSuffix);

            m_PP_Values.Remove(classify);
            m_PP_EditBuffers.Remove(classify);
            m_PP_EditStates.Remove(classify);

            UnityEngine.PlayerPrefs.SetString(c_PPClassifyIndexKey, string.Join(",", m_PP_Values.Keys));
            UnityEngine.PlayerPrefs.Save();
        }

        /// <summary>
        /// 删除指定条目；若分类因此变为空，级联清除分类记录。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        private void DeletePlayerPrefsItem(string classify, string item)
        {
            UnityEngine.PlayerPrefs.DeleteKey(classify + c_PPKeySeparator + item);
            m_PP_Values[classify].Remove(item);
            m_PP_EditBuffers[classify].Remove(item);
            m_PP_EditStates[classify].Remove(item);

            if (m_PP_Values[classify].Count == 0)
            {
                DeletePlayerPrefsClassify(classify);
            }
            else
            {
                UnityEngine.PlayerPrefs.SetString(
                    classify + c_PPItemIndexSuffix,
                    string.Join(",", m_PP_Values[classify].Keys));
                UnityEngine.PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 绘制 Runtime 模式 PlayerPrefs 数据区（通过 Manager 读取，只读展示）。
        /// </summary>
        /// <param name="mgr">PlayerPrefs Manager 实例，为 null 时显示未初始化提示。</param>
        private void DrawRuntimePlayerPrefs(IPlayerPrefsManager mgr)
        {
            if (mgr == null)
            {
                EditorUtil.Draw.Label("PlayerPrefs", "Manager 未初始化", false);
                return;
            }

            var classifyRaw = UnityEngine.PlayerPrefs.GetString(c_PPClassifyIndexKey, "");
            var classifies = string.IsNullOrEmpty(classifyRaw)
                ? System.Array.Empty<string>()
                : classifyRaw.Split(',');

            bool open = DrawClassifyFoldout("PP_rt", $"PlayerPrefs（Runtime）({classifies.Length})");
            if (!open)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                m_PPSearchText = DrawBackendSearchBar(m_PPSearchText);
                if (classifies.Length == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    foreach (var classify in classifies)
                    {
                        if (string.IsNullOrEmpty(classify))
                        {
                            continue;
                        }

                        var items = mgr.GetAllItemNames(classify);
                        bool hasMatch = false;
                        foreach (var item in items)
                        {
                            if (MatchSearch(m_PPSearchText, classify, item, mgr.GetString(classify, item)))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        bool clOpen = DrawClassifyFoldout("PP_rt_" + classify, $"{classify} ({items.Length})");

                        if (!clOpen)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            foreach (var item in items)
                            {
                                string value = mgr.GetString(classify, item);
                                if (!MatchSearch(m_PPSearchText, classify, item, value))
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
