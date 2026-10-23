/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SerializedPropertyValues.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   受控配置与 Inspector 共用的序列化字段描述及严格值转换
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只处理已经由领域服务定位的字段；不定位任意目标、不调用方法、不保存资产。</summary>
    internal static class SerializedPropertyValues
    {
        /// <summary>实际 Unity 字段描述；默认值未读取声明时明确 unknown，敏感值只报告是否存在。</summary>
        [Serializable]
        internal sealed class FieldView
        {
            public string path;
            public string displayName;
            public string tooltip;
            public string type;
            public string propertyType;
            public bool editable;
            public bool sensitive;
            public bool hasValue;
            public string valueJson;
            public string defaultStatus = "unknown";
            public string[] enumNames;
        }

        /// <summary>描述可见的真实字段和相对路径；只读访问，不补数组或管理引用实例。</summary>
        internal static FieldView[] Describe(SerializedProperty root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var result = new List<FieldView>();
            SerializedProperty cursor = root.Copy();
            SerializedProperty end = root.GetEndProperty();
            bool enter = true;
            do
            {
                string relative = cursor.propertyPath == root.propertyPath ? string.Empty :
                    cursor.propertyPath.Substring(root.propertyPath.Length + 1);
                bool sensitive = ContainsSensitive(cursor);
                JToken value = Read(cursor);
                result.Add(new FieldView
                {
                    path = relative, displayName = cursor.displayName, tooltip = cursor.tooltip,
                    type = cursor.type, propertyType = cursor.propertyType.ToString(),
                    editable = CanWrite(cursor), sensitive = sensitive,
                    hasValue = value != null && value.Type != JTokenType.Null &&
                        !(value.Type == JTokenType.String && value.Value<string>().Length == 0),
                    valueJson = sensitive || value == null ? null : value.ToString(Formatting.None),
                    enumNames = cursor.propertyType == SerializedPropertyType.Enum ? cursor.enumNames : null,
                });
                // 管理引用只展开已有字段，类型元数据不作为可修改叶子。
                enter = cursor.hasVisibleChildren;
            }
            while (cursor.NextVisible(enter) && !SerializedProperty.EqualContents(cursor, end));
            return result.ToArray();
        }

        /// <summary>严格解析一个 JSON 值；拒绝重复属性、额外内容和超深结构，不含代码执行。</summary>
        internal static JToken Parse(string json)
        {
            if (json == null || json.Length > 32768) throw new InvalidOperationException("字段 JSON 值缺失或过长。");
            int position = 0;
            ReadJsonSyntax(json, ref position, 0);
            SkipJsonWhitespace(json, ref position);
            if (position != json.Length) throw new InvalidOperationException("字段值只能包含一个标准 JSON 值。");
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
            {
                JToken value = JToken.ReadFrom(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidOperationException("字段值只能包含一个 JSON 值。");
                return value;
            }
        }

        /// <summary>先验证标准 JSON 文法，拒绝 Newtonsoft 兼容语法中的注释、单引号、尾逗号和非有限常量。</summary>
        private static void ReadJsonSyntax(string json, ref int position, int depth)
        {
            SkipJsonWhitespace(json, ref position);
            if (depth > 32 || position >= json.Length) throw new InvalidOperationException("JSON 值缺失或结构过深。");
            char first = json[position];
            if (first == '"') { ReadJsonString(json, ref position); return; }
            if (first == '{' || first == '[')
            {
                bool dictionary = first == '{';
                char end = dictionary ? '}' : ']';
                position++;
                SkipJsonWhitespace(json, ref position);
                if (position < json.Length && json[position] == end) { position++; return; }
                while (true)
                {
                    if (dictionary)
                    {
                        ReadJsonString(json, ref position);
                        SkipJsonWhitespace(json, ref position);
                        if (position >= json.Length || json[position++] != ':') throw new InvalidOperationException("JSON 对象缺少冒号。");
                    }
                    ReadJsonSyntax(json, ref position, depth + 1);
                    SkipJsonWhitespace(json, ref position);
                    if (position >= json.Length) throw new InvalidOperationException("JSON 容器未闭合。");
                    if (json[position] == end) { position++; return; }
                    if (json[position++] != ',') throw new InvalidOperationException("JSON 容器缺少逗号。");
                    SkipJsonWhitespace(json, ref position);
                }
            }
            Match token = Regex.Match(json.Substring(position), @"\A(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)", RegexOptions.CultureInvariant);
            if (!token.Success) throw new InvalidOperationException("JSON 值必须使用标准 token 语法。");
            position += token.Length;
        }

        /// <summary>检查双引号字符串及 JSON 唯一允许的转义；不接受原始控制字符。</summary>
        private static void ReadJsonString(string json, ref int position)
        {
            if (position >= json.Length || json[position++] != '"') throw new InvalidOperationException("JSON 对象属性必须使用双引号。");
            while (position < json.Length)
            {
                char c = json[position++];
                if (c == '"') return;
                if (c < 32) throw new InvalidOperationException("JSON 字符串含未转义控制字符。");
                if (c != '\\') continue;
                if (position >= json.Length) break;
                c = json[position++];
                if (c == 'u')
                {
                    for (int i = 0; i < 4; i++)
                        if (position >= json.Length || !Uri.IsHexDigit(json[position++])) throw new InvalidOperationException("JSON Unicode 转义无效。");
                }
                else if ("\"\\/bfnrt".IndexOf(c) < 0) throw new InvalidOperationException("JSON 字符串转义无效。");
            }
            throw new InvalidOperationException("JSON 字符串未闭合。");
        }

        /// <summary>JSON 空白只接受空格、制表符、回车和换行，拒绝其它 Unicode 空白。</summary>
        private static void SkipJsonWhitespace(string json, ref int position)
        {
            while (position < json.Length && " \t\r\n".IndexOf(json[position]) >= 0) position++;
        }

        /// <summary>在调用者提供的 SerializedObject 上设置已定位字段；不 Apply、不保存，调用者承担预演与事务。</summary>
        internal static void Set(SerializedProperty property, JToken value)
        {
            if (property == null || value == null || !CanWrite(property))
                throw new InvalidOperationException("字段不可编辑或值缺失。");
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (!(value is JArray values) || values.Count > 1024)
                    throw new InvalidOperationException("列表字段需要不超过 1024 项的 JSON 数组。");
                property.arraySize = values.Count;
                for (int i = 0; i < values.Count; i++) Set(property.GetArrayElementAtIndex(i), values[i]);
                return;
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    Require(value, JTokenType.String);
                    property.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Boolean:
                    Require(value, JTokenType.Boolean);
                    property.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    Require(value, JTokenType.Integer);
                    SetInteger(property, value);
                    break;
                case SerializedPropertyType.Float:
                    if (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)
                        throw new InvalidOperationException("浮点字段需要 JSON 数字。");
                    double number = value.Value<double>();
                    if (double.IsNaN(number) || double.IsInfinity(number) ||
                        (property.numericType.ToString() == "Float" && Math.Abs(number) > float.MaxValue))
                        throw new InvalidOperationException("浮点字段超出有效范围。");
                    property.doubleValue = number;
                    break;
                case SerializedPropertyType.Enum:
                    Require(value, JTokenType.String);
                    int index = Array.IndexOf(property.enumNames, value.Value<string>());
                    if (index < 0) throw new InvalidOperationException("枚举需要当前字段精确的枚举名称。");
                    property.enumValueIndex = index;
                    break;
                case SerializedPropertyType.ObjectReference:
                    SetReference(property, value);
                    break;
                case SerializedPropertyType.Generic:
                    SetObject(property, value);
                    break;
                case SerializedPropertyType.Vector2:
                    double[] v2 = Numbers(value, "x", "y"); property.vector2Value = new Vector2((float)v2[0], (float)v2[1]); break;
                case SerializedPropertyType.Vector3:
                    double[] v3 = Numbers(value, "x", "y", "z"); property.vector3Value = new Vector3((float)v3[0], (float)v3[1], (float)v3[2]); break;
                case SerializedPropertyType.Vector4:
                    double[] v4 = Numbers(value, "x", "y", "z", "w"); property.vector4Value = new Vector4((float)v4[0], (float)v4[1], (float)v4[2], (float)v4[3]); break;
                case SerializedPropertyType.Color:
                    double[] c = Numbers(value, "r", "g", "b", "a"); property.colorValue = new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3]); break;
                case SerializedPropertyType.Rect:
                    double[] r = Numbers(value, "x", "y", "width", "height"); property.rectValue = new Rect((float)r[0], (float)r[1], (float)r[2], (float)r[3]); break;
                default: throw new InvalidOperationException("当前字段类型尚无受控值转换；不能以代码或反射旁路设置。");
            }
        }

        /// <summary>只读提取支持的字段值；对象引用使用 GUID/localId，不返回会漂移的 instanceID。</summary>
        internal static JToken Read(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (property.arraySize > 1024) return null;
                var values = new JArray();
                for (int i = 0; i < property.arraySize; i++)
                {
                    JToken item = Read(property.GetArrayElementAtIndex(i));
                    if (item == null) return null;
                    values.Add(item);
                }
                return values;
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: return new JValue(property.stringValue);
                case SerializedPropertyType.Boolean: return new JValue(property.boolValue);
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character: return property.numericType.ToString() == "UInt64" ? new JValue(property.ulongValue) : new JValue(property.longValue);
                case SerializedPropertyType.Float: return new JValue(property.doubleValue);
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length ?
                        new JValue(property.enumNames[property.enumValueIndex]) : new JValue(property.intValue);
                case SerializedPropertyType.ObjectReference:
                    if (property.objectReferenceValue == null) return JValue.CreateNull();
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(property.objectReferenceValue, out string guid, out long localId))
                        return new JObject { ["assetGuid"] = guid, ["localId"] = localId };
                    return null;
                case SerializedPropertyType.Vector2: return new JObject { ["x"] = property.vector2Value.x, ["y"] = property.vector2Value.y };
                case SerializedPropertyType.Vector3: return new JObject { ["x"] = property.vector3Value.x, ["y"] = property.vector3Value.y, ["z"] = property.vector3Value.z };
                case SerializedPropertyType.Vector4: return new JObject { ["x"] = property.vector4Value.x, ["y"] = property.vector4Value.y, ["z"] = property.vector4Value.z, ["w"] = property.vector4Value.w };
                case SerializedPropertyType.Color: return new JObject { ["r"] = property.colorValue.r, ["g"] = property.colorValue.g, ["b"] = property.colorValue.b, ["a"] = property.colorValue.a };
                case SerializedPropertyType.Rect: return new JObject { ["x"] = property.rectValue.x, ["y"] = property.rectValue.y, ["width"] = property.rectValue.width, ["height"] = property.rectValue.height };
                case SerializedPropertyType.Generic:
                    var composite = new JObject();
                    SerializedProperty child = property.Copy();
                    SerializedProperty end = property.GetEndProperty();
                    if (child.NextVisible(true)) do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        if (child.depth != property.depth + 1) continue;
                        JToken part = Read(child);
                        if (part == null) return null;
                        composite[child.name] = part;
                    } while (child.NextVisible(false));
                    return composite;
                default: return null;
            }
        }

        /// <summary>判定已经定位字段的可写类型；引擎身份、引用类型元数据及管理引用根不能被替换。</summary>
        internal static bool CanWrite(SerializedProperty property)
        {
            if (property == null || property.propertyPath == "m_Script" || property.propertyPath == "m_ObjectHideFlags" ||
                property.propertyPath == "m_Name" || property.propertyPath == "m_EditorClassIdentifier" || property.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal)) return false;
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                // 管理引用列表只能编辑已存在子字段；更换或新建多态类型须走领域实例入口。
                string element = property.arrayElementType ?? string.Empty;
                return element.IndexOf("managedReference", StringComparison.OrdinalIgnoreCase) < 0 && property.arraySize <= 1024 &&
                    (property.arraySize == 0 || property.GetArrayElementAtIndex(0).propertyType != SerializedPropertyType.ManagedReference);
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: case SerializedPropertyType.Boolean: case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float: case SerializedPropertyType.Enum: case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.LayerMask: case SerializedPropertyType.Character: case SerializedPropertyType.Generic:
                case SerializedPropertyType.Vector2: case SerializedPropertyType.Vector3: case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Color: case SerializedPropertyType.Rect: return true;
                default: return false;
            }
        }

        /// <summary>敏感路径只报告存在性；覆盖密钥、令牌、密码等常见配置字段及其子项。</summary>
        internal static bool IsSensitive(string path) => path != null &&
            new[] { "password", "secret", "token", "aeskey", "aesiv", "apikey", "accesskey", "keybase64", "credential", "keystorepass", "keyaliaspass", "privatekey", "keystorepath", "devkey", "appkey", "clientkey", "sdkkey", "webhook" }
                .Any(part => path.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>父结构只要包含敏感子字段也隐藏聚合值，避免从对象或数组读回已脱敏叶子的内容。</summary>
        private static bool ContainsSensitive(SerializedProperty property)
        {
            if (IsSensitive(property.propertyPath)) return true;
            if (!property.hasVisibleChildren) return false;
            SerializedProperty cursor = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            while (cursor.NextVisible(true) && !SerializedProperty.EqualContents(cursor, end))
                if (IsSensitive(cursor.propertyPath)) return true;
            return false;
        }

        /// <summary>按 Unity 数值类型检查整数范围，避免截断、溢出或隐式字符串转换。</summary>
        private static void SetInteger(SerializedProperty property, JToken value)
        {
            decimal number;
            try { number = decimal.Parse(value.ToString(Formatting.None), CultureInfo.InvariantCulture); }
            catch { throw new InvalidOperationException("整数字段超出可支持范围。"); }
            string type = property.numericType.ToString();
            decimal min = long.MinValue, max = long.MaxValue;
            switch (type)
            {
                case "Int8": min = sbyte.MinValue; max = sbyte.MaxValue; break;
                case "UInt8": min = byte.MinValue; max = byte.MaxValue; break;
                case "Int16": min = short.MinValue; max = short.MaxValue; break;
                case "UInt16": min = ushort.MinValue; max = ushort.MaxValue; break;
                case "Int32": min = int.MinValue; max = int.MaxValue; break;
                case "UInt32": min = uint.MinValue; max = uint.MaxValue; break;
                case "UInt64": min = 0; max = ulong.MaxValue; break;
            }
            if (property.propertyType == SerializedPropertyType.Character) { min = char.MinValue; max = char.MaxValue; }
            if (number < min || number > max) throw new InvalidOperationException("整数字段超出真实类型范围。");
            if (type == "UInt64") property.ulongValue = (ulong)number;
            else property.longValue = (long)number;
        }

        /// <summary>对象只能引用真实资产 GUID/localId 或明确 null；不接受场景 instanceID 和任意类型。</summary>
        private static void SetReference(SerializedProperty property, JToken value)
        {
            if (value.Type == JTokenType.Null) { property.objectReferenceValue = null; return; }
            if (!(value is JObject obj) || obj.Count != 2 || obj["assetGuid"]?.Type != JTokenType.String || obj["localId"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("对象引用需要 assetGuid/localId 或 null。");
            string guid = obj["assetGuid"].Value<string>();
            if (guid == null || guid.Length != 32 || !guid.All(Uri.IsHexDigit)) throw new InvalidOperationException("对象引用 GUID 无效。");
            string path = AssetDatabase.GUIDToAssetPath(guid);
            long localId = obj["localId"].Value<long>();
            UnityEngine.Object target = AssetDatabase.LoadAllAssetsAtPath(path).SingleOrDefault(item =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string itemGuid, out long id) && itemGuid == guid && id == localId);
            if (target == null) throw new InvalidOperationException("对象引用资产不存在或 localId 不匹配。");
            string requiredName = property.type.StartsWith("PPtr<$", StringComparison.Ordinal) ? property.type.Substring(6).TrimEnd('>') :
                property.type.StartsWith("PPtr<", StringComparison.Ordinal) ? property.type.Substring(5).TrimEnd('>', '$') : null;
            if (requiredName != null && !TypeHierarchy(target.GetType()).Any(type => type.Name == requiredName))
                throw new InvalidOperationException("对象引用与字段真实类型不匹配。");
            property.objectReferenceValue = target;
        }

        /// <summary>枚举对象真实基类和接口以验证引用兼容性，不根据用户类型名解析或实例化类型。</summary>
        private static IEnumerable<Type> TypeHierarchy(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType) yield return current;
            foreach (Type contract in type.GetInterfaces()) yield return contract;
        }

        /// <summary>只允许当前可见的直接子字段按名赋值；未提供子字段保留，不替换类型或隐藏字段。</summary>
        private static void SetObject(SerializedProperty property, JToken value)
        {
            if (!(value is JObject obj) || obj.Count == 0) throw new InvalidOperationException("结构字段需要非空 JSON 对象。");
            var names = new HashSet<string>();
            SerializedProperty cursor = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            if (cursor.NextVisible(true)) do
            {
                if (SerializedProperty.EqualContents(cursor, end)) break;
                if (cursor.depth == property.depth + 1) names.Add(cursor.name);
            } while (cursor.NextVisible(false));
            foreach (JProperty item in obj.Properties())
            {
                if (!names.Contains(item.Name)) throw new InvalidOperationException("结构包含未知、隐藏或不可编辑子字段。");
                Set(property.FindPropertyRelative(item.Name), item.Value);
            }
        }

        /// <summary>验证向量等固定对象的精确成员及有限浮点范围，不接受额外属性。</summary>
        private static double[] Numbers(JToken value, params string[] names)
        {
            if (!(value is JObject obj) || obj.Count != names.Length || names.Any(name => obj[name] == null))
                throw new InvalidOperationException("值对象成员与字段类型不一致。");
            return names.Select(name =>
            {
                JToken token = obj[name];
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) throw new InvalidOperationException("值对象成员必须是数字。");
                double number = token.Value<double>();
                if (double.IsInfinity(number) || double.IsNaN(number) || Math.Abs(number) > float.MaxValue)
                    throw new InvalidOperationException("值对象数字超出范围。");
                return number;
            }).ToArray();
        }

        /// <summary>拒绝 JSON token 与真实字段类型不符；不采用宽松 ToObject 隐式转换。</summary>
        private static void Require(JToken value, JTokenType type)
        {
            if (value.Type != type) throw new InvalidOperationException("JSON 值类型与真实字段类型不一致。");
        }
    }
}
