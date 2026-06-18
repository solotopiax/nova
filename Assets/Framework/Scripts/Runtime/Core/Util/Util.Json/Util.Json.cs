/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.Json.cs
 * author:    taoye
 * created:   2026/1/26
 * descrip:   Json工具
 ***************************************************************/

using Newtonsoft.Json;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// Json 工具。
        /// </summary>
        public static class Json
        {
            /// <summary>
            /// 默认序列化设置（缓存，避免每次调用创建新实例）。
            /// </summary>
            private static readonly JsonSerializerSettings s_DefaultSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            /// <summary>
            /// 序列化对象为 Json 字符串。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="obj">对象实例。</param>
            /// <param name="formatting">格式化选项。</param>
            /// <param name="settings">序列化设置。</param>
            /// <returns>Json 字符串。</returns>
            public static string Serialize<T>(T obj, Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null)
            {
                return JsonConvert.SerializeObject(obj, formatting, settings ?? s_DefaultSettings);
            }

            /// <summary>
            /// 序列化对象为 Json 字符串（非泛型重载）。
            /// 当对象的静态类型在编译期不可知（如反射获得 object / 运行期多态参数）时使用，
            /// 与 `Deserialize(string, Type, ...)` 成对。
            /// </summary>
            /// <param name="obj">对象实例。</param>
            /// <param name="type">声明类型，用于控制序列化契约（传 null 则按运行时类型）。</param>
            /// <param name="formatting">格式化选项。</param>
            /// <param name="settings">序列化设置。</param>
            /// <returns>Json 字符串。</returns>
            public static string Serialize(object obj, System.Type type, Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null)
            {
                return JsonConvert.SerializeObject(obj, type, formatting, settings ?? s_DefaultSettings);
            }

            /// <summary>
            /// 反序列化 Json 字符串为对象。
            /// </summary>
            /// <typeparam name="T">对象类型。</typeparam>
            /// <param name="json">Json 字符串。</param>
            /// <param name="settings">序列化设置。</param>
            /// <returns>对象实例。</returns>
            public static T Deserialize<T>(string json, JsonSerializerSettings settings = null)
            {
                return JsonConvert.DeserializeObject<T>(json, settings ?? s_DefaultSettings);
            }

            /// <summary>
            /// 反序列化 Json 字符串为指定类型实例（非泛型重载）。
            /// Runner 在运行期根据 PipifyStep.ParamsType 反射反序列化参数时使用。
            /// </summary>
            /// <param name="json">Json 字符串。</param>
            /// <param name="type">目标类型。</param>
            /// <param name="settings">序列化设置。</param>
            /// <returns>对象实例。</returns>
            public static object Deserialize(string json, System.Type type, JsonSerializerSettings settings = null)
            {
                return JsonConvert.DeserializeObject(json, type, settings ?? s_DefaultSettings);
            }
        }
    }
}
