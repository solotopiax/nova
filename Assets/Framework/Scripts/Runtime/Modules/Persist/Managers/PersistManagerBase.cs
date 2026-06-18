/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistManagerBase.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   持久化管理器泛型基类，提取三套 ManagerBase 的公共逻辑
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 持久化管理器泛型基类。
    /// 提取 FileFragment / PlayerPrefs / SQLite 三套 ManagerBase 的公共字段、
    /// 自动保存计时器、入参校验以及扩展类型（long/double/bytes/object）的 virtual 默认实现。
    /// 泛型参数 TConfig 为具体管理器的配置类型，以保留强类型 Initialize 签名。
    /// </summary>
    /// <typeparam name="TConfig">管理器配置类型。</typeparam>
    internal abstract class PersistManagerBase<TConfig> : FrameworkManager where TConfig : PersistManagerConfigBase
    {
        /// <summary>
        /// 持久化管理器优先级。
        /// 最高优先级（0）确保 Update 最先执行、Shutdown 最后执行，
        /// 使其他模块有机会在 Shutdown 前完成数据写入。
        /// </summary>
        public override int Priority => 0;

        /// <summary>
        /// 是否启用 AES 加密存储值。
        /// </summary>
        protected bool m_UseAESEncrypt;

        /// <summary>
        /// 自动保存间隔（秒）。0 或负数表示禁用自动保存。
        /// </summary>
        protected float m_AutoSaveInterval;

        /// <summary>
        /// 距离下次自动保存的剩余时间（秒）。
        /// </summary>
        protected float m_AutoSaveTimer;

        /// <summary>
        /// 初始化基类公共字段，子类 Initialize 中第一行调用。
        /// </summary>
        /// <param name="config">配置信息。</param>
        protected void InitializeBase(TConfig config)
        {
            m_UseAESEncrypt = config.UseAESEncrypt;
            m_AutoSaveInterval = config.AutoSaveInterval;
            m_AutoSaveTimer = config.AutoSaveInterval;
        }

        /// <summary>
        /// 尝试执行自动保存：递减计时器，归零时调用 Save() 并重置计时器。
        /// 子类 Update() 中调用此方法即可接入自动保存机制。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间（秒）。</param>
        protected void TickAutoSave(float deltaTime)
        {
            if (m_AutoSaveInterval <= 0f)
            {
                return;
            }

            m_AutoSaveTimer -= deltaTime;
            if (m_AutoSaveTimer <= 0f)
            {
                m_AutoSaveTimer = m_AutoSaveInterval;
                Save();
            }
        }

        /// <summary>
        /// 校验分类名不为 null 或空字符串。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <exception cref="ArgumentException">分类名为 null 或空时抛出。</exception>
        protected static void ValidateClassify(string classify)
        {
            if (string.IsNullOrEmpty(classify))
            {
                throw new ArgumentException("classify 不得为 null 或空字符串。", nameof(classify));
            }
        }

        /// <summary>
        /// 校验分类名和条目名均不为 null 或空字符串。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <exception cref="ArgumentException">名称为 null 或空时抛出。</exception>
        protected static void ValidateClassifyAndItem(string classify, string item)
        {
            ValidateClassify(classify);
            if (string.IsNullOrEmpty(item))
            {
                throw new ArgumentException("item 不得为 null 或空字符串。", nameof(item));
            }
        }

        /// <summary>
        /// 初始化管理器。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract UniTask Initialize(TConfig config);

        /// <summary>
        /// 管理器轮询。
        /// 子类实现自动保存、心跳等逐帧逻辑。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// 子类实现强制落盘、释放连接等清理逻辑。
        /// </summary>
        public abstract override void Shutdown();

        /// <summary>
        /// 从持久化存储加载数据到内存。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        public abstract UniTask<bool> Load();

        /// <summary>
        /// 保存全部数据。
        /// </summary>
        /// <returns>成功返回 true。</returns>
        public abstract bool Save();

        /// <summary>
        /// 保存指定分类的数据。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>成功返回 true。</returns>
        public abstract bool Save(string classify);

        /// <summary>
        /// 判断指定条目是否存在。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>存在返回 true。</returns>
        public abstract bool HasItem(string classify, string item);

        /// <summary>
        /// 删除指定条目。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <returns>删除成功返回 true。</returns>
        public abstract bool RemoveItem(string classify, string item);

        /// <summary>
        /// 删除指定分类下的全部条目。
        /// </summary>
        /// <param name="classify">分类名。</param>
        public abstract void RemoveAll(string classify);

        /// <summary>
        /// 获取指定分类下的全部条目名数组。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目名数组，不存在时返回空数组。</returns>
        public abstract string[] GetAllItemNames(string classify);

        /// <summary>
        /// 将指定分类下的全部条目名填充到列表。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="results">结果列表，方法会追加而非清空。</param>
        public abstract void GetAllItemNames(string classify, List<string> results);

        /// <summary>
        /// 获取所有已注册分类的名称。
        /// </summary>
        /// <returns>分类名数组。</returns>
        public abstract string[] GetAllClassifyNames();

        /// <summary>
        /// 获取指定分类下的条目数量。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <returns>条目数量。</returns>
        public abstract int Count(string classify);

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public abstract bool GetBool(string classify, string item, bool defaultValue = default);

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public abstract void SetBool(string classify, string item, bool value);

        /// <summary>
        /// 读取整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public abstract int GetInt(string classify, string item, int defaultValue = default);

        /// <summary>
        /// 写入整型值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public abstract void SetInt(string classify, string item, int value);

        /// <summary>
        /// 读取浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public abstract float GetFloat(string classify, string item, float defaultValue = default);

        /// <summary>
        /// 写入浮点值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public abstract void SetFloat(string classify, string item, float value);

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public abstract string GetString(string classify, string item, string defaultValue = "");

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public abstract void SetString(string classify, string item, string value);

        /// <summary>
        /// 读取长整型值，底层委托 GetString 并解析。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public virtual long GetLong(string classify, string item, long defaultValue = default)
        {
            var raw = GetString(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            return long.TryParse(raw, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 写入长整型值，底层委托 SetString。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public virtual void SetLong(string classify, string item, long value)
        {
            SetString(classify, item, value.ToString());
        }

        /// <summary>
        /// 读取双精度浮点值，底层委托 GetString 并解析。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public virtual double GetDouble(string classify, string item, double defaultValue = default)
        {
            var raw = GetString(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 写入双精度浮点值，底层委托 SetString。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public virtual void SetDouble(string classify, string item, double value)
        {
            SetString(classify, item, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 读取字节数组，底层委托 GetString 并 Base64 解码。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public virtual byte[] GetBytes(string classify, string item, byte[] defaultValue = null)
        {
            var raw = GetString(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.FromBase64String(raw);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Persist, "GetBytes 反序列化失败 [{0}::{1}]: {2}", classify, item, ex.Message);
                return defaultValue;
            }
        }

        /// <summary>
        /// 写入字节数组，底层委托 SetString 并 Base64 编码。
        /// </summary>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public virtual void SetBytes(string classify, string item, byte[] value)
        {
            SetString(classify, item, value != null ? Convert.ToBase64String(value) : string.Empty);
        }

        /// <summary>
        /// 读取对象，底层委托 GetString 并 JSON 反序列化。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="defaultValue">不存在时的默认值。</param>
        /// <returns>读取到的值，不存在时返回默认值。</returns>
        public virtual T GetObject<T>(string classify, string item, T defaultValue = default)
        {
            var raw = GetString(classify, item, null);
            if (raw == null)
            {
                return defaultValue;
            }

            try
            {
                return Util.Json.Deserialize<T>(raw);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Persist, "GetObject 反序列化失败 [{0}::{1}]: {2}", classify, item, ex.Message);
                return defaultValue;
            }
        }

        /// <summary>
        /// 写入对象，底层委托 SetString 并 JSON 序列化。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="classify">分类名。</param>
        /// <param name="item">条目名。</param>
        /// <param name="value">要写入的值。</param>
        public virtual void SetObject<T>(string classify, string item, T value)
        {
            SetString(classify, item, Util.Json.Serialize(value));
        }
    }
}
