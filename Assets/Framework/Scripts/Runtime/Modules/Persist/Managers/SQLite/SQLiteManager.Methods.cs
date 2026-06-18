/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManager.Methods.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLiteManager —— 私有方法
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class SQLiteManager : PersistManagerBase<SQLiteManagerConfig>, ISQLiteManager
    {
        /// <summary>
        /// 校验 SQLite 表名合法性：不得为空，且仅允许字母、数字和下划线。
        /// </summary>
        /// <param name="classify">分类名（表名）。</param>
        /// <exception cref="System.ArgumentException">名称不合法时抛出。</exception>
        private static void ValidateSQLiteClassify(string classify)
        {
            ValidateClassify(classify);
            for (int i = 0; i < classify.Length; i++)
            {
                char c = classify[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    throw new System.ArgumentException(Txt.Format("classify 包含不允许的字符 '{0}'，仅支持字母、数字和下划线。", c), nameof(classify));
                }
            }
        }

#if !UNITY_WEBGL
        /// <summary>
        /// 标记指定分类的表为脏（有未提交的写缓冲）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void MarkDirty(string classify)
        {
            m_DirtyTables.Add(classify);
        }

        /// <summary>
        /// 读取字符串值并按需转换类型。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的字符串，不存在时返回默认值。</returns>
        private string ReadRaw(string classify, string item, string defaultValue = "")
        {
            ValidateClassifyAndItem(classify, item);
            var table = GetOrCreateTable(classify);
            return table.GetString(item, defaultValue);
        }

        /// <summary>
        /// 写入字符串值到写缓冲并标记表为脏。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的字符串值。</param>
        private void WriteRaw(string classify, string item, string value)
        {
            ValidateClassifyAndItem(classify, item);
            var table = GetOrCreateTable(classify);
            table.SetString(item, value);
            MarkDirty(classify);
        }
#endif
    }
}
