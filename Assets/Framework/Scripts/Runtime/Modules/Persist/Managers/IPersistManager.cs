/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPersistManager.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   持久化管理器公共接口，提取三套子接口的重复契约
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 持久化管理器公共接口。
    /// 提取 IFileFragmentManager / IPlayerPrefsManager / ISQLiteManager 三套子接口的重复契约，
    /// 涵盖 Load / Save / 条目增删查、全部 Get/Set 类型操作及分类枚举。
    /// </summary>
    public interface IPersistManager
    {
        /// <summary>
        /// 从底层存储加载数据到内存。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        UniTask<bool> Load();

        /// <summary>
        /// 将全部内存缓存落盘。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        bool Save();

        /// <summary>
        /// 将指定分类的缓存落盘。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>成功返回 true。</returns>
        bool Save(string classify);

        /// <summary>
        /// 判断指定条目是否存在。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>存在返回 true。</returns>
        bool HasItem(string classify, string item);

        /// <summary>
        /// 删除指定条目，成功返回 true。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        bool RemoveItem(string classify, string item);

        /// <summary>
        /// 删除指定分类下的全部条目。
        /// </summary>
        /// <param name="classify">分类名。</param>
        void RemoveAll(string classify);

        /// <summary>
        /// 获取指定分类下的全部条目名数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目名数组。</returns>
        string[] GetAllItemNames(string classify);

        /// <summary>
        /// 将指定分类下的全部条目名追加到列表（避免数组分配）。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="results">结果列表，方法会追加而非清空。</param>
        void GetAllItemNames(string classify, List<string> results);

        /// <summary>
        /// 获取数据库中所有分类的名称。
        /// </summary>
        /// <returns>分类名数组。</returns>
        string[] GetAllClassifyNames();

        /// <summary>
        /// 获取指定分类下的条目数量。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目数量。</returns>
        int Count(string classify);

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        bool GetBool(string classify, string item, bool defaultValue = default);

        /// <summary>
        /// 读取整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        int GetInt(string classify, string item, int defaultValue = default);

        /// <summary>
        /// 读取浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        float GetFloat(string classify, string item, float defaultValue = default);

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        string GetString(string classify, string item, string defaultValue = "");

        /// <summary>
        /// 读取长整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        long GetLong(string classify, string item, long defaultValue = default);

        /// <summary>
        /// 读取双精度浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        double GetDouble(string classify, string item, double defaultValue = default);

        /// <summary>
        /// 读取字节数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        byte[] GetBytes(string classify, string item, byte[] defaultValue = null);

        /// <summary>
        /// 读取对象（JSON 反序列化）。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        T GetObject<T>(string classify, string item, T defaultValue = default);

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetBool(string classify, string item, bool value);

        /// <summary>
        /// 写入整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetInt(string classify, string item, int value);

        /// <summary>
        /// 写入浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetFloat(string classify, string item, float value);

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetString(string classify, string item, string value);

        /// <summary>
        /// 写入长整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetLong(string classify, string item, long value);

        /// <summary>
        /// 写入双精度浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetDouble(string classify, string item, double value);

        /// <summary>
        /// 写入字节数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetBytes(string classify, string item, byte[] value);

        /// <summary>
        /// 写入对象（JSON 序列化）。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        void SetObject<T>(string classify, string item, T value);
    }
}
