/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerPrefsManager.Methods.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   PlayerPrefs 持久化管理器 —— 私有方法
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class PlayerPrefsManager : PersistManagerBase<PlayerPrefsManagerConfig>, IPlayerPrefsManager
    {
        /// <summary>
        /// 校验分类名：不得为空、不得包含 "::" 或 ","。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <exception cref="System.ArgumentException">分类名不合法时抛出。</exception>
        private static void ValidateClassifyName(string classify)
        {
            ValidateClassify(classify);

            if (classify.Contains(c_KeySeparator))
            {
                throw new System.ArgumentException($"classify 不得包含 \"{c_KeySeparator}\"：{classify}");
            }

            if (classify.Contains(","))
            {
                throw new System.ArgumentException($"classify 不得包含逗号：{classify}");
            }
        }

        /// <summary>
        /// 构造数据键：{classify}::{item}。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>PlayerPrefs 键字符串。</returns>
        private string BuildKey(string classify, string item)
        {
            return string.Concat(classify, c_KeySeparator, item);
        }

        /// <summary>
        /// 构造分类条目索引键：{classify}::__items__。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>PlayerPrefs 键字符串。</returns>
        private string BuildItemIndexKey(string classify)
        {
            return string.Concat(classify, c_KeySeparator, "__items__");
        }

        /// <summary>
        /// 校验 classify 和 item 名称：不得包含分隔符 "::" 或 ","，item 不得等于保留名 "__items__"。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <exception cref="System.ArgumentException">名称不合法时抛出。</exception>
        private void ValidateNames(string classify, string item)
        {
            ValidateClassifyAndItem(classify, item);

            if (classify.Contains(c_KeySeparator))
            {
                throw new System.ArgumentException($"classify 不得包含 \"{c_KeySeparator}\"：{classify}");
            }

            if (classify.Contains(","))
            {
                throw new System.ArgumentException($"classify 不得包含逗号：{classify}");
            }

            if (item.Contains(c_KeySeparator))
            {
                throw new System.ArgumentException($"item 不得包含 \"{c_KeySeparator}\"：{item}");
            }

            if (item.Contains(","))
            {
                throw new System.ArgumentException($"item 不得包含逗号：{item}");
            }

            if (item == "__items__")
            {
                throw new System.ArgumentException("item 不得使用保留名 \"__items__\"。");
            }
        }

        /// <summary>
        /// 标记分类为脏并设置全局脏标记。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void MarkDirty(string classify)
        {
            m_DirtyClassifies.Add(classify);
            m_IsDirty = true;
        }

        /// <summary>
        /// 写入值到 PlayerPrefs（内存）并标记脏。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的原始字符串值。</param>
        private void WriteValue(string classify, string item, string value)
        {
            ValidateNames(classify, item);

            if (!m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                set = new HashSet<string>();
                m_ItemNameGroups[classify] = set;
            }

            set.Add(item);

            var stored = m_UseAESEncrypt ? Util.Encrypt.AES.EncryptString(value) : value;
            _SetString(BuildKey(classify, item), stored);
            MarkDirty(classify);
        }

        /// <summary>
        /// 从 PlayerPrefs 恢复全部分类索引到内存。
        /// </summary>
        private void LoadItemNameGroups()
        {
            var classifyRaw = _GetString(c_ClassifyIndexKey);
            if (string.IsNullOrEmpty(classifyRaw))
            {
                return;
            }

            var classifies = classifyRaw.Split(',');
            foreach (var classify in classifies)
            {
                if (string.IsNullOrEmpty(classify))
                {
                    continue;
                }

                var itemsRaw = _GetString(BuildItemIndexKey(classify));
                var set = new HashSet<string>();

                if (!string.IsNullOrEmpty(itemsRaw))
                {
                    foreach (var item in itemsRaw.Split(','))
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            set.Add(item);
                        }
                    }
                }

                m_ItemNameGroups[classify] = set;
            }
        }

        /// <summary>
        /// 将当前全分类列表写回 PlayerPrefs 索引键。
        /// </summary>
        private void RefreshClassifyNameListToSave()
        {
            _SetString(c_ClassifyIndexKey, string.Join(",", m_ItemNameGroups.Keys));
        }

        /// <summary>
        /// 将指定分类的条目名列表写回 PlayerPrefs 索引键。
        /// </summary>
        /// <param name="classify">分类名。</param>
        private void RefreshItemNameListToSave(string classify)
        {
            if (m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                _SetString(BuildItemIndexKey(classify), string.Join(",", set));
            }
        }

        /// <summary>
        /// 将所有脏分类的条目索引刷新到 PlayerPrefs（内存），
        /// 循环结束后刷新一次分类名索引。
        /// </summary>
        private void FlushDirtyIndex()
        {
            foreach (var classify in m_DirtyClassifies)
            {
                RefreshItemNameListToSave(classify);
            }

            RefreshClassifyNameListToSave();
        }
    }
}
