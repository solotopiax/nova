/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.SQLite.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   Persist 组件编辑器面板 —— SQLite 数据区
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PersistComponentInspector : BaseComponentInspector
    {
#if !UNITY_WEBGL
        /// <summary>
        /// SQLite 辅助行类型（用于 GetAllData 查询 Name/Value 列）。
        /// </summary>
        private class SQLiteRow
        {
            /// <summary>
            /// 条目名（对应数据库 Name 列）。
            /// SQLite ORM (Query) 需要 set 访问器来反射赋值，因此保留 { get; set; }。
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// 条目值（对应数据库 Value 列，可能经过 AES 加密）。
            /// SQLite ORM (Query) 需要 set 访问器来反射赋值，因此保留 { get; set; }。
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// SQLite 初始化：非运行时打开连接并加载数据，运行时从 Manager 读取。
        /// </summary>
        private void OnEnableSQLite()
        {
            m_SQL_Values.Clear();
            m_SQL_EditBuffers.Clear();
            m_SQL_EditStates.Clear();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string dbPath = Path.Persist.SQLite.FileFullPath;
            if (!Util.SysIO.File.Exists(dbPath))
            {
                return;
            }

            try
            {
                string pwd = m_SQLiteCipherPassword?.stringValue;
                m_SQLiteConnection = Util.SQLite.CreateConnection(dbPath, string.IsNullOrEmpty(pwd) ? null : pwd);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Editor, $"SQLite 无法建立连接：{e.Message}");
                return;
            }

            LoadSQLiteEditorData();
        }

        /// <summary>
        /// SQLite 禁用：提交并关闭连接。
        /// </summary>
        private void OnDisableSQLite()
        {
            if (m_SQLiteConnection != null)
            {
                Util.SQLite.CloseConnection(m_SQLiteConnection);
                m_SQLiteConnection = null;
            }
        }

        /// <summary>
        /// 切换 SQLite AES 开关时，将数据库中全部条目做批量明文↔密文转换并回写，然后重载内存。
        /// m_SQL_Values 存放的是数据库原始值（LoadSQLiteEditorData 不做解密），可直接作为迁移源。
        /// </summary>
        /// <param name="enableAES">
        /// true：当前存档为明文，转为密文；false：当前存档为密文，转为明文。
        /// </param>
        private void MigrateSQLiteAES(bool enableAES)
        {
            if (m_SQLiteConnection == null)
            {
                return;
            }

            foreach (var table in m_SQL_Values)
            {
                foreach (var item in table.Value)
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
                    Util.SQLite.UpdateData(m_SQLiteConnection, table.Key, item.Key,
                        new Dictionary<string, object> { { "Value", migrated } });
                }
            }
            Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
            LoadSQLiteEditorData();
        }

        /// <summary>
        /// 获取明文临时副本的绝对路径（与原 DB 同目录，固定命名 game_preview.db）。
        /// </summary>
        private string GetPreviewDbPath()
        {
            return Util.SysIO.Path.Combine(Util.SysIO.Path.GetDirectoryName(Path.Persist.SQLite.FileFullPath), "game_preview.db");
        }

        /// <summary>
        /// 打开 SQLiteStudio 可视化工具。
        /// 无 Cipher 密码时直接打开原文件；有 Cipher 密码时先导出明文临时副本（同时解密 AES 条目），
        /// 用 SQLiteStudio 打开副本，编辑完成后点击"应用预览"按钮可将修改回写到原加密数据库。
        /// </summary>
        private void OpenSQLiteStudioDecrypted()
        {
            string dbPath = Path.Persist.SQLite.FileFullPath;
            string cipherPwd = m_SQLiteCipherPassword.stringValue;

            // 无 Cipher 密码，直接打开原文件
            if (string.IsNullOrEmpty(cipherPwd))
            {
                EditorUtil.FileSystem.OpenSQLiteStudio(dbPath);
                return;
            }

            // 有 Cipher 密码，导出去除 Cipher 的副本供 SQLiteStudio 打开。
            // 条目值原样复制（AES 勾选时为密文，未勾选时为明文），与数据库实际存储格式保持一致。
            string tempPath = GetPreviewDbPath();
            Util.SQLite.DeleteDatabase(tempPath);

            SqlCipher4Unity3D.SQLiteConnection tempConn = null;
            try
            {
                tempConn = Util.SQLite.CreateConnection(tempPath, null);
                var tableNames = Util.SQLite.GetTableNames(m_SQLiteConnection);

                foreach (var tableName in tableNames)
                {
                    Util.SQLite.CreateTable(tempConn, tableName, new Dictionary<string, Type> { { "Value", typeof(string) } });
                    var rows = Util.SQLite.GetAllData<SQLiteRow>(m_SQLiteConnection, tableName);
                    foreach (var row in rows)
                    {
                        if (row?.Name == null)
                        {
                            continue;
                        }

                        Util.SQLite.InsertOrReplaceData(tempConn, tableName, row.Name, new Dictionary<string, object> { { "Value", row.Value ?? string.Empty } });
                    }
                }

                Util.SQLite.CommitAndBeginTransaction(tempConn);
                Util.SQLite.CloseConnection(tempConn);

                Log.Debug(LogTag.Editor, $"已导出副本至 {tempPath}，在 SQLiteStudio 中修改完毕后，点击 Inspector 中 [应用预览] 将修改回写到原加密数据库。");
                EditorUtil.FileSystem.OpenSQLiteStudio(tempPath);
            }
            catch (Exception e)
            {
                if (tempConn != null)
                {
                    Util.SQLite.CloseConnection(tempConn);
                }

                Util.SQLite.DeleteDatabase(tempPath);
                Log.Error(LogTag.Editor, $"导出副本失败：{e.Message}");
            }
        }

        /// <summary>
        /// 将临时副本（game_preview.db）的修改回写到原加密数据库。
        /// 副本中的条目值格式与原 DB 一致（AES 开启时为密文，关闭时为明文），直接原样回写，不做额外加解密。
        /// 完成后删除副本并重载编辑器数据。
        /// </summary>
        private void ApplyPreviewChanges()
        {
            string tempPath = GetPreviewDbPath();

            if (!Util.SysIO.File.Exists(tempPath))
            {
                Log.Warning(LogTag.Editor, "未找到临时副本文件，请先点击 [可视化工具] 生成预览。");
                return;
            }

            if (m_SQLiteConnection == null)
            {
                Log.Error(LogTag.Editor, "SQLite 连接未建立，无法应用预览修改。");
                return;
            }

            SqlCipher4Unity3D.SQLiteConnection previewConn = null;
            try
            {
                previewConn = Util.SQLite.CreateConnection(tempPath, null);

                // 清空原加密 DB，从副本原样重建（值格式已与原 DB 一致，不做加解密）
                Util.SQLite.ClearDatabase(m_SQLiteConnection);

                var tableNames = Util.SQLite.GetTableNames(previewConn);
                foreach (var tableName in tableNames)
                {
                    Util.SQLite.CreateTable(m_SQLiteConnection, tableName, new Dictionary<string, Type> { { "Value", typeof(string) } });
                    var rows = Util.SQLite.GetAllData<SQLiteRow>(previewConn, tableName);
                    foreach (var row in rows)
                    {
                        if (row?.Name == null)
                        {
                            continue;
                        }

                        Util.SQLite.InsertOrReplaceData(m_SQLiteConnection, tableName, row.Name, new Dictionary<string, object> { { "Value", row.Value ?? string.Empty } });
                    }
                }

                Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                Util.SQLite.CloseConnection(previewConn);
                Util.SQLite.DeleteDatabase(tempPath);

                LoadSQLiteEditorData();
                Log.Debug(LogTag.Editor, "预览修改已成功回写到原加密数据库，临时副本已删除。");
            }
            catch (Exception e)
            {
                if (previewConn != null)
                {
                    Util.SQLite.CloseConnection(previewConn);
                }

                Log.Error(LogTag.Editor, $"应用预览修改失败：{e.Message}");
            }
        }

        /// <summary>
        /// 修改 SQLite Cipher 密码后，用新密码重建数据库文件（删旧建新），并将内存中的原始存档数据原样写入，
        /// 维持当前 AES 条目加密状态不变，仅更换文件级 Cipher Key。完成后重载编辑器数据。
        /// </summary>
        private void RefreshCipherEncrypt()
        {
            // 快照当前内存中的原始 DB 值（可能已被 AES 加密，按原样迁移）
            var snapshot = new SortedDictionary<string, SortedDictionary<string, string>>();
            foreach (var table in m_SQL_Values)
            {
                snapshot[table.Key] = new SortedDictionary<string, string>(table.Value);
            }

            // 关闭旧连接
            OnDisableSQLite();

            // 删除旧数据库文件（含 -shm / -wal / -journal 全部附属文件，统一走 Util.SQLite.DeleteDatabase）
            string dbPath = Path.Persist.SQLite.FileFullPath;
            Util.SQLite.DeleteDatabase(dbPath);

            // 保存新密码到序列化属性
            m_SQLiteCipherPassword.stringValue = m_TmpSQLiteCipherPassword;
            m_SQLiteCipherPassword.serializedObject.ApplyModifiedProperties();

            // 用新 Cipher 密码创建连接
            try
            {
                string pwd = m_SQLiteCipherPassword.stringValue;
                m_SQLiteConnection = Util.SQLite.CreateConnection(dbPath, string.IsNullOrEmpty(pwd) ? null : pwd);
            }
            catch
            {
                Log.Error(LogTag.Editor, "存档转换失败：无法以新密码建立 SQLite 连接，请检查设置。");
                return;
            }

            // 将快照数据原样写入新数据库（维持原 AES 加密状态）
            foreach (var table in snapshot)
            {
                Util.SQLite.CreateTable(m_SQLiteConnection, table.Key, new Dictionary<string, Type> { { "Value", typeof(string) } });
                foreach (var item in table.Value)
                {
                    Util.SQLite.InsertOrReplaceData(m_SQLiteConnection, table.Key, item.Key, new Dictionary<string, object> { { "Value", item.Value } });
                }
            }
            Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);

            // 重载编辑器数据
            LoadSQLiteEditorData();

            Log.Debug(LogTag.Editor, "SQLite 存档转换完成，Cipher 密码已更新。");
        }

        private void LoadSQLiteEditorData()
        {
            m_SQL_Values.Clear();
            m_SQL_EditBuffers.Clear();
            m_SQL_EditStates.Clear();

            if (m_SQLiteConnection == null)
            {
                return;
            }

            var tableNames = Util.SQLite.GetTableNames(m_SQLiteConnection);
            foreach (var tableName in tableNames)
            {
                var rows = Util.SQLite.GetAllData<SQLiteRow>(m_SQLiteConnection, tableName);
                var values = new SortedDictionary<string, string>();
                var buffers = new SortedDictionary<string, string>();
                var states = new SortedDictionary<string, bool>();

                foreach (var row in rows)
                {
                    if (row?.Name == null)
                    {
                        continue;
                    }

                    values[row.Name] = row.Value ?? string.Empty;
                    buffers[row.Name] = string.Empty;
                    states[row.Name] = false;
                }

                m_SQL_Values[tableName] = values;
                m_SQL_EditBuffers[tableName] = buffers;
                m_SQL_EditStates[tableName] = states;
            }
        }

        /// <summary>
        /// 绘制 Editor 模式 SQLite 数据区（通过独立连接直接读写数据库，支持编辑、删除与清空）。
        /// </summary>
        private void DrawEditorSQLite()
        {
            if (m_SQLiteConnection == null && !Util.SysIO.File.Exists(Path.Persist.SQLite.FileFullPath))
            {
                DrawClassifyFoldout("SQL_section", "SQLite（Editor）（文件不存在）");
                return;
            }

            if (m_SQLiteConnection == null)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    DrawClassifyFoldout("SQL_section", "SQLite（Editor）（连接失败）");
                    EditorUtil.Draw.DangerButton("删除数据库", false, () =>
                    {
                        if (EditorUtil.Draw.Panel.Confirm("确认删除", "将永久删除 SQLite 数据库文件。"))
                        {
                            Util.SQLite.DeleteDatabase(Path.Persist.SQLite.FileFullPath);
                            OnEnableSQLite();
                        }
                    }, GUILayout.Width(80));
                });
                return;
            }

            bool useAES = m_UseAESForSQLite.boolValue;
            string header = $"SQLite（Editor）({m_SQL_Values.Count})";

            bool sectionOpen = false;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                sectionOpen = DrawClassifyFoldout("SQL_section", header);
                EditorUtil.Draw.FlexibleSpace();
