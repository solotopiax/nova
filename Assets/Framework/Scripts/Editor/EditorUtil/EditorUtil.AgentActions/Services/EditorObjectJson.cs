/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorObjectJson.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   严格读取 Unity 编辑器组件 JSON 的序列化快照
 ***************************************************************/

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 只读解析 EditorJsonUtility 组件快照，不访问组件 Getter 或改写源对象。
    /// </summary>
    internal static class EditorObjectJson
    {
        /// <summary>
        /// 只接受扁平组件根或唯一 MonoBehaviour 对象根；保留字符串类型，拒绝重复键、额外 JSON 及缺失的对象型设置字段。
        /// </summary>
        internal static bool TryReadSnapshot<T>(string json, string componentName, string requiredField, out T snapshot, out string error) where T : class
        {
            snapshot = null;
            error = null;
            try
            {
                using var input = new StringReader(json);
                using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None };
                JObject component = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });
                if (reader.Read())
                {
                    error = componentName + " 组件 JSON 根结构不受支持。";
                    return false;
                }
                if (component.TryGetValue("MonoBehaviour", StringComparison.Ordinal, out JToken wrapper))
                {
                    if (component.Count != 1 || !(wrapper is JObject wrappedComponent))
                    {
                        error = componentName + " 组件 JSON 根结构不受支持。";
                        return false;
                    }
                    component = wrappedComponent;
                }
                if (!(component[requiredField] is JObject))
                {
                    error = componentName + "." + requiredField + " 未配置。";
                    return false;
                }
                snapshot = JsonUtility.FromJson<T>(component.ToString(Formatting.None));
                if (snapshot == null)
                {
                    error = componentName + " 组件 JSON 无法解析。";
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                // 配置正文可能包含秘密；错误只报告边界，不回显解析器消息或 JSON 内容。
                snapshot = null;
                error = componentName + " 组件 JSON 无法解析。";
                return false;
            }
        }
    }
}
