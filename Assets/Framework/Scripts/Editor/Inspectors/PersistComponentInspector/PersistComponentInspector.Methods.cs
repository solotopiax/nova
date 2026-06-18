/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   Persist 组件编辑器面板定制 —— 公共方法（配置区 + 数据区入口）
 ***************************************************************/

using System;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PersistComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// PlayerPrefs 中存储所有分类名列表的索引键。
        /// </summary>
        private const string c_PPClassifyIndexKey = "__pp_classifies__";

        /// <summary>
        /// 分类条目索引键的后缀，格式：{classify}::__items__。
        /// </summary>
        private const string c_PPItemIndexSuffix = "::__items__";

        /// <summary>
        /// 分类与条目名之间的分隔符，格式：{classify}::{item}。
        /// </summary>
        private const string c_PPKeySeparator = "::";

        /// <summary>
        /// FileFragment 持久化文件的扩展名。
        /// </summary>
        private const string c_FFFileExtension = ".dat";

        /// <summary>
        /// 绘制配置区（管理器选择 + 加密设置）。
        /// </summary>
        private void DrawConfigs()
        {
            DrawManagerSection();
            DrawEncryptSection();
            DrawAutoSaveSection();
        }

        /// <summary>
        /// 绘制管理器类型选择区（三后端下拉选择 + 自定义扩展说明）。
        /// </summary>
        private void DrawManagerSection()
        {
            EditorUtil.Draw.TypesSelector("PlayerPrefs 管理器", m_PlayerPrefsManagerTypeNames, m_CurPlayerPrefsManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("FileFragment 管理器", m_FileFragmentManagerTypeNames, m_CurFileFragmentManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("SQLite 管理器", m_SQLiteManagerTypeNames, m_CurSQLiteManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)实现 IPlayerPrefsManager 接口的自定义类型将出现在 PlayerPrefs 管理器列表",
                "(2)实现 IFileFragmentManager 接口的自定义类型将出现在 FileFragment 管理器列表",
                "(3)实现 ISQLiteManager 接口的自定义类型将出现在 SQLite 管理器列表",
                "(4)SQLite 后端在 WebGL 平台以静默空操作运行",
                "(5)WebGL 下 Initialize 输出警告，Get 返回默认值，Set 被忽略"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制加密配置区（AES 开关 + SQLite Cipher 密码）。
        /// </summary>
        private void DrawEncryptSection()
        {
            EditorUtil.Draw.Toggle("PlayerPrefs 启用 AES 加密", m_UseAESForPlayerPrefs, true, null,
                () => MigratePlayerPrefsAES(m_UseAESForPlayerPrefs.boolValue), GUILayout.Width(175));
            EditorUtil.Draw.Toggle("FileFragment 启用 AES 加密", m_UseAESForFileFragment, true, null,
                () => MigrateFileFragmentAES(m_UseAESForFileFragment.boolValue), GUILayout.Width(175));
#if !UNITY_WEBGL
            EditorUtil.Draw.Toggle("SQLite 启用 AES 加密", m_UseAESForSQLite, true, null,
                () => MigrateSQLiteAES(m_UseAESForSQLite.boolValue), GUILayout.Width(175));
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("SQLite Cipher 密码", true, GUILayout.Width(175));
                m_TmpSQLiteCipherPassword = EditorUtil.Draw.TextField(m_TmpSQLiteCipherPassword, true);
                EditorUtil.Draw.DisabledGroup(m_TmpSQLiteCipherPassword == m_SQLiteCipherPassword.stringValue, () =>
                {
                    EditorUtil.Draw.Button("存档转换", true, RefreshCipherEncrypt, GUILayout.Width(80));
                });
            });
#endif
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)AES 加密勾选后该后端写入自动加密、读取自动解密",
                "(2)Inspector 始终以明文展示存档内容",
                "(3)切换 AES 开关会立即对已有存档执行批量迁移",
                "(4)迁移确保开关状态与实际存储格式一致",
                "(5)SQLite Cipher 密码在 AES 条目加密之外额外对整个数据库文件启用 Cipher 加密",
                "(6)Cipher 加密依赖 SqlCipher4Unity3D",
                "(7)密码为空时跳过文件级加密"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制自动保存配置区（三后端各自的自动保存间隔）。
        /// </summary>
        private void DrawAutoSaveSection()
        {
            EditorUtil.Draw.Property("PlayerPrefs 自动保存间隔(秒)", m_AutoSaveIntervalPlayerPrefs, true, GUILayout.Width(175));
            EditorUtil.Draw.Property("FileFragment 自动保存间隔(秒)", m_AutoSaveIntervalFileFragment, true, GUILayout.Width(175));
            EditorUtil.Draw.Property("SQLite 自动保存间隔(秒)", m_AutoSaveIntervalSQLite, true, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)自动保存间隔设定每隔多少秒将脏数据落盘",
                "(2)取值 0 或负数禁用自动保存",
                "(3)禁用自动保存后仅在 Shutdown 或手动 Save 时落盘"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 判断 classify/item/value 是否匹配搜索关键词（不区分大小写）。
        /// 同时考虑全局搜索和后端搜索，两者均为空时视为全部匹配。
        /// </summary>
        /// <param name="backendSearch">后端搜索关键词。</param>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名（可为 null 表示仅匹配分类）。</param>
        /// <param name="value">条目值（可为 null 表示不参与匹配）。</param>
        /// <returns>匹配返回 true。</returns>
        private bool MatchSearch(string backendSearch, string classify, string item = null, string value = null)
        {
            if (!string.IsNullOrEmpty(m_GlobalSearchText))
            {
                if (!ContainsIgnoreCase(classify, m_GlobalSearchText) && !ContainsIgnoreCase(item, m_GlobalSearchText) && !ContainsIgnoreCase(value, m_GlobalSearchText))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(backendSearch))
            {
                if (!ContainsIgnoreCase(classify, backendSearch) && !ContainsIgnoreCase(item, backendSearch) && !ContainsIgnoreCase(value, backendSearch))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 不区分大小写的包含检查。
        /// </summary>
        /// <param name="source">源字符串。</param>
        /// <param name="keyword">搜索关键词。</param>
        /// <returns>包含返回 true。</returns>
        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            return source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 数据区：Editor 模式读底层存储，Runtime 模式读 Manager 实例。
        /// </summary>
        private void DrawDataSection()
        {
            DrawGlobalSearchBar();

            if (EditorApplication.isPlaying)
            {
                DrawRuntimeData();
            }
            else
            {
                DrawEditorData();
            }
        }

        /// <summary>
        /// 绘制全局搜索栏。
        /// </summary>
        private void DrawGlobalSearchBar()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("全局搜索", false, GUILayout.Width(60));
                m_GlobalSearchText = EditorUtil.Draw.TextField(m_GlobalSearchText, false);
                if (!string.IsNullOrEmpty(m_GlobalSearchText))
                {
                    EditorUtil.Draw.Button("×", false, () => { m_GlobalSearchText = string.Empty; }, GUILayout.Width(22));
                }
            });
        }

        /// <summary>
        /// 绘制后端搜索栏（内联在各后端折叠区内部）。
        /// </summary>
        /// <param name="searchText">当前搜索文本（引用传递以支持修改）。</param>
        /// <returns>更新后的搜索文本。</returns>
        private string DrawBackendSearchBar(string searchText)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("搜索", false, GUILayout.Width(30));
                searchText = EditorUtil.Draw.TextField(searchText, false);
                if (!string.IsNullOrEmpty(searchText))
                {
                    EditorUtil.Draw.Button("×", false, () => { searchText = string.Empty; }, GUILayout.Width(22));
                }
            });
            return searchText;
        }

        /// <summary>
        /// 绘制 Runtime 模式数据区（从各 Manager 实例读取，只读展示）。
        /// </summary>
        private void DrawRuntimeData()
        {
            var comp = (PersistComponent)target;
            DrawRuntimePlayerPrefs(comp.PlayerPrefs);
            DrawRuntimeFileFragment(comp.FileFragment);
            DrawRuntimeSQLite(comp.SQLite);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Editor 模式数据区（直接读写底层存储，支持增删改）。
        /// </summary>
        private void DrawEditorData()
        {
            EditorUtil.Draw.DangerButton("清除全部持久化数据", false, () =>
            {
                if (EditorUtil.Draw.Panel.Confirm("确认清除", "将同时删除 PlayerPrefs、FileFragment 和 SQLite 中全部 Persist 数据，此操作不可撤销。"))
                {
                    ClearAllPlayerPrefs();
                    ClearAllFileFragment(NovaFramework.Runtime.Path.Persist.FileFragment.FolderFullPath);
#if !UNITY_WEBGL
                    if (m_SQLiteConnection != null)
                    {
                        Util.SQLite.ClearDatabase(m_SQLiteConnection);
                        Util.SQLite.CommitAndBeginTransaction(m_SQLiteConnection);
                        LoadSQLiteEditorData();
                    }
#endif
                }
            });
            DrawEditorPlayerPrefs();
            DrawEditorFileFragment();
#if !UNITY_WEBGL
            DrawEditorSQLite();
#endif
        }

        /// <summary>
        /// 安全读取 .dat 文件并反序列化为键值字典（二进制格式）。
        /// FileFragment 的 AES 作用于文件级（整体加解密），条目值始终为明文。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="useAES">是否对整个文件内容进行 AES 解密。</param>
        /// <returns>反序列化后的键值字典，解析失败时返回 null。</returns>
        protected System.Collections.Generic.Dictionary<string, string> TryReadFragmentFile(string filePath, bool useAES = false)
        {
            try
            {
                var bytes = Util.SysIO.File.ReadAllBytesSync(filePath);
                if (bytes == null || bytes.Length == 0)
                {
                    return new System.Collections.Generic.Dictionary<string, string>();
                }

                if (useAES)
                {
                    bytes = Util.Encrypt.AES.DecryptBytes(bytes);
                }

                var result = new System.Collections.Generic.Dictionary<string, string>();
                using (var ms = new System.IO.MemoryStream(bytes))
                using (var br = new System.IO.BinaryReader(ms, Encoding.UTF8))
                {
                    var count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var key = br.ReadString();
                        var value = br.ReadString();
                        result[key] = value;
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 将字典序列化并写入 .dat 文件（二进制格式）。
        /// FileFragment 的 AES 作用于文件级（整体加解密），条目值始终为明文传入。
        /// </summary>
        /// <param name="filePath">目标文件路径。</param>
        /// <param name="data">待写入的键值字典（值为明文）。</param>
        /// <param name="useAES">是否对整个文件内容进行 AES 加密后写入。</param>
        protected void WriteFragmentFile(string filePath, System.Collections.Generic.Dictionary<string, string> data, bool useAES = false)
        {
            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(data.Count);
                foreach (var kv in data)
                {
                    bw.Write(kv.Key);
                    bw.Write(kv.Value ?? string.Empty);
                }

                bw.Flush();
                bytes = ms.ToArray();
            }

            if (useAES)
            {
                bytes = Util.Encrypt.AES.EncryptBytes(bytes);
            }

            Util.SysIO.File.WriteAllBytesSync(filePath, bytes);
        }

        /// <summary>
        /// 对存储值解码以展示，展示的一定是明文。
        /// AES 开关与存储格式始终一致（切换时自动迁移），useAES 可精确指导是否解密。
        /// </summary>
        protected string DecodeForDisplay(string raw, bool useAES)
        {
            if (string.IsNullOrEmpty(raw) || !useAES)
            {
                return raw;
            }

            try
            {
                return Util.Encrypt.AES.DecryptString(raw);
            }
            catch
            {
                return raw;
            }
        }

        /// <summary>
        /// 对明文值加密以存储：useAES 为 true 时 AES 加密，否则原样返回。
        /// </summary>
        protected string EncodeForStorage(string display, bool useAES)
            => useAES ? Util.Encrypt.AES.EncryptString(display) : display;

        /// <summary>
        /// 绘制分类折叠标题行，返回是否展开。
        /// </summary>
        protected bool DrawClassifyFoldout(string key, string label)
            => EditorUtil.Draw.Foldout(label, key);

        /// <summary>
        /// 绘制条目行（名称 + 值 + 编辑开关 + 保存按钮 + 删除按钮）。
        /// 保存与删除动作通过回调传入，GUIUtility.ExitGUI 在按钮内部自动调用。
        /// </summary>
        /// <param name="itemName">条目名称。</param>
        /// <param name="editBuffer">编辑缓冲区，外部持久化以保留跨帧输入状态。</param>
        /// <param name="isEditing">是否处于编辑模式，外部持久化。</param>
        /// <param name="storedValue">当前存储的原始值（供展示解码用）。</param>
        /// <param name="useAES">是否对展示值进行 AES 解码。</param>
        /// <param name="onSave">保存回调，参数为编辑后的明文值；由调用方负责写入存储并重置编辑状态。</param>
        /// <param name="onDelete">删除回调；由调用方负责移除数据并处理级联清理。</param>
        protected void DrawItemRow(
            string itemName,
            ref string editBuffer,
            ref bool isEditing,
            string storedValue,
            bool useAES,
            Action<string> onSave,
            Action onDelete)
        {
            // 使用局部变量避免在 lambda 中捕获 ref 参数（C# 限制）
            string localBuffer = editBuffer;
            bool localEditing = isEditing;
            string display = DecodeForDisplay(storedValue, useAES);

            // 保存时捕获当前 buffer 值（TextArea 在 Horizontal 回调之后绘制，此处值正确）
            string bufferSnapshot = localBuffer;
            EditorUtil.Draw.Layout.Horizontal("box", () =>
            {
                EditorUtil.Draw.Label(itemName, false, GUILayout.MinWidth(80));
                EditorUtil.Draw.FlexibleSpace();

                if (!localEditing)
                {
                    EditorUtil.Draw.Label(display, EditorStyles.wordWrappedLabel, false, GUILayout.MaxWidth(200));
                }

                bool newEditing = EditorUtil.Draw.ToggleLeft("编辑", localEditing, false, GUILayout.Width(45));
                if (newEditing != localEditing)
                {
                    localEditing = newEditing;
                    localBuffer = localEditing ? display : string.Empty;
                    bufferSnapshot = localBuffer;
                }

                EditorUtil.Draw.DisabledGroup(!localEditing || localBuffer == display, () =>
                {
                    EditorUtil.Draw.Button("保存", false, () => onSave?.Invoke(bufferSnapshot), GUILayout.Width(40));
                });

                EditorUtil.Draw.DangerButton("清除", false, onDelete, GUILayout.Width(44));
            });

            if (localEditing)
            {
                localBuffer = EditorUtil.Draw.TextArea(localBuffer, false, GUILayout.MinHeight(40), GUILayout.ExpandWidth(true));
            }

            // 仅写回中间状态（切换编辑模式、输入文本）；保存/删除由回调处理，不依赖此处回写
            editBuffer = localBuffer;
            isEditing = localEditing;
        }
    }
}
