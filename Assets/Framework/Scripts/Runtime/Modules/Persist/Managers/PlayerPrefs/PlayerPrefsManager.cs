/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerPrefsManager.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   PlayerPrefs 持久化管理器
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PlayerPrefs 持久化管理器。
    /// 存储模型：classify → item → value（string），全部写入 Unity PlayerPrefs。
    /// IO 优化：SetXxx 仅写内存 + 脏标记，Update 检测脏标记后批量落盘，避免高频写入触发多次磁盘刷新。
    /// </summary>
    internal sealed partial class PlayerPrefsManager : PersistManagerBase<PlayerPrefsManagerConfig>, IPlayerPrefsManager
    {
        /// <summary>
        /// 初始化 PlayerPrefsManager 的新实例。
        /// </summary>
        public PlayerPrefsManager() { }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override async UniTask Initialize(PlayerPrefsManagerConfig config)
        {
            InitializeBase(config);
            await Load();
        }

        /// <summary>
        /// 管理器轮询：驱动自动保存计时器。
        /// 禁用自动保存（interval 为 0 或负数）时不做任何操作，仅靠 Shutdown 或手动 Save() 落盘。
        /// </summary>
        public override void Update()
        {
            TickAutoSave(UnityEngine.Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 关闭并清理管理器：将未落盘数据强制写入存储。
        /// </summary>
        public override void Shutdown()
        {
            if (m_IsDirty)
            {
                FlushDirtyIndex();
                _Save();
            }

            m_ItemNameGroups.Clear();
            m_DirtyClassifies.Clear();
            m_IsDirty = false;
        }

        /// <summary>
        /// 从 PlayerPrefs 加载全部分类索引到内存。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        public override UniTask<bool> Load()
        {
            m_ItemNameGroups.Clear();
            LoadItemNameGroups();
            return UniTask.FromResult(true);
        }

        /// <summary>
        /// 将所有脏分类的索引及值落盘。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        public override bool Save()
        {
            FlushDirtyIndex();
            _Save();
            m_IsDirty = false;
            m_DirtyClassifies.Clear();
            return true;
        }

        /// <summary>
        /// 将指定分类的索引及值落盘。
        /// 注意：PlayerPrefs 底层不支持分类保存，此方法等同于全量落盘。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>成功返回 true。</returns>
        public override bool Save(string classify)
        {
            ValidateClassifyName(classify);
            if (m_DirtyClassifies.Contains(classify))
            {
                RefreshItemNameListToSave(classify);
                RefreshClassifyNameListToSave();
                m_DirtyClassifies.Remove(classify);
            }

            if (m_DirtyClassifies.Count == 0)
            {
                m_IsDirty = false;
            }

            _Save();
            return true;
        }

        /// <summary>
        /// 判断指定条目是否存在。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>存在返回 true。</returns>
        public override bool HasItem(string classify, string item)
        {
            ValidateNames(classify, item);
            return m_ItemNameGroups.TryGetValue(classify, out var set) && set.Contains(item);
        }

        /// <summary>
        /// 删除指定条目。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        public override bool RemoveItem(string classify, string item)
        {
            ValidateNames(classify, item);
            if (!m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                return false;
            }

            if (!set.Remove(item))
            {
                return false;
            }

            _DeleteKey(BuildKey(classify, item));
            MarkDirty(classify);

            if (set.Count == 0)
            {
                m_ItemNameGroups.Remove(classify);
            }

            return true;
        }

        /// <summary>
        /// 删除指定分类下的全部条目。
        /// </summary>
        /// <param name="classify">分类名。</param>
        public override void RemoveAll(string classify)
        {
            ValidateClassifyName(classify);
            if (!m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                return;
            }

            foreach (var item in set)
            {
                _DeleteKey(BuildKey(classify, item));
            }

            _DeleteKey(BuildItemIndexKey(classify));
            m_ItemNameGroups.Remove(classify);
            m_DirtyClassifies.Remove(classify);
            RefreshClassifyNameListToSave();
            _Save();
        }

        /// <summary>
        /// 获取指定分类下的全部条目名数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目名数组，不存在时返回空数组。</returns>
        public override string[] GetAllItemNames(string classify)
        {
            ValidateClassifyName(classify);
            if (!m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                return System.Array.Empty<string>();
            }

            var result = new string[set.Count];
            set.CopyTo(result);
            return result;
        }

        /// <summary>
        /// 将指定分类下的全部条目名填充到列表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="results">结果列表，方法会追加而非清空。</param>
        public override void GetAllItemNames(string classify, List<string> results)
        {
            ValidateClassifyName(classify);
            if (!m_ItemNameGroups.TryGetValue(classify, out var set))
            {
                return;
            }

            foreach (var item in set)
            {
                results.Add(item);
            }
        }

        /// <summary>
        /// 获取指定分类下的条目数量。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目数量。</returns>
        public override int Count(string classify)
        {
            ValidateClassifyName(classify);
            return m_ItemNameGroups.TryGetValue(classify, out var set) ? set.Count : 0;
        }

        /// <summary>
        /// 获取所有已注册分类（PlayerPrefs）的名称。
        /// </summary>
        /// <returns>分类名数组。</returns>
        public override string[] GetAllClassifyNames()
        {
            var keys = m_ItemNameGroups.Keys;
            var result = new string[keys.Count];
            keys.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override bool GetBool(string classify, string item, bool defaultValue = default)
        {
            ValidateNames(classify, item);
            var raw = _GetString(BuildKey(classify, item));
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            var value = m_UseAESEncrypt ? Util.Encrypt.AES.DecryptString(raw) : raw;
            return value == c_BoolTrue;
        }

        /// <summary>
        /// 读取整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override int GetInt(string classify, string item, int defaultValue = default)
        {
            ValidateNames(classify, item);
            var raw = _GetString(BuildKey(classify, item));
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            var value = m_UseAESEncrypt ? Util.Encrypt.AES.DecryptString(raw) : raw;
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 读取浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override float GetFloat(string classify, string item, float defaultValue = default)
        {
            ValidateNames(classify, item);
            var raw = _GetString(BuildKey(classify, item));
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            var value = m_UseAESEncrypt ? Util.Encrypt.AES.DecryptString(raw) : raw;
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public override string GetString(string classify, string item, string defaultValue = "")
        {
            ValidateNames(classify, item);
            var raw = _GetString(BuildKey(classify, item));
            if (string.IsNullOrEmpty(raw))
            {
                return defaultValue;
            }

            return m_UseAESEncrypt ? Util.Encrypt.AES.DecryptString(raw) : raw;
        }

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetBool(string classify, string item, bool value)
        {
            var str = value ? c_BoolTrue : c_BoolFalse;
            WriteValue(classify, item, str);
        }

        /// <summary>
        /// 写入整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetInt(string classify, string item, int value)
        {
            WriteValue(classify, item, value.ToString());
        }

        /// <summary>
        /// 写入浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetFloat(string classify, string item, float value)
        {
            WriteValue(classify, item, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public override void SetString(string classify, string item, string value)
        {
            WriteValue(classify, item, value ?? string.Empty);
        }
    }
}