#if UNITY_EDITOR_WIN
                if (Util.SysIO.File.Exists(Path.Persist.SQLite.FileFullPath))
                {
                    EditorUtil.Draw.Button("可视化工具", false, OpenSQLiteStudioDecrypted, GUILayout.Width(80));
                }

                if (!string.IsNullOrEmpty(m_SQLiteCipherPassword.stringValue) && Util.SysIO.File.Exists(GetPreviewDbPath()))
                {
                    EditorUtil.Draw.Button("应用预览", false, ApplyPreviewChanges, GUILayout.Width(70));
                }
#endif
                EditorUtil.Draw.Button("打开文件夹", false, () =>
                    EditorUtil.FileSystem.OpenFolder(Path.Persist.SQLite.FolderFullPath), GUILayout.Width(75));
                if (m_SQL_Values.Count > 0)
                {
                    EditorUtil.Draw.DangerButton("清除全部", false, () =>
                    {
                        if (EditorUtil.Draw.Panel.Confirm("确认清除", "将删除数据库中的所有表和数据，此操作不可撤销。"))
                        {
                            Util.SQLite.ClearDatabase(m_SQLiteConnection);
                            Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                            LoadSQLiteEditorData();
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
                m_SQLSearchText = DrawBackendSearchBar(m_SQLSearchText);
                if (m_SQL_Values.Count == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    var classifies = new List<string>(m_SQL_Values.Keys);
                    foreach (var classify in classifies)
                    {
                        var values = m_SQL_Values[classify];

                        bool hasMatch = false;
                        foreach (var kv in values)
                        {
                            if (MatchSearch(m_SQLSearchText, classify, kv.Key, kv.Value))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        string key = "SQL_ed_" + classify;
                        bool open = false;
                        string classifyCapture = classify;
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            open = DrawClassifyFoldout(key, $"{classify} ({values.Count})");
                            EditorUtil.Draw.FlexibleSpace();
                            EditorUtil.Draw.DangerButton("清除", false, () =>
                            {
                                Util.SQLite.DeleteTable(m_SQLiteConnection, classifyCapture);
                                Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                                m_SQL_Values.Remove(classifyCapture);
                                m_SQL_EditBuffers.Remove(classifyCapture);
                                m_SQL_EditStates.Remove(classifyCapture);
                            }, GUILayout.Width(44));
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
                                if (!MatchSearch(m_SQLSearchText, classify, item, raw))
                                {
                                    continue;
                                }

                                string buf = m_SQL_EditBuffers[classify].TryGetValue(item, out var b) ? b : string.Empty;
                                bool editing = m_SQL_EditStates[classify].TryGetValue(item, out var e) && e;

                                DrawItemRow(item, ref buf, ref editing, raw, useAES,
                                    plainValue =>
                                    {
                                        m_SQL_EditStates[classify][item] = false;
                                        m_SQL_EditBuffers[classify][item] = string.Empty;
                                        string stored = EncodeForStorage(plainValue, useAES);
                                        m_SQL_Values[classify][item] = stored;
                                        Util.SQLite.UpdateData(m_SQLiteConnection, classify, item, new Dictionary<string, object> { { "Value", stored } });
                                        Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                                    },
                                    () =>
                                    {
                                        Util.SQLite.DeleteData(m_SQLiteConnection, classify, item);
                                        Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                                        m_SQL_Values[classify].Remove(item);
                                        m_SQL_EditBuffers[classify].Remove(item);
                                        m_SQL_EditStates[classify].Remove(item);

                                        if (m_SQL_Values[classify].Count == 0)
                                        {
                                            Util.SQLite.DeleteTable(m_SQLiteConnection, classify);
                                            Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                                            m_SQL_Values.Remove(classify);
                                            m_SQL_EditBuffers.Remove(classify);
                                            m_SQL_EditStates.Remove(classify);
                                        }
                                    });

                                m_SQL_EditBuffers[classify][item] = buf;
                                m_SQL_EditStates[classify][item] = editing;
                            }
                        });
                    }
                }
            });
        }
