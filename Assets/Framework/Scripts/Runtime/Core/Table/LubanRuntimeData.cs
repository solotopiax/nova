/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LubanRuntimeData.cs
 * author:    taoye
 * created:   2026/7/31
 * descrip:   Luban 运行时数据格式分派
 ***************************************************************/

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 为专用模块提供无业务语义的 Luban JSON/Binary 运行时分派能力。
    /// </summary>
    public static class LubanRuntimeData
    {
        /// <summary>
        /// 按指定数据格式创建异步数据接收器。
        /// </summary>
        /// <param name="format">Luban 数据格式。</param>
        /// <param name="cache">模块本次加载使用的数据缓存。</param>
        /// <param name="unit">当前数据单元。</param>
        /// <param name="loadFunc">资源异步加载委托。</param>
        /// <param name="releaseFunc">资源释放委托。</param>
        /// <returns>与格式对应的数据接收器。</returns>
        public static DataReceiver CreateReceiver(
            LubanDataFormat format,
            LubanDataCache cache,
            IDataTableUnitSetting unit,
            DataReceiver.LoadAssetAsyncFunc loadFunc,
            DataReceiver.ReleaseAssetAction releaseFunc)
        {
            return format == LubanDataFormat.Binary
                ? new LubanBinaryDataReceiver(cache, unit, loadFunc, releaseFunc)
                : new LubanDataReceiver(cache, unit, loadFunc, releaseFunc);
        }

        /// <summary>
        /// 按指定数据格式创建同步数据接收器。
        /// </summary>
        /// <param name="format">Luban 数据格式。</param>
        /// <param name="cache">模块本次加载使用的数据缓存。</param>
        /// <param name="unit">当前数据单元。</param>
        /// <param name="loadFunc">资源同步加载委托。</param>
        /// <param name="releaseFunc">资源释放委托。</param>
        /// <returns>与格式对应的数据接收器。</returns>
        public static DataReceiver CreateReceiver(
            LubanDataFormat format,
            LubanDataCache cache,
            IDataTableUnitSetting unit,
            DataReceiver.LoadAssetSyncFunc loadFunc,
            DataReceiver.ReleaseAssetAction releaseFunc)
        {
            return format == LubanDataFormat.Binary
                ? new LubanBinaryDataReceiver(cache, unit, loadFunc, releaseFunc)
                : new LubanDataReceiver(cache, unit, loadFunc, releaseFunc);
        }

        /// <summary>
        /// 按指定数据格式从缓存构建 Luban Tables；JSON 与 Binary 加载委托保持分支隔离。
        /// </summary>
        /// <param name="format">Luban 数据格式。</param>
        /// <param name="tablesClassName">生成的 Tables 类短名称。</param>
        /// <param name="namespace_">生成代码所在命名空间。</param>
        /// <param name="cache">模块本次加载使用的数据缓存。</param>
        /// <param name="logTag">缓存缺项时使用的日志标签。</param>
        /// <param name="dataKind">日志中显示的数据类别。</param>
        /// <returns>表类型到运行时表实例的映射；构建失败返回 null。</returns>
        public static Dictionary<Type, ITable> LoadTables(
            LubanDataFormat format,
            string tablesClassName,
            string namespace_,
            LubanDataCache cache,
            string logTag,
            string dataKind)
        {
            if (format == LubanDataFormat.Binary)
            {
                return LubanTablesLoader.LoadBinary(tablesClassName, namespace_, key =>
                {
                    if (cache.DataMap.TryGetValue(key, out object value) && value is byte[] bytes)
                    {
                        return bytes;
                    }

                    Log.Warning(logTag, "数据缓存中未找到 {0} Binary 数据：{1}", dataKind, key);
                    return Array.Empty<byte>();
                });
            }

            Func<string, JArray> jsonLoader = key =>
            {
                if (cache.DataMap.TryGetValue(key, out object value) && value is JArray jsonArray)
                {
                    return jsonArray;
                }

                Log.Warning(logTag, "数据缓存中未找到 {0} JSON 数据：{1}", dataKind, key);
                return new JArray();
            };
            return LubanTablesLoader.Load(tablesClassName, namespace_, jsonLoader);
        }
    }
}