#endif

        /// <summary>
        /// 绘制 Runtime 模式 SQLite 数据区（通过 Manager 读取，只读展示）。
        /// </summary>
        /// <param name="mgr">SQLite Manager 实例，为 null 时显示未初始化提示。</param>
        private void DrawRuntimeSQLite(ISQLiteManager mgr)
        {
            if (mgr == null)
            {
                EditorUtil.Draw.Label("SQLite", "Manager 未初始化", false);
                return;
            }

            var classifyNames = mgr.GetAllClassifyNames();
            bool open = DrawClassifyFoldout("SQL_rt", $"SQLite（Runtime）({classifyNames.Length})");
            if (!open)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                m_SQLSearchText = DrawBackendSearchBar(m_SQLSearchText);
                if (classifyNames.Length == 0)
                {
                    EditorUtil.Draw.Label("（暂无数据）", EditorStyles.centeredGreyMiniLabel, false);
                }
                else
                {
                    foreach (var classify in classifyNames)
                    {
                        var items = mgr.GetAllItemNames(classify);

                        bool hasMatch = false;
                        foreach (var item in items)
                        {
                            if (MatchSearch(m_SQLSearchText, classify, item, mgr.GetString(classify, item)))
                            {
                                hasMatch = true;
                                break;
                            }
                        }

                        if (!hasMatch)
                        {
                            continue;
                        }

                        bool clOpen = DrawClassifyFoldout("SQL_rt_" + classify, $"{classify} ({items.Length})");

                        if (!clOpen)
                        {
                            continue;
                        }

                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            foreach (var item in items)
                            {
                                string display = mgr.GetString(classify, item);
                                if (!MatchSearch(m_SQLSearchText, classify, item, display))
                                {
                                    continue;
                                }

                                EditorUtil.Draw.Layout.Horizontal(() =>
                                {
                                    EditorUtil.Draw.Label(item, false, GUILayout.MinWidth(80));
                                    EditorUtil.Draw.FlexibleSpace();
                                    EditorUtil.Draw.Label(display, EditorStyles.wordWrappedLabel, false, GUILayout.MaxWidth(280));
                                });
                            }
                        });
                    }
                }
            });
        }
    }
}
